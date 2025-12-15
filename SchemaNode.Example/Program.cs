using Microsoft.OpenApi.Models;
using MySqlConnector;
using SchemaNode;
using SchemaNode.Components;
using SchemaNode.Example.Components;
using SchemaNode.Http.JsonRpc;
using SchemaNode.MySql;

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
    // Mysql
    .AddMySqlDataSource(builder.Configuration.GetConnectionString("Default")!)

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
    //.AddAppSchemaDataProvider<AppDataMySqlProvider>();       // Mysql application data provider
    .AddAppSchemaDataProvider<InMemoryAppDataProvider>(); // Memory application data provider - for test

// App
var app = builder.Build();
app.UseCors("AllowAll");
app.UseMiddleware<UserInfoMiddleware>();

app
    .UseSchemaApis(enableAppDataApi:true, enableSchemaManage:true)
    .PreLoadSchemaNodes();

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