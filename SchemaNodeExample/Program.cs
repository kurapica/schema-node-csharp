using System.Reflection;
using Microsoft.Extensions.FileProviders;
using SchemaNode;
using SchemaNode.DI;
using SchemaNode.Example;
using SchemaNode.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ILoggerFactory, LoggerFactory>();
builder.Services.AddTransient(typeof(ILogger<>), typeof(Logger<>));
builder.Services.AddSingleton<ICriticalRegionProvider, LocalCriticalRegionProvider>();
builder.Services.AddControllers().AddSchemaApis();

// schema
builder.Services.AddSchemaContext(config => config.PreLoad = true);

var app = builder.Build();
app.UseSchemaApis();

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
app.MapControllerRoute(nameof(Document), Document.URL, new
{
    controller = nameof(Document),
    action = nameof(Document.Execute)
});

app.Run();
