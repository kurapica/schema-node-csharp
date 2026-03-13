using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using System.Reflection;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

public static class AppDataTableExtension
{
    /// <summary>
    /// Prepare the dynamic table for the field
    /// </summary>
   internal static async Task<DynamicTableSchema> PrepareFieldDataAsync(this SchemaContext context, AppFieldType field)
    {
        // no front only & enable & no source ref
        if (!field.EnableDynamicTable)
            return field.Schema ??= field.GenDynamicTableSchema();

        // Return the data
        DynamicTableSchema? schema = field.Schema;
        if (schema != null) return schema;

        IAppDataProvider dataProvider = context.GetService<IAppDataProvider>() ?? throw new InvalidOperationException(APP_DATA_PROVIDER_NOT_EXIST);
        using ICriticalRegion locker = await context.GetLockAsync($"SCHEMA_CONTEXT_DYN_TABLE_CREATION:{field.DynamicTableName}");
        try
        {
            schema = field.Schema;
            if (schema != null) return schema;

            schema = field.GenDynamicTableSchema();
            if (field.SystemMaintain != true)
            {
                // Makes sure the source field is prepared
                if (field.IsForeignView)
                {
                    AppFieldType foreignField = (await context.GetAppTypeAsync(field.View!.App))?.GetField(field.View.Field) ?? throw new InvalidOperationException($"Foreign view field {field.View.App}.{field.View.Field} not exist");
                    await context.PrepareFieldDataAsync(foreignField);
                }
                else
                {
                    // Prepare the dynamic table and join fields
                    await dataProvider.EnsureDynamicTableAsync(schema);
                    if (schema.Joins is { Length: > 0 })
                    {
                        foreach (DynamicTableJoin join in schema.Joins)
                        {
                            AppFieldType joinField = field.Application.GetField(join.Field) ?? throw new InvalidOperationException($"Join field {join.Field} not exist");
                            await context.PrepareFieldDataAsync(joinField);
                        }
                    }
                }
            }

            field.Schema = schema;
            return schema;
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, $"PrepareFieldDataAsync {field.DynamicTableName} Error");
            throw;
        }
    }
    internal static async Task<(AppFieldType appField, IReadOnlyList<PropertyInfo>? primarys)> AssertAppField<T>(this SchemaContext context)
    {
        (string app, string field)? app = typeof(T).GetSystemAppField();
        if (app == null) throw new ArgumentException($"The type {typeof(T).FullName} is not a valid app field data type");

        AppFieldType appFieldType = (await context.GetAppTypeAsync(app.Value.app))?.GetField(app.Value.field) ?? throw new ArgumentException($"The type {typeof(T).FullName} is not a valid app field data type");

        if (appFieldType.SchemaType is ArrayType arrType && arrType.ElementSchemaType is StructType @struct)
        {
            IReadOnlyList<PropertyInfo> primarys = @struct.GetCSharpProperties(true) ?? throw new ArgumentException($"The type {typeof(T).FullName} is not a valid app field data type");
            return (appFieldType, primarys);
        }
        else
        {
            return (appFieldType, null);
        }
    }

    /// <summary>
    /// Assert the app field type contains the value type
    /// </summary>
    internal static void AssertType<T>(this SchemaContext context, AppFieldType field)
    {
        AnySchemaType? type = field.SchemaType;
        if (type is ArrayType arr) type = arr.ElementSchemaType;
        Type? ctype = type?.ToCSharpType();
        if (ctype == null || !ctype.IsAssignableFrom(typeof(T)))
            throw new ArgumentException("The app field type don't contains the value type");
    }
}
