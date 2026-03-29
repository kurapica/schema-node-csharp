using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Schema;
using System.Collections.Concurrent;
using System.Text;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory recognizer schema representation.
/// Type-first design: each recognizer declares a SourceType and structured Parts.
/// Supports parsing (string → type) and emitting (type → string).
/// </summary>
public sealed class RecognizerType : AnySchemaType
{
    #region Data

    /// <summary>
    /// The source type this recognizer describes
    /// </summary>
    public string SourceType { get; private set; } = string.Empty;

    /// <summary>
    /// The structured format parts
    /// </summary> 
    public RecognizerPart[] Parts { get; private set; } = [];

    #endregion

    #region Status

    /// <inheritdoc />
    public override SchemaType Type => SchemaType.Recognizer;

    #endregion

    #region Field

    /// <summary>
    /// The resolved source type
    /// </summary>
    private AnySchemaType? SourceSchemaType;

    /// <summary>
    /// Resolved recognizers for fields that require sub-recognizers
    /// </summary>
    private ConcurrentDictionary<string, RecognizerType> FieldRecognizers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Resolved recognizer for array element (if SourceType is an array)
    /// </summary>
    private RecognizerType? ElementRecognizer;

    /// <summary>
    /// Resolved format functions per part index
    /// </summary>
    private Dictionary<int, FunctionType> PartFormatFuncs { get; set; } = [];

    /// <summary>
    /// Resolved parse functions per part index
    /// </summary>
    private Dictionary<int, FunctionType> PartParseFuncs { get; set; } = [];

    /// <summary>
    /// The compiled flat IR operation array for parse and format
    /// </summary>
    private IRecognizerOp[] Ops = [];

    #endregion

    #region Method

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        RecognizerSchema? recognizer = schema.Recognizer;

        // Data
        SourceType = recognizer?.SourceType ?? string.Empty;
        Parts = recognizer?.Parts ?? [];

        if (recognizer == null)
        {
            Status = SchemaNodeStatus.NoDefinition;
            return;
        }

        // Resolve SourceType
        SourceSchemaType = !string.IsNullOrWhiteSpace(SourceType) ? await context.GetSchemaTypeAsync(SourceType) : null;
        if (SourceSchemaType?.IsValueType != true)
        {
            Status = SchemaNodeStatus.RecognizerWrongSourceType;
            return;
        }

        // Resolve per-part references for struct fields
        for (int pi = 0; pi < Parts.Length; pi++)
        {
            var part = Parts[pi];
            AnySchemaType? partType = null;
            switch (part.Type)
            {
                case FormatPartType.Literal:
                    continue;
                case FormatPartType.Self:
                    if (SourceSchemaType is ScalarType or EnumType)
                        partType = SourceSchemaType;
                    break;
                case FormatPartType.Field:
                    var field = string.IsNullOrWhiteSpace(part.Field) ? null : (SourceSchemaType as StructType)?.GetField(part.Field);
                    partType = field?.SchemaType;
                    break;
                case FormatPartType.Elements:
                    partType = (SourceSchemaType as ArrayType)?.ElementSchemaType;
                    break;
            }

            if (partType == null)
            {
                Status = SchemaNodeStatus.RecognizerWrongSourceType;
                return;
            }

            // If the part type is not a scalar or enum, we need a sub-recognizer
            if (partType is not (ScalarType or EnumType))
            {
                var fieldRecognizer = await FindRecognizerForTypeAsync(context, part.Recognizer);
                if (fieldRecognizer?.SourceSchemaType == null || !fieldRecognizer.SourceSchemaType.CanBeUseAs(partType))
                {
                    Status = SchemaNodeStatus.RecognizerWrongSourceType;
                    return;
                }

                if (part.Type == FormatPartType.Field)
                {
                    FieldRecognizers[part.Field!] = fieldRecognizer;
                    fieldRecognizer.AddRef(this);
                }
                else if (part.Type == FormatPartType.Elements)
                {
                    ElementRecognizer = fieldRecognizer;
                    ElementRecognizer?.AddRef(this);
                }
            }

            // Resolve per-part FormatDescriptor function references
            if (part.Format == null) continue;
            if (!string.IsNullOrWhiteSpace(part.Format.FormatFunc))
            {
                var fNode = await context.GetSchemaTypeAsync(part.Format.FormatFunc);
                if (fNode is FunctionType fft)
                {
                    PartFormatFuncs[pi] = fft;
                    fft.AddRef(this);
                }
            }

            if (!string.IsNullOrWhiteSpace(part.Format.ParseFunc))
            {
                var pNode = await context.GetSchemaTypeAsync(part.Format.ParseFunc);
                if (pNode is FunctionType pft)
                {
                    PartParseFuncs[pi] = pft;
                    pft.AddRef(this);
                }
            }
        }

