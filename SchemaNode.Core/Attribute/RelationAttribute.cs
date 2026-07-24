using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using RelationKind = SchemaNode.Property.Record.RelationKind;

namespace SchemaNode.Attribute;

/// <summary>
/// The relation attribute
/// </summary>
public interface IRelationAttribute
{
    RelationSchema GetRelationSchema(string target);
}

/// <summary>
/// The relation declaration
/// </summary>
/// <typeparam name="TP">The property the relation used for</typeparam>
/// <typeparam name="TR">The relation process property</typeparam>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Assembly | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RelationAttribute<TP, TR> : System.Attribute, IRelationAttribute where TP: IProperty where TR: IProperty
{
    private readonly string _target;
    private readonly RelationStage  _stage;
    private readonly object[] _args;
    
    /// <summary>
    /// The call relation with target specified
    /// </summary>
    public RelationAttribute(string target, params object[] args) : this(RelationStage.LoadInput, target, args){}
    
    /// <summary>
    /// The call relation with target specified
    /// </summary>
    public RelationAttribute(RelationStage stage, string target, object[] args)
    {
        _stage = stage;
        _target = target;
        _args = args;
    }

    public RelationSchema GetRelationSchema(string target)
    {
        TR prop = Activator.CreateInstance<TR>();
        prop.SetValue(_args.Length == 1 ? _args[0] : _args);

        string kind = typeof(TR).GetMetaProperty<RelationKind>()?.GetValue<string>()
                      ?? throw new Exception($"The {typeof(TR).Name} can't be used as relation process.");

        RelationSchema schema = new RelationSchema
        {
            Target = string.IsNullOrWhiteSpace(_target) || _target.Equals(NODE_SELF, StringComparison.OrdinalIgnoreCase) ? target : _target,
            Kind = kind,
            Stage = _stage,
            Property = typeof(TP).GetSchemaType() ??
                       throw new Exception($"The {typeof(TP).Name} is not a valid property.")
        };
        schema.SetProperty(prop);
        return schema;
    }
}
