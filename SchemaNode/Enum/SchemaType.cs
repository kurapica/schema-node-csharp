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
    /// The struct field node, sub-schema of the struct node
    /// </summary>
    StructField,

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
    /// The property for other schemas
    /// </summary>
    Property,

    /// <summary>
    /// The application
    /// </summary>
    App,

    /// <summary>
    /// The application field, The sub field of the application node
    /// </summary>
    AppField,

    /// <summary>
    /// The application workflow
    /// </summary>
    AppWorkflow,
}

/// <summary>
/// The value schema type
/// </summary>
public enum ValueSchemaType
{
    All = 0,
    Scalar = SchemaType.Scalar,
    Enum = SchemaType.Enum,
    Struct = SchemaType.Struct,
    Array = SchemaType.Array,
    Json = SchemaType.Json,

    // Scalar value type
    Number = 100,
    Int,
    Single,
    Double,
    Bool,
    Char,
    String,
    Date,
    Year,
    YearMonth,
    FullDate,
    Namespace, // Namespace is also a value type, as it can be used as a reference in some cases

    // Enum value type
    IntEnum = 200,
    FlagsEnum,
    StringEnum
}