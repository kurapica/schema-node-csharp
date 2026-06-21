using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Property.Convert;
using SchemaNode.Runtime;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema;

/// <summary>
/// The schema of the recognizer type.
/// A recognizer declares a string representation (format) for a known SourceType,
/// and supports both parsing (string → type) and emitting (type → string).
/// The format is defined as structured Parts (not a DSL string), suitable for frontend configuration.
/// </summary>
[SchemaApp]
[Schema($"{NS_SYSTEM_SCHEMA_DEF_RECOGNIZER}.schema")]
public sealed class RecognizerSchema: ISchemaExtensions
{
    /// <summary>
    /// The recognizer typeName
    /// </summary>
    [Index]
    [JsonIgnore]
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Name { get; set; }

    /// <summary>
    /// The source type this recognizer describes (required).
    /// Must be a known value type: Scalar, Enum, Struct, or Array.
    /// </summary>
    [Schema(NS_SYSTEM_SCHEMA_TYPE_RULE_VALUE)]
    [Required]
    public string SourceType { get; set; } = string.Empty;

    /// <summary>
    /// The structured format parts that define the string representation.
    /// Each part is a Literal, Field, or ArrayRepeat with type-specific configuration
    /// including character validation rules and validation functions.
    /// </summary>
    public RecognizerPartSchema[] Parts { get; set; } = [];

    /// <summary>
    /// The relations between parts
    /// </summary>
    public RecognizerRelationSchema[]? Relations { get; set; }

    /// <summary>
    /// The extensions
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; set; }
}

/// <summary>
/// A single part of the recognizer format template, designed for frontend configuration.
/// Each part carries its own type-specific properties and optional validation rules.
/// Convert properties (IConvertProperty) are stored in the Extensions dictionary,
/// supporting extensible bidirectional conversion (parse and emit), similar to how
/// StructFieldSchema uses properties for constraint and presentation features.
/// Execution is strictly phased: Prefix → Suffix are structural; Content converters are the rest.
/// Emit order: Content converters → Prefix → Suffix.
/// Parse order (reverse): Prefix → Suffix → Content converters (reversed).
/// </summary>
[SchemaApp]
[Schema($"{NS_SYSTEM_SCHEMA_DEF_RECOGNIZER}.part")]
public sealed class RecognizerPartSchema : ISchemaExtensions
{
    /// <summary>
    /// The part type
    /// </summary>
    public RecognizerPartType Type { get; set; }

    /// <summary>
    /// Literal: the text to match/emit
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Field: the struct field typeName this part binds to
    /// </summary>
    public string? Field { get; set; }

    /// <summary>
    /// Field names this part depends on and must be parsed before this part (used for ordering in prefix-mode parsing).
    /// </summary>
    public string[]? Depends { get; set; }

