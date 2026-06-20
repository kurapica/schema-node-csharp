using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Service;

namespace SchemaNode.RefactorTest;

[TestClass]
public sealed class RuntimeInitializationTest
{
    [TestMethod]
    public async Task InitSchemaRuntime_WithCoreAndAppAssemblies_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddAppSchemaAssemblies(typeof(AppService).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();
        await provider.InitSchemaRuntimeAsync();
    }
}