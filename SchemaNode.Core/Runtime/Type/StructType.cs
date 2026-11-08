using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Property.Struct;
using SchemaNode.Runtime.Interface;
using SchemaNode.Schema;
using SchemaNode.Struct;
using SchemaNode.Utility;
using System.Reflection;
using static SchemaNode.Utility.Constant;
using Type = System.Type;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory struct schema representation
/// </summary>
public sealed class StructType: ValueType, IRelationProvider, INodeTypeReLoadHandler
{
    #region Fields
    
    /// <summary>
    /// The struct fields
    /// </summary>
    private List<StructFieldType> _fields = [];
        
    /// <summary>
    /// The relations between the fields
    /// </summary>
    private List<RelationType>? _relations;

    /// <summary>
    /// The relation schemas already applied, used to distinct relations from
    /// struct level and attach properties.
    /// </summary>
    private List<RelationSchema>? _relationSchemas;

    /// <summary>
    /// The struct schema this type is loaded from
    /// </summary>
    private StructSchema? _structSchema;

    /// <summary>
    /// The number of declared fields, attach fields are always placed after them.
    /// </summary>
    private int _declaredFieldCount;

    /// <summary>
    /// The property types of the mounted attach fields, keyed by field name.
    /// The priority is recomputed from these on every sort: a property type can be
    /// mounted while its value type is still loading, and its priority must not be
    /// frozen with that partial state.
    /// </summary>
    private readonly Dictionary<string, PropertyType> _attachTypes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The schema type that first mounted an attach field, keyed by field name.
    /// Two attached kinds can declare different property schemas with the same
    /// field name; the first declaration wins, which matches the TS lookups
    /// (<c>getField</c> returns the first match) and keeps the field count bounded.
    /// </summary>
    private readonly Dictionary<string, string> _attachOwners = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The current load generation. Deferred attach mounts capture it and no-op
    /// when a newer load already reset the fields, so a mount scheduled by an
    /// older load can't inject a stale field into the new field list.
    /// </summary>
    private int _loadVersion;

    /// <summary>
    /// Attach property priority by node kind, aligned with the TS
    /// <c>struct/runtime.ts</c> VALUE_TYPE_PRIORITY.
    /// </summary>
    private static readonly Dictionary<string, double> VALUE_TYPE_PRIORITY = new(StringComparer.OrdinalIgnoreCase)
    {
        [NODE_KIND_BOOL] = 9000,
        [NODE_KIND_ENUM] = 8000,
        [NODE_KIND_STRING] = 7000,
        [NODE_KIND_DECIMAL] = 6000,
        [NODE_KIND_DATE] = 5000,
        [NODE_KIND_OBJECT] = 0,
        [NODE_KIND_STRUCT] = 3000,
        [NODE_KIND_ARRAY] = 2000,
    };

    /// <summary>
    /// Extra priority for common struct value types.
    /// </summary>
    private static readonly Dictionary<string, double> STRUCT_TYPE_PRIORITY = new(StringComparer.OrdinalIgnoreCase)
    {
        [NS_SYSTEM_LOCALE_STRING] = 900,
        [NS_SYSTEM_RANGE_YEAR] = 800,
        [NS_SYSTEM_RANGE_MONTH] = 700,
        [NS_SYSTEM_RANGE_DATE] = 600,
        [NS_SYSTEM_RANGE_FULL_DATE] = 500,
    };

    #endregion

    #region Implementations

    /// <inheritdoc />
    public void OnNodeTypeLoaded(NodeType[] types)
    {
        if (types.Any(t => _fields.Any(f => f.Type == t)))
            this.NotifyReload(types);
    }

