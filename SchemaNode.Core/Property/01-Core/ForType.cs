using SchemaNode.Utility;

namespace SchemaNode.Property.Core;

/// <summary>
/// Declare what node types the property is defined for
/// </summary>
public class ForType : Property<string[]>
{
    /// <inheritdoc/>
    public override void SetValue<TValue>(TValue value)
    {
        switch (value)
        {
            case string str:
                base.SetValue(new[] { str });
                break;
            case IEnumerable<string> strEnumerable:
                base.SetValue(strEnumerable.ToArray());
                break;
            case Type type:
                base.SetValue(new[] { type.GetSchemaType() ?? throw new Exception($"The {type.FullName} has no schema type.") });
                break;
            case Type[] types:
                base.SetValue(types.Select(t => t.GetSchemaType() ?? throw new Exception($"The {t.FullName} has no schema type.")).ToArray());
                break;
            case object[] objArray:
                base.SetValue(objArray.Select(obj =>
                {
                    return obj switch
                    {
                        string s => s,
                        Type t => t.GetSchemaType() ?? throw new Exception($"The {t.FullName} has no schema type."),
                        _ => throw new ArgumentException(
                            $"Each element in the array for {nameof(ForType)} must be a string or a Type.")
                    };
                }).ToArray());
                break;
            default:
                throw new ArgumentException($"Value for {nameof(ForType)} must be a string or an enumerable of strings.");
        }
    }
}