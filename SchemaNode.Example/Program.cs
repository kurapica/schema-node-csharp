using Microsoft.OpenApi;
using MySqlConnector;
using SchemaNode.Example.Components;
using SchemaNode.Http;
using SchemaNode.MySql;
using SchemaNode.Schema.Provider;
using SchemaNode.Service;

var builder = WebApplication.CreateBuilder(args);

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
    .AddSchemaStorageProvider<DynamicAppEntryStorageProvider>() // save schema as application data

    // Mysql
    .AddMySqlDataSource(builder.Configuration.GetConnectionString("Default")!)
    .AddAppDataProvider<AppDataMySqlProvider>() // Mysql application data provider

    // PostgreSQL
    //.AddNpgsqlDataSource(builder.Configuration.GetConnectionString("PostgreSql")!)
    //.AddAppDataProvider<AppDataPostgreSqlProvider>() // PostgreSQL application data provider

    // For test only
    //.AddAppDataProvider<InMemoryAppDataProvider>() // Memory application data provider - for test

    // Mcp
    //.AddSchemaMcp()

    .AddAppSchemaAssembly<Program>()
    .PrepareSchemaRuntime();

// App
var app = builder.Build();
app.UseCors("AllowAll");
app.UseMiddleware<UserInfoMiddleware>();

app
    .UseSchemaApis(enableAppDataApi: true, enableSchemaManage: true);
    //.MapSchemaMcp();

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