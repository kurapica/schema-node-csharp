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

    public StructNode(ISchemaContext context, StructType type, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders)
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

        if (FullMode)
        {
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
    public override bool IsEmpty => _csharpObject == null && _fields.All(f => f.IsEmpty);

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
                Dictionary<string, IValueAccess> fieldMap = new (StringComparer.OrdinalIgnoreCase);
                IValueAccess? unpackNode = null;
                foreach ((StructFieldType f, var v)  in GetFields())
                {
                    fieldMap.Add(f.Name, v);
                    if (f.Unpack ?? false)
                        unpackNode = v;
                }

                JsonObject? packData = unpackNode != null ? new JsonObject() : null;
                foreach ((string key, JsonNode? val) in obj)
                {
                    if (fieldMap.TryGetValue(key.ToLower(), out var field))
                    {
                        if (!field.TrySetValue(val)) return false;
                    }
                    else if (packData != null)
                    {
                        packData[key] = val?.DeepClone();
                    }
                }

                if (unpackNode is { IsEmpty: true } && packData != null && !packData.IsEmpty())
                    if (!unpackNode.TrySetValue(packData)) return false;
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
        StructNode clone = new(Context, (StructType)Type, null, null, PropertyProviders.ToArray());
        int i = 0;
        foreach (var _ in (Type as StructType)!.GetFields())
        {
            clone._fields[i] = _fields[i].Clone();
            i++;
        }
        clone._original = _original?.DeepClone() as JsonObject;
        return clone;
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
        foreach ((StructFieldType f, var v) in GetFields())
        {
            if (v.IsEmpty) continue;
            if (f.Unpack ?? false)
            {
                if (v.TryGetValue(out JsonObject? obj) && obj != null)
                    foreach ((string key, JsonNode? val) in obj)
                        result[key] = val?.DeepClone();
            }
            else if (v.TryGetValue(out JsonNode? d) && d != null && !d.IsEmpty())
            {
                result.Add(f.Name, d.DeepClone());
            }
        }
        return result;
    }

    #endregion
}
