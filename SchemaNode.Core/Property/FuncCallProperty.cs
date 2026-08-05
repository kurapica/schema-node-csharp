using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Property.Common;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using ValueType = SchemaNode.Schema.ValueType;

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
                base.SetValue(new  FuncCall { Func = func });
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

/// <summary>
/// The function call 
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY}.{nameof(FuncCall)}")]
public class FuncCall
{
    [Meta<SchemaType>(typeof(ValueType))]
    [Meta<DisplayOnly>(true)]
    [Meta<InVisible>(true)]
    public string Return { get; set; } = string.Empty;
    
    /// <summary>
    /// The function name
    /// </summary>
    [Meta<SchemaType>(typeof(FuncType))]
    [Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_RETURN, NODE_SELF, $"@{nameof(Return)}")]
    public string Func { get; set; } = string.Empty;

    /// <summary>
    /// The call arguments
    /// </summary>
    public CallArg[] Args { get; set; } = [];
}