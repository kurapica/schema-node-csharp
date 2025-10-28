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
    //.AddSchemaStorageProvider<JsonSchemaStorageProvider>()    // Save schema as json file
    .AddSchemaStorageProvider<DynamicSchemaStorageProvider>()   // save schema as application data
    //.AddAppSchemaDataProvider<AppSchemaDataProvider>();       // Mysql application data provider
    .AddAppSchemaDataProvider<InMemoryAppSchemaDataProvider>(); // Memory application data provider - for test

var app = builder.Build();
app.UseCors("AllowAll");
app.UseResponseCompression();

app.UseSchemaApis(enableAppDataApi:true, enableSchemaManage:true);
app.PreLoadSchemaNodes(); // for schema server

#region Swagger

CachedZipFileProvider swaggerProvider = new(typeof(Program).Assembly, "SchemaNode.Example.swagger.zip", "Swagger");
app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = swaggerProvider,
    DefaultFileNames = new List<string>
    {
        "index.html"
    }
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = swaggerProvider
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