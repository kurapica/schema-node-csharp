namespace SchemaNode.Enum;

/// <summary>
/// The node relation type
/// </summary>
public enum RelationType
{
    /// <summary>
    /// The node type, only works for struct field
    /// </summary>
    Type,

    /// <summary>
    /// Invisible
    /// </summary>
    Invisible,

    /// <summary>
    /// Visible
    /// </summary>
    Visible,
    
    /// <summary>
    /// Disable
    /// </summary>
    Disable,

    /// <summary>
    /// As Default
    /// </summary>
    Default,

    /// <summary>
    /// Assign
    /// </summary>
    Assign,

    /// <summary>
    /// Only use for init
    /// </summary>
    InitOnly,

    /// <summary>
    /// low limit
    /// </summary>
    LowLimit,

    /// <summary>
    /// up limit
    /// </summary>
    UpLimit,
  
    /// <summary>
    /// root, for enum or scalar values with tree structure
    /// </summary>
    Root,

    /// <summary>
    /// Enum blacklist
    /// </summary>
    BlackList,

    /// <summary>
    /// Enum whitelist
    /// </summary>
    WhiteList,

    /// <summary>
    /// Enum can choose any level
    /// </summary>
    AnyLevel,

    /// <summary>
    /// The cascade limit
    /// </summary>
    Cascade,

    /// <summary>
    /// Single flag value for enum
    /// </summary>
    SingleFlag,
    
    /// <summary>
    /// The display name of the node
    /// </summary>
    Display,
    
    /// <summary>
    /// Union validation
    /// </summary>
    Validation,
    
    /// <summary>
    /// App field reference
    /// </summary>
    Reference,
}