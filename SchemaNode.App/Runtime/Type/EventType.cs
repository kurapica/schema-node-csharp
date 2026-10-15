using SchemaNode.Context;
using SchemaNode.Event;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Schema;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory event schema representation
/// </summary>
public sealed class EventType: NodeType, IValueTypeAccess, IRelationProvider
{
    #region Fields

    private EventSchema? _eventSchema;

    /// <summary>
    /// The relations between the fields
    /// </summary>
    private List<RelationType>? _relations;

    /// <summary>
    /// The system value type
    /// </summary>
    private IValueTypeAccess? _systemObjectType;

    #endregion

    #region Properties

    /// <summary>
    /// The payload type
    /// </summary>
    public ValueType? Payload { get; private set; }

    /// <summary>
    /// The function arguments
    /// </summary>
    public FunctionNodeArgument[] Args { get; private set; } = [];

    /// <inheritdoc />
    public override bool IsUsed => true;

    #endregion
    
    #region Method

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context)
    {
        _eventSchema = GetProperty<EventProperty>()?.Value;
        if (_eventSchema == null)
            Error = ErrorCodes.NO_DEFINITION;

        Args = _eventSchema?.Args?.Select(a => (FunctionNodeArgument)a).ToArray() ?? [];

        if (!string.IsNullOrWhiteSpace(_eventSchema?.Payload))
        {
            Payload = await context.GetNodeTypeAsync<ValueType>(_eventSchema.Payload, Generics, GenericParams);
            if (Payload == null)
                Error ??= AppErrorCodes.EVENT_PAYLOAD_NOT_VALID;
        }

        // Argument types
        HashSet<string> existNames = [];
        foreach (FunctionNodeArgument arg in Args)
        {
            if (string.IsNullOrWhiteSpace(arg.Name))
            {
                Error ??= ErrorCodes.FUNC_ARG_NO_NAME;
                return;
            }
            else if (!existNames.Add(arg.Name))
            {
                Error ??= ErrorCodes.FUNC_ARG_DUPLICATE_NAME;
                return;
            }

            arg.ValueType = !string.IsNullOrWhiteSpace(arg.Type) ? await context.GetNodeTypeAsync<ValueType>(arg.Type, Generics) : null;

            if (arg.ValueType == null)
            {
                Error ??= ErrorCodes.FUNC_ARG_WRONG_TYPE;
                return;
            }
        }

        // for relation
        _systemObjectType = context.System.Object;

        // Load Relation
        if (_eventSchema != null && !IsGeneric && _eventSchema.GetProperty<Relations>()?.Value is { Length: > 0 } relations)
        {
            foreach (RelationSchema relation in relations)
            {
                // Gets the target type
                IValueTypeAccess? currentType = GetAccessValueType(relation.Target);
                if (currentType == null) continue;

                // Gets the property type
                var kind = (context.Runtime as SchemaRuntime)?.GetSchemaKindByNodeKind(currentType.Kind);
                PropertyType? prop = await context.GetPropertyType(relation.Property, kind, SCHEMA_KIND_NODE_FUNC_ARG);
                if (prop == null) continue;

                // Only work for constraint properties
                Type? propType = prop.GetCsharpType();
                if (propType == null) continue;

                var relationType = await relation.LoadAsync(context, this, kind, SCHEMA_KIND_NODE_FUNC_ARG);
                Error ??= relationType.Error;

                _relations ??= [];
                _relations.Add(relationType);
            }
        }
    }

    /// <inheritdoc />
    public override void Unload()
    {
        Payload = null;
        _relations = null;
    }

    /// <inheritdoc />
    public override IEnumerable<NodeType> GetReferenceTypes()
    {
        if (Payload != null)
            yield return Payload;        

        foreach (var t in base.GetReferenceTypes())
            yield return t;
    }

    /// <summary>
    /// Gets relations
    /// </summary>
    public IEnumerable<RelationType> GetRelations() => _relations?.AsEnumerable() ?? [];

    /// <inheritdoc/>
    public IValueTypeAccess? GetAccessValueType(string path)
    {
        string[] access = path.Split('.', 2, StringSplitOptions.TrimEntries);
        if (access[0].Equals(FUNC_RETURN, StringComparison.OrdinalIgnoreCase)) return Payload;
        var a = Args?.FirstOrDefault(a => a.Name.Equals(access[0], StringComparison.OrdinalIgnoreCase));
        if (a != null)
            return a.ValueType;
        return _systemObjectType; // only used to pass the relation checks
    }

    /// <inheritdoc/>
    public IEnumerable<Entry<string>> GetAccessEntries()
    {
        yield return new Entry<string>
        {
            Value = FUNC_RETURN,
            HasChildren = Payload?.HasAccessEntries
        };
        foreach (FunctionNodeArgument a in Args)
        {
            var entry = new Entry<string>
            {
                Value = a.Name,
                HasChildren = false
            };
            var display = a.GetProperty<Display>();
            if (display != null) entry.SetProperty(display);
            yield return entry;
        }
    }

    /// <inheritdoc/>
    public bool IsAssignableTo(IValueTypeAccess other)
    {
        return false;
    }

    /// <inheritdoc/>
    public bool HasAccessEntries => true;

    /// <inheritdoc/>
    public IValueAccess Create(IValueAccess? parent = null, IPropertyProvider? propertyProvider = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public IValueAccess From(object? value, IValueAccess? parent = null, IPropertyProvider? propertyProvider = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public override Type GetCsharpType(bool nullable = false)
    {
        var baseType = base.GetCsharpType();

        if (baseType != null && baseType.IsGenericTypeDefinition && GenericParams is { Count: > 0 })
            baseType = baseType.MakeGenericType(GenericParams.Select(g => g.GetCsharpType()).ToArray()!);

        return baseType ?? typeof(StructNode);
    }


    /// <inheritdoc/>
    public Task<IValueAccess?> ValidateValueAsync(ISchemaContext context, object? value)
    {
        return Task.FromResult<IValueAccess?>(null);
    }

    /// <summary>
    /// Gets the event instance with arguments
    /// </summary>
    public async Task<BaseEvent?> GetEventInstance(SchemaContext context, object?[]? args = null)
    {
        var type = GetCsharpType();
        if (type == null) return null;
        if (Args == null || Args.Length == 0) return Activator.CreateInstance(type) as BaseEvent;
        
        object?[] genArgs = new object?[Args.Length];
        for (int i = 0; i < Args.Length; i++)
        {
            var arg = Args[i];
            var argType = (await context.GetNodeTypeAsync<ValueType>(arg.Type))?.GetCsharpType();
            if (argType == null) return null; // keep simple
            genArgs[i] = argType.TryConvert(args?.ElementAtOrDefault(i), out var result)  ? result : null;
        }
        return Activator.CreateInstance(type, genArgs) as BaseEvent;
    }

    #endregion
}