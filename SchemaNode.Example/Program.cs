using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi;
using MySqlConnector;
using SchemaNode;
using SchemaNode.Components.Provider;
using SchemaNode.Http;
using System.Text;
using Microsoft.AspNetCore.ResponseCompression;
using SchemaNode.MySql;

var builder = WebApplication.CreateBuilder(args);

// Mysql
builder.Services.AddMySqlDataSource(builder.Configuration.GetConnectionString("Default")!);

// for test
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/json"
    });
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});

// schema
builder.Services
    .AddSchemaNode()
    //.AddSchemaStorageProvider<JsonSchemaStorageProvider>()
    .AddSchemaStorageProvider<DynamicSchemaStorageProvider>()
    .AddAppSchemaDataProvider<AppSchemaDataProvider>();

var app = builder.Build();
app.UseCors("AllowAll");
app.UseResponseCompression();

app.UseSchemaApis(enableAppDataApi:true, enableSchemaManage:true);
app.PreLoadSchemaNodes(); // for schema server

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