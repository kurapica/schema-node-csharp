using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Property.App;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory application workflow schema representation.
/// It only references the workflow type definition and delegates the execution to <see cref="WorkflowType"/>.
/// </summary>
public sealed class AppWorkflowType
{
    #region Constructors

    internal AppWorkflowType(AppType app, AppWorkflowSchema schema)
    {
        Application = app;
        _appWorkflowSchema = schema;
        Active = schema.Active;
    }

    #endregion
    
    #region Fields

    private readonly AppWorkflowSchema _appWorkflowSchema;
    private IProperty[]? _props;
    private NodeType[]? _refTypes;
    private WorkflowType? _workflow;

    // There should be only one work flow to active, normally forkable workflow
    private WorkflowContext? _rootWorkflow;
    
    #endregion
    
    #region Properties

    /// <summary>
    /// The application node
    /// </summary>
    public AppType Application { get; }
    
    /// <summary>
    /// The application name
    /// </summary>
    public string App => Application.Name;
    
    /// <summary>
    /// The seqNo
    /// </summary>
    public int Seqno => _appWorkflowSchema.Seqno;

    /// <summary>
    /// The workflow name
    /// </summary>
    public string Name => _appWorkflowSchema.Name;

    /// <summary>
    /// The referenced workflow type
    /// </summary>
    public string Type => _appWorkflowSchema.Type;
        
    /// <summary>
    /// Active the workflow
    /// </summary>
    public bool Active { get; internal set; }
        
    /// <summary>
    /// The app workflow schema
    /// </summary>
    internal AppWorkflowSchema Schema => _appWorkflowSchema;
    
    #endregion
    
    #region States

    /// <summary>
    /// The application field error
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Whether the workflow is activated
    /// </summary>
    public bool Activated => _rootWorkflow != null && _rootWorkflow.GetWorkflowStatus() != Enum.Workflow.WorkflowStatus.Terminated;

    /// <summary>
    /// Gets the root workflow context
    /// </summary>
    public WorkflowContext? RootWorkflowContext => _rootWorkflow;
    
    #endregion

    #region Methods

    /// <summary>
    /// Load the workflow schema
    /// </summary>
    public async Task LoadAsync(SchemaContext context, CancellationToken token = default)
    {
        Error = null;
        _props = _appWorkflowSchema.GetProperties(context.Runtime.GetSchemaKindPropertyTypes(SCHEMA_KIND_APP_WORKFLOW)).ToArray();
        (_refTypes, Error) = await _appWorkflowSchema.LoadPropertiesAsync(context, _props, token: token).ConfigureAwait(false);

        _workflow = !string.IsNullOrWhiteSpace(Type) ? await context.GetNodeTypeAsync<WorkflowType>(Type, token: token).ConfigureAwait(false) : null;

        if (_workflow is null)
            Error ??= AppErrorCodes.APP_WORKFLOW_TYPE_NOT_VALID;
    }

    /// <summary>
    /// Active the workflow
    /// </summary>
    public async Task ActiveAsync(SchemaContext context, CancellationToken token = default)
    {
        if (_workflow == null || Activated) return;
        Active = true;
        _rootWorkflow = await _workflow.ProcessWorkflowAsync(context);
    }

    /// <summary>
    /// Deactivate the workflow
    /// </summary>
    public async Task DeactivateAsync()
    {
        if (!Activated) return;
        await _rootWorkflow!.TerminateAsync().ConfigureAwait(false);
        Active = false;
    }

    /// <summary>
    /// Gets the reference types
    /// </summary>
    public IEnumerable<NodeType> GetReferenceTypes()
    {
        if (_workflow != null)
            foreach (var node in _workflow.GetReferenceTypes())
                yield return node;

        if (_refTypes != null)
            foreach (var node in _refTypes)
                yield return node;
    }

    /// <summary>
    /// Get the application workflow schema
    /// </summary>
    /// <returns></returns>
    public async Task<AppWorkflowSchema> GetSchemaAsync(SchemaContext context, bool noAuth = false, CancellationToken token = default)
    {
        AppWorkflowSchema schema = new AppWorkflowSchema
        {
            App = _appWorkflowSchema.App,
            Name = _appWorkflowSchema.Name,
            Seqno = _appWorkflowSchema.Seqno,
            Active = _appWorkflowSchema.Active,
            Type = _appWorkflowSchema.Type,
        };
        schema.CombineProperties(_appWorkflowSchema);

        // The auth properties
        if (!noAuth)
        {
            bool isSystem = Application.GetProperty<SystemDefined>()?.Value == true;
            schema.SetProperty<SchemaCreate, bool>(!isSystem && await context.AuthorizeAsync(this, PolicyScope.SchemaCreate, true, token).ConfigureAwait(false));
            schema.SetProperty<SchemaRead, bool>(await context.AuthorizeAsync(this, PolicyScope.SchemaRead, true, token).ConfigureAwait(false));
            schema.SetProperty<SchemaUpdate, bool>(!isSystem && await context.AuthorizeAsync(this, PolicyScope.SchemaUpdate, true, token).ConfigureAwait(false));
            schema.SetProperty<SchemaDelete, bool>(!isSystem && await context.AuthorizeAsync(this, PolicyScope.SchemaDelete, true, token).ConfigureAwait(false));
        }
        
        return schema;
    }

    /// <summary>
    /// Gets the property with given type
    /// </summary>
    public T? GetProperty<T>() where T : class, IProperty => _props?.OfType<T>().FirstOrDefault();

    /// <summary>
    /// Gets the constraints
    /// </summary>
    public IEnumerable<T> GetProperties<T>() => _props?.OfType<T>() ?? [];
    
    /// <summary>
    /// Gets the property by property name
    /// </summary>
    public IProperty? GetProperty(string propertyName) => _props?.FirstOrDefault(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));

    #endregion
}
