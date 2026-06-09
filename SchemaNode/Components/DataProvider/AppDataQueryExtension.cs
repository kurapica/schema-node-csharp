using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Nodes;
using SchemaNode.Enum;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

public static class AppDataQueryExtension
{
    /// <summary>
    /// Gets the field data
    /// </summary>
    public static async Task<(AnySchemaNode? value, int total)> GetAppFieldDataAsync(this SchemaContext context,
        AppFieldType field, AppSchemaDataResult type, AppSchemaDataFilter? filter = null,
        int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, string? dataField = null, 
        bool forUpdate = false, bool genDisplayOnly = false)
    {
        // Front end only
        if (!field.EnableDynamicTable) return (null, 0);

        var dataProvider = context.GetService<IAppDataProvider>();
        if (dataProvider == null) throw new InvalidOperationException(APP_DATA_PROVIDER_NOT_EXIST);

        DynamicTableSchema schema = await context.PrepareFieldDataAsync(field);

        try
        {
            (AnySchemaNode? result, int total) = await dataProvider
                .QueryDynamicTableAsync(schema, type, filter, skip, take, desc, orderBy, dataField, forUpdate);

            // Generate display only fields
            if (genDisplayOnly)
                await schema.GenerateDisplayOnlyFields(context, result);

            return (result, total);
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Gets the field data
    /// </summary>
    public static async Task<AnySchemaNode?> GetAppFieldDataAsync(this SchemaContext context,
        AppFieldType field, AnySchemaNode nodes, bool forUpdate = false, bool genDisplayOnly = false)
    {
        // Front end only
        if (!field.EnableDynamicTable) return null;

        var dataProvider = context.GetService<IAppDataProvider>();
        if (dataProvider == null) throw new InvalidOperationException(APP_DATA_PROVIDER_NOT_EXIST);

        DynamicTableSchema schema = await context.PrepareFieldDataAsync(field);

        try
        {
            AnySchemaNode? result = null;

            if (field.SchemaType is ArrayType { Primary: { Length: > 0 } } arrType)
            {
                if (nodes is StructTypeNode @struct)
                {
                    AppSchemaDataFilter? filter = null;
                    foreach (string s in arrType.Primary)
                    {
                        var fieldNode = @struct.GetField(s);
                        if (fieldNode == null) return null;
                        var caseFilter = new AppSchemaDataFilterBinary(LogicType.Equal,
                            new AppSchemaDataFilterField(s),
                            new AppSchemaDataFilterValue(fieldNode));
                        filter = filter == null ? caseFilter : filter.AndAlso(caseFilter);
                    }

                    (result, _) = await dataProvider.QueryDynamicTableAsync(schema, AppSchemaDataResult.First, filter,
                        forUpdate: forUpdate);
                }
                else if (nodes is ArrayTypeNode { Count: > 0 } arrNodes)
                {
                    result = await dataProvider.QueryOriginNodesAsync(schema, arrNodes.Cast<StructTypeNode>(),
                        forUpdate);
                }
            }
            else
            {
                // single record, just query directly
                (result, _) = await dataProvider.QueryDynamicTableAsync(schema, AppSchemaDataResult.First, forUpdate: forUpdate);
            }

            // Generate display only fields
            if (genDisplayOnly)
                await schema.GenerateDisplayOnlyFields(context, result);

            return result;
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex.Message);
            throw;
        }
    }
    
    /// <summary>
    /// Gets the field data
    /// </summary>
    public static async Task<AnySchemaNode?> GetAppFieldDataAsync(this SchemaContext context,
        AppFieldType field, IEnumerable<StructTypeNode> nodes, bool forUpdate = false, bool genDisplayOnly = false)
    {
        // Front end only
        if (!field.EnableDynamicTable) return null;

        var dataProvider = context.GetService<IAppDataProvider>();
        if (dataProvider == null) throw new InvalidOperationException(APP_DATA_PROVIDER_NOT_EXIST);

        DynamicTableSchema schema = await context.PrepareFieldDataAsync(field);

        try
        {
            AnySchemaNode? result = null;

            if (field.SchemaType is ArrayType { Primary: { Length: > 0 } })
                result = await dataProvider.QueryOriginNodesAsync(schema, nodes, forUpdate);

            // Generate display only fields
            if (genDisplayOnly)
                await schema.GenerateDisplayOnlyFields(context, result);

            return result;
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex.Message);
            throw;
        }
    }
    
    /// <summary>
    /// Gets the filter field data for data source compile expression
    /// </summary>
    public static async Task<AnySchemaNode?> GetSchemaDataAsync(
        this SchemaContext context, string app, string field, string? target, AppSchemaDataResult type, AppSchemaDataFilter? filter = null, 
        int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, string? dataField = null)
    {
        AppType? appType = await context.GetAppTypeAsync(app);
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
            target = context.GetSchemaContextItem<Access>()?.Target ?? string.Empty;
        
        if (!isValidFilter || string.IsNullOrEmpty(target) && appType?.ScopeType != AppScopeType.SystemLevel) return type switch
        {
            AppSchemaDataResult.Count => SchemaContext.SystemInt.CreateNode(0),
            AppSchemaDataResult.Exist => SchemaContext.SystemBool.CreateNode(false),
            AppSchemaDataResult.First => null,
            AppSchemaDataResult.Last => null,
            AppSchemaDataResult.Field => new ArrayTypeNode(((appField.SchemaType as ArrayType)!.ElementSchemaType as StructType)!.GetField(dataField!)!.SchemaType!),
            _ => new ArrayTypeNode(appField.SchemaType!)
        };
        
        using var stack = context.StackAccess(app, target);
        (AnySchemaNode? res, _) = await context.GetAppFieldDataAsync(appField, type, filter, skip, take, desc, orderBy, dataField);
        return res;
    }
}
