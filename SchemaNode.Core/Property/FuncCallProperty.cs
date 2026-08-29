using System.Text.Json.Nodes;
using SchemaNode.Schema;
using SchemaNode.Utility;

namespace SchemaNode.Property;

/// <summary>
/// The function call properties
/// </summary>
public abstract class FuncCallProperty : Property<FuncCall>, ITypeRefProperty
{
    /// <inheritdoc/>
    public IEnumerable<string> GetRefTypes()
    {
        if (Value?.Func is not null)
            yield return Value.Func;
    }

    /// <inheritdoc/>
    public override void SetValue<TValue>(TValue value)
    {
        switch (value)
        {
            case string func when !string.IsNullOrEmpty(func):
                base.SetValue(new FuncCall { Func = func });
                break;
            case object[] { Length: > 0 } args when args[0] is string f && !string.IsNullOrEmpty(f):
                base.SetValue(new FuncCall
                {
                    Func = f,
                    Args = args.Skip(1).Select(a => 
                        a is string str
                            ? str.StartsWith('@')
                                ? new CallArg { Source = str[1..].ToCamelCase() }
                                : str.StartsWith('$')
                                    ? new CallArg { Source = str }
                                    : new CallArg { Value = JsonValue.Create(str) }
                            : new CallArg { Value = a.ToJsonNode() }).ToArray()
                });
                break;

            // For other cases, try to convert to FuncCall directly
            default:
                base.SetValue(value);
                break;
        }
    }
}
