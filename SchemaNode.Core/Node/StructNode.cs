using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Struct;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Text.Json.Nodes;
using static SchemaNode.Utility.Constant;
using StructType = SchemaNode.Runtime.StructType;

namespace SchemaNode.Node;

/// <summary>
/// The struct data node
/// </summary>
public class StructNode : DataNode
{
    private readonly IValueAccess[] _fields;
    private object? _csharpObject;
    private JsonObject? _original;
    private Dictionary<string, List<RelationInfo>>? _fieldRelations;
    private HashSet<string>? _fieldTypeTrack;
    private HashSet<StructType>? _randomStructTypes;

    #region Constructor

    public StructNode(ISchemaContext? context, StructType type, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders)
        : this(context, type, value, parent, true, propertyProviders)
    {
    }

    /// <summary>
    /// Creates the struct node, the clone creates the fields uninitialized because they are replaced and initialized afterwards
    /// </summary>
    private StructNode(ISchemaContext? context, StructType type, object? value, IValueAccess? parent, bool initialize, params IPropertyProvider[] propertyProviders)
        : base(context, type, null, parent, propertyProviders)
    {
        // init fields, the field declared node type is only used in full mode
        _fields = type.GetFields().Select(p =>
        {
            Runtime.ValueType fieldType = p.Type ?? throw new Exception($"The struct {type.Name}'s field {p.Name} has not valid value type");
            System.Type? nodeType = FullMode ? p.Property?.GetMetaProperty<DataNodeType>()?.Value : null;
            return DataNode.CreateNode(context, fieldType, nodeType, null, this, p);
        }).ToArray();

        if (value is not null)
        {
            if (!TrySetValue(value))
                throw new InvalidCastException($"Failed to set value to schema type {type.Name}.");
            if (FullMode)
                Confirm();
        }

        if (initialize)
            InitializeFields();
    }

    /// <summary>
    /// Apply the field property effects, keep the raw data update and attach the type relations
    /// </summary>
    private void InitializeFields()
    {
        if (!FullMode) return;

        // keep raw data update and apply property effects
        foreach (var field in _fields.OfType<DataNode>())
        {
            field.ApplyPropertyEffects();
            if (field.DisplayOnly) continue;
            RecordSubscription(field.Subscribe(items => WriteBackRawValue(items.Item1, items.Item2)), this);
        }

        // attach relations from type
        RelationInfo info = new() { Owner = this };
        info.Relations.AddRange(Type.GetRelations());
        AttachRelations([info]);
    }

    #endregion

    #region Dispose

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (_randomStructTypes is not null)
        {
            foreach (var type in _randomStructTypes)
                type.Dispose();
            _randomStructTypes.Clear();
            _randomStructTypes = null;
        }

        foreach (var field in _fields)
            if (field is IDisposable disposable)
                disposable.Dispose();
        _fieldRelations = null;
        _fieldTypeTrack = null;

