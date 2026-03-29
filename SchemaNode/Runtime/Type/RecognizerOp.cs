using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Schema;
using System.Globalization;
using System.Text;

namespace SchemaNode.Runtime;

/// <summary>
/// Interface for recognizer IR operations supporting parse and format.
/// Each operation handles one atomic step in the recognizer pipeline.
/// </summary>
public interface IRecognizerOp
{
    /// <summary>
    /// Parse: consume from input starting at <paramref name="pos"/>.
    /// Returns the new position on success, or a negative value on failure.
    /// Field values are stored in <paramref name="state"/>.Fields;
    /// terminal values are stored in <paramref name="state"/>.Value.
    /// </summary>
    int Parse(SchemaContext context, string input, int pos, RecognizerParseState state);

    /// <summary>
    /// Format: emit the string representation into <paramref name="sb"/>.
    /// Returns true on success, false on failure.
    /// </summary>
    bool Format(SchemaContext context, AnySchemaNode value, StringBuilder sb);
}

/// <summary>
/// Shared mutable state for the recognizer parse pipeline
/// </summary>
public sealed class RecognizerParseState
{
    /// <summary>
    /// Collected field values (used by struct recognizers)
    /// </summary>
    public Dictionary<string, AnySchemaNode?> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The final parsed value
    /// </summary>
    public AnySchemaNode? Value { get; set; }
}

/// <summary>
/// Match / emit a literal text segment
/// </summary>
public sealed record RecognizerLiteralOp(string Text) : IRecognizerOp
{
    public int Parse(SchemaContext context, string input, int pos, RecognizerParseState state)
    {
        if (pos + Text.Length > input.Length)
            return -1;
        if (!input.AsSpan(pos, Text.Length).SequenceEqual(Text.AsSpan()))
            return -1;
        return pos + Text.Length;
    }

    public bool Format(SchemaContext context, AnySchemaNode value, StringBuilder sb)
    {
        sb.Append(Text);
        return true;
    }
}

/// <summary>
/// Match / emit a struct field value.
/// The extraction strategy is pre-determined at compile time:
///   - Boundary != null   → match by next literal boundary
///   - otherwise          → consume to end of input
/// </summary>
public sealed record RecognizerFieldOp(
    string Field,
    string? Boundary,
    RecognizerType? SubRecognizer,
    StructFieldSchema? FieldSchema,
    FunctionType? FormatFunc = null,
    FunctionType? ParseFunc = null) : IRecognizerOp
{
    public int Parse(SchemaContext context, string input, int pos, RecognizerParseState state)
    {
        string fieldValue;

        if (Boundary != null)
        {
            int boundaryIdx = input.IndexOf(Boundary, pos, StringComparison.Ordinal);
            if (boundaryIdx < 0) return -1;
            fieldValue = input[pos..boundaryIdx];
            pos = boundaryIdx;
        }
        else
        {
            fieldValue = input[pos..];
            pos = input.Length;
        }

        if (ParseFunc != null)
        {
            var task = ParseFunc.CallAsync<AnySchemaNode>(context, [fieldValue]);
            var result = task.IsCompletedSuccessfully ? task.Result : task.GetAwaiter().GetResult();
            if (result != null) { state.Fields[Field] = result; return pos; }
        }

        if (SubRecognizer != null)
        {
            var task = SubRecognizer.RecognizeAsync(context, fieldValue);
            var subResult = task.IsCompletedSuccessfully ? task.Result : task.GetAwaiter().GetResult();
            if (!subResult.Success) return -1;
            state.Fields[Field] = subResult.Value;
        }
        else
        {
            state.Fields[Field] = ConvertFieldValue(FieldSchema, fieldValue);
        }

        return pos;
    }

    public bool Format(SchemaContext context, AnySchemaNode value, StringBuilder sb)
    {
        if (value is not StructTypeNode obj) return false;
        var fieldNode = obj.GetField(Field);
        if (fieldNode == null) return false;

        if (FormatFunc != null)
        {
            var task = FormatFunc.CallAsync<string>(context, [fieldNode]);
            var result = task.IsCompletedSuccessfully ? task.Result : task.GetAwaiter().GetResult();
            if (result != null) { sb.Append(result); return true; }
        }

        if (SubRecognizer != null)
        {
            var task = SubRecognizer.EmitAsync(context, fieldNode);
            var emitted = task.IsCompletedSuccessfully ? task.Result : task.GetAwaiter().GetResult();
            if (emitted == null) return false;
            sb.Append(emitted);
        }
        else
        {
            sb.Append(fieldNode.ToString());
        }

        return true;
    }

    private static AnySchemaNode? ConvertFieldValue(StructFieldSchema? field, string rawValue)
    {
        if (field?.SchemaType == null) return null;
        return field.SchemaType.CreateNode(rawValue);
    }
}