    /// <summary>
    /// The extensions
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; set; }

    #region Runtime

    /// <summary>
    /// The properties loaded from Extensions
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    internal IProperty[]? Properties { get; set; }

    /// <summary>
    /// The convert properties from Extensions
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    internal IConvertProperty[]? ConvertProperties { get; set; }

    /// <summary>
    /// The prefix property
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    internal IPrefixProperty? PrefixProperty { get; private set;  }  

    /// <summary>
    /// The prefix property loaded from Extensions, used for recognizer parts that require a prefix for validation (e.g. Field, Elements)
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    internal string? Prefix { get; private set; }

    /// <summary>
    /// The suffix property
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    internal ISuffixProperty? SuffixProperty { get; private set; }

    /// <summary>
    /// The suffix property loaded from Extensions, used for recognizer parts that require a suffix for validation (e.g. Field, Elements)
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    internal string? Suffix { get; private set; }

    /// <summary>
    /// The recognizer property
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    internal RecognizerProperty? RecognizerProperty { get; set; }

    /// <summary>
    /// The reference properties from Extensions, used for recognizer parts that reference other types (e.g. Field with a struct type)
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    internal AnySchemaType[]? RefTypes { get; set; }

    /// <summary>
    /// The schema node status
    /// </summary>
    public SchemaNodeStatus? Status { get; internal set; } = SchemaNodeStatus.Ready;

    #endregion

    #region Method

    internal async Task LoadRecognizerPart(SchemaContext context, RecognizerType type, AnySchemaType partType, string? name = null)
    {
        UnloadRecognizerPart(type);

        // Collect property names referenced by relations for this part
        var relationProps = !string.IsNullOrWhiteSpace(name)
            ? type.Relations?.Where(r => r.Part.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(r => r.Prop)
            : null;

        if (Extensions is { Count: > 0 } || relationProps?.Any() == true)
        {
            Properties = PropertyType.GetProperties<IProperty>(context, SchemaType.RecognizerPart, Extensions ?? new(), partType)?.ToArray();
            if (Properties is { Length: > 0 })
            {
                // Structural: prefix and suffix are separated from content converters
                PrefixProperty = Properties.FirstOrDefault(p => p is IPrefixProperty) as IPrefixProperty;
                SuffixProperty = Properties.FirstOrDefault(p => p is ISuffixProperty) as ISuffixProperty;
                Prefix = PrefixProperty?.Prefix(name, type);
                Suffix = SuffixProperty?.Suffix(name, type);

                // RecognizerProperty is also structural in that it links to the recognizer
                RecognizerProperty = Properties.FirstOrDefault(p => p is RecognizerProperty) as RecognizerProperty;

                // Content converters exclude structural prefix/suffix properties
                ConvertProperties = Properties
                    .Where(p => p is IConvertProperty and not (IPrefixProperty or ISuffixProperty or Property.Convert.RecognizerProperty))
                    .Cast<IConvertProperty>()
                    .ToArray();

                // Wire up PadChar/PadLeft to MinDigits for cooperative behavior
                var minDigits = ConvertProperties.FirstOrDefault(p => p is MinDigitsProperty) as MinDigitsProperty;
                var padChar = ConvertProperties.FirstOrDefault(p => p is PadCharProperty) as PadCharProperty;
                var padLeft = ConvertProperties.FirstOrDefault(p => p is PadLeftProperty) as PadLeftProperty;
                bool padLeftVal = padLeft?.Value ?? true;
                if (minDigits != null)
                {
                    if (padChar?.Value is { Length: > 0 } pc) minDigits.PadChar = pc[0];
                    minDigits.PadLeft = padLeftVal;
                }
                if (padChar != null) padChar.PadLeft = padLeftVal;

                // Resolve type references from convert properties
                List<AnySchemaType>? refTypes = null;
                foreach (var typeRef in Properties.Where(p => p is ITypeRefProperty).Cast<ITypeRefProperty>())
                {
                    string? typeName = typeRef.GetValue<string>();
                    if (string.IsNullOrWhiteSpace(typeName)) continue;
                    var node = await context.GetSchemaTypeAsync(typeName);
                    if (node == null)
                    {
                        Status = SchemaNodeStatus.RecognizerWrongFuncRef;
                        type.Status = SchemaNodeStatus.RecognizerWrongFuncRef;
                    }
                    else
                    {
                        refTypes ??= [];
                        refTypes.Add(node);
                    }
                }

                RefTypes = refTypes?.ToArray();
                refTypes?.ForEach(r => r.AddRef(type));
            }
        }

        // Load RecognizerProperty from the Recognizer field (type-checked here)
        bool requireRecognizer = partType is StructType || partType is ArrayType arrType && arrType.ElementSchemaType is StructType;
        if (requireRecognizer || RecognizerProperty != null)
        {
            /*if (RecognizerProperty?.Recognizer?.SourceSchemaType == null ||
                !(RecognizerProperty.Recognizer.SourceSchemaType.CanBeUseAs(partType) ||
                  (partType is ArrayType arrayType && arrayType.ElementSchemaType != null && 
                  RecognizerProperty.Recognizer.SourceSchemaType.CanBeUseAs(arrayType.ElementSchemaType)) ||
                  (RecognizerProperty.TargetsArray && 
                   RecognizerProperty.Recognizer.SourceSchemaType is ArrayType recArrType && 
                   recArrType.ElementSchemaType != null && 
                   recArrType.ElementSchemaType.CanBeUseAs(partType))))
            {
                Status = SchemaNodeStatus.RecognizerPartWrongSubRecognizer;
                type.Status = SchemaNodeStatus.RecognizerPartWrongSubRecognizer;
                return;
            }*/
        }
    }

    internal void UnloadRecognizerPart(RecognizerType type)
    {
        if (RefTypes is { Length: > 0 })
        {
            foreach (var refType in RefTypes)
                refType.RemoveRef(type);
        }

        Properties = null;
        ConvertProperties = null;
        Prefix = null;
        Suffix = null;
        PrefixProperty = null;
        SuffixProperty = null;
        RecognizerProperty = null;
        RefTypes = null;
    }

    #endregion
}

/// <summary>
/// Defines a dynamic recognizer selection for a given Part.
/// Based on already-parsed field values (via Func/Args), the appropriate RecognizerType
/// is resolved at parse/emit time instead of being statically bound.
/// </summary>
[SchemaApp]
[Schema($"{NS_SYSTEM_SCHEMA_DEF_RECOGNIZER}.relation")]
public class RecognizerRelationSchema
{
    /// <summary>
    /// The field name of the Part whose recognizer is dynamically determined.
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public required string Part { get; set; }

    /// <summary>
    /// The property of the relation, so the function can modify it dynamically
    /// </summary>
    [Schema(NS_SYSTEM_SCHEMA_PROPERTY)]
    public required string Prop { get; set; }

    /// <summary>
    /// Function that returns a RecognizerType name given the current parsed data.
    /// </summary>
    [Schema(NS_SYSTEM_SCHEMA_TYPE_FUNC)]
    public required string Func { get; set; }

    /// <summary>
    /// Arguments passed to the function. Name references a parsed field; Value is a constant.
    /// </summary>
    public FuncCallArg[] Args { get; set; } = [];

    /// <summary>
    /// Runtime: resolved function type
    /// </summary>
    [JsonIgnore]
    [NotMapped]
    public FunctionType? FuncNode { get; set; }

    /// <summary>
    /// Runtime: status of this relation
    /// </summary>
    [NotMapped]
    public SchemaNodeStatus? Status { get; set; }
}