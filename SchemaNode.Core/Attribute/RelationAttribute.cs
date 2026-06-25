using System.Text.Json.Nodes;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Runtime;
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
    /// The stage of the relation been applied
    /// </summary>
    public RelationStage Stage { get; }

    /// <summary>
    /// Generate the relation schema data
    /// </summary>
    IRelationProcess GetRelationProcess();

    /// <summary>
    /// Gets the relation schema
    /// </summary>
    public RelationSchema GetRelationSchema(SchemaRuntime runtime, string defaultTarget, Func<Type, string, Type[]?, string?> typeResolver)
    {
        RelationSchema relationSchema = new()
        {
            Target = Target ?? defaultTarget,
            Property = typeResolver(Property, NS_SYSTEM_SCHEMA_PROPERTY, null) ?? throw new InvalidOperationException($"Cannot resolve property type {Property.FullName}"),
            Kind = Kind,
            Stage = Stage
        };

        IRelationProcess process = GetRelationProcess();
        Type propType = runtime.GetSchemaKindProperty(SCHEMA_KIND_RELATION, process.GetType())
                        ?? throw new Exception($"Failed to find relation property for process type '{process.GetType().FullName}'.");
        relationSchema.SetProperty(propType, process);
        return relationSchema;
    }
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

    /// <summary>
    /// The call relation with target specified
    /// </summary>
    public RelationAttribute(RelationStage stage, string func, params object[] args)
    {
        Stage = stage;
        Func = func;
        Args = args;
    }

    /// <inheritdoc/>
    public string Kind { get; } = "call";

    /// <inheritdoc/>
    public string? Target { get; }
    
    /// <inheritdoc/>
    public Type Property { get; } = typeof(T);

    /// <inheritdoc/>
    public RelationStage Stage { get; } = RelationStage.Load | RelationStage.Input;

    /// <summary>
    /// The function
    /// </summary>
    string Func { get; }
    
    /// <summary>
    /// The call arguments
    /// </summary>
    object[] Args { get; }
    
    /// <inheritdoc/>
    public IRelationProcess GetRelationProcess()
    {
        return new Relation.Call
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

/// <summary>
/// The assignment relation
/// </summary>
public sealed class RelationAssign<T> : System.Attribute, IRelationAttribute where T: IProperty
{
    object? Value { get; }

    public RelationAssign(string target, object value)
    {
        Target = target;
        IProperty? prop = Activator.CreateInstance(Property) as IProperty;
        if (prop == null) return;
        prop.SetValue(value);
        Value = prop.GetValue<object>();
    }

    public RelationAssign(string target, params object[] values)
    {
        Target = target;
        IProperty? prop = Activator.CreateInstance(Property) as IProperty;
        if (prop == null) return;
        prop.SetValue(values);
        Value = prop.GetValue<object>();
    }

    public RelationAssign(RelationStage stage, string target, object value)
    {
        Stage = stage;
        Target = target;
        IProperty? prop = Activator.CreateInstance(Property) as IProperty;
        if (prop == null) return;
        prop.SetValue(value);
        Value = prop.GetValue<object>();
    }

    public RelationAssign(RelationStage stage, string target, params object[] values)
    {
        Stage = stage;
        Target = target;
        IProperty? prop = Activator.CreateInstance(Property) as IProperty;
        if (prop == null) return;
        prop.SetValue(values);
        Value = prop.GetValue<object>();
    }

    /// <inheritdoc/>
    public string Kind { get; } = "assign";

    /// <inheritdoc/>
    public string Target { get; }
    
    /// <inheritdoc/>
    public Type Property { get; } = typeof(T);

    /// <inheritdoc/>
    public RelationStage Stage { get; } = RelationStage.Load | RelationStage.Input;

    /// <inheritdoc/>
    public IRelationProcess GetRelationProcess()
    {
        return new Relation.Assign { Value = Value };
    }
}