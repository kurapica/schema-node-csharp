using System.Text;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi;
using SchemaNode;
using SchemaNode.Components;
using SchemaNode.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ILoggerFactory, LoggerFactory>();
builder.Services.AddTransient(typeof(ILogger<>), typeof(Logger<>));
builder.Services.AddSingleton<ICriticalRegionProvider, LocalCriticalRegionProvider>();

// schema
builder.Services.AddSchemaNode(config => config.PreLoad = true);

var app = builder.Build();
app.UseSchemaApis();
app.PreLoadSchemaNodes();

// swagger
EmbeddedFileProvider fileProvider = new(typeof(Program).Assembly, "SchemaNode.Example.Swagger");
app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = fileProvider,
    DefaultFileNames = new List<string>
    {
        "index.html"
    }
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = fileProvider
});
app.MapGet("document.json", () =>
{
    OpenApiDocument document = SchemaApiDocument.Generate();

    // Serialize the document.
    StringBuilder resultBuilder = new();
    TextWriter documentTextWriter = new StringWriter(resultBuilder);
    IOpenApiWriter documentWriter = new OpenApiJsonWriter(documentTextWriter);
    document.SerializeAsV31(documentWriter);

    // Finish.
    return Results.Content(resultBuilder.ToString().Replace("$dynamicRef", "$ref"));
});

app.Run();