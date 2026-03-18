using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using System.ComponentModel.DataAnnotations;
using SchemaNode.Enum;
using static SchemaNode.Utility.Constant;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SchemaNode.Schema;

/// <summary>
/// The schema of the recognizer type
/// </summary>
[SchemaApp]
[Schema($"{NS_SYSTEM_SCHEMA_DEF_RECOGNIZER}.schema")]
public sealed class RecognizerSchema
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
    [Schema(NS_SYSTEM_SCHEMA_TYPE_RULE_VALUE)]
    public string Result { get; set; } = string.Empty;
        
    /// <summary>
    /// The parts of recognizer
    /// </summary>
    public RecognizerPart[] Parts { get; set; } = [];

    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}

/// <summary>
/// The step of recognizer
/// </summary>
[Schema($"{NS_SYSTEM_SCHEMA_DEF_RECOGNIZER}.part")]
public class RecognizerPart
{
    /// <summary>
    /// The step name, only required when binding to state or referenced by other steps
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Name { get; set; }
    
    /// <summary>
    /// The recognize type
    /// </summary>
    public RecognizeType Type { get; set;  }

    /// <summary>
    /// The literal text for matching
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// The character set definition for matching in Charset mode
    /// </summary>
    public RecognizerCharSet[]? Sets { get; set; }

    /// <summary>
    /// The single character for matching in Any mode
    /// </summary>
    public string? Set { get; set; }

    /// <summary>
    /// The function name for validation or conversion
    /// </summary>
    [Schema(NS_SYSTEM_SCHEMA_TYPE_FUNC)]
    public string? Func { get; set; }

    /// <summary>
    /// The function call arguments
    /// </summary>
    public FuncCallArg[]? Args { get; set; }

    /// <summary>
    /// The explicit return type when needed
    /// </summary>
    [Schema(NS_SYSTEM_SCHEMA_TYPE_RULE_VALUE)]
    public string? Return { get; set; }

    /// <summary>
    /// The constant result for classification branches
    /// </summary>
    public JsonNode? Value { get; set; }

    /// <summary>
    /// The min repeat times for this step (0 means optional)
    /// </summary>
    public int? Min { get; set; }

    /// <summary>
    /// The max repeat times for this step
    /// </summary>
    public int? Max { get; set; }

    /// <summary>
    /// Use greedy contains when repeating
    /// </summary>
    public bool? Greedy { get; set; }
    
    /// <summary>
    /// The step can be skipped when matching failed, only for optional step (Min=0)
    /// </summary>
    public bool? Skippable { get; set; }

    /// <summary>
    /// The reverse generation template for this step
    /// </summary>
    public string? Emit { get; set; }

    /// <summary>
    /// The nested parts for grouping or repeating
    /// </summary>
    public RecognizerPart[]? Parts { get; set; }
    
    /// <summary>
    /// The next steps after this step (used as branches)
    /// </summary>
    public  RecognizerPart[]? Nexts { get; set;  }
}

[Schema($"{NS_SYSTEM_SCHEMA_DEF_RECOGNIZER}.charset")]
public class RecognizerCharSet
{
    /// <summary>
    /// The start char
    /// </summary>
    [Schema(NS_SYSTEM_CHAR)]
    public string Start { get; set; } = null!;

    /// <summary>
    /// The end char
    /// </summary>
    [Schema(NS_SYSTEM_CHAR)]
    public string End { get; set; } = null!;
}