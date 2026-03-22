using SchemaNode.Attribute;
using SchemaNode.Enum;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema;

/**
 * The schema of the scalar type
*/
[SchemaApp]
[Schema($"{NS_SYSTEM_SCHEMA_DEF_SCALAR}.schema")]
public sealed class ScalarSchema
{
    /// <summary>
    /// The scalar name
    /// </summary>
    [Index]
    [JsonIgnore]
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Name { get; set; }

    /// <summary>
    /// The base type of the scalar
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    [Schema(NS_SYSTEM_SCHEMA_TYPE_SCALAR)]
    public string? Base { get; set; }

    /// <summary>
    /// The default unit of the scalar value
    /// </summary>
    public LocaleString? Unit { get; set; }

    /// <summary>
    /// The default low limit of the scalar value
    /// </summary>
    public decimal? LowLimit { get; set; }

    /// <summary>
    /// The default up limit of the scalar value
    /// </summary>
    public decimal? UpLimit { get; set; }

    /// <summary>
    /// The default error message of the scalar value
    /// </summary>
    public LocaleString? Error  { get; set; }

    /// <summary>
    /// The white list function
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    [Schema(NS_SYSTEM_SCHEMA_TYPE_RULE_WHITELIST)]
    public string? WhiteList { get; set; }
    
    /// <summary>
    /// As suggest
    /// </summary>
    public bool? AsSuggest { get; set; }

    /// <summary>
    /// The function to validate the scalar value in frontend
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    [Schema(NS_SYSTEM_SCHEMA_TYPE_RULE_VALID)]
    public string? PreValid  { get; set; }

    /// <summary>
    /// The eval function to convert the scalar value
    /// </summary>
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    [Schema(NS_SYSTEM_SCHEMA_TYPE_RULE_VALID)]
    public string? PostValid  { get; set; }// 用来存放额外的字段

    /// <summary>
    /// Cross-platform pattern validation for string scalar types (Lua-pattern-style, not regex).
    /// When configured, string values must match this pattern to be valid.
    /// </summary>
    public Pattern[]? Pattern { get; set; }

    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }

    /// <summary>
    /// Used to combine custom schema to system schema
    /// </summary>
    internal void CombineCustomSchema(ScalarSchema? other)
    {
        if (other == null) return;
        Unit = Unit != null ? Unit.Concat(other.Unit) : other.Unit;
        Error = Error != null ? Error.Concat(other.Error) : other.Error;
        LowLimit = other.LowLimit ?? LowLimit;
        UpLimit = other.UpLimit ?? UpLimit;
        WhiteList = string.IsNullOrWhiteSpace(other.WhiteList) ? WhiteList : other.WhiteList;
        AsSuggest = other.AsSuggest ?? AsSuggest;
        PreValid = string.IsNullOrWhiteSpace(other.PreValid) ? PreValid : other.PreValid;
        PostValid = string.IsNullOrWhiteSpace(other.PostValid) ? PostValid : other.PostValid;
        Pattern = other.Pattern ?? Pattern;
    }
}

/// <summary>
/// A character range for cross-platform pattern validation (not regex).
/// Defines an inclusive range [Start..End] of allowed characters.
/// </summary>
[SchemaApp]
[Schema($"{NS_SYSTEM_SCHEMA_DEF_SCALAR}.charrange")]
public sealed class CharRange
{
    /// <summary>
    /// The start character of the range (inclusive)
    /// </summary>
    public char Start { get; set; }

    /// <summary>
    /// The end character of the range (inclusive)
    /// </summary>
    public char End { get; set; }

    #region Presets

    /// <summary>[0-9]</summary>
    public static readonly CharRange[] Digit = [new() { Start = '0', End = '9' }];

    /// <summary>[0-9a-f] — use with CaseIgnore for full hex</summary>
    public static readonly CharRange[] Hex = [new() { Start = '0', End = '9' }, new() { Start = 'a', End = 'f' }];

    /// <summary>[a-z]</summary>
    public static readonly CharRange[] Lower = [new() { Start = 'a', End = 'z' }];

    /// <summary>[A-Z]</summary>
    public static readonly CharRange[] Upper = [new() { Start = 'A', End = 'Z' }];

    /// <summary>[a-zA-Z]</summary>
    public static readonly CharRange[] Alpha = [new() { Start = 'a', End = 'z' }, new() { Start = 'A', End = 'Z' }];

    /// <summary>[a-zA-Z0-9]</summary>
    public static readonly CharRange[] AlphaDigit = [new() { Start = 'a', End = 'z' }, new() { Start = 'A', End = 'Z' }, new() { Start = '0', End = '9' }];

    #endregion
}

