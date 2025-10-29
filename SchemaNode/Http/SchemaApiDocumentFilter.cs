using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
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
        ISchemaApiProcessor? processor = _services.GetService<ISchemaApiProcessor>();

        foreach (var ((type, request, response), url) in Injection.GetSchemaApis())
        {
            var reqSchema = context.SchemaGenerator.GenerateSchema(request, context.SchemaRepository);
            var resSchema = context.SchemaGenerator.GenerateSchema(response, context.SchemaRepository);

            // 使用 processorDescriptor 生成最终封装
            var wrappedReq = processor?.WrapRequestSchema(context, reqSchema) ?? reqSchema;
            var wrappedRes = processor?.WrapResponseSchema(context, resSchema) ?? resSchema;

            swaggerDoc.Paths[$"/{url.TrimStart('/')}"] = new OpenApiPathItem
            {
                Operations =
                {
                    [OperationType.Post] = new OpenApiOperation
                    {
                        Tags = [ new OpenApiTag { Name = string.Join('.', type.FullName!.Split(".", StringSplitOptions.RemoveEmptyEntries).SkipLast(1).Select(s => s.ToLower())) } ],
                        Summary = $"Schema API ({type.Name})",
                        RequestBody = new OpenApiRequestBody
                        {
                            Content = { ["application/json"] = new OpenApiMediaType { Schema = wrappedReq } }
                        },
                        Responses =
                        {
                            ["200"] = new OpenApiResponse
                            {
                                Description = "Success",
                                Content = { ["application/json"] = new OpenApiMediaType { Schema = wrappedRes } }
                            }
                        }
                    }
                }
            };
        }
    }
}