using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Data;
using SchemaNode.Schema.Provider;
using SchemaNode.Service;

namespace SchemaNode.RefactorTest.Base;

[TestClass]
public abstract class AppTestBase : CoreTestBase
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        InMemoryAppDataProvider.Reset();
        InMemorySchemaStorageProvider.Reset();
        services.AddAppDataProvider<InMemoryAppDataProvider>();
        services.AddSchemaStorageProvider<InMemorySchemaStorageProvider>();
    }
}
