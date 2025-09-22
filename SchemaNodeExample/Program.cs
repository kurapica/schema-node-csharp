using SchemaNode;
using SchemaNode.Example;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ILoggerFactory, LoggerFactory>();
builder.Services.AddTransient(typeof(ILogger<>), typeof(Logger<>));
builder.Services.AddSingleton<ICriticalRegionProvider, LocalCriticalRegionProvider>();
builder.Services.AddControllers().AddMicroserviceApis<Program>();

// schema
builder.Services.AddSchemaContext(config => config.PreLoad = true);

var app = builder.Build();
app.UseRouting();
app.UseMicroserviceApis(true);

// schema
app.UseSchemaApis(config => config.EnableSchemaEdit = true);

app.Run();