        // Add ref
        SourceSchemaType.AddRef(this);

        // Compile the flat IR operation array
        CompileOps();
    }

    /// <summary>
    /// Find a recognizer by its fully qualified name
    /// </summary>
    internal static async Task<RecognizerType?> FindRecognizerForTypeAsync(SchemaContext context, string? recognizerName = null)
    {
        if (string.IsNullOrWhiteSpace(recognizerName))
            return null;

        var result = await context.GetSchemaTypeAsync(recognizerName);
        if (result is RecognizerType rt && rt.Status == SchemaNodeStatus.Ready)
            return rt;

        return null;
    }

    /// <inheritdoc />
    public override ArrayType? GetArrayType(bool exactly = false)
    {
        return null;
    }

    /// <inheritdoc />
    public override IEnumerable<AnySchemaType> GetDependNodes()
    {
        if (SourceSchemaType != null) yield return SourceSchemaType;
        foreach (var fr in FieldRecognizers.Values) yield return fr;
        if (ElementRecognizer != null) yield return ElementRecognizer;
        foreach (var ff in PartFormatFuncs.Values) yield return ff;
        foreach (var pf in PartParseFuncs.Values) yield return pf;
    }

    /// <inheritdoc />
    public override void Release()
    {
        SourceSchemaType?.RemoveRef(this);
        foreach (var fr in FieldRecognizers.Values) fr.RemoveRef(this);
        ElementRecognizer?.RemoveRef(this);
        foreach (var ff in PartFormatFuncs.Values) ff.RemoveRef(this);
        foreach (var pf in PartParseFuncs.Values) pf.RemoveRef(this);
        Ops = [];
    }

    /// <summary>
    /// Compile Parts into a flat IR operation array for both parse and format.
    /// Called once at the end of LoadAsync so that RecognizeAsync / EmitAsync
    /// simply iterate the pre-built array with no branching or look-ahead.
    /// </summary>
    private void CompileOps()
    {
        // Scalar source type
        if (SourceSchemaType is ScalarType scalar)
        {
            var fmt = Parts.FirstOrDefault()?.Format;
            PartFormatFuncs.TryGetValue(0, out var scalarFmtFunc);
            PartParseFuncs.TryGetValue(0, out var scalarParseFunc);
            Ops = [new RecognizerScalarOp(scalar, fmt, scalarFmtFunc, scalarParseFunc)];
            return;
        }

        // Enum source type
        if (SourceSchemaType is EnumType sourceEnumType)
        {
            var fmt = Parts.FirstOrDefault()?.Format;
            PartFormatFuncs.TryGetValue(0, out var enumFmtFunc);
            PartParseFuncs.TryGetValue(0, out var enumParseFunc);
            if (fmt?.Mapping is { Length: > 0 })
                Ops = [new RecognizerEnumMappingOp(fmt.Mapping, sourceEnumType, enumFmtFunc, enumParseFunc)];
            else
                Ops = [new RecognizerEnumLookupOp(sourceEnumType, enumFmtFunc, enumParseFunc)];
            return;
        }

        // Array source type
        if (SourceSchemaType is ArrayType sourceArrayType)
        {
            var arrayPart = Parts.FirstOrDefault(s => s.Type == FormatPartType.Elements);
            string delimiter = arrayPart?.Delimiter ?? ",";
            var elemScalar = sourceArrayType.ElementSchemaType as ScalarType;
            Ops = [new RecognizerArrayOp(delimiter, ElementRecognizer, elemScalar, sourceArrayType)];
            return;
        }

        // Struct source type: flatten each part into an op with pre-computed boundaries
        if (SourceSchemaType is StructType sourceStructType)
        {
            var ops = new List<IRecognizerOp>();

            for (int i = 0; i < Parts.Length; i++)
            {
                var part = Parts[i];

                switch (part.Type)
                {
                    case FormatPartType.Literal:
                        if (part.Text != null)
                            ops.Add(new RecognizerLiteralOp(part.Text));
                        break;

                    case FormatPartType.Field:
                        if (string.IsNullOrWhiteSpace(part.Field)) break;

                        string? boundary = null;

                        // Pre-compute the boundary from the next literal part
                        for (int j = i + 1; j < Parts.Length; j++)
                        {
                            if (Parts[j].Type == FormatPartType.Literal)
                            {
                                boundary = Parts[j].Text;
                                break;
                            }
                        }

                        FieldRecognizers.TryGetValue(part.Field, out var subRec);
                        var fieldSchema = sourceStructType.Fields.FirstOrDefault(
                            f => f.Name.Equals(part.Field, StringComparison.OrdinalIgnoreCase));

                        PartFormatFuncs.TryGetValue(i, out var fieldFmtFunc);
                        PartParseFuncs.TryGetValue(i, out var fieldParseFunc);

                        ops.Add(new RecognizerFieldOp(
                            part.Field,
                            boundary,
                            subRec,
                            fieldSchema,
                            fieldFmtFunc,
                            fieldParseFunc));
                        break;
                }
            }

            ops.Add(new RecognizerStructEndOp(sourceStructType));
            Ops = [.. ops];
            return;
        }

        Ops = [];
    }

    #endregion

    #region Recognize

    /// <summary>
    /// Parse the input string into a structured value based on the compiled IR ops.
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="input">The input string to parse</param>
    /// <returns>The recognition result</returns>
    public Task<RecognizeOutput> RecognizeAsync(SchemaContext context, string input)
    {
        if (Status != SchemaNodeStatus.Ready || Ops.Length == 0)
            return Task.FromResult(RecognizeOutput.Fail(0));

        var state = new RecognizerParseState();
        int pos = 0;

        foreach (var op in Ops)
        {
            pos = op.Parse(context, input, pos, state);
            if (pos < 0)
                return Task.FromResult(RecognizeOutput.Fail(0));
        }

        return Task.FromResult(new RecognizeOutput { Success = true, Position = pos, Value = state.Value });
    }

    #endregion

    #region Emit

    /// <summary>
    /// Generate a string from a structured value using the compiled IR ops.
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="value">The structured value</param>
    /// <returns>The generated string, or null if emission fails</returns>
    public Task<string?> EmitAsync(SchemaContext context, AnySchemaNode value)
    {
        if (Status != SchemaNodeStatus.Ready || Ops.Length == 0)
            return Task.FromResult<string?>(null);

        var sb = new StringBuilder();

        foreach (var op in Ops)
        {
            if (!op.Format(context, value, sb))
                return Task.FromResult<string?>(null);
        }

        return Task.FromResult<string?>(sb.ToString());
    }

    #endregion

    #region Conversion

    /// <summary>
    /// Convert the node to schema
    /// </summary>
    public static implicit operator NodeSchema?(RecognizerType? schema)
    {
        return schema?.ToSchema().With(new RecognizerSchema
        {
            SourceType = schema.SourceType,
            Parts = schema.Parts,
        });
    }
    #endregion
}

/// <summary>
/// The output of a recognition operation
/// </summary>
public class RecognizeOutput
{
    /// <summary>
    /// Whether the recognition succeeded
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The position in the input after recognition
    /// </summary>
    public int Position { get; init; }

    /// <summary>
    /// The structured result value
    /// </summary>
    public AnySchemaNode? Value { get; init; }

    /// <summary>
    /// Create a failed result
    /// </summary>
    public static RecognizeOutput Fail(int position) => new() { Success = false, Position = position };
}