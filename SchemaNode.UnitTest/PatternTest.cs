using SchemaNode.Enum;
using SchemaNode.Schema;

namespace SchemaNode.UnitTest;

/// <summary>
/// Tests for Pattern matching: Group, Literal, CaseIgnore
/// </summary>
[TestClass]
public class PatternTest : TestBase
{
    /// <summary>
    /// Pattern Group and optional Literal matching
    /// </summary>
    [TestMethod]
    public void PatternPart_Group_And_OptionalLiteral()
    {
        // Pattern: ^\d+(\.\d+)?$ — integer or decimal number
        Pattern[] floatPattern =
        [
            new() { Type = PatternType.CharSet, Ranges = CharRange.Digit, Min = 1, Max = 0 },
            new() { Type = PatternType.Group, Min = 0, Max = 1, Parts =
            [
                new() { Type = PatternType.Literal, Text = "." },
                new() { Type = PatternType.CharSet, Ranges = CharRange.Digit, Min = 1, Max = 0 },
            ]},
        ];

        Assert.IsTrue(Pattern.IsMatch("123", floatPattern), "Integer should match");
        Assert.IsTrue(Pattern.IsMatch("123.456", floatPattern), "Decimal should match");
        Assert.IsFalse(Pattern.IsMatch("123.", floatPattern), "Trailing dot should not match");
        Assert.IsFalse(Pattern.IsMatch(".456", floatPattern), "Leading dot should not match");
        Assert.IsFalse(Pattern.IsMatch("", floatPattern), "Empty should not match");
        Assert.IsFalse(Pattern.IsMatch("abc", floatPattern), "Letters should not match");

        // Pattern: ^[a-z]{2}-?[A-Z]{2}$ — language code with optional dash
        Pattern[] langPattern =
        [
            new() { Type = PatternType.CharSet, Ranges = CharRange.Lower, Min = 2, Max = 2 },
            new() { Type = PatternType.Literal, Text = "-", Min = 0 },
            new() { Type = PatternType.CharSet, Ranges = CharRange.Upper, Min = 2, Max = 2 },
        ];

        Assert.IsTrue(Pattern.IsMatch("en-US", langPattern), "en-US should match");
        Assert.IsTrue(Pattern.IsMatch("enUS", langPattern), "enUS without dash should match");
        Assert.IsFalse(Pattern.IsMatch("e-US", langPattern), "Single lowercase should not match");
        Assert.IsFalse(Pattern.IsMatch("en-U", langPattern), "Single uppercase should not match");

        // Pattern: ^[+-]?\d+(\.\d+)?(e-?\d+)?$ — scientific number
        Pattern[] numberPattern =
        [
            new() { Type = PatternType.CharSet, Chars = "+-", Min = 0, Max = 1 },
            new() { Type = PatternType.CharSet, Ranges = CharRange.Digit, Min = 1, Max = 0 },
            new() { Type = PatternType.Group, Min = 0, Max = 1, Parts =
            [
                new() { Type = PatternType.Literal, Text = "." },
                new() { Type = PatternType.CharSet, Ranges = CharRange.Digit, Min = 1, Max = 0 },
            ]},
            new() { Type = PatternType.Group, Min = 0, Max = 1, Parts =
            [
                new() { Type = PatternType.Literal, Text = "e" },
                new() { Type = PatternType.Literal, Text = "-", Min = 0 },
                new() { Type = PatternType.CharSet, Ranges = CharRange.Digit, Min = 1, Max = 0 },
            ]},
        ];

        Assert.IsTrue(Pattern.IsMatch("42", numberPattern), "Integer");
        Assert.IsTrue(Pattern.IsMatch("-42", numberPattern), "Negative integer");
        Assert.IsTrue(Pattern.IsMatch("+42", numberPattern), "Positive integer");
        Assert.IsTrue(Pattern.IsMatch("3.14", numberPattern), "Decimal");
        Assert.IsTrue(Pattern.IsMatch("1.5e10", numberPattern), "Scientific");
        Assert.IsTrue(Pattern.IsMatch("1.5e-3", numberPattern), "Scientific negative exponent");
        Assert.IsFalse(Pattern.IsMatch("", numberPattern), "Empty");
        Assert.IsFalse(Pattern.IsMatch(".", numberPattern), "Just dot");
        Assert.IsFalse(Pattern.IsMatch("abc", numberPattern), "Letters");
    }

