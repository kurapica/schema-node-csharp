using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using System.Net;
using System.Text;

namespace SchemaNode.Example;

/// <summary>
/// Provides a swagger document for all APIs in the microservice.
/// </summary>
public class Document : Controller
{
    /// <summary>
    /// Generate the document
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public IActionResult Execute()
    {
        // Create swagger model.
        OpenApiDocument document = Http.SchemaApiDocument.Generate();

        // Serialize the document.
        StringBuilder resultBuilder = new();
        TextWriter documentTextWriter = new StringWriter(resultBuilder);
        IOpenApiWriter documentWriter = new OpenApiJsonWriter(documentTextWriter);
        document.SerializeAsV31(documentWriter);

        // Finish.
        return new ContentResult
        {
            Content = resultBuilder.ToString().Replace("$dynamicRef", "$ref"),
            ContentType = "application/json",
            StatusCode = (int)HttpStatusCode.OK
        };
    }

    /// <summary>
    /// The document url
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public const string URL = "document.json";
}