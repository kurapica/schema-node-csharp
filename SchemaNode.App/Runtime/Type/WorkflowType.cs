using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Schema;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.AppConstant;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory workflow schema representation
/// </summary>
public sealed class WorkflowType: NodeType, IValueTypeAccess, IRelationProvider
{
    #region Data

    private WorkflowSchema? _workflowSchema;
    
    /// <summary>
    /// The payload type
    /// </summary>
    public ValueType? Payload  { get; private set; }
    
    /// <summary>
    /// The state type
    /// </summary>
    public ValueType? State  { get; private set; }
    
    /// <summary>
    /// The session type
    /// </summary>
    public ValueType? Session { get; private set; }

    /// <summary>
    /// The workflow arguments fetch from workflow context
    /// </summary>
    public FunctionNodeArgument[] Args { get; private set; } = [];

    /// <summary>
    /// The workflow kind
    /// </summary>
    public string WorkflowKind => _workflowSchema?.Kind ?? WORKFLOW_KIND_WORKFLOW;

    /// <summary>
    /// The relations between the fields
    /// </summary>
    private List<RelationType>? _relations;

    /// <summary>
    /// The system value type
    /// </summary>
    private IValueTypeAccess? _systemObjectType;

    #endregion

    #region Method

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context)
    {
        _workflowSchema = GetProperty<WorkflowProperty>()?.Value;

        if (_workflowSchema == null)
        {
            Error = ErrorCodes.NO_DEFINITION;
            return;
        }

        if (_workflowSchema.Payload != null)
        {
            Payload = await context.GetNodeTypeAsync<ValueType>(_workflowSchema.Payload, Generics, GenericParams);
            if (Payload == null)
                Error ??= AppErrorCodes.WORKFLOW_PAYLOAD_NOT_VALID;
        }

        if (_workflowSchema.Settings != null)
        {
            State = await context.GetNodeTypeAsync<ValueType>(_workflowSchema.Settings, Generics, GenericParams);
            if (State == null)
                Error ??= AppErrorCodes.WORKFLOW_STATE_NOT_VALID;
        }

        if (_workflowSchema.Session != null)
        {
            Session = await context.GetNodeTypeAsync<ValueType>(_workflowSchema.Session, Generics, GenericParams);
            if (Session == null)
                Error ??= AppErrorCodes.WORKFLOW_SESSION_NOT_VALID;
        }

        // for relation
        _systemObjectType = context.System.Object;

        // Args
        Args = _workflowSchema.Args?.Select(a => (FunctionNodeArgument)a).ToArray() ?? [];

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

            // Load Relation
            if (!IsGeneric && _workflowSchema.GetProperty<Relations>()?.Value is { Length: > 0 } relations)
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
    }

    public override void Unload()
    {
        _relations = null;
        Payload = null;
        State = null;
        Session = null;
    }

    public override IEnumerable<NodeType> GetReferenceTypes()
    {
        if (Payload != null)
            yield return Payload;

        if (State != null)
            yield return State;

        if (Session != null)
            yield return Session;
        
        foreach (var nodeType in base.GetReferenceTypes())
            yield return nodeType;
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
        var a = Args.FirstOrDefault(a => a.Name.Equals(access[0], StringComparison.OrdinalIgnoreCase));
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

    #endregion
}