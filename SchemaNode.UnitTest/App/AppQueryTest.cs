using SchemaNode.Attribute;
using SchemaNode.Data;
using SchemaNode.Enum;
using SchemaNode.Property.Common;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.Constant;
using Application = SchemaNode.Property.App.App;
using SchemaType = SchemaNode.Property.Core.SchemaType;
using EnableStorage = SchemaNode.Property.App.EnableStorage;

namespace SchemaNode.UnitTest.App;

/// <summary>
/// Tests for app data query operations: filtering, paging, ordering.
/// Uses a simple "book" app with a single entity type.
/// </summary>
[TestClass]
public class AppQueryTest : Base.AppTestBase
{
    const string APP_NAME = "library";

    [Meta<Application>(APP_NAME)]
    [Meta<SchemaType>($"library.{nameof(Book)}")]
    [Meta<EnableStorage>(true)]
    public class Book
    {
        [Meta<PrimaryIndex>]
        public Guid Id { get; set; }

        [Meta<UplimitString>(200)]
        public string Title { get; set; } = null!;

        [Meta<UplimitString>(100)]
        public string Author { get; set; } = null!;

        public long Year { get; set; }

        public long Price { get; set; }
    }

    [TestMethod]
    public async Task GetEntities_WithPaging()
    {
        string target = Guid.NewGuid().ToString();

        // Save 10 books
        var books = new List<Book>();
        for (int i = 1; i <= 10; i++)
        {
            books.Add(new Book
            {
                Id = Guid.NewGuid(),
                Title = $"Book {i:D2}",
                Author = "Author A",
                Year = 2020 + i,
                Price = 100 * i
            });
        }

        await Context.BeginTransactionAsync();
        await Context.SaveEntitiesAsync(target, books);
        await Context.CommitTransactionAsync();

        // Get first 5 (filter on Year > 0 to get all)
        var (page1, total) = await Context.GetEntitiesAsync<Book>(target, b => b.Year > 0, take: 5, skip: 0);
        Assert.AreEqual(5, page1.Count);
        Assert.AreEqual(10, total);
    }

    [TestMethod]
    public async Task GetEntities_WithSkip()
    {
        string target = Guid.NewGuid().ToString();

        var books = new List<Book>();
        for (int i = 1; i <= 5; i++)
        {
            books.Add(new Book
            {
                Id = Guid.NewGuid(),
                Title = $"SkipBook {i}",
                Author = "Author S",
                Year = 2020 + i,
                Price = 50 * i
            });
        }

        await Context.BeginTransactionAsync();
        await Context.SaveEntitiesAsync(target, books);
        await Context.CommitTransactionAsync();

        // Skip 2, take 2
        var (result, total) = await Context.GetEntitiesAsync<Book>(target, b => b.Year > 0, take: 2, skip: 2);
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(5, total);
    }

    [TestMethod]
    public async Task GetEntities_WithOrderBy()
    {
        string target = Guid.NewGuid().ToString();

        var books = new List<Book>
        {
            new() { Id = Guid.NewGuid(), Title = "C Book", Author = "X", Year = 2020, Price = 300 },
            new() { Id = Guid.NewGuid(), Title = "A Book", Author = "X", Year = 2020, Price = 100 },
            new() { Id = Guid.NewGuid(), Title = "B Book", Author = "X", Year = 2020, Price = 200 }
        };

        await Context.BeginTransactionAsync();
        await Context.SaveEntitiesAsync(target, books);
        await Context.CommitTransactionAsync();

        // Order by Price descending (filter all by Year > 0)
        var (result, total) = await Context.GetEntitiesAsync<Book>(target, b => b.Year > 0, take: 10, skip: 0,
            desc: false,
            orderBy: [new AppSchemaDataOrder("price", true)]);
        Assert.AreEqual(3, result.Count);
        Assert.IsTrue(result[0].Price >= result[1].Price);
        Assert.IsTrue(result[1].Price >= result[2].Price);
    }

    [TestMethod]
    public async Task GetEntities_CountResult()
    {
        string target = Guid.NewGuid().ToString();

        var books = new List<Book>();
        for (int i = 1; i <= 7; i++)
        {
            books.Add(new Book
            {
                Id = Guid.NewGuid(),
                Title = $"CountBook {i}",
                Author = i % 2 == 0 ? "Even Author" : "Odd Author",
                Year = 2020,
                Price = 100
            });
        }

        await Context.BeginTransactionAsync();
        await Context.SaveEntitiesAsync(target, books);
        await Context.CommitTransactionAsync();

        // Query even authors — should get 3 (i=2,4,6)
        var evens = await Context.GetEntitiesAsync<Book>(target, b => b.Author == "Even Author");
        Assert.AreEqual(3, evens.Count);

        // Query year = 2020 — all 7
        var all2020 = await Context.GetEntitiesAsync<Book>(target, b => b.Year == 2020);
        Assert.AreEqual(7, all2020.Count);
    }

    [TestMethod]
    public async Task DeleteEntities_ByCondition()
    {
        string target = Guid.NewGuid().ToString();

        var bookA = new Book { Id = Guid.NewGuid(), Title = "Keep", Author = "A", Year = 2020, Price = 100 };
        var bookB1 = new Book { Id = Guid.NewGuid(), Title = "Del1", Author = "B", Year = 2020, Price = 100 };
        var bookB2 = new Book { Id = Guid.NewGuid(), Title = "Del2", Author = "B", Year = 2020, Price = 100 };

        await Context.BeginTransactionAsync();
        await Context.SaveEntitiesAsync(target, [bookA, bookB1, bookB2]);
        await Context.CommitTransactionAsync();

        // Delete specific entity by value
        await Context.BeginTransactionAsync();
        await Context.DeleteEntityAsync(target, bookB1);
        await Context.DeleteEntityAsync(target, bookB2);
        await Context.CommitTransactionAsync();

        var remaining = await Context.GetEntitiesAsync<Book>(target, b => b.Year == 2020);
        Assert.AreEqual(1, remaining.Count);
        Assert.AreEqual("Keep", remaining[0].Title);
    }

    [TestMethod]
    public async Task GetEntity_NotFound_ReturnsNull()
    {
        string target = Guid.NewGuid().ToString();

        var result = await Context.GetEntityAsync<Book>(target, Guid.NewGuid());
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task SaveEntities_OnlyAdd_NoDuplicates()
    {
        string target = Guid.NewGuid().ToString();
        var id = Guid.NewGuid();

        // First save
        await Context.BeginTransactionAsync();
        await Context.SaveEntitiesAsync(target, [new Book { Id = id, Title = "Original", Author = "A", Year = 2020, Price = 100 }]);
        await Context.CommitTransactionAsync();

        // Second save with same ID — should work (upsert behavior)
        await Context.BeginTransactionAsync();
        await Context.SaveEntitiesAsync(target, [new Book { Id = id, Title = "Updated", Author = "A", Year = 2020, Price = 150 }]);
        await Context.CommitTransactionAsync();

        var book = await Context.GetEntityAsync<Book>(target, id);
        Assert.IsNotNull(book);
        Assert.AreEqual("Updated", book.Title);
        Assert.AreEqual(150, book.Price);
    }
}
