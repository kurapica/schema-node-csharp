using System.Text;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi;
using SchemaNode;
using SchemaNode.Components.Provider;
using SchemaNode.Http;

var builder = WebApplication.CreateBuilder(args);

// for test
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// schema
builder.Services.AddSchemaNode(config => config.PreLoad = true).AddSchemaStorageProvider<JsonSchemaStorageProvider>();

var app = builder.Build();
app.UseCors("AllowAll");
app.UseSchemaApis();
app.PreLoadSchemaNodes();

#region Swagger

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

#endregion swagger

app.Run();