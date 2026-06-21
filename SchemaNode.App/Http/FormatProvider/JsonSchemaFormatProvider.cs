using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Http;
using SchemaNode.Schema;
using SchemaNode.Utility;

namespace SchemaNode.Components;

[SchemaFormat("JSON")]
public class JsonSchemaFormatProvider : ISchemaFormatProvider
{
    /// <inheritdoc/>
    public async Task<SchemaApiFile?> GenerateAppSchemaOutput(SchemaContext context, Runtime.AppType app, string format, CancellationToken cancellationToken)
    {
        AppSchema schema = app.GetSchema();
        schema.NodeSchemas = await app.GetNodeSchemas(context, includeUsedBy: false, cancellationToken: cancellationToken);

        // Generate output stream
        return new SchemaApiFile
        {
            Name = $"{app.Name}.json",
            Stream = new MemoryStream(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(schema, JsonOptions.GetJsonOptions(true)))
        };
    }
}
