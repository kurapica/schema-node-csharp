using System.Text.Json.Nodes;
using SchemaNode.Property;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Attribute;

/// <summary>
/// The relation attribute
/// </summary>
public interface IRelationAttribute
{
    /// <summary>
    /// The relation process kind
    /// </summary>
    string Kind { get; }
    
    /// <summary>
    /// The target property type
    /// </summary>
    Type Property { get;  }

    /// <summary>
    /// Generate the relation schema data
    /// </summary>
    IRelationProcessBuilder GetRelationProcess();
}

/// <summary>
/// The default relation using call process
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Assembly | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RelationAttribute<T>(string func, params object[] args): System.Attribute, IRelationAttribute where T: IProperty
{
    /// <inheritdoc/>
    public string Kind { get; } = "call";
    
    /// <inheritdoc/>
    public Type Property { get; } = typeof(T);

    /// <summary>
    /// The function
    /// </summary>
    string Func { get; } = func;
    
    /// <summary>
    /// The call arguments
    /// </summary>
    object[] Args { get; } = args;
    
    /// <inheritdoc/>
    public IRelationProcessBuilder GetRelationProcess()
    {
        return new RelationCallBuilder
        {
            Func = Func,
            Args = Args.Select(a => a is string str
                ? str.StartsWith('$')
                    ? str.StartsWith("$$")
                        ? new CallArg { Value = JsonValue.Create(str[1..]) }
                        : new CallArg { Source = str.Equals(NODE_SELF) || str.Equals(ARRAY_ITSELF) || str.Equals(ARRAY_ELEMENT) ? str : str[1..].ToCamelCase() }
                    : new CallArg{ Value = JsonValue.Create(str) }
                : new CallArg { Value = a.ToJsonNode() }).ToArray()
        };
    }
}
