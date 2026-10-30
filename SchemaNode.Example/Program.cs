using Microsoft.OpenApi;
using MySqlConnector;
using SchemaNode.AI;
using SchemaNode.AI.Chat;
using SchemaNode.Data;
using SchemaNode.Example.Components;
using SchemaNode.Http;
using SchemaNode.MySql;
using SchemaNode.Schema.Provider;
using SchemaNode.Service;

var builder = WebApplication.CreateBuilder(args);

// For local AI testing the development profile uses the in-memory data provider, so the sample
// runs without MySQL. Set "UseInMemoryStorage": false (and start MySQL) to use the SQL provider.
bool useInMemoryStorage = builder.Configuration.GetValue("UseInMemoryStorage", false);

builder.Services
    // Cors
    .AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
    })

    // swagger
    .AddEndpointsApiExplorer()
    .AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "SchemaNode Example API",
            Version = "v1"
        });
    })

    // Ontology — registers ontology format providers (turtle / markdown / jsonld / ssp) for LoadAppSchema API
    //.AddSchemaOntology()
    // Vector — registers embedding + vector store APIs; provider is selected via appsettings.json "SchemaOntology:Provider".
    // Still working on, not production-ready
    //.AddSchemaVector(opts => builder.Configuration.GetSection(OntologyVectorOptions.SectionName).Bind(opts))

    // schema context items for test
    .AddScoped<UserInfo>()
    .AddScoped<UserInfoProvider>()

    // schema
    .WithSchemaApiProtocol<DefaultSchemaApiProtocol>()
    .AddSchemaStorageProvider<DynamicAppEntryStorageProvider>(); // save schema as application data

if (useInMemoryStorage)
{
    // Memory application data provider: no database required, data is lost when the process stops.
    builder.Services.AddAppDataProvider<InMemoryAppDataProvider>();
}
else
{
    // Mysql application data provider
    builder.Services
        .AddMySqlDataSource(builder.Configuration.GetConnectionString("Default")!)
        .AddAppDataProvider<AppDataMySqlProvider>();
}

builder.Services
    // Mcp
    //.AddSchemaMcp()

    // AI schema assistant (chat endpoint + streaming endpoint)
    // The provider is configured in appsettings.json under "SchemaChat".
    .AddSchemaChat(opts => builder.Configuration.GetSection(SchemaChatOptions.SectionName).Bind(opts))

    .AddAppSchemaAssembly<Program>()
    .PrepareSchemaRuntime();

// App
var app = builder.Build();
app.UseCors("AllowAll");
app.UseMiddleware<UserInfoMiddleware>();

app
    .UseSchemaApis(enableAppDataApi: true, enableSchemaManage: true);

// AI schema assistant streaming endpoint: POST /schema/chat-schema-stream
app.MapSchemaChat();

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(); 
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SchemaNode Example API v1");
        c.RoutePrefix = string.Empty;
    });
}

await app.Services.InitSchemaRuntimeAsync();
await app.Services.ActivateSchemaRuntimeAsync();

app.Run();