        base.Dispose(disposing);
    }

    #endregion

    #region Indexer

    // struct[field] access
    public object? this[string name]
    {
        get => GetAccessValue(name);
        set
        {
            IValueAccess? field = GetAccessValue(name);
            if (field == null) return;
            _csharpObject = null;
            field.TrySetValue(value);
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets the struct field type by name
    /// </summary>
    public StructFieldType? GetFieldType(string fieldName) => (Type as StructType)?.GetField(fieldName);

    /// <summary>
    /// Gets the field type and values
    /// </summary>
    public IEnumerable<(StructFieldType field, IValueAccess value)> GetFields()
    {
        int i = 0;
        foreach (var field in (Type as StructType)!.GetFields())
            yield return (field, _fields[i++]);
    }

    /// <inheritdoc/>
    public override void ClearDisplayOnlyNode()
    {
        base.ClearDisplayOnlyNode();
        foreach (var f in _fields.OfType<DataNode>())
            f.ClearDisplayOnlyNode();
    }

    /// <summary>
    /// Try set the value to field
    /// </summary>
    public bool TrySetFieldValue(string fieldName, object? value)
    {
        if (GetAccessValue(fieldName) is not { } field || !field.TrySetValue(value)) return false;
        // assign back
        if (_csharpObject == null) return true;
        try
        {
            var fieldType = (Type as StructType)!.GetField(fieldName);
            if (fieldType?.Property != null)
                fieldType.Property.SetValue(_csharpObject, field.GetValue(fieldType.Property.PropertyType));
        }
        catch
        {
            // ignore assign back error
        }
        return true;
    }

    #endregion

    #region Implementation

    /// <summary>
    /// Gets the access value
    /// </summary>
    public override IValueAccess? GetAccessValue(string path, IValueAccess? node = null)
    {
        if (base.GetAccessValue(path, node) is { } v) return v;
        string[] paths = path.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        var field = _fields.ElementAtOrDefault((Type as StructType)?.GetIndex(paths[0]) ?? -1);
        return paths.Length > 1 ? field?.GetAccessValue(paths[1], node) : field;
    }

    /// <inheritdoc/>
    public override bool Equals(IValueAccess? other)
    {
        if (this == other) return true;
        if (other is not StructNode otherStruct || !Type.IsAssignableTo(otherStruct.Type)) return false;

        foreach (var (field, value) in GetFields())
        {
            if (field.DisplayOnly == true) continue;
            var otherValue = otherStruct.GetAccessValue(field.Name);
            if (otherValue == null || !value.Equals(otherValue)) return false;
        }
        return true;
    }

    /// <inheritdoc/>
    public override bool IsEmpty => _csharpObject == null &&
        _fields.All(f => f is DataNode { DisplayOnly: true } || f.IsEmpty);

    /// <inheritdoc/>
    /// <remarks>
    /// An empty struct is an optional value that was never filled in, so the required
    /// constraints of its children must not surface. The struct's own constraints
    /// (including <see cref="SchemaNode.Property.Common.Require"/>) still decide its validity.
    /// </remarks>
    public override bool IsValid => base.IsValid && (IsEmpty || _fields.All(f => f.GetProperty<DisplayOnly>()?.Value == true || f.IsValid));

    /// <inheritdoc/>
    public override JsonNode? Violated
    {
        get
        {
            if (IsValid) return null;
            JsonObject result = [];
            foreach (var f in _fields.Where(f => !f.IsValid))
                result[f.Name!] = f.Violated;
            return result;
        }
    }

    /// <inheritdoc/>
    public override IEnumerable<DataNode> GetErrorNodes()
    {
        if (!IsEmpty)
            foreach (var field in _fields.OfType<DataNode>())
                if (!field.DisplayOnly && !field.IsValid)
                    foreach (var node in field.GetErrorNodes())
                        yield return node;
        foreach (var node in base.GetErrorNodes())
            yield return node;
    }

    /// <inheritdoc/>
    public override bool TryGetOriginal(Type type, out object? value)
    {
        if (_original is null)
        {
            value = null;
            return false;
        }

        if (type == typeof(JsonObject) || type == typeof(JsonNode))
        {
            value = _original.DeepClone();
            return true;
        }

        if (type == typeof(string))
        {
            value = _original.ToJsonString();
            return true;
        }

        if (CsharpType != null && CsharpType.IsAssignableTo(type))
        {
            value = _original.FromJson(CsharpType);
            return value != null;
        }

        value = null;
        return false;
    }

    /// <inheritdoc/>
    public override bool Changed => _fields.OfType<DataNode>().Any(f => !f.DisplayOnly && f.Changed);

    /// <inheritdoc/>
    public override void Confirm()
    {
        foreach (var field in _fields.OfType<DataNode>())
            field.Confirm();
        _original = GetJsonObject();
    }

    /// <inheritdoc/>
    public override void Reset() => TrySetValue<object>(_original);

    /// <inheritdoc/>
    public sealed override bool TrySetValue<T>(T? value) where T : default
    {
        _csharpObject = null;
        if (value == null)
        {
            ClearValue();
        }
        else switch (value)
        {
            case StructNode @struct when @struct == this:
                break;
            case StructNode @struct when !@struct.Type.IsAssignableTo(Type):
                return false;
            // copy
            case StructNode @struct:
            {
                foreach ((StructFieldType f, var v) in GetFields())
                {
                    var otherValue = @struct.GetAccessValue(f.Name);
                    if (otherValue != null) v.TrySetValue(otherValue);
                }

                break;
            }
            case JsonObject obj:
            {
                // Mirrors the TS StructNode.setValue algorithm: declared fields are assigned by their
                // own key first, an unpack field missing its own key is deferred as a packing target,
                // and the deferred targets are filled afterwards (struct shaped targets first, then
                // free form containers) using the keys nobody consumed yet.
                List<(StructFieldType Field, IValueAccess Node)> fields = [.. GetFields()];
                HashSet<string> consumed = new(StringComparer.OrdinalIgnoreCase);
                List<IValueAccess> packFields = [];

                foreach ((StructFieldType f, IValueAccess v) in fields)
                {
                    if (!consumed.Add(f.Name)) continue;

                    bool present = TryGetJsonProperty(obj, f.Name, out JsonNode? item);

                    // an unpack field is only packed when its own key is absent or null, otherwise
                    // the whole object was given explicitly and is set directly (TS: isNull(d))
                    if ((f.Unpack ?? false) && (!present || IsNullJson(item)))
                    {
                        packFields.Add(v);
                        continue;
                    }

                    // TS calls f.setValue(undefined) for a missing key, which clears the field
                    if (!present)
                    {
                        v.ClearValue();
                        continue;
                    }

                    if (!v.TrySetValue(item)) return false;
                }

                // TS sorts struct shaped targets before the free form containers so a key declared
                // by a struct type can never be swallowed by the container first. The order inside
                // each group stays the declaration order (TS sort is stable).
                List<IValueAccess> orderedPackFields =
                [
                    .. packFields.Where(f => f is StructNode),
                    .. packFields.Where(f => f is not StructNode)
                ];

                foreach (IValueAccess packField in orderedPackFields)
                {
                    if (packField is StructNode structPack)
                    {
                        // only the direct sub-fields take part; a nested unpack field is set by its
                        // own key like any other field (TS: "ignore pack field in sub struct")
                        foreach ((StructFieldType f, IValueAccess v) in structPack.GetFields())
                        {
                            if (!consumed.Add(f.Name)) continue;

                            if (TryGetJsonProperty(obj, f.Name, out JsonNode? item) && !IsNullJson(item))
                            {
                                if (!v.TrySetValue(item)) return false;
                            }
                            else
                            {
                                v.ClearValue();
                            }
                        }
                    }
                    else
                    {
                        JsonObject record = [];
                        foreach ((string key, JsonNode? item) in obj)
                        {
                            if (!consumed.Add(key)) continue;
                            record[key] = item?.DeepClone();
                        }

                        // TS sets the record even when empty; an empty record means an empty value,
                        // which this node expresses as a cleared value.
                        if (record.IsEmpty())
                            packField.ClearValue();
                        else if (!packField.TrySetValue(record))
                            return false;
                    }
                }
                break;
            }
            case JsonNode:
            {
                // TS treats any non object value as an empty object, so the struct ends up cleared.
                ClearValue();
                break;
            }
            default:
            {
                bool isSameType = value.GetType() == CsharpType;
                if (isSameType || value.GetType().IsGenericType && value.GetType().GetGenericTypeDefinition() == CsharpType?.GetGenericTypeDefinition())
                {
                    _csharpObject = value;
                    int i = 0;
                    foreach (var field in (Type as StructType)!.GetFields())
                    {
                        var f = _fields[i];
                        if (field.Property == null || field.DisplayOnly == true)
                            f.ClearValue();
                        else
                        {
                            // Read from the assigned object, not from _csharpObject: a field write-back
                            // (FullMode relation assign) clears _csharpObject midway through the loop,
                            // and GetValue(null) would then throw "Non-static method requires a target".
                            object? v = (isSameType ? field.Property : value.GetType().GetProperty(field.Property.Name)!).GetValue(value);
                            if (v != null)
                            {
                                if (f is AnyNode && v is IValueAccess vNode)
                                {
                                    _fields[i] = vNode;
                                }
                                else
                                    f.TrySetValue(v);
                            }
                        }
                        i++;
                    }
                }
                else
                {
                    throw new InvalidCastException($"Invalid value type {value.GetType()}");
                }

                break;
            }
        }
        OnNext(this);
        return true;
    }

    /// <inheritdoc/>
    public override bool TryGetValue(Type type, out object? value)
    {
        if (type == typeof(JsonObject) || type == typeof(JsonNode))
        {
            value = GetJsonObject();
            return true;
        }

        if (type.IsAssignableFrom(typeof(StructNode)))
        {
            value = this;
            return true;
        }

        if (CsharpType != null && CsharpType.IsAssignableTo(type))
        {
            if (_csharpObject != null)
            {
                value = _csharpObject;
                return true;
            }
            else if (GetJsonObject() is { } obj)
            {
                _csharpObject = obj.FromJson(CsharpType);
                value = _csharpObject;
                return _csharpObject != null;
            }
            value = null;
            return true;
        }

        // convert to string
        if (type == typeof(string))
        {
            value = GetJsonObject().ToJsonString();
            return true;
        }

        value = null;
        return false;
    }

    /// <inheritdoc/>
    public override void ClearValue()
    {
        _csharpObject = null;
        foreach (var field in _fields)
            field.ClearValue();
        OnNext(this);
    }

    /// <inheritdoc/>
    public override IValueAccess Clone()
    {
        StructNode clone = new(null, (StructType)Type, null, null, false, PropertyProviders.ToArray());
        CopyFields(clone, null);
        return clone;
    }

    /// <inheritdoc/>
    public override IValueAccess Clone(SchemaContext? context)
    {
        StructNode clone = new(context, (StructType)Type, null, null, false, PropertyProviders.ToArray());
        CopyFields(clone, context);
        return clone;
    }

    /// <summary>
    /// Copies the fields into the clone and initializes them, the clone keeps the empty fields
    /// which were attached to the type after this node was created
    /// </summary>
    private void CopyFields(StructNode clone, SchemaContext? context)
    {
        // snapshot is required because the type may attach new fields while a field is cloned
        StructFieldType[] fieldTypes = ((StructType)Type).GetFields().ToArray();
        int count = Math.Min(Math.Min(fieldTypes.Length, _fields.Length), clone._fields.Length);
        for (int i = 0; i < count; i++)
            clone.ReplaceField(i, context is null || _fields[i] is not DataNode field ? _fields[i].Clone() : field.Clone(context));

        clone._original = _original?.DeepClone() as JsonObject;
        clone.InitializeFields();
    }

    /// <summary>
    /// Replaces a field created by the constructor, the replaced field has not been initialized at all
    /// </summary>
    private void ReplaceField(int index, IValueAccess field)
    {
        if (_fields[index] is IDisposable placeholder && !ReferenceEquals(placeholder, field))
            placeholder.Dispose();
        _fields[index] = field;
    }

    #endregion

    #region Relation

    /// <inheritdoc/>
    public override void AttachRelations(IRelationInfo[] relations)
    {
        if (!FullMode) return;

        Dictionary<string, List<RelationInfo>> fieldRelations = new(StringComparer.OrdinalIgnoreCase);

        // attach relations from given infos
        foreach (var info in relations)
        {
            foreach (var relation in info.Relations)
            {
                string[] paths = relation.Target.Split('.', StringSplitOptions.RemoveEmptyEntries);
                IValueAccess? curr = info.Owner;
                int index = 0;

                // indicate this
                while (curr is not null && curr != this && index < paths.Length)
                    curr = curr.GetAccessValue(paths[index++], this);
                if (curr is null) continue;

                // attach relation to this
                if (index == paths.Length)
                {
                    relation.Attach(info.Owner, this);
                    continue;
                }

                // navigate to next field
                string next = paths[index];
                if (!fieldRelations.TryGetValue(next, out var fieldInfos))
                {
                    fieldInfos = [];
                    fieldRelations[next] = fieldInfos;
                }

                RelationInfo? exist = fieldInfos.Find(f => f.Owner == info.Owner);
                if (exist is not null)
                    exist.Relations.Add(relation);
                else
                {
                    RelationInfo item = new() { Owner = info.Owner };
                    item.Relations.Add(relation);
                    fieldInfos.Add(item);
                }
            }
        }

        // attach relations from children
        foreach (var field in _fields)
        {
            string? name = field.Name?.ToLowerInvariant();
            if (name is null || !fieldRelations.TryGetValue(name, out var infos) || infos.Count == 0) continue;

            // keep track of field relations
            _fieldRelations ??= new(StringComparer.OrdinalIgnoreCase);
            if (_fieldRelations.TryGetValue(name, out var exist))
                exist.AddRange(infos);
            else
                _fieldRelations[name] = infos;

            // track the override type changes, only work in full mode
            if (HasRelation(infos, typeof(OverrideType), field))
                TrackOverrideType(field, typeof(OverrideType));
            else if (HasRelation(infos, typeof(OverrideFields), field))
                TrackOverrideType(field, typeof(OverrideFields));

            // attach relations to child
            field.AttachRelations(infos.ToArray());
        }
    }

    /// <summary>
    /// Whether the field relations contain the given override property
    /// </summary>
    private static bool HasRelation(List<RelationInfo> infos, Type propertyCtor, IValueAccess field)
        => infos.Any(info => info.Relations.Any(r => r.PropertyCtor == propertyCtor && info.Owner.GetAccessValue(r.Target, field) == field));

    /// <summary>
    /// Track the field override type property change
    /// </summary>
    private void TrackOverrideType(IValueAccess field, Type propertyCtor)
    {
        if (!FullMode || field is not DataNode node) return;

        string? name = node.Name?.ToLowerInvariant();
        if (name is null) return;

        _fieldTypeTrack ??= new(StringComparer.OrdinalIgnoreCase);
        if (!_fieldTypeTrack.Add(name)) return;

        if (propertyCtor == typeof(OverrideType))
            node.SubscribeProperty<OverrideType, string>(items => { _ = TrackOverrideTypeAsync(node, propertyCtor, items.Item3, items.Item4); });
        else
            node.SubscribeProperty<OverrideFields, StructFieldSchema[]>(items => { _ = TrackOverrideTypeAsync(node, propertyCtor, items.Item3, items.Item4); });
    }

    /// <summary>
    /// Replace the field node with a new node created from the override type
    /// </summary>
    private async Task TrackOverrideTypeAsync(DataNode node, Type propertyCtor, object? newValue, object? oldValue)
    {
        if (!FullMode || Context is not SchemaContext ctx) return;
        if (IsSameValue(oldValue, newValue)) return;

        StructType? structType = Type as StructType;
        StructFieldType? structField = node.Name is { } nodeName ? structType?.GetField(nodeName) : null;
        if (structField is null) return;

        int index = Array.IndexOf(_fields, node);
        if (index < 0) return;

        Runtime.ValueType? type;
        StructType? randomType = null;
        if (propertyCtor == typeof(OverrideType))
        {
            type = newValue is string typeName && !string.IsNullOrWhiteSpace(typeName)
                ? await ctx.GetNodeTypeAsync<Runtime.ValueType>(typeName).ConfigureAwait(false)
                : structField.Type;
            if (type is null || type == node.Type) return;
        }
        else
        {
            randomType = await CreateRandomStructTypeAsync(ctx, newValue as StructFieldSchema[] ?? []).ConfigureAwait(false);
            type = randomType;
        }

        IValueAccess created = type.Create(Context, null, this, structField.GetOverrideFieldType(type));
        if (created is not DataNode newNode)
        {
            UnloadRandomStructType(randomType);
            return;
        }

        // copy the current value
        if (!node.IsEmpty && node.TryGetValue(typeof(JsonNode), out object? rawValue) && rawValue is JsonNode jsonValue)
            newNode.TrySetValue(jsonValue);

        // move the subscriptions to the new node
        node.MoveSubscription(newNode);

        // a newer override may have replaced this node while awaiting
        if (_fields[index] != node)
        {
            UnloadRandomStructType(randomType);
            newNode.Dispose();
            return;
        }

        _fields[index] = newNode;

        // unload the replaced random struct type
        if (node.Type is StructType oldType)
            UnloadRandomStructType(oldType);

        node.Dispose();

        newNode.ApplyPropertyEffects();

        // attach relations to the new node
        string? name = newNode.Name?.ToLowerInvariant();
        if (name is not null && _fieldRelations?.TryGetValue(name, out var fieldInfos) == true && fieldInfos.Count > 0)
            newNode.AttachRelations(fieldInfos.ToArray());

        // write back the raw value
        WriteBackRawValue(newNode, newNode.GetValue<object>());
    }

    /// <summary>
    /// Create a random struct type for the override fields
    /// </summary>
    private async Task<StructType> CreateRandomStructTypeAsync(SchemaContext context, StructFieldSchema[] fields)
    {
        NodeSchema schema = NodeSchema.Create(context.Runtime, NODE_KIND_STRUCT, nameof(CreateRandomStructTypeAsync));
        schema.SetProperty<StructProperty, StructSchema>(new StructSchema { Fields = fields });

        StructType type = new();
        await type.LoadTypeAsync(context, schema).ConfigureAwait(false);

        _randomStructTypes ??= [];
        _randomStructTypes.Add(type);
        return type;
    }

    /// <summary>
    /// Unload the random struct type if it was created by the override fields
    /// </summary>
    private void UnloadRandomStructType(StructType? type)
    {
        // dispose to fully unload the random type, including the used-by references
        if (type is not null && _randomStructTypes?.Remove(type) == true)
            type.Dispose();
    }

    /// <summary>
    /// Whether the override property value is unchanged
    /// </summary>
    private static bool IsSameValue(object? oldValue, object? newValue)
    {
        if (ReferenceEquals(oldValue, newValue)) return true;
        if (oldValue is null || newValue is null) return false;
        if (oldValue is StructFieldSchema[] oldFields && newValue is StructFieldSchema[] newFields)
            return oldFields.Length == newFields.Length &&
                   oldFields.Zip(newFields).All(pair => pair.First.Name == pair.Second.Name && pair.First.Type == pair.Second.Type);
        return oldValue.Equals(newValue);
    }

    #endregion

    #region Utility

    private void WriteBackRawValue(IValueAccess field, object? value)
    {
        _csharpObject = null;
        if (Parent != null && GetPropertyValue<Require, bool>() != true)
        {
            if (value is null || value is IValueAccess access && access.IsEmpty)
            {
                if (IsEmpty && GetPropertyValue<DisableConstraint, bool>() != true)
                {
                    SetPropertyValue<DisableConstraint, bool>(true);
                    ClearConstraints();
                }
            }
            else if (GetPropertyValue<DisableConstraint, bool>() == true)
            {
                SetPropertyValue<DisableConstraint, bool>(false);
                ClearConstraints();
            }
        }
        OnNext(this);
    }

    private JsonObject GetJsonObject()
    {
        JsonObject result = [];
        foreach ((StructFieldType f, IValueAccess v) in GetFields())
        {
            if (v.IsEmpty) continue;
            // a hidden field that fails its constraints is not part of the serialized value
            if (v is DataNode node && !node.IsValid && !node.Visible) continue;

            if (f.Unpack ?? false)
            {
                if (v.TryGetValue(typeof(JsonObject), out object? packed) && packed is JsonObject obj)
                {
                    foreach ((string key, JsonNode? value) in obj)
                    {
                        // TS keeps the first owner of a key and drops null values
                        if (IsNullJson(value) || result.ContainsKey(key)) continue;
                        result[key] = value?.DeepClone();
                    }
                }
                else if (v.TryGetValue(typeof(JsonNode), out object? raw) && raw is JsonNode d && !d.IsEmpty())
                {
                    // TS falls back to the field name when the unpack value is not an object
                    result[f.Name] = d.DeepClone();
                }
            }
            else if (v.TryGetValue(typeof(JsonNode), out object? raw) && raw is JsonNode d && !d.IsEmpty())
            {
                result[f.Name] = d.DeepClone();
            }
        }
        return result;
    }

    /// <summary>
    /// Whether the json value counts as null for the pack/unpack rules (TS isNull: null or "")
    /// </summary>
    private static bool IsNullJson(JsonNode? value)
        => value is null || value is JsonValue v && v.TryGetValue(out string? text) && text.Length == 0;

    /// <summary>
    /// Gets the property with a case insensitive match, callers pass schema field names in any case
    /// </summary>
    private static bool TryGetJsonProperty(JsonObject obj, string name, out JsonNode? value)
    {
        if (obj.TryGetPropertyValue(name, out value)) return true;

        foreach ((string key, JsonNode? item) in obj)
        {
            if (!string.Equals(key, name, StringComparison.OrdinalIgnoreCase)) continue;
            value = item;
            return true;
        }

        value = null;
        return false;
    }

    #endregion
}
