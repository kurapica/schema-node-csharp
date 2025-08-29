using SchemaNode.Config;
using SchemaNode.Enum;

namespace SchemaNode.Schema;

/// <summary>
/// The struct schema.
/// </summary>
public class StructSchema
{
    /// <summary>
    /// The base struct type to be inherited from.
    /// </summary>
    public string? Base { get; set; }
    
    /// <summary>
    /// The struct fields
    /// </summary>
    public IStructFieldConfig[] Fields { get; set; } = [];
    
    /// <summary>
    /// The relations between the fields
    /// </summary>
    public StructFieldRelation[]? Relations { get; set; }
}

/// <summary>
/// The struct field config
/// </summary>
public interface IStructFieldConfig: ISchemaConfig
{
    /// <summary>
    /// The field name
    /// </summary>
    public string Name { get; set; }
}

/// <summary>
/// The struct scalar field config
/// </summary>
public interface IStructScalarFieldConfig : IStructFieldConfig, IScalarConfig
{
}

/// <summary>
/// The struct enum field config
/// </summary>
public interface IStructEnumFieldConfig : IStructFieldConfig, IEnumConfig
{
}

/// <summary>
/// The relation between fields
/// </summary>
public class StructFieldRelation
{
    /// <summary>
    /// The target field, can use . for deep fields
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// The relation function
    /// </summary>
    public string Func { get; set; } = string.Empty;

    /// <summary>
    /// The func arguments
    /// </summary>
    public  FunctionCallArgument[] Args { get; set; } = [];

    /// <summary>
    /// The relationType type
    /// </summary>
    public RelationType Type { get; set; } = RelationType.Default;
}