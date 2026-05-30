using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property;

/// <summary>
/// The function call property
/// </summary>
internal interface IFuncCallProperty : IProperty
{
    void TrySetFuncCall(string func, object[] args);
}

/// <summary>
/// The function call properties
/// </summary>
public abstract class FuncCallProperty<T> : Property<T>, IFuncCallProperty, ITypeRefProperty where T : FuncCall, new()
{
    public IEnumerable<string> GetRefTypes()
    {
        if (Value?.Func is not null)
            yield return Value.Func;
    }

    public void TrySetFuncCall(string func, object[] args)
    {
        if (string.IsNullOrEmpty(func)) return;
        var call = new T
        {
            Func = func,
            Args = args.Select(a => a is string str
                ? str.StartsWith('$')
                    ? str.StartsWith("$$")
                        ? new CallArg { Value = JsonValue.Create(str[1..]) }
                        : new CallArg { Source = str.Equals(NODE_SELF) || str.Equals(ARRAY_PREVIOUS) || str.Equals(ARRAY_ELEMENT) ? str : str[1..].ToCamelCase() }
                    : new CallArg{ Value = JsonValue.Create(str) }
                : new CallArg { Value = a.ToJsonNode() }).ToArray()
        };
        SetValue(call);
    }
}

/// <summary>
/// The function call 
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_COMMON}.{nameof(FuncCall)}")]
public class FuncCall
{
    /// <summary>
    /// The function name
    /// </summary>
    public string Func { get; set; } = string.Empty;

    /// <summary>
    /// The call arguments
    /// </summary>
    public CallArg[] Args { get; set; } = [];
}