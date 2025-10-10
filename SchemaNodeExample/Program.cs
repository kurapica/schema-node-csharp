using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi;
using MySqlConnector;
using SchemaNode;
using SchemaNode.Components.Provider;
using SchemaNode.Http;
using System.Data;
using System.Text;
using SchemaNode.MySql;

var builder = WebApplication.CreateBuilder(args);

// Mysql
var connString = builder.Configuration.GetConnectionString("Default");
builder.Services.AddTransient<IDbConnection>(sp => new MySqlConnection(connString));
builder.Services.AddTransient<MySqlConnection>(sp => new MySqlConnection(connString));

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
builder.Services
    .AddSchemaNode()
    .AddSchemaStorageProvider<JsonSchemaStorageProvider>()
    .AddAppSchemaDataProvider<AppSchemaDataProvider>();

var app = builder.Build();
app.UseCors("AllowAll");

app.UseSchemaApis(enableAppDataApi:true);
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