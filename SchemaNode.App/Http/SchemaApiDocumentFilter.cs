using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using SchemaNode.App.Http;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SchemaNode.App.Http;

/// <summary>
/// Swagger document filter that adds OpenAPI path entries for all registered <see cref="SchemaApi{TRequest,TResponse}"/> types.
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

        foreach (var (apiType, url) in AppInjection.GetSchemaApis())
        {
            var reqSchema = context.SchemaGenerator.GenerateSchema(apiType.Request, context.SchemaRepository);
            var resSchema = context.SchemaGenerator.GenerateSchema(apiType.Response, context.SchemaRepository);

            var wrappedReq = apiType.UseDefaultProtocol ? reqSchema : protocol?.WrapRequestSchema(context, reqSchema) ?? reqSchema;
            var wrappedRes = apiType.UseDefaultProtocol ? resSchema : protocol?.WrapResponseSchema(context, resSchema) ?? resSchema;

            swaggerDoc.Paths[$"/{url.TrimStart('/')}"] = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>
                {
                    [HttpMethod.Post] = new OpenApiOperation
                    {
                        Tags = new HashSet<OpenApiTagReference>
                        {
                            new OpenApiTagReference(string.Join('.', apiType.Api.FullName!
                                .Split(".", StringSplitOptions.RemoveEmptyEntries)
                                .SkipLast(1)
                                .Select(s => s.ToLowerInvariant())))
                        },
                        Summary = $"Schema API ({apiType.Api.Name})",
                        RequestBody = new OpenApiRequestBody
                        {
                            Content = new Dictionary<string, OpenApiMediaType>
                            {
                                ["application/json"] = new OpenApiMediaType { Schema = wrappedReq }
                            }
                        },
                        Responses = new OpenApiResponses
                        {
                            ["200"] = new OpenApiResponse
                            {
                                Description = "Success",
                                Content = new Dictionary<string, OpenApiMediaType>
                                {
                                    ["application/json"] = new OpenApiMediaType { Schema = wrappedRes }
                                }
                            }
                        }
                    }
                }
            };
        }
    }
}
