using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Enum;

namespace SchemaNode.Data;

public static class AppDataQueryExtension
{
    /// <summary>
    /// Gets the field data
    /// </summary>
    public static async Task<(IValueAccess? value, int total)> GetAppFieldDataAsync(this SchemaContext context,
        AppFieldType field, AppSchemaDataResult type, AppSchemaDataFilter? filter = null,
        int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, string? dataField = null, 
        bool forUpdate = false, bool genDisplayOnly = false, CancellationToken token = default)
    {
        // Front end only
        if (!(field.EnableDynamicTable || field.IsForeignView)) return (null, 0);

        var dataProvider = context.GetService<IAppDataProvider>();
        if (dataProvider == null) throw new InvalidOperationException("The data provider is not configured");

        DynamicTableSchema schema = await context.PrepareFieldDataAsync(field, token).ConfigureAwait(false);

        try
        {
            (IValueAccess? result, int total) = await dataProvider
                .QueryDynamicTableAsync(schema, type, filter, skip, take, desc, orderBy, dataField, forUpdate, token).ConfigureAwait(false);

            // Generate display only fields
            if (genDisplayOnly)
                await schema.GenerateDisplayOnlyFields(context, result, token).ConfigureAwait(false);

            return (result, total);
        }
        catch (Exception ex)
        {
            context.LogError(ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Gets the field data
    /// </summary>
    public static async Task<IValueAccess?> GetAppFieldDataAsync(this SchemaContext context,
        AppFieldType field, IValueAccess nodes, bool forUpdate = false, bool genDisplayOnly = false, CancellationToken token = default)
    {
        // Front end only
        if (!field.EnableDynamicTable) return null;

        var dataProvider = context.GetService<IAppDataProvider>();
        if (dataProvider == null) throw new InvalidOperationException("The data provider is not configured");

        DynamicTableSchema schema = await context.PrepareFieldDataAsync(field, token).ConfigureAwait(false);

        try
        {
            IValueAccess? result = null;

            if (field.ValueType is ArrayType { Primary: { Count: > 0 } } arrType)
            {
                if (nodes is StructNode @struct)
                {
                    AppSchemaDataFilter? filter = null;
                    foreach (string s in arrType.Primary)
                    {
                        var fieldNode = @struct.GetAccessValue(s);
                        if (fieldNode == null) return null;
                        var caseFilter = new AppSchemaDataFilterBinary(LogicType.Equal,
                            new AppSchemaDataFilterField(s),
                            new AppSchemaDataFilterValue(fieldNode));
                        filter = filter == null ? caseFilter : filter.AndAlso(caseFilter);
                    }

                    (result, _) = await dataProvider.QueryDynamicTableAsync(schema, AppSchemaDataResult.First, filter,
                        forUpdate: forUpdate, token: token).ConfigureAwait(false);
                }
                else if (nodes is ArrayNode { Count: > 0 } arrNodes)
                {
                    result = await dataProvider.QueryOriginNodesAsync(context, schema, arrNodes.OfType<StructNode>(),
                        forUpdate, token).ConfigureAwait(false);
                }
            }
            else
            {
                // single record, just query directly
                (result, _) = await dataProvider.QueryDynamicTableAsync(schema, AppSchemaDataResult.First, forUpdate: forUpdate, token: token).ConfigureAwait(false);
            }

            // Generate display only fields
            if (genDisplayOnly)
                await schema.GenerateDisplayOnlyFields(context, result, token).ConfigureAwait(false);

            return result;
        }
        catch (Exception ex)
        {
            context.LogError(ex.Message);
            throw;
        }
    }
    
    /// <summary>
    /// Gets the field data
    /// </summary>
    public static async Task<IValueAccess?> GetAppFieldDataAsync(this SchemaContext context,
        AppFieldType field, IEnumerable<StructNode> nodes, bool forUpdate = false, bool genDisplayOnly = false, CancellationToken token = default)
    {
        // Front end only
        if (!field.EnableDynamicTable) return null;

        var dataProvider = context.GetService<IAppDataProvider>();
        if (dataProvider == null) throw new InvalidOperationException("The data provider is not configured");

        DynamicTableSchema schema = await context.PrepareFieldDataAsync(field, token).ConfigureAwait(false);

        try
        {
            IValueAccess? result = null;

            if (field.ValueType is ArrayType { Primary: { Count: > 0 } })
                result = await dataProvider.QueryOriginNodesAsync(context, schema, nodes, forUpdate, token).ConfigureAwait(false);

            // Generate display only fields
            if (genDisplayOnly)
                await schema.GenerateDisplayOnlyFields(context, result, token).ConfigureAwait(false);

            return result;
        }
        catch (Exception ex)
        {
            context.LogError(ex.Message);
            throw;
        }
    }
    
    /// <summary>
    /// Gets the filter field data for data source compile expression
    /// </summary>
    public static async Task<IValueAccess?> GetSchemaDataAsync(
        this SchemaContext context, string app, string field, string? target, AppSchemaDataResult type, AppSchemaDataFilter? filter = null, 
        int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, string? dataField = null,
        CancellationToken token = default)
    {
        AppType? appType = await context.GetAppTypeAsync(app, token: token).ConfigureAwait(false);
        AppFieldType? appField = appType?.GetField(field);
        if (appField == null) return null;
        
        // Validate and transform filter
        bool isValidFilter = filter == null;
        if (filter != null)
        {
            isValidFilter = filter.Transform(out AppSchemaDataFilter? final);
            filter = final;

            // Avoid invalid filter types like false means no data
            if (isValidFilter && filter is AppSchemaDataFilterValue or AppSchemaDataFilterField)
                isValidFilter = false;
        }

        if (string.IsNullOrEmpty(target))
            target = context.GetContextItem<Access>()?.Target ?? string.Empty;
        
        if (!isValidFilter || string.IsNullOrEmpty(target) && appType?.ScopeType != AppScopeType.SystemLevel) return type switch
        {
            AppSchemaDataResult.Count => context.System.Int.Create(context, 0),
            AppSchemaDataResult.Exist => context.System.Bool.Create(context, false),
            AppSchemaDataResult.First => null,
            AppSchemaDataResult.Last => null,
            AppSchemaDataResult.Field => new ArrayNode(context, ((appField.ValueType as ArrayType)!.Element as StructType)!.GetField(dataField!)!.Type!),
            _ => new ArrayNode(context, appField.ValueType!)
        };
        
        using var stack = context.StackAccess(app, target);
        (IValueAccess? res, _) = await context.GetAppFieldDataAsync(appField, type, filter, skip, take, desc, orderBy, dataField, token: token).ConfigureAwait(false);
        return res;
    }
}
