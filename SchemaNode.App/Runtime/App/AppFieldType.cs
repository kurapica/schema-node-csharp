using System.Collections.Concurrent;
using System.Reflection;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Text.RegularExpressions;
using SchemaNode.Function;
using SchemaNode.Property.App;
using SchemaNode.Relation;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using DataCombine = SchemaNode.Schema.DataCombine;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBePrivate.Global

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory application field schema representation
/// </summary>
public sealed class AppFieldType
{
    #region Constructors

    internal AppFieldType(AppType app, AppFieldSchema schema)
    {
        Application = app;
        _appFieldSchema = schema;
    }

    #endregion
    
    #region Fields

    private readonly AppFieldSchema _appFieldSchema;
    private IProperty[]? _props;
    private NodeType[]? _refTypes;
    private ConcurrentDictionary<Type, object>? _items;
    private List<PropertyInfo>? _primarys;
    
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
    /// The seqno
    /// </summary>
    public int Seqno => _appFieldSchema.Seqno;

    /// <summary>
    /// The field name.
    /// </summary>
    public string Name => _appFieldSchema.Name;

    /// <summary>
    /// The field type.
    /// </summary>
    public string Type => _appFieldSchema.Type;

    /// <summary>
    /// The field is disabled
    /// </summary>
    public bool? Disable { get; private set; }
    
    /// <summary>
    /// The field type node
    /// </summary>
    public ValueType? ValueType { get; private set; }
    
    #endregion
    
    #region Foreign & View
    
    /// <summary>
    /// The foreign key settings
    /// </summary>
    public Foreign[]? Foreigns { get; private set; }

    /// <summary>
    /// The field view setting
    /// </summary>
    public FieldView? View { get; private set; }

    /// <summary>
    /// Whether the field is a view for foreign view, only readable
    /// </summary>
    public bool IsForeignView => !string.IsNullOrWhiteSpace(View?.App);

    #endregion
    
    #region Push

    /// <summary>
    /// The field function node
    /// </summary>
    public FunctionType? PushFunc { get; private set; }

    /// <summary>
    /// The call arguments
    /// </summary>
    public AppFieldType? PushSource { get; private set; }
    
    /// <summary>
    /// The push function schema
    /// </summary>
    public FunctionTypeSchema? PushFuncSchema { get; private set; }
    
    /// <summary>
    /// The third-party push field info
    /// </summary>
    public DataPushThirdFieldInfo[]? ThirdPushFields { get; private set; }

    #endregion
    
    #region Storage

    /// <summary>
    /// Enable the backend storage
    /// </summary>
    public bool EnableStorage { get; private set; }
    
    /// <summary>
    /// The app field storage topology
    /// </summary>
    public FieldStorageTopology? Topology { get; private set; }
    /// <summary>
    /// The storage table name
    /// </summary>
    public string? TableName { get; private set; }
    
    /// <summary>
    /// The entity attribute value table name
    /// </summary>
    public string? AttrTableName { get; private set; }
    
    /// <summary>
    /// The app field is using increase update mode, no full data push allowed, always using page query
    /// </summary>
    public bool? Pageable { get; private set; }
    
    #endregion
    
    #region The data combine rules

    /// <summary>
    /// The combine rule for scalar/enum type
    /// </summary>
    public DataCombineType? Combine => _appFieldSchema.Combine;
    
    /// <summary>
    /// The combine rule for struct or struct-array type
    /// </summary>
    public DataCombine[]? Combines => _appFieldSchema.Combines;

    #endregion

    #region Field Filter

    /// <summary>
    /// The field filters
    /// </summary>
    public FieldFilter[]? Filters { get; private set; }

    #endregion
    
    #region States

    /// <summary>
    /// The application field error
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Enable dynamic table
    /// </summary>
    public bool EnableDynamicTable => Disable != true && EnableStorage;

    /// <summary>
    /// Has observers
    /// </summary>
    public bool HasObserver => _observers is { Count: > 0 }; 

    #endregion

    #region Relationship

    /// <summary>
    /// The fields that subscribe the update of this field.
    /// </summary>
    public IReadOnlyList<AppFieldType>? Observers => _observers;
    
    /// <summary>
    ///  the observers in the same app
    /// </summary>
    List<AppFieldType>? _observers;
    
    #endregion
    
    #region Method

