using System.Runtime.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Property.Core;

namespace SchemaNode.WPF.Test.Type;

/// <summary>Enum used by the enum and enum-array cases.</summary>
[Meta<SchemaType>("test.mode")]
internal enum TestMode
{
    [EnumMember(Value = "alpha")] Alpha,
    [EnumMember(Value = "beta")] Beta,
    [EnumMember(Value = "gamma")] Gamma,
}

/// <summary>Single string field.</summary>
[Meta<SchemaType>("test.text")]
internal class StringCase
{
    public string Text { get; set; } = "hello world";
}

/// <summary>Single int field.</summary>
[Meta<SchemaType>("test.count")]
internal class IntCase
{
    public int Count { get; set; } = 7;
}

/// <summary>Single decimal field.</summary>
[Meta<SchemaType>("test.amount")]
internal class DecimalCase
{
    public decimal Amount { get; set; } = 12.34m;
}

/// <summary>Single date field. Core's date node holds a <see cref="DateTimeOffset"/>.</summary>
[Meta<SchemaType>("test.date")]
internal class DateCase
{
    public DateTimeOffset When { get; set; } = new(2026, 11, 3, 9, 30, 0, TimeSpan.FromHours(8));
}

/// <summary>Single bool field.</summary>
[Meta<SchemaType>("test.flag")]
internal class BoolCase
{
    public bool Flag { get; set; } = true;
}

/// <summary>Single-level enum field.</summary>
[Meta<SchemaType>("test.enum")]
internal class EnumCase
{
    public TestMode Mode { get; set; } = TestMode.Beta;
}

/// <summary>Required string that starts with no value, so the required constraint is violated.</summary>
[Meta<SchemaType>("test.required")]
internal class RequiredCase
{
    public string? Text { get; set; }
}

/// <summary>Plain string array field.</summary>
[Meta<SchemaType>("test.array")]
internal class ArrayCase
{
    public string[] Items { get; set; } = ["alpha", "beta", "gamma"];
}

/// <summary>Struct containing a nested struct and a nested struct array.</summary>
[Meta<SchemaType>("test.nested")]
internal class NestedCase
{
    public MinMax Inner { get; set; } = new() { Min = 5, Max = 6 };

    public MinMax[] Lines { get; set; } = [new() { Min = 7, Max = 8 }];
}
