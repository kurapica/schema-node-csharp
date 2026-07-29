using SchemaNode.Utility;

namespace SchemaNode.UnitTest.Core;

/// <summary>
/// Verifies the built-in locale files embedded in the Core/App assemblies are
/// loaded by <c>Locale.TryLoad</c> during runtime preparation.
/// </summary>
[TestClass]
public class LocaleTest : Base.CoreTestBase
{
    [TestMethod]
    public void EmbeddedLocales_AreLoaded()
    {
        List<string> locales = Context.GetAvailableLocales().ToList();

        CollectionAssert.Contains(locales, "enUS");
        CollectionAssert.Contains(locales, "zhCN");
    }
}
