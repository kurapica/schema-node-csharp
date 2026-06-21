using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Context;
using SchemaNode.Runtime;
using SchemaNode.Service;
using NamespaceType = SchemaNode.Runtime.NamespaceType;

namespace SchemaNode.RefactorTest.Base;

/// <summary>
/// Shared base class for all Core-level unit tests.
/// Sets up the schema runtime with AddAppSchemaAssembly and InitSchemaRuntimeAsync.
/// </summary>
[TestClass]
public abstract class CoreTestBase
{
    protected ServiceProvider Provider { get; private set; } = null!;
    protected SchemaContext Context { get; private set; } = null!;

    [TestInitialize]
    public async Task CoreSetup()
    {
        var services = new ServiceCollection();
        services.AddAppSchemaAssembly<CoreTestBase>();

        Provider = services.BuildServiceProvider();
        await Provider.InitSchemaRuntimeAsync();
        Context = Provider.GetRequiredService<SchemaContext>();
    }

    [TestCleanup]
    public async Task CoreTeardown()
    {
        await Provider.DisposeAsync();
    }
}
