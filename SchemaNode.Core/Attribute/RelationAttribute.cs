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
    /// The target
    /// </summary>
    string? Target { get; }
    
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
public sealed class RelationAttribute<T> : System.Attribute, IRelationAttribute where T: IProperty
{
    /// <summary>
    /// The call relation with target specified
    /// </summary>
    public RelationAttribute(string target, string func, params object[] args)
    {
        if (target.StartsWith('$') && !target.StartsWith("$$"))
        {
            Target = target.Equals(NODE_SELF) || target.Equals(ARRAY_PREVIOUS) || target.Equals(ARRAY_ELEMENT) ? target : target[1..].ToCamelCase();
            Func = func;
            Args = args;
        }
        else
        {
            Func = target;
            Args = args.Prepend(func).ToArray();
        }
    }
    
    /// <summary>
    /// The call relation with target specified
    /// </summary>
    public RelationAttribute(string func, params object[] args)
    {
        Func = func;
        Args = args;
    }

    /// <inheritdoc/>
    public string Kind { get; } = "call";

    /// <inheritdoc/>
    public string? Target { get; }
    
    /// <inheritdoc/>
    public Type Property { get; } = typeof(T);

    /// <summary>
    /// The function
    /// </summary>
    string Func { get; }
    
    /// <summary>
    /// The call arguments
    /// </summary>
    object[] Args { get; }
    
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
                        : new CallArg { Source = str.Equals(NODE_SELF) || str.Equals(ARRAY_PREVIOUS) || str.Equals(ARRAY_ELEMENT) ? str : str[1..].ToCamelCase() }
                    : new CallArg{ Value = JsonValue.Create(str) }
                : new CallArg { Value = a.ToJsonNode() }).ToArray()
        };
    }
}