    /// <summary>
    /// CaseIgnore on CharSet, Literal, and Group propagation
    /// </summary>
    [TestMethod]
    public void PatternPart_CaseIgnore()
    {
        // CharSet CaseIgnore: [0-9a-f] with CaseIgnore matches uppercase hex
        Pattern[] hexPattern =
        [
            new() { Type = PatternType.CharSet, Ranges = CharRange.Hex, CaseIgnore = true, Min = 1, Max = 0 },
        ];

        Assert.IsTrue(Pattern.IsMatch("abc", hexPattern), "lowercase hex");
        Assert.IsTrue(Pattern.IsMatch("ABC", hexPattern), "uppercase hex");
        Assert.IsTrue(Pattern.IsMatch("aB3cF", hexPattern), "mixed case hex");
        Assert.IsFalse(Pattern.IsMatch("xyz", hexPattern), "non-hex should fail");

        // Without CaseIgnore: [0-9a-f] does not match uppercase
        Pattern[] hexStrictPattern =
        [
            new() { Type = PatternType.CharSet, Ranges = CharRange.Hex, Min = 1, Max = 0 },
        ];

        Assert.IsTrue(Pattern.IsMatch("abc", hexStrictPattern), "lowercase hex strict");
        Assert.IsFalse(Pattern.IsMatch("ABC", hexStrictPattern), "uppercase hex should fail strict");

        // Literal CaseIgnore
        Pattern[] literalCiPattern =
        [
            new() { Type = PatternType.Literal, Text = "hello", CaseIgnore = true },
        ];

        Assert.IsTrue(Pattern.IsMatch("hello", literalCiPattern), "exact case");
        Assert.IsTrue(Pattern.IsMatch("HELLO", literalCiPattern), "all upper");
        Assert.IsTrue(Pattern.IsMatch("HeLLo", literalCiPattern), "mixed case");
        Assert.IsFalse(Pattern.IsMatch("world", literalCiPattern), "different text");

        // Group CaseIgnore propagation: Group(CaseIgnore=true) → Parts inherit
        Pattern[] groupCiPattern =
        [
            new() { Type = PatternType.Group, CaseIgnore = true, Parts =
            [
                new() { Type = PatternType.Literal, Text = "ab" },
                new() { Type = PatternType.CharSet, Ranges = CharRange.Hex, Min = 2, Max = 2 },
            ]},
        ];

        Assert.IsTrue(Pattern.IsMatch("ab0f", groupCiPattern), "group lowercase");
        Assert.IsTrue(Pattern.IsMatch("AB0F", groupCiPattern), "group uppercase — propagated");
        Assert.IsTrue(Pattern.IsMatch("Ab0f", groupCiPattern), "group mixed");
        Assert.IsFalse(Pattern.IsMatch("abzz", groupCiPattern), "non-hex chars after literal");

        // Part-level CaseIgnore overrides parent: Group(CaseIgnore=true) but child Part(CaseIgnore=false)
        Pattern[] overridePattern =
        [
            new() { Type = PatternType.Group, CaseIgnore = true, Parts =
            [
                new() { Type = PatternType.CharSet, Ranges = CharRange.Lower, CaseIgnore = false, Min = 2, Max = 2 },
                new() { Type = PatternType.CharSet, Ranges = CharRange.Digit, Min = 2, Max = 2 },
            ]},
        ];

        Assert.IsTrue(Pattern.IsMatch("ab12", overridePattern), "lowercase with override");
        Assert.IsFalse(Pattern.IsMatch("AB12", overridePattern), "uppercase rejected by explicit CaseIgnore=false");
    }
}
