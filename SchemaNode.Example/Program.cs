#pragma warning disable SKEXP0001
using Microsoft.OpenApi;
using MySqlConnector;
using SchemaNode;
using SchemaNode.Ontology;
using SchemaNode.Ontology.Services;
using SchemaNode.Components;
using SchemaNode.Example.Components;
using SchemaNode.Http.JsonRpc;
using SchemaNode.McpHost;
using SchemaNode.MySql;
using SchemaNode.PostgreSQL;

var builder = WebApplication.CreateBuilder(args);

// Kafka
//builder.Services.AddSingleton<IConsumer<string, byte[]>>(sp =>
//{
//   var config = sp.GetRequiredService<IOptions<KafkaOptions>>().Value;

//    var consumerConfig = new ConsumerConfig
//    {
//        BootstrapServers = config.BootstrapServers,
//        GroupId = config.GroupId,
//        AutoOffsetReset = AutoOffsetReset.Earliest,
//        EnableAutoCommit = false
//    };

//    return new ConsumerBuilder<string, byte[]>(consumerConfig)
//        .SetErrorHandler((_, e) =>
//        {
//            var logger = sp.GetRequiredService<ILogger<KafkaEventSource>>();
//            logger.LogError("Kafka error: {Error}", e.Reason);
//        })
//        .Build();
//});

builder.Services
    // AI — must be registered before AddSchemaNode so that the SchemaNode.AI
    // assembly is included in the SchemaApi discovery scan.
    // Provider is selected via appsettings.json "SchemaNodeAI:Provider".
    .AddSchemaNodeAI(opts => builder.Configuration.GetSection(OntologyVectorOptions.SectionName).Bind(opts))

    // Mysql
    .AddMySqlDataSource(builder.Configuration.GetConnectionString("Default")!)
    // PostgreSQL with pgvector support (also registers OntologyVectorPostgreSqlService)
    .AddNpgsqlDataSourceWithVector(builder.Configuration.GetConnectionString("PostgreSql")!)

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

    // schema context items
    .AddScoped<UserInfo>()
    .AddScoped<UserInfoProvider>()

    // schema
    .AddSchemaNode<JsonRpcSchemaApiProtocol>()
    .AddSchemaStorageProvider<DynamicSchemaStorageProvider>() // save schema as application data
    //.AddAppSchemaDataProvider<AppDataMySqlProvider>() // Mysql application data provider
    .AddAppSchemaDataProvider<AppDataPostgreSqlProvider>() // PostgreSQL application data provider
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