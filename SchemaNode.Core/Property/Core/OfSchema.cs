namespace SchemaNode.Property.Core;

/// <summary>
/// Declare the schema type of the given schema kinds
/// </summary>
public class OfSchema : Property<string[]>
{
    public override void SetValue<TValue>(TValue value)
    {
        if (value is string single)
            base.SetValue(new[] { single });
        else
            base.SetValue(value);
    }
}
