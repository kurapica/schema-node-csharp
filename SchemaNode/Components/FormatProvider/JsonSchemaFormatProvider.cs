using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Http;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;

namespace SchemaNode.Components;

[SchemaFormat("JSON")]
public class JsonSchemaFormatProvider : ISchemaFormatProvider
{
    /// <inheritdoc/>
    public async Task<SchemaApiFile?> GenerateAppSchemaOutput(SchemaContext context, AppType app, string format, CancellationToken cancellationToken)
    {
        AppSchema schema = new()
        {
            Name = app.Name,
            Display = app.Display,
            ScopePolicy = app.ScopePolicy,
            Auth = app.Auth?.Name,
            Auths = app.Auths,
            Status = app.Status,
            HasApps = app.Apps is { Length: > 0 },
            HasFields = app.Fields is { Count: > 0 },
            Workflows = app.Workflows?.Select(w => (AppWorkflowSchema)w).ToArray(),
            Additional = app.Additional,
            Apps = app.Apps?.Select(a =>
            {
                AppType? childNode = app.SubAppList?.Values.FirstOrDefault(p => p.Name.Equals(a.Name, StringComparison.OrdinalIgnoreCase));
                return new AppSchema
                {
                    Name = a.Name,
                    Display = a.Display,
                    ScopePolicy = a.ScopePolicy,
                    Auth = a.Auth,
                    Auths = a.Auths,
                    Status = a.Status,
                    Additional = a.Additional,
                    HasApps = (a.HasApps ?? false) || a.Apps is { Length: > 0 } || childNode?.Apps is { Length: > 0 },
                    HasFields = (a.HasFields ?? false) || a.Fields is { Length: > 0 } || childNode?.Fields is { Count: > 0 },
                };
            }).ToArray(),
        };

        if (app.Fields is { Count: > 0 })
        {
            schema.Fields = app.Fields.Select(p => (AppFieldSchema)p).ToArray();
            schema.Relations = app.Relations?.Select(r => new StructRelationSchema
            {
                Field = !string.IsNullOrEmpty(r.DataField) ? $"{r.AppField}.{r.DataField}" : r.AppField,
                Prop = r.Prop,
                Func = r.Func,
                Args = r.Args.Select(a => new FuncCallArg
                {
                    Name = !string.IsNullOrEmpty(a.DataField) ? $"{a.AppField}.{a.DataField}" : a.AppField,
                    Value = a.Value,
                }).ToArray()
            }).ToArray();

            schema.NodeSchemas = await app.GetNodeSchemas(context, includeUsedBy: false, cancellationToken: cancellationToken);
        }

        // Generate output stream
        return new SchemaApiFile
        {
            Name = $"{app.Name}.json",
            Stream = new MemoryStream(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(schema, Extension.GetJsonOptions(true)))
        };
    }
}
