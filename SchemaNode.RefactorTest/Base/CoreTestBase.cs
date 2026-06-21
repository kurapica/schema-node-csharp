using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Context;
using SchemaNode.Runtime;
using SchemaNode.Service;
using NamespaceType = SchemaNode.Runtime.NamespaceType;

namespace SchemaNode.RefactorTest.Base;

[TestClass]
public abstract class CoreTestBase
{
    protected ServiceProvider Provider { get; private set; } = null!;
    protected SchemaContext Context { get; private set; } = null!;

    /// <summary>
    /// Override to register additional services before the provider is built.
    /// </summary>
    protected virtual void ConfigureServices(IServiceCollection services) { }

    [TestInitialize]
    public async Task CoreSetup()
    {
        var services = new ServiceCollection();
        services.AddAppSchemaAssembly<CoreTestBase>();
        ConfigureServices(services);

        Provider = services.BuildServiceProvider();
        await Provider.InitSchemaRuntimeAsync();
        Context = Provider.GetRequiredService<SchemaContext>();
    }

    [TestCleanup]
    public async Task CoreTeardown()
    {
        if (Provider != null)
            await Provider.DisposeAsync();
    }
}
