using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SchemaNode.Http;

/// <summary>
/// The schema api document filter
/// </summary>
public class SchemaApiDocumentFilter : IDocumentFilter
{
    private readonly IServiceProvider _services;

    public SchemaApiDocumentFilter(IServiceProvider services)
    {
        _services = services;
    }

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        ISchemaApiProtocol? protocol = _services.GetService<ISchemaApiProtocol>();

        foreach (var ((type, request, response, useDefaultProtocol), url) in Injection.GetSchemaApis())
        {
            var reqSchema = context.SchemaGenerator.GenerateSchema(request, context.SchemaRepository);
            var resSchema = context.SchemaGenerator.GenerateSchema(response, context.SchemaRepository);

            var wrappedReq = useDefaultProtocol ? reqSchema : protocol?.WrapRequestSchema(context, reqSchema) ?? reqSchema;
            var wrappedRes = useDefaultProtocol ? resSchema : protocol?.WrapResponseSchema(context, resSchema) ?? resSchema;

            swaggerDoc.Paths[$"/{url.TrimStart('/')}"] = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>
                {
                    [HttpMethod.Post] = new OpenApiOperation
                    {
                        Tags = new HashSet<OpenApiTagReference> {
                            new OpenApiTagReference(string.Join('.', type.FullName!.Split(".", StringSplitOptions.RemoveEmptyEntries).SkipLast(1).Select(s => s.ToLower(System.Globalization.CultureInfo.CurrentCulture)))) 
                        },
                        Summary = $"Schema API ({type.Name})",
                        RequestBody = new OpenApiRequestBody
                        {
                            Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new OpenApiMediaType { Schema = wrappedReq } }
                        },
                        Responses = new OpenApiResponses
                        {
                            ["200"] = new OpenApiResponse
                            {
                                Description = "Success",
                                Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new OpenApiMediaType { Schema = wrappedRes } }
                            }
                        }
                    }
                }
            };
        }
    }
}