/// <summary>
/// Assemble collected fields into a StructTypeNode (terminal op for struct recognizers).
/// On parse: verifies that the entire input has been consumed and builds the result.
/// On format: no-op (field ops have already appended).
/// </summary>
public sealed record RecognizerStructEndOp(StructType StructType) : IRecognizerOp
{
    public int Parse(SchemaContext context, string input, int pos, RecognizerParseState state)
    {
        if (pos != input.Length) return -1;

        var node = new StructTypeNode(StructType);
        foreach (var field in StructType.Fields)
        {
            if (state.Fields.TryGetValue(field.Name, out var value) && value != null)
                node.SetField(field.Name, value);
        }
        state.Value = node;
        return pos;
    }

    public bool Format(SchemaContext context, AnySchemaNode value, StringBuilder sb)
    {
        return true;
    }
}

/// <summary>
/// Parse / format a scalar value with optional FormatDescriptor transformations
/// </summary>
public sealed record RecognizerScalarOp(ScalarType Scalar, FormatDescriptor? FormatDesc, FunctionType? FormatFunc = null, FunctionType? ParseFunc = null) : IRecognizerOp
{
    public int Parse(SchemaContext context, string input, int pos, RecognizerParseState state)
    {
        if (ParseFunc != null)
        {
            var task = ParseFunc.CallAsync<AnySchemaNode>(context, [input]);
            var result = task.IsCompletedSuccessfully ? task.Result : task.GetAwaiter().GetResult();
            if (result != null) { state.Value = result; return input.Length; }
        }

        string parsed = input;

        if (FormatDesc != null)
        {
            if (FormatDesc.Trim == true) parsed = parsed.Trim();
            if (FormatDesc.ToUpper == true) parsed = parsed.ToUpperInvariant();
            if (FormatDesc.ToLower == true) parsed = parsed.ToLowerInvariant();

            if (FormatDesc.PadChar != null && (Scalar.IsInt || Scalar.IsNumber || Scalar.IsDouble || Scalar.IsSingle))
            {
                parsed = FormatDesc.PadLeft != false
                    ? parsed.TrimStart(FormatDesc.PadChar.Value)
                    : parsed.TrimEnd(FormatDesc.PadChar.Value);
                if (parsed.Length == 0) parsed = "0";
            }
        }

        object? value;
        if (Scalar.IsBool)
        {
            if (!bool.TryParse(parsed, out var b)) return -1;
            value = b;
        }
        else if (Scalar.IsInt)
        {
            if (!long.TryParse(parsed, out var n)) return -1;
            value = n;
        }
        else if (Scalar.IsNumber || Scalar.IsDouble || Scalar.IsSingle)
        {
            if (!decimal.TryParse(parsed, CultureInfo.InvariantCulture, out var d)) return -1;
            value = d;
        }
        else if (Scalar.IsDate || Scalar.IsFullDate || Scalar.IsYear || Scalar.IsYearMonth)
        {
            if (FormatDesc?.Layout != null)
            {
                if (!DateTimeOffset.TryParseExact(parsed, FormatDesc.Layout, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
                    return -1;
                value = dto.ToString("O");
            }
            else
            {
                value = parsed;
            }
        }
        else
        {
            value = parsed;
        }

        state.Value = new ScalarTypeNode(Scalar, value);
        return input.Length;
    }

    public bool Format(SchemaContext context, AnySchemaNode value, StringBuilder sb)
    {
        if (FormatFunc != null)
        {
            var task = FormatFunc.CallAsync<string>(context, [value]);
            var result = task.IsCompletedSuccessfully ? task.Result : task.GetAwaiter().GetResult();
            if (result != null) { sb.Append(result); return true; }
        }

        if (FormatDesc == null)
        {
            sb.Append(value.ToString());
            return true;
        }

        string raw = value.ToString();

        // DateTime layout
        if ((Scalar.IsDate || Scalar.IsFullDate || Scalar.IsYear || Scalar.IsYearMonth) && FormatDesc.Layout != null)
        {
            if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
            {
                sb.Append(dto.ToString(FormatDesc.Layout, CultureInfo.InvariantCulture));
                return true;
            }
        }

        // Numeric formatting
        if (Scalar.IsInt || Scalar.IsNumber || Scalar.IsDouble || Scalar.IsSingle)
        {
            if (FormatDesc.Precision != null && decimal.TryParse(raw, CultureInfo.InvariantCulture, out var d))
                raw = d.ToString($"F{FormatDesc.Precision.Value}", CultureInfo.InvariantCulture);

            if (FormatDesc.MinDigits != null)
            {
                string sign = "";
                string body = raw;
                if (body.StartsWith('-'))
                {
                    sign = "-";
                    body = body[1..];
                }
                int dotIdx = body.IndexOf('.');
                string intPart = dotIdx >= 0 ? body[..dotIdx] : body;
                string decPart = dotIdx >= 0 ? body[dotIdx..] : "";
                char pad = FormatDesc.PadChar ?? '0';
                if (intPart.Length < FormatDesc.MinDigits.Value)
                    intPart = intPart.PadLeft(FormatDesc.MinDigits.Value, pad);
                raw = sign + intPart + decPart;
            }

            if (FormatDesc.MaxDigits != null)
            {
                string sign = "";
                string body = raw;
                if (body.StartsWith('-'))
                {
                    sign = "-";
                    body = body[1..];
                }
                int dotIdx = body.IndexOf('.');
                string intPart = dotIdx >= 0 ? body[..dotIdx] : body;
                string decPart = dotIdx >= 0 ? body[dotIdx..] : "";
                if (intPart.Length > FormatDesc.MaxDigits.Value)
                    intPart = intPart[^FormatDesc.MaxDigits.Value..];
                raw = sign + intPart + decPart;
            }
        }

        // String transformations
        if (FormatDesc.Trim == true) raw = raw.Trim();
        if (FormatDesc.ToUpper == true) raw = raw.ToUpperInvariant();
        if (FormatDesc.ToLower == true) raw = raw.ToLowerInvariant();

        // General padding (non-numeric)
        if (FormatDesc.PadChar != null && FormatDesc.MinDigits != null
            && !(Scalar.IsInt || Scalar.IsNumber || Scalar.IsDouble || Scalar.IsSingle))
        {
            raw = FormatDesc.PadLeft != false
                ? raw.PadLeft(FormatDesc.MinDigits.Value, FormatDesc.PadChar.Value)
                : raw.PadRight(FormatDesc.MinDigits.Value, FormatDesc.PadChar.Value);
        }

        sb.Append(raw);
        return true;
    }
}

/// <summary>
/// Parse / format an enum value using pre-built dictionaries for bidirectional mapping.
/// Constructed from Entry[] at compile time for O(1) lookup.
/// </summary>
public sealed class RecognizerEnumMappingOp : IRecognizerOp
{
    private readonly Dictionary<string, string> _displayToValue;
    private readonly Dictionary<string, string> _valueToDisplay;
    private readonly EnumType _enumType;
    private readonly FunctionType? _formatFunc;
    private readonly FunctionType? _parseFunc;

    public RecognizerEnumMappingOp(Entry[] mapping, EnumType enumType, FunctionType? formatFunc = null, FunctionType? parseFunc = null)
    {
        _enumType = enumType;
        _formatFunc = formatFunc;
        _parseFunc = parseFunc;
        _displayToValue = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _valueToDisplay = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in mapping)
        {
            string displayKey = entry.Label?.Key ?? entry.Value;

            _displayToValue.TryAdd(displayKey, entry.Value);
            _valueToDisplay.TryAdd(entry.Value, displayKey);

            if (entry.Label?.Trans != null)
            {
                foreach (var tran in entry.Label.Trans)
                {
                    if (!string.IsNullOrEmpty(tran.Tran))
                        _displayToValue.TryAdd(tran.Tran, entry.Value);
                }
            }
        }
    }

    public int Parse(SchemaContext context, string input, int pos, RecognizerParseState state)
    {
        if (_parseFunc != null)
        {
            var task = _parseFunc.CallAsync<AnySchemaNode>(context, [input]);
            var result = task.IsCompletedSuccessfully ? task.Result : task.GetAwaiter().GetResult();
            if (result != null) { state.Value = result; return input.Length; }
        }

        if (_displayToValue.TryGetValue(input, out var enumValue))
        {
            state.Value = new EnumTypeNode(_enumType, enumValue);
            return input.Length;
        }
        return -1;
    }

    public bool Format(SchemaContext context, AnySchemaNode value, StringBuilder sb)
    {
        if (_formatFunc != null)
        {
            var task = _formatFunc.CallAsync<string>(context, [value]);
            var result = task.IsCompletedSuccessfully ? task.Result : task.GetAwaiter().GetResult();
            if (result != null) { sb.Append(result); return true; }
        }

        string enumVal = value.ToString();
        sb.Append(_valueToDisplay.TryGetValue(enumVal, out var display) ? display : enumVal);
        return true;
    }
}

/// <summary>
/// Parse / format an enum value by looking up enum value/name from the EnumType
/// </summary>
public sealed record RecognizerEnumLookupOp(EnumType EnumType, FunctionType? FormatFunc = null, FunctionType? ParseFunc = null) : IRecognizerOp
{
    public int Parse(SchemaContext context, string input, int pos, RecognizerParseState state)
    {
        if (ParseFunc != null)
        {
            var task = ParseFunc.CallAsync<AnySchemaNode>(context, [input]);
            var result = task.IsCompletedSuccessfully ? task.Result : task.GetAwaiter().GetResult();
            if (result != null) { state.Value = result; return input.Length; }
        }

        var loadTask = EnumType.LoadEnumSubListAsync(context, "");
        var values = loadTask.IsCompletedSuccessfully ? loadTask.Result : loadTask.GetAwaiter().GetResult();
        if (values == null) return -1;

        foreach (var ev in values)
        {
            if (input.Equals(ev.Value, StringComparison.OrdinalIgnoreCase) ||
                input.Equals((string?)ev.Name, StringComparison.OrdinalIgnoreCase))
            {
                state.Value = new EnumTypeNode(EnumType, ev.Value);
                return input.Length;
            }
        }

        return -1;
    }

