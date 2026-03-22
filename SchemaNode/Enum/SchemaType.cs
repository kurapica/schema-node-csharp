namespace SchemaNode.Enum;

/// <summary>
/// Schema types.
/// </summary>
public enum SchemaType
{
    /// <summary>
    /// The namespace node
    /// </summary>
    Namespace,

    /// <summary>
    /// The scalar node
    /// </summary>
    Scalar,

    /// <summary>
    /// The num node
    /// </summary>
    Enum,

    /// <summary>
    /// The struct node
    /// </summary>
    Struct,

    /// <summary>
    /// The array node
    /// </summary>
    Array,
    
    /// <summary>
    /// The complex json node, we don't care how it organized
    /// Useful for entities defined in C#
    /// </summary>
    Json,
    
    /// <summary>
    /// The system event
    /// </summary>
    Event,
    
    /// <summary>
    /// The work flow
    /// </summary>
    Workflow,
    
    /// <summary>
    /// The permission policy
    /// </summary>
    Policy,

    /// <summary>
    /// The function node
    /// </summary>
    Func,

    /// <summary>
    /// Represents a component that performs recognition tasks, such as pattern recognition.
    /// </summary>
    Recognizer,

    /// <summary>
    /// The constraint property
    /// </summary>
    Constraint,

    /// <summary>
    /// The presentation property
    /// </summary>
    Presentation,
}