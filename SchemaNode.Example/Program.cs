#pragma warning disable SKEXP0001
using Microsoft.OpenApi;
using MySqlConnector;
using SchemaNode;
using SchemaNode.AI;
using SchemaNode.Components;
using SchemaNode.Example.Components;
using SchemaNode.Http.JsonRpc;
using SchemaNode.McpHost;
using SchemaNode.MySql;
using SchemaNode.PostgreSQL;

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
    .AddSchemaOntology()
    // Vector — registers embedding + vector store APIs; provider is selected via appsettings.json "SchemaOntology:Provider".
    // Still working on, not production-ready
    //.AddSchemaVector(opts => builder.Configuration.GetSection(OntologyVectorOptions.SectionName).Bind(opts))

    // schema context items for test
    .AddScoped<UserInfo>()
    .AddScoped<UserInfoProvider>()

    // schema
    .AddSchemaNode<JsonRpcSchemaApiProtocol>()
    .AddSchemaStorageProvider<DynamicSchemaStorageProvider>() // save schema as application data

    // Mysql
    // .AddMySqlDataSource(builder.Configuration.GetConnectionString("Default")!)
    //.AddAppSchemaDataProvider<AppDataMySqlProvider>() // Mysql application data provider

    // PostgreSQL
    .AddNpgsqlDataSource(builder.Configuration.GetConnectionString("PostgreSql")!)
    .AddAppSchemaDataProvider<AppDataPostgreSqlProvider>() // PostgreSQL application data provider

    // For test only
    //.AddAppSchemaDataProvider<InMemoryAppDataProvider>() // Memory application data provider - for test

    // Mcp
    .AddSchemaMcpHost();

// App
var app = builder.Build();
app.UseCors("AllowAll");
app.UseMiddleware<UserInfoMiddleware>();

app
    .UseSchemaApis(enableAppDataApi: true, enableSchemaManage: true)
    .PreLoadSchemaNodes()
    .MapSchemaMcpHost();

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(); 
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
        c.RoutePrefix = string.Empty;
    });
}

app.Run();