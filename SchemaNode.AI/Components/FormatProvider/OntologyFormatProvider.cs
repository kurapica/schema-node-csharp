using System.Text;
using SchemaNode.Attribute;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Http;
using SchemaNode.Runtime;

namespace SchemaNode.AI;

[SchemaFormat(OntologyTextTemplates.FormatTurtle)]
[SchemaFormat(OntologyTextTemplates.FormatMarkdown)]
[SchemaFormat(OntologyTextTemplates.FormatJsonLd)]
[SchemaFormat(OntologyTextTemplates.FormatSsp)]
public class OntologyFormatProvider : ISchemaFormatProvider
{
    /// <inheritdoc/>
    public async Task<SchemaApiFile?> GenerateAppSchemaOutput(
        SchemaContext context, AppType app, string format, CancellationToken cancellationToken)
    {
        OntologyGraph graph = await context.BuildAppOntologyAsync(
            app.Name, OntologyOptions.Current.BaseUri, cancellationToken: cancellationToken);

        string content = OntologyTextTemplates.Render(graph, format, context.GetLocale());

        string ext = format.ToLowerInvariant() switch
        {
            OntologyTextTemplates.FormatMarkdown => "md",
            OntologyTextTemplates.FormatJsonLd   => "jsonld",
            OntologyTextTemplates.FormatSsp      => "ssp",
            _                                    => "ttl",
        };

        string safeName = app.Name.Replace('.', '_').Replace(' ', '_');

        return new SchemaApiFile
        {
            Name   = $"{safeName}_ontology.{ext}",
            Stream = new MemoryStream(Encoding.UTF8.GetBytes(content)),
        };
    }
}
