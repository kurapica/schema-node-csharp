using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SchemaNode.Components;

namespace SchemaNode.UnitTest
{
    [TestClass]
    public class TestBase
    {
        protected ServiceProvider ServiceProvider { get; private set; } = null!;

        [TestInitialize]
        public void Setup()
        {
            InMemoryAppDataProvider.Reset();

            var services = new ServiceCollection();

            services
                .AddSchemaNode()
                .AddAppSchemaDataProvider<InMemoryAppDataProvider>()
                .AddSchemaStorageProvider<DynamicSchemaStorageProvider>();

            ServiceProvider = services.BuildServiceProvider();
            ServiceProvider.PreLoadSchemaNodes().GetAwaiter().GetResult();
        }

        [TestCleanup]
        public void Teardown()
        {
            ServiceProvider.Dispose();
        }
    }
}

