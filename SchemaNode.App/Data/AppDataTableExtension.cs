using SchemaNode.Context;
using SchemaNode.Runtime;
using SchemaNode.Components;
using ValueType = SchemaNode.Runtime.ValueType;

namespace SchemaNode.Data;

public static class AppDataTableExtension
{
    /// <summary>
    /// Prepare the dynamic table for the field
    /// </summary>
   internal static async Task<DynamicTableSchema> PrepareFieldDataAsync(this SchemaContext context, AppFieldType field)
    {
        // no front only & enable & no source ref
        if (!field.EnableDynamicTable) return field.GetDynamicTableSchema();

        // Return the data
        DynamicTableSchema? schema = field.GetItem<DynamicTableSchema>();
        if (schema != null) return schema;

        IAppDataProvider dataProvider = context.GetService<IAppDataProvider>() ?? throw new InvalidOperationException("The app data provider is not available");
        using ICriticalRegion locker = await context.GetLockAsync($"SCHEMA_CONTEXT_DYN_TABLE_CREATION:{field.DynamicTableName}");
        try
        {
            schema = field.GetItem<DynamicTableSchema>();
            if (schema != null) return schema;

            schema = field.GetDynamicTableSchema();
            
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

            return schema;
        }
        catch (Exception ex)
        {
            context.LogError(ex, $"PrepareFieldDataAsync {field.DynamicTableName} Error");
            throw;
        }
    }
    
    internal static async Task<AppFieldType> AssertAppField<T>(this SchemaContext context)
    {
        (string app, string field)? app = (context.Runtime as AppSchemaRuntime)?.GetSystemAppField<T>();
        if (app == null) throw new ArgumentException($"The type {typeof(T).FullName} is not a valid app field data type");
        return (await context.GetAppTypeAsync(app.Value.app))?.GetField(app.Value.field) ?? throw new ArgumentException($"The type {typeof(T).FullName} is not a valid app field data type");
    }

    /// <summary>
    /// Assert the app field type contains the value type
    /// </summary>
    internal static void AssertType<T>(this SchemaContext context, AppFieldType field)
    {
        ValueType? type = field.ValueType;
        if (type is ArrayType arr) type = arr.Element;
        Type? ctype = type?.GetCsharpType();
        if (ctype == null || !ctype.IsAssignableFrom(typeof(T)))
            throw new ArgumentException("The app field type don't contains the value type");
    }
}
