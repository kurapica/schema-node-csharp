using MySqlConnector;
using SchemaNode;
using SchemaNode.Components.Provider;
using Microsoft.OpenApi.Models;
using SchemaNode.Http.JsonRpc;
using SchemaNode.MySql;

var builder = WebApplication.CreateBuilder(args);
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

    // schema
    .AddSchemaNode()
    //.AddSchemaStorageProvider<JsonSchemaStorageProvider>()    // Save schema as json file
    .AddSchemaStorageProvider<DynamicSchemaStorageProvider>() // save schema as application data
    //.AddAppSchemaDataProvider<AppSchemaDataProvider>();       // Mysql application data provider
    .AddAppSchemaDataProvider<InMemoryAppSchemaDataProvider>() // Memory application data provider - for test

    // schema api
    .AddSchemaApis<JsonRpcSchemaApiProtocol>();

// App
var app = builder.Build();
app.UseCors("AllowAll");

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