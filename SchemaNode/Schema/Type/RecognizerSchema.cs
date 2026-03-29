using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using System.ComponentModel.DataAnnotations;
using static SchemaNode.Utility.Constant;
using System.Text.Json;

namespace SchemaNode.Schema;

/// <summary>
/// The schema of the recognizer type.
/// A recognizer declares a string representation (format) for a known SourceType,
/// and supports both parsing (string → type) and emitting (type → string).
/// The format is defined as structured Parts (not a DSL string), suitable for frontend configuration.
/// </summary>
[SchemaApp]
[Schema($"{NS_SYSTEM_SCHEMA_DEF_RECOGNIZER}.schema")]
public sealed class RecognizerSchema: IAdditionalProperty
{
    /// <summary>
    /// The recognizer name
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
    public RecognizerPart[] Parts { get; set; } = [];

    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}

/// <summary>
/// A single part of the recognizer format template, designed for frontend configuration.
/// Each part carries its own type-specific properties and optional validation rules.
/// </summary>
[SchemaApp]
[Schema($"{NS_SYSTEM_SCHEMA_DEF_RECOGNIZER}.part")]
public sealed class RecognizerPart
{
    /// <summary>
    /// The part type
    /// </summary>
    public FormatPartType Type { get; set; }

    /// <summary>
    /// Literal: the text to match/emit
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Field: the struct field name this part binds to
    /// </summary>
    public string? Field { get; set; }

    /// <summary>
    /// ArrayRepeat: the delimiter between array elements
    /// </summary>
    public string? Delimiter { get; set; }

    /// <summary>
    /// A special recognizer for the given part
    /// </summary>
    [Schema(NS_SYSTEM_SCHEMA_TYPE_RECOGNIZER)]
    public string? Recognizer { get; set; }

    /// <summary>
    /// The format descriptor for scalar/enum formatting and parsing.
    /// For scalar types, provides C-style format options (digits, padding, trimming, date layout, etc.).
    /// For enum types, provides inline mapping via Entry[] and/or function-based conversion.
    /// </summary>
    public FormatDescriptor? Format { get; set; }
}

/// <summary>
/// Describes how a scalar or enum value is formatted (emitted) and parsed (recognized).
/// For scalar source types, provides C-format-style options (number precision, padding, casing, datetime layout).
/// For enum source types, provides a two-level mapping:
///   Level 1 (inline): an Entry[] mapping from enum values to display strings (supports localization).
///   Level 2 (function): FormatFunc / ParseFunc for arbitrary conversion logic.
/// </summary>
[SchemaApp]
[Schema($"{NS_SYSTEM_SCHEMA_DEF_RECOGNIZER}.format")]
public sealed class FormatDescriptor
{
    // ── Number ──────────────────────────────────────────────────────────

    /// <summary>
    /// Minimum number of digits (zero-padded on the left if shorter).
    /// Applies to integer or numeric scalar types.
    /// Example: MinDigits = 3, value 7 → "007"
    /// </summary>
    public int? MinDigits { get; set; }

    /// <summary>
    /// Maximum number of digits (truncated from the left if longer).
    /// Applies to integer or numeric scalar types.
    /// </summary>
    public int? MaxDigits { get; set; }

    /// <summary>
    /// Number of decimal places for floating-point scalar types.
    /// Example: Precision = 2, value 3.1 → "3.10"
    /// </summary>
    public int? Precision { get; set; }

    // ── Padding ─────────────────────────────────────────────────────────

    /// <summary>
    /// The padding character. Used together with MinDigits or a fixed-width layout.
    /// Default pad char is '0' for numbers when MinDigits is set.
    /// </summary>
    public char? PadChar { get; set; }

    /// <summary>
    /// Whether to pad on the left (true) or the right (false).
    /// Default is true (left-padding).
    /// </summary>
    public bool? PadLeft { get; set; }

    // ── String ──────────────────────────────────────────────────────────

    /// <summary>
    /// Whether to trim leading and trailing whitespace from the string value.
    /// </summary>
    public bool? Trim { get; set; }

    /// <summary>
    /// Whether to convert the string value to upper case.
    /// </summary>
    public bool? ToUpper { get; set; }

    /// <summary>
    /// Whether to convert the string value to lower case.
    /// </summary>
    public bool? ToLower { get; set; }

    // ── DateTime ────────────────────────────────────────────────────────

    /// <summary>
    /// Date/time layout string (e.g., "yyyy-MM-dd", "HH:mm:ss").
    /// Applied when the scalar base type is a date or datetime.
    /// </summary>
    public string? Layout { get; set; }

    // ── Enum mapping (Level 1: inline) ──────────────────────────────────

    /// <summary>
    /// Inline enum-to-display mapping. Each Entry.Value matches an enum value,
    /// and Entry.Label provides the display string with optional localization.
    /// When emitting, the enum value is replaced by the matching Entry's label key.
    /// When parsing, the display string is mapped back to the enum value.
    /// </summary>
    public Entry[]? Mapping { get; set; }

    // ── Enum mapping (Level 2: function-based) ──────────────────────────

    /// <summary>
    /// The fully qualified name of a function that converts a typed value to its string representation.
    /// Signature: (value) → string
    /// </summary>
    [Schema(NS_SYSTEM_SCHEMA_TYPE_FUNC)]
    public string? FormatFunc { get; set; }

    /// <summary>
    /// The fully qualified name of a function that converts a string representation back to a typed value.
    /// Signature: (string) → value
    /// </summary>
    [Schema(NS_SYSTEM_SCHEMA_TYPE_FUNC)]
    public string? ParseFunc { get; set; }
}
