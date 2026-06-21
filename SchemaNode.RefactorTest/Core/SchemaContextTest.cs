using SchemaNode.Context;

namespace SchemaNode.RefactorTest.Core;

/// <summary>
/// Tests for SchemaContext context item operations in SchemaNode.Core
/// </summary>
[TestClass]
public class SchemaContextTest : Base.CoreTestBase
{
    /// <summary>
    /// Set and get a context item by type
    /// </summary>
    [TestMethod]
    public void ContextItem_SetAndGet()
    {
        Context.SetContextItem<string>("hello");
        string? result = Context.GetContextItem<string>();
        Assert.AreEqual("hello", result);
    }

    /// <summary>
    /// Get a missing context item returns default (null for reference types)
    /// </summary>
    [TestMethod]
    public void ContextItem_GetMissing_ReturnsNull()
    {
        string? result = Context.GetContextItem<string>();
        Assert.IsNull(result);
    }

    /// <summary>
    /// GetOrAddContextItem creates a value once and caches it
    /// </summary>
    [TestMethod]
    public void ContextItem_GetOrAdd_CreatesOnce()
    {
        int callCount = 0;
        var item = Context.GetOrAddContextItem(() =>
        {
            callCount++;
            return "created";
        });
        
        Assert.AreEqual("created", item);
        Assert.AreEqual(1, callCount);

        // Second call should return cached value
        var item2 = Context.GetOrAddContextItem(() =>
        {
            callCount++;
            return "should not be called";
        });
        
        Assert.AreEqual("created", item2);
        Assert.AreEqual(1, callCount, "Factory should only be called once");
    }

    /// <summary>
    /// Context item can be updated (overwritten)
    /// </summary>
    [TestMethod]
    public void ContextItem_CanBeOverwritten()
    {
        Context.SetContextItem<int>(42);
        Assert.AreEqual(42, Context.GetContextItem<int>());

        Context.SetContextItem<int>(99);
        Assert.AreEqual(99, Context.GetContextItem<int>());
    }

    /// <summary>
    /// Context item set with null clears the cached value
    /// </summary>
    [TestMethod]
    public void ContextItem_SetNull_ClearsValue()
    {
        Context.SetContextItem<string>("temp");
        Assert.AreEqual("temp", Context.GetContextItem<string>());

        Context.SetContextItem<string>(null);
        Assert.IsNull(Context.GetContextItem<string>());
    }
}
