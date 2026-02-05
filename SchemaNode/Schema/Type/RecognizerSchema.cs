using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using System.ComponentModel.DataAnnotations;
using SchemaNode.Enum;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema;

/// <summary>
/// The schema of the recognizer type
/// </summary>
[SchemaApp]
public class RecognizerSchema
{
    /// <summary>
    /// The recognizer name
    /// </summary>
    [Index]
    [JsonIgnore]
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Name { get; set; }

    /// <summary>
    /// The state schema type after recognition
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string State { get; set; } = string.Empty;
    
    /// <summary>
    /// The steps of recognizer
    /// </summary>
    public RecognizerStep[] Steps { get; set; } = [];
}

/// <summary>
/// The step of recognizer
/// </summary>
public class RecognizerStep
{
    /// <summary>
    /// The recognize type
    /// </summary>
    public RecognizeType Type { get; set;  }
    
    /// <summary>
    /// The next steps after this step
    /// </summary>
    public  RecognizerStep[]? Nexts { get; set;  }
}