/// <summary>
/// A single step in a cross-platform pattern sequence (Lua-pattern-style, not regex).
/// Each step matches a portion of the input string.
/// The full pattern is an ordered sequence of PatternParts that must match contiguously.
/// </summary>
[SchemaApp]
[Schema($"{NS_SYSTEM_SCHEMA_DEF_SCALAR}.pattern")]
public sealed class Pattern
{
    /// <summary>
    /// The type of this pattern step: Literal, CharSet, Any, or Group
    /// </summary>
    public PatternType Type { get; set; }

    /// <summary>
    /// Literal: the exact text to match
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// CharSet: allowed character ranges (e.g., [0-9], [a-z])
    /// </summary>
    public CharRange[]? Ranges { get; set; }

    /// <summary>
    /// CharSet: allowed specific characters (e.g., "+-_.@")
    /// </summary>
    public string? Chars { get; set; }

    /// <summary>
    /// Group: sub-pattern sequence that is matched as a single unit.
    /// The entire sub-sequence must match contiguously, and the group
    /// itself can be optional (Min=0) or repeated (Min/Max).
    /// </summary>
    public Pattern[]? Parts { get; set; }

    /// <summary>
    /// Minimum repetition count (default: 1).
    /// Set to 0 for optional matching (Literal, Group).
    /// </summary>
    public int? Min { get; set; }

    /// <summary>
    /// Maximum repetition count (default: 1, null/0 means unlimited for CharSet/Any)
    /// </summary>
    public int? Max { get; set; }

    /// <summary>
    /// When true, matching is case-insensitive for this part.
    /// For Group, this setting propagates to all sub-parts unless they explicitly override it.
    /// When null, inherits from the parent context.
    /// </summary>
    public bool? CaseIgnore { get; set; }

    /// <summary>
    /// Match a pattern sequence against the input starting at the given position.
    /// Returns the number of characters consumed, or -1 if the pattern does not match.
    /// </summary>
    /// <param name="input">The input string to match against</param>
    /// <param name="start">The starting position in the input</param>
    /// <param name="pattern">The pattern parts to match</param>
    /// <param name="caseIgnore">Inherited case-ignore setting from the parent context</param>
    public static int Match(string input, int start, Pattern[] pattern, bool caseIgnore = false)
    {
        int pos = start;

        foreach (var pp in pattern)
        {
            int min = pp.Min ?? 1;
            int max = pp.Max ?? (pp.Type is PatternType.Literal or PatternType.Group ? 1 : min);
            if (max <= 0) max = int.MaxValue;
            bool ci = pp.CaseIgnore ?? caseIgnore;

            switch (pp.Type)
            {
                case PatternType.Literal:
                {
                    if (pp.Text == null) continue;
                    if (pos + pp.Text.Length <= input.Length &&
                        input.AsSpan(pos, pp.Text.Length).Equals(pp.Text.AsSpan(),
                            ci ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    {
                        pos += pp.Text.Length;
                    }
                    else if (min > 0)
                    {
                        return -1;
                    }
                    break;
                }

                case PatternType.CharSet:
                {
                    int count = 0;
                    while (pos < input.Length && count < max && MatchCharSet(input[pos], pp, ci))
                    {
                        pos++;
                        count++;
                    }
                    if (count < min) return -1;
                    break;
                }

                case PatternType.Any:
                {
                    int count = 0;
                    while (pos < input.Length && count < max)
                    {
                        pos++;
                        count++;
                    }
                    if (count < min) return -1;
                    break;
                }

                case PatternType.Group:
                {
                    if (pp.Parts is not { Length: > 0 }) continue;
                    int count = 0;
                    while (count < max)
                    {
                        int consumed = Match(input, pos, pp.Parts, ci);
                        if (consumed < 0) break;
                        pos += consumed;
                        count++;
                    }
                    if (count < min) return -1;
                    break;
                }
            }
        }

        return pos - start;
    }

    /// <summary>
    /// Match a pattern against the entire input string.
    /// Returns true if the full string matches the pattern exactly.
    /// </summary>
    public static bool IsMatch(string input, Pattern[] pattern)
    {
        int consumed = Match(input, 0, pattern);
        return consumed == input.Length;
    }

    /// <summary>
    /// Check whether a character matches the CharSet rules of a Pattern
    /// </summary>
    public static bool MatchCharSet(char c, Pattern pp, bool caseIgnore = false)
    {
        if (MatchCharSetCore(c, pp)) return true;
        if (!caseIgnore) return false;
        char flipped = char.IsUpper(c) ? char.ToLowerInvariant(c) : char.ToUpperInvariant(c);
        return flipped != c && MatchCharSetCore(flipped, pp);
    }

    private static bool MatchCharSetCore(char c, Pattern pp)
    {
        if (pp.Ranges != null)
        {
            foreach (var range in pp.Ranges)
            {
                if (c >= range.Start && c <= range.End) return true;
            }
        }
        return pp.Chars != null && pp.Chars.Contains(c);
    }
}