namespace SchemaNode.Property;

/// <summary>
/// The order property
/// </summary>
public interface IOrderProperty : IProperty
{
    int Order { get; set; }
}

/// <summary>
/// The property with order
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class OrderProperty<T> : Property<T>, IOrderProperty
{
    /// <summary>
    /// The property order
    /// </summary>
    public int Order { get; set; }

    public override void SetValue<TValue>(TValue value)
    {
        if (value is object[] values)
        {
            switch (values.Length)
            {
                case 1:
                    base.SetValue(values[0]);
                    break;
                case > 1:
                    if (values[0] is int i)
                    {
                        Order = i;
                        base.SetValue(values[1]);
                    }
                    else if (values[1] is int j)
                    {
                        Order = j;
                        base.SetValue(values[0]);
                    }
                    break;
            }
        }
        else
            base.SetValue(value);
    }
}