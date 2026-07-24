using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Data;
using SchemaNode.Schema.Provider;
using SchemaNode.Service;

namespace SchemaNode.UnitTest.Base;

[TestClass]
public abstract class AppTestBase : CoreTestBase
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        InMemoryAppDataProvider.Reset();
        services.AddAppDataProvider<InMemoryAppDataProvider>();
        services.AddSchemaStorageProvider<DynamicAppEntryStorageProvider>();
    }
}
