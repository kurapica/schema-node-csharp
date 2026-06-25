using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Context;
using SchemaNode.Runtime;
using SchemaNode.Service;
using NamespaceType = SchemaNode.Runtime.NamespaceType;

namespace SchemaNode.RefactorTest;

[TestClass]
public sealed class RuntimeInitializationTest
{
    [TestMethod]
    public async Task InitSchemaRuntime_WithCoreAndAppAssemblies_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddAppSchemaAssembly<RuntimeInitializationTest>();
        services.PrepareSchemaRuntime();

        await using ServiceProvider provider = services.BuildServiceProvider();
        await provider.InitSchemaRuntimeAsync();
        
        SchemaContext context = provider.GetRequiredService<SchemaContext>();
        NamespaceType? root = (context.Runtime as SchemaRuntime)?.RootNamespace;
        Console.WriteLine($"Root namespace: {root}");
    }
}