    /// <inheritdoc />
    public override Type GetCsharpType(bool nullable = false)
    {
        var baseType = base.GetCsharpType();

        if (baseType != null && baseType.IsGenericTypeDefinition && GenericParams is { Count: > 0})
            baseType = baseType.MakeGenericType(GenericParams.Select(g => g.GetCsharpType()).ToArray()!);

        return baseType ?? typeof(StructNode);
    }

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context, CancellationToken token = default)
    {
        // reset, invalidate deferred mounts scheduled by a previous load
        int version = ++_loadVersion;
        _fields = [];
        _relations = null;
        _relationSchemas = null;
        _structSchema = null;
        _declaredFieldCount = 0;
        _attachTypes.Clear();
        _attachOwners.Clear();
        
        // load struct schema
        StructSchema? @struct = GetProperty<StructProperty>()?.Value;
        if (@struct == null)
        {
            Error = ErrorCodes.NO_DEFINITION;
            return;
        }
        _structSchema = @struct;
        
        // load fields
        Type? cType = Schema?.Type;
        foreach (StructFieldSchema field in @struct.Fields)
        {
            field.Error = null;
            StructFieldType fieldType = new();
            await fieldType.LoadAsync(context, field, Generics, GenericParams, cType?.GetProperty(field.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase), token).ConfigureAwait(false);
            Error ??= field.Error;
            _fields.Add(fieldType);
        }
        _declaredFieldCount = _fields.Count;

        // attach fields from the schema kinds declared by the Attach property
        List<(StructFieldType Field, PropertyType PropertyType, string SchemaType)> attachFields = [];
        List<RelationSchema> attachRelations = [];
        string? attachKind = @struct.GetProperty<Attach>()?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(attachKind))
        {
            // The same property type can be declared by multiple attached kinds,
            // keep the first declaration only so the union can't grow unbounded.
            HashSet<string> handledTypes = new(StringComparer.OrdinalIgnoreCase);
            foreach (string kind in GetAttachKinds(attachKind, context.Runtime))
            {
                foreach (Type propCtor in context.Runtime.GetSchemaKindPropertyTypes(kind))
                {
                    string? schemaType = propCtor.GetSchemaType();
                    if (string.IsNullOrWhiteSpace(schemaType) || !handledTypes.Add(schemaType)) continue;

                    PropertyType? propType = await context.GetNodeTypeAsync<PropertyType>(schemaType, token: token).ConfigureAwait(false) as PropertyType;
                    if (propType == null || propType.ValueType == null || string.IsNullOrEmpty(propType.Name))
                    {
                        // The property type is not ready: its schema is unresolved,
                        // its value type is not loaded yet, or a circular re-entry
                        // handed back an instance whose NodeSchema is not assigned
                        // (Name is empty, so Kind / Stackable would resolve wrong).
                        // Mounting now would freeze an arbitrary field type and
                        // priority. Retry through the deferred queue instead of
                        // dropping the attached property permanently.
                        context.DeferLoadTask(deferToken => MountAttachFieldAsync(context, schemaType, version, deferToken), token);
                        continue;
                    }
                    await BuildAttachFieldAsync(context, propType, schemaType, attachFields, attachRelations, token).ConfigureAwait(false);
                }
            }
        }

        // Attach fields are ordered by priority, aligned with the TS struct runtime
        foreach (var item in attachFields)
            InsertAttachField(item.Field, item.PropertyType, item.SchemaType);

        // Load Relation
        if (IsGeneric) return;
        if (@struct.GetProperty<Relations>()?.Value is { Length: > 0 } relations)
        {
            foreach (RelationSchema relation in relations)
                await AddRelationAsync(context, relation, token).ConfigureAwait(false);
        }

        // Relations declared by the attached property types
        foreach (RelationSchema relation in attachRelations)
            await AddRelationAsync(context, relation, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override void Unload()
    {
        _loadVersion++;
        _fields = [];
        _relations = null;
        _relationSchemas = null;
        _structSchema = null;
        _declaredFieldCount = 0;
        _attachTypes.Clear();
        _attachOwners.Clear();
    }

    /// <summary>
    /// Gets the property with the given type
    /// </summary>
    public override T? GetProperty<T>() where T : class 
        => base.GetProperty<T>() ?? Runtime?.GetSchemaKindProperty<T>(SchemaKind);

    /// <summary>
    /// Gets the properties with the given type
    /// </summary>
    public override IEnumerable<T> GetProperties<T>()
        => this.JoinProperties(base.GetProperties<T>(), Runtime?.GetSchemaKindProperties<T>(SchemaKind));

    /// <inheritdoc />
    public override IEnumerable<NodeType> GetReferenceTypes()
    {
        foreach (StructFieldType node in _fields.ToArray())
        {
            foreach (NodeType nodeType in node.GetReferenceTypes())
                yield return nodeType;
        }

        if (_relations != null)
            foreach (var r in _relations.OfType<INodeReferences>())
                foreach (NodeType node in r.GetReferenceTypes())
                    yield return node;
                
        foreach (NodeType nodeType in base.GetReferenceTypes())
            yield return nodeType;
    }

    /// <inheritdoc />
    public override IValueTypeAccess? GetAccessValueType(string path)
    {
        IValueTypeAccess? g = base.GetAccessValueType(path);
        if (g != null) return g;
        
        ReadOnlySpan<char> remain = null;
        int index = path.IndexOf('.');
        if (index > 0)
        {
            remain = path.AsSpan()[(index + 1)..];
            path = path[..index];
        }
        foreach (StructFieldType field in _fields)
        {
            if (path.Equals(field.Name, StringComparison.OrdinalIgnoreCase))
                return remain.IsEmpty ? field.Type : field.Type?.GetAccessValueType(remain.ToString());
        }
        return null;
    }

    /// <inheritdoc />
    public override bool IsAssignableTo(IValueTypeAccess other)
    {
        if (base.IsAssignableTo(other)) return true;
        if (other is not StructType @struct) return false;
        return @struct._fields.Any(v => _fields.Any(f => f.Name.Equals(v.Name, StringComparison.OrdinalIgnoreCase))) 
               && @struct._fields.All(v =>
               {
                   StructFieldType? match = _fields.FirstOrDefault(f => f.Name.Equals(v.Name, StringComparison.OrdinalIgnoreCase));
                   return match?.Type == null ? !(v.Require ?? false) : v.Type != null && match.Type.IsAssignableTo(v.Type);
               });
    }

    /// <inheritdoc />
    public override IValueAccess Create(ISchemaContext context, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders)
    {
        System.Type? nodeType = (context as SchemaContext)?.FullMode == true
            ? Schema?.Type?.GetMetaProperty<DataNodeType>()?.Value
            : null;

        return nodeType is null
            ? new StructNode(context, this, value, parent, propertyProviders)
            : DataNode.CreateNode(context, this, nodeType, value, parent, propertyProviders);
    }

    /// <inheritdoc />
    public override IEnumerable<Entry<string>> GetAccessEntries()
    {
        return GetFields()
            .Where(field => _structSchema == null || _structSchema.Fields.Any(f => f.Name.Equals(field.Name, StringComparison.OrdinalIgnoreCase)))
            .Select(field =>
        {
            var entry = new Entry<string>
            {
                Value = field.Name,
                HasChildren = field.Type?.HasAccessEntries ?? false
            };
            var display = field.GetProperty<Display>();
            if (display != null) entry.SetProperty(display);
            return entry;
        });
    }

    /// <summary>
    /// Gets relations
    /// </summary>
    public override IEnumerable<RelationType> GetRelations() => _relations?.AsEnumerable() ?? [];

    #endregion

    #region Methods
    
    /// <summary>
    /// Gets the fields
    /// </summary>
    public IEnumerable<StructFieldType> GetFields() => _fields.AsEnumerable();

    /// <summary>
    /// Gets the field by name
    /// </summary>
    public StructFieldType? GetField(ReadOnlySpan<char> fieldName)
    {
        foreach (StructFieldType field in _fields)
        {
            if  (field.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                return field;
        }
        return null;
    }
        
    /// <summary>
    /// Gets the field index, -1 if not found
    /// </summary>
    public int GetIndex(ReadOnlySpan<char> fieldName)
    {
        for (int i = 0; i < _fields.Count; i++)
        {
            if (fieldName.SeqEquals(_fields[i].Name, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }
    
    /// <summary>
    /// Gets relations for the given field name
    /// </summary>
    public IEnumerable<RelationType> GetRelations(string fieldName)
        => _relations?.Where(r => r.Target.Equals(fieldName, StringComparison.OrdinalIgnoreCase) ||
                                  r.Target.StartsWith($"{fieldName}.", StringComparison.OrdinalIgnoreCase)) ?? [];

    /// <summary>
    /// Mount an attach field after the declared fields and re-sort the attach section.
    /// The field is rebuilt when the same attach property schema is mounted again,
    /// because its property type may have been reloaded with a different value type.
    /// A declared field always wins, and the first attached property schema that
    /// claims a field name wins over later ones (TS resolves by the first match),
    /// so the union of multiple attached kinds can't duplicate or inflate fields.
    /// </summary>
    private void InsertAttachField(StructFieldType field, PropertyType propertyType, string schemaType)
    {
        if (IsDeclaredField(field.Name)) return;

        if (_attachOwners.TryGetValue(field.Name, out string? owner) && !owner.Equals(schemaType, StringComparison.OrdinalIgnoreCase))
            return;

        _attachOwners[field.Name] = schemaType;
        _attachTypes[field.Name] = propertyType;
        int index = GetIndex(field.Name);
        if (index >= 0)
            _fields[index] = field;
        else
            _fields.Add(field);
        SortAttachFields();
    }

    /// <summary>
    /// Whether the field is declared by the struct schema itself.
    /// </summary>
    private bool IsDeclaredField(ReadOnlySpan<char> fieldName)
    {
        for (int i = 0; i < _declaredFieldCount && i < _fields.Count; i++)
        {
            if (fieldName.SeqEquals(_fields[i].Name, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Sort the attach section by the TS priority rule, keeping declared fields first.
    /// </summary>
    private void SortAttachFields()
    {
        if (_fields.Count <= Math.Max(_declaredFieldCount, 0)) return;

        StructFieldType[] declares = _fields.Take(_declaredFieldCount).ToArray();
        StructFieldType[] attaches = _fields.Skip(_declaredFieldCount).ToArray();
        Array.Sort(attaches, (a, b) => CompareAttachFields((a, GetFieldPriority(a)), (b, GetFieldPriority(b))));
        _fields = [.. declares, .. attaches];
    }

    private double GetFieldPriority(StructFieldType field)
        => _attachTypes.TryGetValue(field.Name, out PropertyType? propType) ? GetAttachPropertyPriority(propType) : 0;

    /// <summary>
    /// Mount one attach field after the deferred property type is fully loaded.
    /// Idempotent, the field is skipped when it is already present.
    /// </summary>
    private async Task MountAttachFieldAsync(SchemaContext context, string schemaType, int version, CancellationToken token = default)
    {
        if (version != _loadVersion) return;

        StructSchema? @struct = GetProperty<StructProperty>()?.Value;
        if (@struct == null) return;
        _structSchema ??= @struct;

        PropertyType? propType = await context.GetNodeTypeAsync<PropertyType>(schemaType, token: token).ConfigureAwait(false) as PropertyType;
        if (version != _loadVersion) return;
        if (propType?.ValueType == null || string.IsNullOrEmpty(propType.Name) || string.IsNullOrWhiteSpace(propType.Property)) return;
        if (IsDeclaredField(propType.Property)) return;

        List<(StructFieldType Field, PropertyType PropertyType, string SchemaType)> attachFields = [];
        List<RelationSchema> attachRelations = [];
        await BuildAttachFieldAsync(context, propType, schemaType, attachFields, attachRelations, token).ConfigureAwait(false);
        if (version != _loadVersion) return;

        foreach (var item in attachFields)
            InsertAttachField(item.Field, item.PropertyType, item.SchemaType);

        // Generic structs carry no attach relations, mirroring the TS runtime
        if (IsGeneric) return;

        // Apply struct level relations targeting the newly mounted field
        if (@struct.GetProperty<Relations>()?.Value is { Length: > 0 } relations)
        {
            foreach (RelationSchema relation in relations)
            {
                if (!relation.Target.Equals(propType.Property, StringComparison.OrdinalIgnoreCase) &&
                    !relation.Target.StartsWith($"{propType.Property}.", StringComparison.OrdinalIgnoreCase)) continue;
                await AddRelationAsync(context, relation, token).ConfigureAwait(false);
            }
        }

        foreach (RelationSchema relation in attachRelations)
            await AddRelationAsync(context, relation, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Build the runtime field for an attach property type and collect its relations.
    /// </summary>
    private async Task BuildAttachFieldAsync(SchemaContext context, PropertyType propType, string schemaType,
        List<(StructFieldType Field, PropertyType PropertyType, string SchemaType)> attachFields, List<RelationSchema> attachRelations,
        CancellationToken token = default)
    {
        ValueType? valueType = propType.ValueType;
        if (valueType == null || string.IsNullOrWhiteSpace(propType.Property)) return;

        SchemaRuntime runtime = (context.Runtime as SchemaRuntime)!;
        bool stackable = propType.GetProperty<Stackable>()?.Value ?? false;
        StructFieldSchema fieldSchema = new()
        {
            Name = propType.Property,
            Type = stackable ? $"{NS_SYSTEM_LIST}<{valueType.Name}>" : valueType.Name
        };

        // Copy the properties the field supported, the same filter as the TS runtime
        List<string> kinds = [SCHEMA_KIND_NODE_STRUCT_FIELD, runtime.GetSchemaKindByNodeKind(valueType.Kind)];
        if (valueType is ArrayType { Element: { } element })
            kinds.Add(runtime.GetSchemaKindByNodeKind(element.Kind));

        foreach (IProperty property in propType.GetProperties<IProperty>())
        {
            if (!property.HasValue) continue;
            if (!kinds.Any(kind => runtime.IsPropertyForSchema(property.GetType(), kind))) continue;
            fieldSchema.SetProperty(property);
        }

        StructFieldType fieldType = new();
        await fieldType.LoadAsync(context, fieldSchema, token: token).ConfigureAwait(false);
        attachFields.Add((fieldType, propType, schemaType));

        // Save the property relations, stackable fields are list fields so the
        // relation target must be rewritten to the array element.
        if (propType.GetProperty<Relations>()?.Value is { Length: > 0 } propRelations)
        {
            foreach (RelationSchema relation in propRelations)
            {
                string target = relation.Target ?? string.Empty;
                attachRelations.Add(stackable && target.StartsWith($"{fieldSchema.Name}.", StringComparison.OrdinalIgnoreCase)
                    ? CloneRelation(relation, $"{fieldSchema.Name}.{ARRAY_ELEMENT}.{target[(fieldSchema.Name.Length + 1)..]}")
                    : relation);
            }
        }
    }

    /// <summary>
    /// Add a relation to the struct type, skip if the same relation was applied.
    /// </summary>
    private async Task AddRelationAsync(SchemaContext context, RelationSchema relation, CancellationToken token = default)
    {
        _relationSchemas ??= [];
        if (_relationSchemas.Any(r => r.Equals((PropertyOwner)relation))) return;

        // Gets the target type
        IValueTypeAccess? currentType = GetAccessValueType(relation.Target);
        if (currentType == null) return;

        // Gets the property type
        string? kind = (context.Runtime as SchemaRuntime)?.GetSchemaKindByNodeKind(currentType.Kind);
        PropertyType? prop = await context.GetPropertyType(relation.Property, token, kind, SCHEMA_KIND_NODE_STRUCT_FIELD).ConfigureAwait(false);
        if (prop == null) return;

        // Only work for constraint properties
        if (prop.GetCsharpType() == null) return;

        RelationType relationType = await relation.LoadAsync(context, this, [kind, SCHEMA_KIND_NODE_STRUCT_FIELD], token).ConfigureAwait(false);
        Error ??= relationType.Error;

        _relations ??= [];
        _relations.Add(relationType);
        _relationSchemas.Add(relation);
    }

    private static RelationSchema CloneRelation(RelationSchema relation, string target) => new()
    {
        Target = target,
        TargetType = relation.TargetType,
        TargetKind = relation.TargetKind,
        OwnerKind = relation.OwnerKind,
        Property = relation.Property,
        ValueType = relation.ValueType,
        Kind = relation.Kind,
        Extensions = relation.Extensions?.DeepClone() as System.Text.Json.Nodes.JsonObject
    };

    /// <summary>
    /// Parse the Attach declaration. The value is stored as one string, multiple
    /// kinds are separated by comma or semicolon (the C# declaration can't carry an
    /// array value because the Attach property is a string property).
    /// </summary>
    private static IEnumerable<string> GetAttachKinds(string attach, ISchemaRuntime runtime)
    {
        HashSet<string> handled = new(StringComparer.OrdinalIgnoreCase);
        foreach (string raw in attach.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string kind = ResolveAttachKind(raw, runtime);
            if (string.IsNullOrWhiteSpace(kind) || !handled.Add(kind)) continue;
            yield return kind;
        }
    }

    /// <summary>
    /// Resolve an attach kind, accept both the full schema kind and the unique
    /// short form such as "struct" for "node.struct".
    /// </summary>
    private static string ResolveAttachKind(string kind, ISchemaRuntime runtime)
    {
        foreach ((string schemaKind, _) in runtime.GetSchemaKinds())
        {
            if (schemaKind.Equals(kind, StringComparison.OrdinalIgnoreCase))
                return schemaKind;
        }

        string? match = null;
        foreach ((string schemaKind, _) in runtime.GetSchemaKinds())
        {
            int index = schemaKind.LastIndexOf('.');
            string shortName = index >= 0 ? schemaKind[(index + 1)..] : schemaKind;
            if (!shortName.Equals(kind, StringComparison.OrdinalIgnoreCase)) continue;
            if (match != null) return kind; // ambiguous, leave it unresolved
            match = schemaKind;
        }
        return match ?? kind;
    }

    /// <summary>
    /// Get the priority of an attach property, aligned with the TS struct runtime.
    /// </summary>
    private static double GetAttachPropertyPriority(PropertyType propType)
    {
        ValueType? valueType = propType.ValueType;
        if (valueType == null) return 0;

        double priority = VALUE_TYPE_PRIORITY.GetValueOrDefault(valueType.Kind, 0);
        if (valueType is ArrayType array)
            priority += VALUE_TYPE_PRIORITY.GetValueOrDefault(array.Element?.Kind ?? string.Empty, 0) / 10;
        else if (valueType is StructType @struct)
            return priority + (STRUCT_TYPE_PRIORITY.TryGetValue(@struct.Name, out double structPriority)
                ? structPriority
                : @struct.GetFields().Count() * -10);

        priority += propType.ForSchemas.Count();
        return priority;
    }

    /// <summary>
    /// Sort attach fields by priority desc, display first for locale strings, then by name.
    /// </summary>
    private static int CompareAttachFields((StructFieldType Field, double Priority) a,
        (StructFieldType Field, double Priority) b)
    {
        if (a.Priority > b.Priority) return -1;
        if (a.Priority < b.Priority) return 1;
        if (a.Field.Type?.Name.Equals(NS_SYSTEM_LOCALE_STRING, StringComparison.OrdinalIgnoreCase) == true)
        {
            if (a.Field.Name.Equals("display", StringComparison.OrdinalIgnoreCase)) return -1;
            if (b.Field.Name.Equals("display", StringComparison.OrdinalIgnoreCase)) return 1;
        }
        int compare = string.Compare(a.Field.Name, b.Field.Name, StringComparison.OrdinalIgnoreCase);
        return compare != 0 ? compare : string.CompareOrdinal(a.Field.Name, b.Field.Name);
    }

    #endregion
}

/// <summary>
/// The runtime struct field type
/// </summary>
public class StructFieldType : INodeReferences, IPropertyProvider
{
    /// <summary>
    /// The field name
    /// </summary>
    public string Name { get; private set; } = String.Empty;

    /// <summary>
    /// The type node ref
    /// </summary>
    public ValueType? Type { get; private set; }
    
    /// <summary>
    /// The property info
    /// </summary>
    public PropertyInfo? Property { get; private set; }
    
    /// <summary>
    /// The properties
    /// </summary>
    internal IProperty[]? Properties { get; private set; }

    /// <summary>
    /// The constraint properties
    /// </summary>
    public IEnumerable<IConstraintProperty> Constraints => GetProperties<IConstraintProperty>().Reverse();

    /// <summary>
    /// The ref types from the properties in Extensions
    /// </summary>
    internal NodeType[]? RefTypes { get; private set; }
    
    /// <summary>
    /// The node data is required.
    /// </summary>
    public bool? Require { get; private set; }

    /// <summary>
    /// The node should be display only, won't be submitted.
    /// </summary>
    public bool? DisplayOnly { get; private set; }

    /// <summary>
    /// Unpack/pack additional data for the json node.
    /// </summary>
    public bool? Unpack { get; private set; }

    /// <summary>
    /// The default value of the node.
    /// </summary>
    public IValueAccess? Default { get; private set; }

    /// <summary>
    /// The override field types cache, keyed by the override value type
    /// </summary>
    private Dictionary<ValueType, StructFieldType>? _overrideMap;

    /// <summary>
    /// Gets the struct field type with the overridden value type, the field keeps the same properties
    /// </summary>
    public StructFieldType GetOverrideFieldType(ValueType? type)
    {
        if (type is null || type == Type) return this;
        _overrideMap ??= new Dictionary<ValueType, StructFieldType>();
        if (_overrideMap.TryGetValue(type, out StructFieldType? cached)) return cached;

        StructFieldType field = new()
        {
            Name = Name,
            Type = type,
            Property = Property,
            Properties = Properties,
            RefTypes = RefTypes,
            Require = Require,
            DisplayOnly = DisplayOnly,
            Unpack = Unpack,
            Default = Default
        };
        _overrideMap[type] = field;
        return field;
    }

    /// <summary>
    /// Load struct field schema
    /// </summary>
    internal async Task LoadAsync(SchemaContext context, StructFieldSchema field, IReadOnlyList<GenericParameter>? generics = null, IReadOnlyList<NodeType>? genericParams = null, PropertyInfo? property = null, CancellationToken token = default)
    {
        Property = property;
        Type = await context.GetNodeTypeAsync<ValueType>(field.Type, generics, genericParams, token: token).ConfigureAwait(false);

        if (Type == null)
        {
            field.Error = ErrorCodes.STRUCT_FIELD_WRONG_TYPE;
            return;
        }

        ValueType? propType = Type;
        if (propType is ArrayType arrayType) propType = arrayType.Element;

        // Properties
        var nameProp = new Name();
        nameProp.SetValue(field.Name);

        var propTypes = context.Runtime.GetSchemaKindPropertyTypes(SCHEMA_KIND_NODE_STRUCT_FIELD);
        if (propType != null) propTypes = propTypes.Concat(context.Runtime.GetSchemaKindPropertyTypes((context.Runtime as SchemaRuntime)!.GetSchemaKindByNodeKind(propType.Kind))).Distinct();
        IProperty[] props = field.GetProperties(propTypes).Prepend(nameProp).ToArray();
        
        (RefTypes, string? error) = await field.LoadPropertiesAsync(context, props, Type, token).ConfigureAwait(false);
        field.Error ??= error;
        
        // init
        Name = field.Name;
        Properties = props;

        // Useful properties
        Require = GetProperty<Require>()?.Value;
        DisplayOnly = GetProperty<DisplayOnly>()?.Value;
        Unpack =  GetProperty<Unpack>()?.Value;
        Default = GetProperty<Default>() is {} defProp ? await Type.ValidateValueAsync(context, defProp.Value, token).ConfigureAwait(false) : null;
    }

    /// <summary>
    /// Gets all references types
    /// </summary>
    public IEnumerable<NodeType> GetReferenceTypes()
    {
        if (Type != null) yield return Type;
        if (RefTypes != null)
            foreach (NodeType nodeType in RefTypes)
                yield return nodeType;
    }
    
    /// <summary>
    /// Get the property with property type
    /// </summary>
    public T? GetProperty<T>() where T : class, IProperty 
        => Properties?.OfType<T>().FirstOrDefault() ?? (Type?.GetProperty<T>() is { Inheritable: true } p ? p : null);

    /// <summary>
    /// Gets the properties
    /// </summary>
    public IEnumerable<T> GetProperties<T>() where T : IProperty
        => this.JoinProperties(Properties?.OfType<T>(), Type?.GetProperties<T>().Where(p => p.Inheritable));
}