    /// <summary>
    /// Load the app field schema
    /// </summary>
    public async Task LoadAsync(SchemaContext context)
    {
        Error = null;
        
        ValueType = await context.GetNodeTypeAsync<ValueType>(Type);
        if (ValueType == null) Error = AppErrorCodes.APP_FIELD_TYPE_NOT_VALID;
        
        _props = _appFieldSchema.GetProperties(context.Runtime.GetSchemaKindPropertyTypes(SCHEMA_KIND_APP_FIELD)).ToArray();
        (_refTypes, Error) = await _appFieldSchema.LoadPropertiesAsync(context, _props, ValueType);

        _appFieldSchema.Error = Error;
        Foreigns = _appFieldSchema.Foreigns;
        View = _appFieldSchema.View;
        
        // Cache
        Disable = GetProperty<Disable>()?.Value;
        EnableStorage = GetProperty<EnableStorage>()?.Value ?? false;
        Topology = GetProperty<Topology>()?.Value;
        TableName = GetProperty<TableName>()?.Value;
        AttrTableName = GetProperty<AttrTableName>()?.Value;
        Pageable = GetProperty<Pageable>()?.Value;
        Filters = GetProperty<Filters>()?.Value;
        
        // loading
        StructType? structType = ((ValueType as ArrayType)?.Element ?? ValueType) as StructType;
        
        // primary property info
        if (ValueType is ArrayType arr && arr.Primary is { Count: > 0} && structType != null && structType.GetCsharpType() is { } ctype)
        {
            _primarys = [];
            foreach (string primary in arr.Primary)
            {
                _primarys.Add(ctype.GetProperty(primary, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                    ?? throw new Exception($"Primary property {primary} not found in type {ctype.FullName} for app {App} field {Name}"));
            }
        }

        // Loading source & push
        if (!string.IsNullOrWhiteSpace(_appFieldSchema.Source))
        {
            if (await context.GetNodeTypeAsync<FunctionType>(_appFieldSchema.Push ?? string.Empty) is { Args.Length: 1 } funcNode)
            {
                PushFunc = funcNode;
                
                AppFieldType? pushSource = Application.GetField(_appFieldSchema.Source);
                if (pushSource == null || (pushSource.ValueType ?? await context.GetNodeTypeAsync<ValueType>(pushSource.Type))
                    is not ArrayType { Element: not null, Primary: { Count: > 0}} array ||
                    funcNode.Args[0].ValueType != null && funcNode.Args[0].ValueType is not GenericType && 
                    !array.Element.IsAssignableTo(funcNode.Args[0].ValueType!))
                {
                    Error = AppErrorCodes.APP_FIELD_PUSH_FUNC_NOT_VALID;
                }
                else
                {
                    // Register to observers
                    pushSource.AddObserver(this);
                    PushSource = pushSource;
        
                    // Compile with data push compile context
                    funcNode.ClearRuntimeFuncCache<DataPushCompileContext>(); // must reset the field reference
                    DataPushCompileContext compileContext = new DataPushCompileContext(context, funcNode);
                    try
                    {
                        FunctionTypeSchema pushSchema = await compileContext.VisitFunctionType();
                        DataPushThirdFieldInfo[] pushField = compileContext.ThirdFields;
                        if (pushField.Length > 0)
                        {
                            ThirdPushFields = compileContext.ThirdFields;
                            foreach (DataPushThirdFieldInfo push in pushField)
                                Application.GetField(push.Field)?.AddObserver(this);
                        }
                        PushFuncSchema = pushSchema;
                    }
                    catch(FunctionVisitException fv)
                    {
                        Error = fv.Status;
                    }
                    catch(Exception ex)
                    {
                        context.LogError(ex,$"AppType.LoadAsync: push function compile error for app {App} field {Name}");
                        Error = AppErrorCodes.APP_FIELD_PUSH_FUNC_NOT_VALID;
                    }
                }
            }
            else
            {
                Error ??= AppErrorCodes.APP_FIELD_PUSH_FUNC_NOT_VALID;
            }
        }
        
        // valid the foreign key reference
        if (Foreigns is { Length: > 0})
        {
            foreach (Foreign foreign in Foreigns)
            {
                if (string.IsNullOrWhiteSpace(foreign.Field) ||
                    string.IsNullOrWhiteSpace(foreign.App) ||
                    await context.GetAppTypeAsync(foreign.App) is not { } refApp ||
                    refApp.ScopeType == AppScopeType.SystemLevel ||
                    structType?.GetField(foreign.Field) == null)
                {
                    Error ??= AppErrorCodes.APP_FIELD_FOREIGN_NOT_VALID;
                    break;
                }
                foreign.AppType = refApp;
            }
        }

        // Check source app & field as view
        if (!string.IsNullOrWhiteSpace(View?.App) || !string.IsNullOrWhiteSpace(View?.Field))
        {
            if (structType == null || string.IsNullOrWhiteSpace(View?.App) || string.IsNullOrWhiteSpace(View?.Field) ||
                await context.GetAppTypeAsync(View.App) is not { } sourceApp ||
                sourceApp.ScopeType == AppScopeType.SystemLevel ||
                sourceApp.GetField(View.Field) is not { } sourceField ||
                sourceField.Foreigns == null || sourceField.Foreigns.Length == 0 || 
                sourceField.Foreigns.All(f => !f.App.Equals(Name, StringComparison.OrdinalIgnoreCase)) ||
                !string.IsNullOrWhiteSpace(View.Map) && structType.GetField(View.Map) == null)
            {
                Error ??= AppErrorCodes.APP_FIELD_VIEW_NOT_VALID;
            }
            else
            {
                View.AppType = sourceApp;
                var sourceFieldType = sourceField.ValueType;
                if (sourceFieldType is ArrayType arrType)
                    sourceFieldType = arrType.Element;

                if (sourceFieldType is not StructType sourceStruct)
                {
                    Error ??= AppErrorCodes.APP_FIELD_VIEW_NOT_VALID;
                }
                else
                {
                    // Check fields
                    foreach (StructFieldType f in structType.GetFields())
                    {
                        if (f.Type == null)
                        {
                            Error ??= AppErrorCodes.APP_FIELD_VIEW_NOT_VALID;
                            break;
                        }

                        if (f.Name.Equals(View.Map, StringComparison.OrdinalIgnoreCase))
                            continue;

                        // Match the source field
                        if (f.DisplayOnly == true)
                        {
                            // Can be generated by other fields, or from the other field of the source app
                            RelationType? relation = structType.GetRelations(f.Name).FirstOrDefault(r => r.ForProperty<Default>());

                            if (relation is not { Process: CallProcess call } ||$"{NS_SYSTEM_DATA}.{nameof(SystemAppData.getfield)}".Equals(call.Func, StringComparison.OrdinalIgnoreCase) &&
                                !sourceApp.Name.Equals(call.Args.FirstOrDefault()?.Value?.ToValue<string>(), StringComparison.OrdinalIgnoreCase))
                            {
                                Error ??= AppErrorCodes.APP_FIELD_VIEW_NOT_VALID;
                                break;
                            }
                        }
                        else if (sourceStruct.GetField(f.Name) is { Type: not null } sourceFieldMatch)
                        {
                            if (!sourceFieldMatch.Type.IsAssignableTo(f.Type))
                            {
                                Error ??= AppErrorCodes.APP_FIELD_VIEW_NOT_VALID;
                                break;
                            }
                        }
                        else
                        {
                            Error ??= AppErrorCodes.APP_FIELD_VIEW_NOT_VALID;
                            break;
                        }
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Gets the reference types
    /// </summary>
    public IEnumerable<NodeType> GetReferenceTypes()
    {
        if (ValueType != null)
            yield return ValueType;
        
        if (_refTypes != null)
            foreach (var node in _refTypes)
                yield return node;
    }

    /// <summary>
    /// Get the application field schema
    /// </summary>
    public async Task<AppFieldSchema> GetSchemaAsync(SchemaContext context)
    {
        AppFieldSchema schema = new AppFieldSchema
        {
            App = _appFieldSchema.App,
            Name = _appFieldSchema.Name,
            Seqno = _appFieldSchema.Seqno,
            Type = _appFieldSchema.Type,
            Source = _appFieldSchema.Source,
            Push =  _appFieldSchema.Push,
            Combine = _appFieldSchema.Combine,
            Combines = _appFieldSchema.Combines?.ToArray(),
            Foreigns = _appFieldSchema.Foreigns?.Select(f => new Foreign
            {
                App = f.App,
                Field = f.Field,
            }).ToArray(),
            View = _appFieldSchema.View != null ? new FieldView
            {
                App = _appFieldSchema.View.App,
                Field = _appFieldSchema.View.Field,
                Map = _appFieldSchema.View.Map,
            } : null,
        };
        schema.CombineProperties(_appFieldSchema);
        
        // The auth properties
        schema.SetProperty<SchemaCreate, bool>(await context.AuthorizeAsync(this, PolicyScope.SchemaCreate, true));
        schema.SetProperty<SchemaRead, bool>(await context.AuthorizeAsync(this, PolicyScope.SchemaRead, true));
        schema.SetProperty<SchemaUpdate, bool>(await context.AuthorizeAsync(this, PolicyScope.SchemaUpdate, true));
        schema.SetProperty<SchemaDelete, bool>(await context.AuthorizeAsync(this, PolicyScope.SchemaDelete, true));
        schema.SetProperty<DataCreate, bool>(await context.AuthorizeAsync(this, PolicyScope.DataCreate, true));
        schema.SetProperty<DataRead, bool>(await context.AuthorizeAsync(this, PolicyScope.DataRead, true));
        schema.SetProperty<DataUpdate, bool>(await context.AuthorizeAsync(this, PolicyScope.DataUpdate, true));
        schema.SetProperty<DataDelete, bool>(await context.AuthorizeAsync(this, PolicyScope.DataDelete, true));
        
        // The block columns
        // column access check
        if ((ValueType is ArrayType arr ? arr.Element : ValueType) is StructType @struct)
        {
            List<string>? ignoreFields = null;
            foreach (StructFieldType f in @struct.GetFields())
            {
                // Authorize with order
                bool authorized = true;
                foreach (string evaluator in this.GetColPolicies(f.Name))
                {
                    authorized = await context.AuthorizeAsync(evaluator, true);
                    if (authorized) break;
                }

                if (authorized) continue;

                ignoreFields ??= [];
                ignoreFields.Add(f.Name);
            }

            if (ignoreFields is { Count: > 0 })
                schema.SetProperty<BlockColumns, string[]>(ignoreFields.ToArray());
        }

        return schema;
    }
    
    /// <summary>
    /// Gets the property with given type
    /// </summary>
    public T? GetProperty<T>() where T : class, IProperty => _props?.OfType<T>().FirstOrDefault() ?? ValueType?.GetProperty<T>();

    /// <summary>
    /// Gets the constraints
    /// </summary>
    public IEnumerable<T> GetProperties<T>() where T : class, IProperty
    {
        if (_props != null)
        {
            foreach (var prop in _props.OfType<T>())
            {
                yield return prop;
                if (!prop.Stackable) yield break;
            }
        }
        if (ValueType  != null)
        {
            foreach (var prop in ValueType.GetProperties<T>())
            {
                yield return prop;
                if (!prop.Stackable) yield break;
            }
        }
    }
    
    /// <summary>
    /// Gets the primary properties for the struct array type, return empty if the field is not struct array or no primary defined
    /// </summary>
    public IReadOnlyList<PropertyInfo> GetPrimaryProperties() => _primarys ?? [];
    
    /// <summary>
    /// Add observer
    /// </summary>
    public void AddObserver(AppFieldType observer)
    {
        _observers ??= [];
        if (!_observers.Contains(observer))
            _observers.Add(observer);
    }
    
    /// <summary>
    /// Validate field by DynamicTableSchema and return nullable
    /// </summary>
    public async Task<DataNode?> ValidateDataAsync(SchemaContext context, object? value)
        => await ValueType!.ValidateValueAsync(context, value);

    /// <summary>
    /// Save item for the app field type
    /// </summary>
    public void SetItem<T>(T? obj)
    {
        if (obj != null)
        {
            _items ??= [];
            _items[typeof(T)] = obj;
        }
        else
        {
            _items?.TryRemove(typeof(T), out _);
        }
    }
    
    /// <summary>
    /// Gets item
    /// </summary>
    public T? GetItem<T>() => _items?.TryGetValue(typeof(T), out object? obj) == true ? (T)obj : default(T?);
    
    #endregion

    #region Dynamic table

    // Gets the data field dynamic table name
    public string DynamicTableName => 
        IsForeignView 
        ? View!.AppType?.GetField(View.Field)?.DynamicTableName ?? throw new Exception($"Foreign view app {View.App} or field {View.Field} not exist")
        : !string.IsNullOrWhiteSpace(TableName)
            ? TableName 
            : $"{DYNAMIC_TABLE_PREFIX}_{Regex.Replace(App, @"\W+", "_")}_{Name}";
    
    /// <summary>
    /// Gets the attribute table name for the field, which is used to store the attribute data of the field, only for dynamic type
    /// </summary>
    public string AttributeTableName => 
        IsForeignView
        ? View!.AppType?.GetField(View.Field)?.AttributeTableName ?? throw new Exception($"Foreign view app {View.App} or field {View.Field} not exist")
        : Topology== FieldStorageTopology.AttributeBased
            ? (!string.IsNullOrWhiteSpace(AttrTableName) 
                ? AttrTableName 
                : !string.IsNullOrWhiteSpace(TableName) 
                    ? $"{EAV_TABLE_PREFIX}_{TableName}" 
                    : $"{EAV_TABLE_PREFIX}_{Regex.Replace(App, @"\W+", "_")}_{Name}")
            : string.Empty;

    #endregion
}