    public bool Format(SchemaContext context, AnySchemaNode value, StringBuilder sb)
    {
        if (FormatFunc != null)
        {
            var task = FormatFunc.CallAsync<string>(context, [value]);
            var result = task.IsCompletedSuccessfully ? task.Result : task.GetAwaiter().GetResult();
            if (result != null) { sb.Append(result); return true; }
        }

        sb.Append(value.ToString());
        return true;
    }
}

/// <summary>
/// Parse / format an array by splitting on a delimiter and recursively handling elements
/// </summary>
public sealed record RecognizerArrayOp(string Delimiter, RecognizerType? ElementRecognizer, ScalarType? ElementScalar, ArrayType ArrayType) : IRecognizerOp
{
    public int Parse(SchemaContext context, string input, int pos, RecognizerParseState state)
    {
        var segments = SplitByDelimiter(input, Delimiter);
        var arr = new ArrayTypeNode(ArrayType);

        foreach (var segment in segments)
        {
            if (string.IsNullOrEmpty(segment)) continue;

            if (ElementRecognizer != null)
            {
                var task = ElementRecognizer.RecognizeAsync(context, segment);
                var elemResult = task.IsCompletedSuccessfully ? task.Result : task.GetAwaiter().GetResult();
                if (!elemResult.Success) return -1;
                if (elemResult.Value != null) arr.Add(elemResult.Value);
            }
            else if (ElementScalar != null)
            {
                arr.Add(ElementScalar.CreateNode(segment) ?? new ScalarTypeNode(ElementScalar, segment));
            }
            else
            {
                arr.Add(segment);
            }
        }

        state.Value = arr;
        return input.Length;
    }

    public bool Format(SchemaContext context, AnySchemaNode value, StringBuilder sb)
    {
        if (value is not ArrayTypeNode arr) return false;

        for (int i = 0; i < arr.Count; i++)
        {
            if (i > 0) sb.Append(Delimiter);
            var elem = arr[i];
            if (elem == null) continue;

            if (ElementRecognizer != null && elem is AnySchemaNode elemNode)
            {
                var task = ElementRecognizer.EmitAsync(context, elemNode);
                var emitted = task.IsCompletedSuccessfully ? task.Result : task.GetAwaiter().GetResult();
                if (emitted == null) return false;
                sb.Append(emitted);
            }
            else
            {
                sb.Append(elem.ToString());
            }
        }

        return true;
    }

    private static List<string> SplitByDelimiter(string input, string delimiter)
    {
        var parts = new List<string>();
        int pos = 0;
        while (pos <= input.Length)
        {
            int idx = input.IndexOf(delimiter, pos, StringComparison.Ordinal);
            if (idx < 0)
            {
                parts.Add(input[pos..]);
                break;
            }
            parts.Add(input[pos..idx]);
            pos = idx + delimiter.Length;
        }
        return parts;
    }
}
