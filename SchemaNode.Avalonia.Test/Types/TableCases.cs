using SchemaNode.Attribute;
using SchemaNode.Property.Core;

namespace SchemaNode.Avalonia.Test.Types;

/// <summary>Nested discount struct used to exercise a two-level struct header group.</summary>
[Meta<SchemaType>("test.discount")]
internal class Discount
{
    public decimal Rate { get; set; } = 0.1m;
    public string Reason { get; set; } = "seasonal";
}

/// <summary>
/// Order line with a primary index. The index makes Core generate the companion array type
/// <c>test.orderlines</c> (naming rule: struct name plus the <c>s</c> suffix).
/// </summary>
[Meta<SchemaType>("test.orderline")]
internal class OrderLine
{
    [Meta<PrimaryIndex>]
    public string Code { get; set; } = "A-1";

    public int Quantity { get; set; } = 2;

    public decimal UnitPrice { get; set; } = 9.5m;

    public Discount Discount { get; set; } = new();
}

/// <summary>
/// Invoice with a struct-array field. The primary index generates <c>test.invoices</c>, and the
/// nested <c>lines</c> field resolves to the generated <c>test.orderlines</c> array type.
/// </summary>
[Meta<SchemaType>("test.invoice")]
internal class Invoice
{
    [Meta<PrimaryIndex>]
    public string Number { get; set; } = "INV-1";

    public string Customer { get; set; } = "ACME";

    public OrderLine[] Lines { get; set; } = [];
}
