using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.UnitTest;

/// <summary>
/// Tests for RecognizerType: parsing, emitting, pattern-based parsing, and convert property pipeline
/// </summary>
[TestClass]
public class RecognizerTypeTest : TestBase
{
    static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // ─────────────────────────────────────────────────────────────────────
    // RecognizerType: type-first format-driven parsing and emitting
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Save a struct recognizer with format "{key}-{value}" and verify it loads correctly
    /// </summary>
    [TestMethod]
    public async Task RecognizerType_SaveAndLoad()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        // Define source struct: { key: string, value: string }
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.kvresult",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields =
                [
                    new StructFieldSchema { Name = "key",   Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "value", Type = NS_SYSTEM_STRING }
                ]
            }
        });

        // Define recognizer: SourceType = test.kvresult, Parts = [field(key), literal(-), field(value)]
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.kvrecognizer",
            Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = "test.kvresult",
                Parts =
                [
                    new RecognizerPartSchema { Type = RecognizerPartType.Field, Field = "key" },
                    new RecognizerPartSchema { Type = RecognizerPartType.Literal, Text = "-" },
                    new RecognizerPartSchema { Type = RecognizerPartType.Field, Field = "value" },
                ]
            }
        });

        var recognizer = await ctx.GetSchemaTypeAsync<RecognizerType>("test.kvrecognizer");
        Assert.IsNotNull(recognizer);
        Assert.AreEqual(SchemaType.Recognizer, recognizer.Type);
        Assert.AreEqual(SchemaNodeStatus.Ready, recognizer.Status);
        Assert.AreEqual("test.kvresult", recognizer.SourceType);
        Assert.AreEqual(3, recognizer.Parts.Length);
    }

    /// <summary>
    /// Recognizer parses a simple "KEY-VALUE" string into structured fields
    /// </summary>
    [TestMethod]
    public async Task RecognizerType_ParseSimpleKV()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();
        await SetupKVRecognizerAsync(ctx);

        var recognizer = await ctx.GetSchemaTypeAsync<RecognizerType>("test.kvrecognizer");
        Assert.IsNotNull(recognizer);

        var result = await recognizer.RecognizeAsync(ctx, "Color-Red");
        Assert.IsTrue(result.Success, "Should successfully parse 'Color-Red'");
        Assert.IsNotNull(result.Value);

        var obj = result.Value as StructTypeNode;
        Assert.IsNotNull(obj);
        Assert.AreEqual("Color", obj.GetField("key")?.ToString());
        Assert.AreEqual("Red",   obj.GetField("value")?.ToString());
    }

    /// <summary>
    /// Recognizer fails gracefully on invalid input (missing separator)
    /// </summary>
    [TestMethod]
    public async Task RecognizerType_ParseFail()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();
        await SetupKVRecognizerAsync(ctx);

        var recognizer = await ctx.GetSchemaTypeAsync<RecognizerType>("test.kvrecognizer");
        Assert.IsNotNull(recognizer);

        // No separator → fail
        var result = await recognizer.RecognizeAsync(ctx, "ColorRed");
        Assert.IsFalse(result.Success);
    }

    /// <summary>
    /// SKU-like parsing: color-size-material format
    /// </summary>
    [TestMethod]
    public async Task RecognizerType_SKUParsing()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        // SKU struct
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.sku",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields =
                [
                    new StructFieldSchema { Name = "color",    Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "size",     Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "material", Type = NS_SYSTEM_STRING }
                ]
            }
        });

        // SKU recognizer: field(color) - literal(-) - field(size) - literal(-) - field(material)
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.skurecognizer",
            Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = "test.sku",
                Parts =
                [
                    new RecognizerPartSchema { Type = RecognizerPartType.Field, Field = "color" },
                    new RecognizerPartSchema { Type = RecognizerPartType.Literal, Text = "-" },
                    new RecognizerPartSchema { Type = RecognizerPartType.Field, Field = "size" },
                    new RecognizerPartSchema { Type = RecognizerPartType.Literal, Text = "-" },
                    new RecognizerPartSchema { Type = RecognizerPartType.Field, Field = "material" },
                ]
            }
        });

        var recognizer = await ctx.GetSchemaTypeAsync<RecognizerType>("test.skurecognizer");
        Assert.IsNotNull(recognizer);
        Assert.AreEqual(SchemaNodeStatus.Ready, recognizer.Status);

        var result = await recognizer.RecognizeAsync(ctx, "Red-XL-Cotton");
        Assert.IsTrue(result.Success);
        var obj = result.Value as StructTypeNode;
        Assert.IsNotNull(obj);
        Assert.AreEqual("Red",    obj.GetField("color")?.ToString());
        Assert.AreEqual("XL",     obj.GetField("size")?.ToString());
        Assert.AreEqual("Cotton", obj.GetField("material")?.ToString());
    }

    /// <summary>
    /// Emit: reverse-generate a string from structured data using format template
    /// </summary>
    [TestMethod]
    public async Task RecognizerType_Emit()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.sku",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields =
                [
                    new StructFieldSchema { Name = "color",    Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "size",     Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "material", Type = NS_SYSTEM_STRING }
                ]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.skurecognizer",
            Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = "test.sku",
                Parts =
                [
                    new RecognizerPartSchema { Type = RecognizerPartType.Field, Field = "color" },
                    new RecognizerPartSchema { Type = RecognizerPartType.Literal, Text = "-" },
                    new RecognizerPartSchema { Type = RecognizerPartType.Field, Field = "size" },
                    new RecognizerPartSchema { Type = RecognizerPartType.Literal, Text = "-" },
                    new RecognizerPartSchema { Type = RecognizerPartType.Field, Field = "material" },
                ]
            }
        });

        var recognizer = await ctx.GetSchemaTypeAsync<RecognizerType>("test.skurecognizer");
        Assert.IsNotNull(recognizer);

        var skuType = await ctx.GetSchemaTypeAsync<StructType>("test.sku");
        Assert.IsNotNull(skuType);
        var skuNode = new StructTypeNode(skuType);
        skuNode.SetField("color", "Blue");
        skuNode.SetField("size", "M");
        skuNode.SetField("material", "Silk");

        var emitted = await recognizer.EmitAsync(ctx, skuNode);
        Assert.AreEqual("Blue-M-Silk", emitted);
    }

    /// <summary>
    /// Recognizer with wrong source type should have error status
    /// </summary>
    [TestMethod]
    public async Task RecognizerType_WrongSourceType_Status()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.badrecognizer",
            Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = "nonexistent.type",
                Parts =
                [
                    new RecognizerPartSchema { Type = RecognizerPartType.Field, Field = "x" },
                ]
            }
        });

        var recognizer = await ctx.GetSchemaTypeAsync<RecognizerType>("test.badrecognizer");
        Assert.IsNotNull(recognizer);
        Assert.AreEqual(SchemaNodeStatus.RecognizerWrongSourceType, recognizer.Status);
    }

    /// <summary>
    /// Struct with typed fields: integer field is properly converted during parsing
    /// </summary>
    [TestMethod]
    public async Task RecognizerType_StructWithTypedFields()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.person",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields =
                [
                    new StructFieldSchema { Name = "name", Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "age",  Type = NS_SYSTEM_INT }
                ]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.personrecognizer",
            Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = "test.person",
                Parts =
                [
                    new RecognizerPartSchema { Type = RecognizerPartType.Field, Field = "name" },
                    new RecognizerPartSchema { Type = RecognizerPartType.Literal, Text = ":" },
                    new RecognizerPartSchema { Type = RecognizerPartType.Field, Field = "age" },
                ]
            }
        });

        var recognizer = await ctx.GetSchemaTypeAsync<RecognizerType>("test.personrecognizer");
        Assert.IsNotNull(recognizer);

        var result = await recognizer.RecognizeAsync(ctx, "Alice:30");
        Assert.IsTrue(result.Success);
        var obj = result.Value as StructTypeNode;
        Assert.IsNotNull(obj);
        Assert.AreEqual("Alice", obj.GetField("name")?.ToString());
        Assert.AreEqual(30L, obj.GetField("age")?.ToValue<long>());
    }

    /// <summary>
    /// Array recognizer: parse comma-separated integer list
    /// </summary>
    [TestMethod]
    public async Task RecognizerType_ArrayParsing()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.intlistrecognizer",
            Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = NS_SYSTEM_INTS,
                Parts =
                [
                    new RecognizerPartSchema { Type = RecognizerPartType.Elements, Extensions = new() { ["commaSuffix"] = JsonSerializer.SerializeToElement(true) } },
                ]
            }
        });

        var recognizer = await ctx.GetSchemaTypeAsync<RecognizerType>("test.intlistrecognizer");
        Assert.IsNotNull(recognizer);

        var result = await recognizer.RecognizeAsync(ctx, "1,2,3,4,5");
        Assert.IsTrue(result.Success);
        var arr = result.Value as ArrayTypeNode;
        Assert.IsNotNull(arr);
        Assert.AreEqual(5, arr.Count);
        Assert.AreEqual("1", arr[0]?.ToString());
        Assert.AreEqual("5", arr[4]?.ToString());
    }

    /// <summary>
    /// Array recognizer: emit array to delimited string
    /// </summary>
    [TestMethod]
    public async Task RecognizerType_ArrayEmit()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.intlistrecognizer",
            Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = NS_SYSTEM_INTS,
                Parts =
                [
                    new RecognizerPartSchema { Type = RecognizerPartType.Elements, Extensions = new() { ["commaSuffix"] = JsonSerializer.SerializeToElement(true) } },
                ]
            }
        });

        var recognizer = await ctx.GetSchemaTypeAsync<RecognizerType>("test.intlistrecognizer");
        Assert.IsNotNull(recognizer);

        var arrType = await ctx.GetSchemaTypeAsync<ArrayType>(NS_SYSTEM_INTS);
        Assert.IsNotNull(arrType);
        var arr = new ArrayTypeNode(arrType);
        arr.Add(10L);
        arr.Add(20L);
        arr.Add(30L);
        var emitted = await recognizer.EmitAsync(ctx, arr);
        Assert.AreEqual("10,20,30", emitted);
    }

    /// <summary>
    /// Enum recognizer: parse enum value from string
    /// </summary>
    [TestMethod]
    public async Task RecognizerType_EnumParsing()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.color",
            Type = SchemaType.Enum,
            Enum = new EnumSchema
            {
                Type = EnumValueType.String,
                Values =
                [
                    new EnumValueInfo { Value = "red",   Name = "Red" },
                    new EnumValueInfo { Value = "green", Name = "Green" },
                    new EnumValueInfo { Value = "blue",  Name = "Blue" }
                ]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.colorrecognizer",
            Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = "test.color",
            }
        });

        var recognizer = await ctx.GetSchemaTypeAsync<RecognizerType>("test.colorrecognizer");
        Assert.IsNotNull(recognizer);

        var result = await recognizer.RecognizeAsync(ctx, "red");
        Assert.IsTrue(result.Success);
        Assert.AreEqual("red", result.Value?.ToString());

        // Match by name (case-insensitive)
        var result2 = await recognizer.RecognizeAsync(ctx, "Blue");
        Assert.IsTrue(result2.Success);
        Assert.AreEqual("blue", result2.Value?.ToString());
    }

    /// <summary>
    /// Scalar recognizer: parse integer from string
    /// </summary>
    [TestMethod]
    public async Task RecognizerType_ScalarParsing()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.intrecognizer",
            Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = NS_SYSTEM_INT,
            }
        });

        var recognizer = await ctx.GetSchemaTypeAsync<RecognizerType>("test.intrecognizer");
        Assert.IsNotNull(recognizer);

        var result = await recognizer.RecognizeAsync(ctx, "42");
        Assert.IsTrue(result.Success);
        Assert.AreEqual(42L, result.Value?.ToValue<long>());

        // Invalid number should fail
        var fail = await recognizer.RecognizeAsync(ctx, "abc");
        Assert.IsFalse(fail.Success);
    }

    /// <summary>
    /// Struct + nested array: a struct with an array field parsed via sub-recognizer
    /// </summary>
    [TestMethod]
    public async Task RecognizerType_StructWithNestedArray()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        // Define a custom string array type
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name  = "test.taglist",
            Type  = SchemaType.Array,
            Array = new ArraySchema { Element = NS_SYSTEM_STRING }
        });

        // Define an order struct
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.order",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields =
                [
                    new StructFieldSchema { Name = "id",    Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "items", Type = "test.taglist" }
                ]
            }
        });

        // Recognizer for the tag list array (semicolon-separated)
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.taglistrecognizer",
            Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = "test.taglist",
                Parts =
                [
                    new RecognizerPartSchema { Type = RecognizerPartType.Elements, Extensions = new() { ["semicolonSuffix"] = JsonSerializer.SerializeToElement(true) } },
                ]
            }
        });

        // Recognizer for the order struct with explicit sub-recognizer reference
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.orderrecognizer",
            Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = "test.order",
                Parts =
                [
                    new RecognizerPartSchema { Type = RecognizerPartType.Field, Field = "id" },
                    new RecognizerPartSchema { Type = RecognizerPartType.Literal, Text = "|" },
                    new RecognizerPartSchema { Type = RecognizerPartType.Field, Field = "items", Extensions = new() { ["recognizer"] = System.Text.Json.JsonSerializer.SerializeToElement("test.taglistrecognizer") } },
                ]
            }
        });

        var recognizer = await ctx.GetSchemaTypeAsync<RecognizerType>("test.orderrecognizer");
        Assert.IsNotNull(recognizer);
        Assert.AreEqual(SchemaNodeStatus.Ready, recognizer.Status);

        var result = await recognizer.RecognizeAsync(ctx, "ORD001|apple;banana;cherry");
        Assert.IsTrue(result.Success);
        var obj = result.Value as StructTypeNode;
        Assert.IsNotNull(obj);
        Assert.AreEqual("ORD001", obj.GetField("id")?.ToString());

        var items = obj.GetField("items") as ArrayTypeNode;
        Assert.IsNotNull(items);
        Assert.AreEqual(3, items.Count);
        Assert.AreEqual("apple", items[0]?.ToString());
        Assert.AreEqual("cherry", items[2]?.ToString());
    }

    /// <summary>
    /// Roundtrip: parse then emit should produce the original string
    /// </summary>
    [TestMethod]
    public async Task RecognizerType_Roundtrip()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.coord",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields =
                [
                    new StructFieldSchema { Name = "x", Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "y", Type = NS_SYSTEM_STRING }
                ]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.coordrecognizer",
            Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = "test.coord",
                Parts =
                [
                    new RecognizerPartSchema { Type = RecognizerPartType.Literal, Text = "(" },
                    new RecognizerPartSchema { Type = RecognizerPartType.Field, Field = "x" },
                    new RecognizerPartSchema { Type = RecognizerPartType.Literal, Text = "," },
                    new RecognizerPartSchema { Type = RecognizerPartType.Field, Field = "y" },
                    new RecognizerPartSchema { Type = RecognizerPartType.Literal, Text = ")" },
                ]
            }
        });

        var recognizer = await ctx.GetSchemaTypeAsync<RecognizerType>("test.coordrecognizer");
        Assert.IsNotNull(recognizer);

        const string input = "(10,20)";
        var parseResult = await recognizer.RecognizeAsync(ctx, input);
        Assert.IsTrue(parseResult.Success);

        var emitResult = await recognizer.EmitAsync(ctx, parseResult.Value!);
        Assert.AreEqual(input, emitResult);
    }

    /// <summary>
    /// Multiple recognizers for the same SourceType with different tags
    /// </summary>
    [TestMethod]
    public async Task RecognizerType_MultipleFormats()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.point",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields =
                [
                    new StructFieldSchema { Name = "x", Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "y", Type = NS_SYSTEM_STRING }
                ]
            }
        });

        // Default format: x,y
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.pointrecognizer.default",
            Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = "test.point",
                Parts =
                [
                    new RecognizerPartSchema { Type = RecognizerPartType.Field, Field = "x" },
                    new RecognizerPartSchema { Type = RecognizerPartType.Literal, Text = "," },
                    new RecognizerPartSchema { Type = RecognizerPartType.Field, Field = "y" },
                ],
            }
        });

        // Display format: (x, y)
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.pointrecognizer.display",
            Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = "test.point",
                Parts =
                [
                    new RecognizerPartSchema { Type = RecognizerPartType.Literal, Text = "(" },
                    new RecognizerPartSchema { Type = RecognizerPartType.Field, Field = "x" },
                    new RecognizerPartSchema { Type = RecognizerPartType.Literal, Text = ", " },
                    new RecognizerPartSchema { Type = RecognizerPartType.Field, Field = "y" },
                    new RecognizerPartSchema { Type = RecognizerPartType.Literal, Text = ")" },
                ],
            }
        });

        var defaultR = await ctx.GetSchemaTypeAsync<RecognizerType>("test.pointrecognizer.default");
        var displayR = await ctx.GetSchemaTypeAsync<RecognizerType>("test.pointrecognizer.display");
        Assert.IsNotNull(defaultR);
        Assert.IsNotNull(displayR);

        // Both should parse their respective formats
        var r1 = await defaultR.RecognizeAsync(ctx, "10,20");
        Assert.IsTrue(r1.Success);
        Assert.AreEqual("10", (r1.Value as StructTypeNode)?.GetField("x")?.ToString());

        var r2 = await displayR.RecognizeAsync(ctx, "(30, 40)");
        Assert.IsTrue(r2.Success);
        Assert.AreEqual("30", (r2.Value as StructTypeNode)?.GetField("x")?.ToString());
        Assert.AreEqual("40", (r2.Value as StructTypeNode)?.GetField("y")?.ToString());

        // Emit with display format
        var emitted = await displayR.EmitAsync(ctx, r1.Value!);
        Assert.AreEqual("(10, 20)", emitted);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Convert property pipeline: scalar formatting & enum inline mapping
    // ─────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ConvertPipeline_Scalar_MinDigits_Emit()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.fmtint", Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = NS_SYSTEM_INT,
                Parts = [new RecognizerPartSchema { Type = RecognizerPartType.Self, Extensions = new() { ["minDigits"] = J(5) } }]
            }
        });

        var recognizer = await ctx.GetSchemaTypeAsync<RecognizerType>("test.fmtint");
        Assert.IsNotNull(recognizer);
        Assert.AreEqual(SchemaNodeStatus.Ready, recognizer.Status);

        var intType = await ctx.GetSchemaTypeAsync<ScalarType>(NS_SYSTEM_INT);
        Assert.IsNotNull(intType);

        Assert.AreEqual("00042",  await recognizer.EmitAsync(ctx, intType.CreateNode(42L)!));
        Assert.AreEqual("123456", await recognizer.EmitAsync(ctx, intType.CreateNode(123456L)!));
    }

    [TestMethod]
    public async Task ConvertPipeline_Scalar_MaxDigits_Emit()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.fmtmaxint", Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = NS_SYSTEM_INT,
                Parts = [new RecognizerPartSchema { Type = RecognizerPartType.Self, Extensions = new() { ["maxDigits"] = J(3) } }]
            }
        });

        var recognizer = await ctx.GetSchemaTypeAsync<RecognizerType>("test.fmtmaxint");
        Assert.IsNotNull(recognizer);
        var intType = await ctx.GetSchemaTypeAsync<ScalarType>(NS_SYSTEM_INT);
        Assert.IsNotNull(intType);

        Assert.AreEqual("456", await recognizer.EmitAsync(ctx, intType.CreateNode(123456L)!));
        Assert.AreEqual("42",  await recognizer.EmitAsync(ctx, intType.CreateNode(42L)!));
    }

    [TestMethod]
    public async Task ConvertPipeline_Scalar_Precision_Emit()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.fmtdec", Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = NS_SYSTEM_DOUBLE,
                Parts = [new RecognizerPartSchema { Type = RecognizerPartType.Self, Extensions = new() { ["precision"] = J(2) } }]
            }
        });

        var recognizer = await ctx.GetSchemaTypeAsync<RecognizerType>("test.fmtdec");
        Assert.IsNotNull(recognizer);
        var doubleType = await ctx.GetSchemaTypeAsync<ScalarType>(NS_SYSTEM_DOUBLE);
        Assert.IsNotNull(doubleType);

        Assert.AreEqual("3.10", await recognizer.EmitAsync(ctx, doubleType.CreateNode(3.1m)!));
        Assert.AreEqual("3.14", await recognizer.EmitAsync(ctx, doubleType.CreateNode(3.14159m)!));
    }

    [TestMethod]
    public async Task ConvertPipeline_Scalar_PadChar_Emit()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.fmtpadint", Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = NS_SYSTEM_INT,
                Parts = [new RecognizerPartSchema { Type = RecognizerPartType.Self, Extensions = new() { ["minDigits"] = J(5), ["padChar"] = J(" ") } }]
            }
        });

        var recognizer = await ctx.GetSchemaTypeAsync<RecognizerType>("test.fmtpadint");
        Assert.IsNotNull(recognizer);
        var intType = await ctx.GetSchemaTypeAsync<ScalarType>(NS_SYSTEM_INT);
        Assert.IsNotNull(intType);

        Assert.AreEqual("   42", await recognizer.EmitAsync(ctx, intType.CreateNode(42L)!));
    }

    [TestMethod]
    public async Task ConvertPipeline_Scalar_StringTransform_Emit()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        // ToUpper
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.fmtupper", Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = NS_SYSTEM_STRING,
                Parts = [new RecognizerPartSchema { Type = RecognizerPartType.Self, Extensions = new() { ["toUpper"] = J(true) } }]
            }
        });

        var upper = await ctx.GetSchemaTypeAsync<RecognizerType>("test.fmtupper");
        Assert.IsNotNull(upper);
        var strType = await ctx.GetSchemaTypeAsync<ScalarType>(NS_SYSTEM_STRING);
        Assert.IsNotNull(strType);
        Assert.AreEqual("HELLO", await upper.EmitAsync(ctx, strType.CreateNode("hello")!));

        // ToLower
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.fmtlower", Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = NS_SYSTEM_STRING,
                Parts = [new RecognizerPartSchema { Type = RecognizerPartType.Self, Extensions = new() { ["toLower"] = J(true) } }]
            }
        });

        var lower = await ctx.GetSchemaTypeAsync<RecognizerType>("test.fmtlower");
        Assert.IsNotNull(lower);
        Assert.AreEqual("hello", await lower.EmitAsync(ctx, strType.CreateNode("HELLO")!));

        // Trim
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.fmttrim", Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = NS_SYSTEM_STRING,
                Parts = [new RecognizerPartSchema { Type = RecognizerPartType.Self, Extensions = new() { ["trim"] = J(true) } }]
            }
        });

        var trim = await ctx.GetSchemaTypeAsync<RecognizerType>("test.fmttrim");
        Assert.IsNotNull(trim);
        Assert.AreEqual("hello", await trim.EmitAsync(ctx, strType.CreateNode("  hello  ")!));
    }

    [TestMethod]
    public async Task ConvertPipeline_Scalar_Parse_StripPadding()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.fmtparsepad", Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = NS_SYSTEM_INT,
                Parts = [new RecognizerPartSchema { Type = RecognizerPartType.Self, Extensions = new() { ["minDigits"] = J(5), ["padChar"] = J("0") } }]
            }
        });

        var recognizer = await ctx.GetSchemaTypeAsync<RecognizerType>("test.fmtparsepad");
        Assert.IsNotNull(recognizer);

        var result = await recognizer.RecognizeAsync(ctx, "00042");
        Assert.IsTrue(result.Success);
        Assert.AreEqual(42L, result.Value?.ToValue<long>());
    }

    [TestMethod]
    public async Task ConvertPipeline_Scalar_Parse_StringTransform()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.fmtparseupper", Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = NS_SYSTEM_STRING,
                Parts = [new RecognizerPartSchema { Type = RecognizerPartType.Self, Extensions = new() { ["trim"] = J(true), ["toUpper"] = J(true) } }]
            }
        });

        var recognizer = await ctx.GetSchemaTypeAsync<RecognizerType>("test.fmtparseupper");
        Assert.IsNotNull(recognizer);

        var result = await recognizer.RecognizeAsync(ctx, "  hello  ");
        Assert.IsTrue(result.Success);
        Assert.AreEqual("HELLO", result.Value?.ToString());
    }

    [TestMethod]
    public async Task ConvertPipeline_Scalar_MinDigitsAndPrecision_Emit()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.fmtcombined", Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = NS_SYSTEM_DOUBLE,
                Parts = [new RecognizerPartSchema { Type = RecognizerPartType.Self, Extensions = new() { ["minDigits"] = J(3), ["precision"] = J(2) } }]
            }
        });

        var recognizer = await ctx.GetSchemaTypeAsync<RecognizerType>("test.fmtcombined");
        Assert.IsNotNull(recognizer);
        var doubleType = await ctx.GetSchemaTypeAsync<ScalarType>(NS_SYSTEM_DOUBLE);
        Assert.IsNotNull(doubleType);

        // 3.1 → precision first "3.10", then pad integer part → "003.10"
        Assert.AreEqual("003.10", await recognizer.EmitAsync(ctx, doubleType.CreateNode(3.1m)!));
    }

    [TestMethod]
    public async Task ConvertPipeline_Enum_InlineMapping_Emit()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.fmtcolor", Type = SchemaType.Enum,
            Enum = new EnumSchema
            {
                Type = EnumValueType.String,
                Values = [new EnumValueInfo { Value = "R", Name = "Red" }, new EnumValueInfo { Value = "G", Name = "Green" }, new EnumValueInfo { Value = "B", Name = "Blue" }]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.fmtcolorrecognizer", Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = "test.fmtcolor",
                Parts = [new RecognizerPartSchema
                {
                    Type = RecognizerPartType.Self,
                    Extensions = new() { ["mapping"] = JMapping([new Entry { Value = "R", Label = "Red" }, new Entry { Value = "G", Label = "Green" }, new Entry { Value = "B", Label = "Blue" }]) }
                }]
            }
        });

        var recognizer = await ctx.GetSchemaTypeAsync<RecognizerType>("test.fmtcolorrecognizer");
        Assert.IsNotNull(recognizer);
        Assert.AreEqual(SchemaNodeStatus.Ready, recognizer.Status);
        var enumType = await ctx.GetSchemaTypeAsync<EnumType>("test.fmtcolor");
        Assert.IsNotNull(enumType);

        Assert.AreEqual("Red",   await recognizer.EmitAsync(ctx, enumType.CreateNode("R")!));
        Assert.AreEqual("Green", await recognizer.EmitAsync(ctx, enumType.CreateNode("G")!));
    }

    [TestMethod]
    public async Task ConvertPipeline_Enum_InlineMapping_Parse()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.fmtstatus", Type = SchemaType.Enum,
            Enum = new EnumSchema
            {
                Type = EnumValueType.Int,
                Values = [new EnumValueInfo { Value = "1", Name = "Active" }, new EnumValueInfo { Value = "2", Name = "Inactive" }, new EnumValueInfo { Value = "3", Name = "Pending" }]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.fmtstatusrecognizer", Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = "test.fmtstatus",
                Parts = [new RecognizerPartSchema
                {
                    Type = RecognizerPartType.Self,
                    Extensions = new() { ["mapping"] = JMapping([new Entry { Value = "1", Label = "Active" }, new Entry { Value = "2", Label = "Inactive" }, new Entry { Value = "3", Label = "Pending" }]) }
                }]
            }
        });

        var recognizer = await ctx.GetSchemaTypeAsync<RecognizerType>("test.fmtstatusrecognizer");
        Assert.IsNotNull(recognizer);

        var result1 = await recognizer.RecognizeAsync(ctx, "Active");
        Assert.IsTrue(result1.Success);
        Assert.AreEqual("1", result1.Value?.ToString());

        var result2 = await recognizer.RecognizeAsync(ctx, "Pending");
        Assert.IsTrue(result2.Success);
        Assert.AreEqual("3", result2.Value?.ToString());

        var fail = await recognizer.RecognizeAsync(ctx, "Unknown");
        Assert.IsFalse(fail.Success);
    }

    [TestMethod]
    public async Task ConvertPipeline_Enum_InlineMapping_Localized()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.fmtpriority", Type = SchemaType.Enum,
            Enum = new EnumSchema
            {
                Type = EnumValueType.String,
                Values = [new EnumValueInfo { Value = "H", Name = "High" }, new EnumValueInfo { Value = "M", Name = "Medium" }, new EnumValueInfo { Value = "L", Name = "Low" }]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.fmtpriorityrecognizer", Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = "test.fmtpriority",
                Parts = [new RecognizerPartSchema
                {
                    Type = RecognizerPartType.Self,
                    Extensions = new() { ["mapping"] = JMapping([
                        new Entry { Value = "H", Label = new LocaleString("High", [new LocaleTran("zh", "高"), new LocaleTran("ja", "高い")]) },
                        new Entry { Value = "M", Label = new LocaleString("Medium", [new LocaleTran("zh", "中"), new LocaleTran("ja", "中くらい")]) },
                        new Entry { Value = "L", Label = new LocaleString("Low", [new LocaleTran("zh", "低"), new LocaleTran("ja", "低い")]) },
                    ]) }
                }]
            }
        });

        var recognizer = await ctx.GetSchemaTypeAsync<RecognizerType>("test.fmtpriorityrecognizer");
        Assert.IsNotNull(recognizer);

        var r1 = await recognizer.RecognizeAsync(ctx, "High");
        Assert.IsTrue(r1.Success);
        Assert.AreEqual("H", r1.Value?.ToString());

        var r2 = await recognizer.RecognizeAsync(ctx, "高");
        Assert.IsTrue(r2.Success);
        Assert.AreEqual("H", r2.Value?.ToString());

        var r3 = await recognizer.RecognizeAsync(ctx, "低い");
        Assert.IsTrue(r3.Success);
        Assert.AreEqual("L", r3.Value?.ToString());

        var priorityType = await ctx.GetSchemaTypeAsync<EnumType>("test.fmtpriority");
        Assert.IsNotNull(priorityType);
        Assert.AreEqual("Medium", await recognizer.EmitAsync(ctx, priorityType.CreateNode("M")!));
    }

    [TestMethod]
    public async Task ConvertPipeline_Enum_InlineMapping_Roundtrip()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.fmtsize", Type = SchemaType.Enum,
            Enum = new EnumSchema
            {
                Type = EnumValueType.String,
                Values = [new EnumValueInfo { Value = "S", Name = "Small" }, new EnumValueInfo { Value = "M", Name = "Medium" }, new EnumValueInfo { Value = "L", Name = "Large" }, new EnumValueInfo { Value = "XL", Name = "Extra Large" }]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.fmtsizerecognizer", Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = "test.fmtsize",
                Parts = [new RecognizerPartSchema
                {
                    Type = RecognizerPartType.Self,
                    Extensions = new() { ["mapping"] = JMapping([new Entry { Value = "S", Label = "Small" }, new Entry { Value = "M", Label = "Medium" }, new Entry { Value = "L", Label = "Large" }, new Entry { Value = "XL", Label = "Extra Large" }]) }
                }]
            }
        });

        var recognizer = await ctx.GetSchemaTypeAsync<RecognizerType>("test.fmtsizerecognizer");
        Assert.IsNotNull(recognizer);
        var sizeType = await ctx.GetSchemaTypeAsync<EnumType>("test.fmtsize");
        Assert.IsNotNull(sizeType);

        var emitted = await recognizer.EmitAsync(ctx, sizeType.CreateNode("XL")!);
        Assert.AreEqual("Extra Large", emitted);

        var parsed = await recognizer.RecognizeAsync(ctx, emitted!);
        Assert.IsTrue(parsed.Success);
        Assert.AreEqual("XL", parsed.Value?.ToString());
    }

    [TestMethod]
    public async Task ConvertPipeline_Scalar_NegativeNumber_MinDigits()
    {
        var ctx = ServiceProvider.GetRequiredService<SchemaContext>();

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.fmtnegint", Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = NS_SYSTEM_INT,
                Parts = [new RecognizerPartSchema { Type = RecognizerPartType.Self, Extensions = new() { ["minDigits"] = J(4) } }]
            }
        });

        var recognizer = await ctx.GetSchemaTypeAsync<RecognizerType>("test.fmtnegint");
        Assert.IsNotNull(recognizer);
        var intType = await ctx.GetSchemaTypeAsync<ScalarType>(NS_SYSTEM_INT);
        Assert.IsNotNull(intType);

        Assert.AreEqual("-0007", await recognizer.EmitAsync(ctx, intType.CreateNode(-7L)!));
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────

    private static async Task SetupKVRecognizerAsync(SchemaContext ctx)
    {
        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name   = "test.kvresult",
            Type   = SchemaType.Struct,
            Struct = new StructSchema
            {
                Fields =
                [
                    new StructFieldSchema { Name = "key",   Type = NS_SYSTEM_STRING },
                    new StructFieldSchema { Name = "value", Type = NS_SYSTEM_STRING }
                ]
            }
        });

        await ctx.SaveSchemaAsync(new NodeSchema
        {
            Name = "test.kvrecognizer",
            Type = SchemaType.Recognizer,
            Recognizer = new RecognizerSchema
            {
                SourceType = "test.kvresult",
                Parts =
                [
                    new RecognizerPartSchema { Type = RecognizerPartType.Field, Field = "key" },
                    new RecognizerPartSchema { Type = RecognizerPartType.Literal, Text = "-" },
                    new RecognizerPartSchema { Type = RecognizerPartType.Field, Field = "value" },
                ]
            }
        });
    }

    /// <summary>Shorthand: serialize a value to JsonElement</summary>
    static JsonElement J<T>(T value) => JsonSerializer.SerializeToElement(value);

    /// <summary>Shorthand: serialize Entry[] mapping to JsonElement with camelCase</summary>
    static JsonElement JMapping(Entry[] entries) => JsonSerializer.SerializeToElement(entries, CamelCase);
}
