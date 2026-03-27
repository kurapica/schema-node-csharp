using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Attribute;
using SchemaNode.Node;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components.Property.Constraint;

[SchemaProperty([SchemaType.Scalar, SchemaType.StructField], [ValueSchemaType.String], includeArray: true, optionDepends: [nameof(RequireProperty)])]
public class PatternProperty : SchemaProperty<Pattern[]>, IConstraintProperty
{
    public bool? ValidateScalar(SchemaContext context, ScalarTypeNode node, StructTypeNode? parent = null)
    {
        if (node.IsEmpty || Value is not { Length: > 0 }) return null;
        ScalarType scalar = (ScalarType)node.SchemaType;

        if (scalar.IsString)
            return Pattern.IsMatch(node.Value!.ToString()!, Value);

        return null;
    }

    public bool? ValidateArray(SchemaContext context, ArrayTypeNode node, StructTypeNode? parent = null)
    {
        if (node.IsEmpty || Value is not { Length: > 0 }) return null;
        if (node.ElementType is not ScalarType scalar) return null;
        if (scalar.IsString)
        {
            foreach (var item in node)
            {
                if (item.IsEmpty) continue;
                if (!Pattern.IsMatch(item.Value!.ToString()!, Value))
                    return false;
            }
            return true;
        }
        return true;
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