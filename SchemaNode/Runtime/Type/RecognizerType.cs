using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Property.Convert;
using SchemaNode.Schema;
using System.Globalization;
using System.Text;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory recognizerType schema representation.
/// Type-first design: each recognizerType declares a SourceType and structured Parts.
/// Supports parsing (string → type) and emitting (type → string).
/// </summary>
public sealed class RecognizerType : AnySchemaType
{
    #region Data

    /// <summary>
    /// The source type this recognizerType describes
    /// </summary>
    public string SourceType { get; private set; } = string.Empty;

    /// <summary>
    /// The structured format parts
    /// </summary> 
    public RecognizerPartSchema[] Parts { get; private set; } = [];

    /// <summary>
    /// The relations between parts
    /// </summary>
    public RecognizerRelationSchema[]? Relations { get; set; }

    #endregion

    #region Status

    /// <inheritdoc />
    public override SchemaType Type => SchemaType.Recognizer;

    #endregion

    #region Field

    /// <summary>
    /// The resolved source type
    /// </summary>
    internal AnySchemaType? SourceSchemaType;

    /// <summary>
    /// Resolved RecognizerRelations keyed by part field name
    /// </summary>
    private Dictionary<string, List<RecognizerRelationSchema>>? RelationsByPart;

    #endregion

    #region Method

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        // Reset runtime state
        RelationsByPart = null;
        SourceSchemaType = null;

        RecognizerSchema? recognizer = schema.Recognizer;

        // Data
        SourceType = recognizer?.SourceType ?? string.Empty;
        Parts = recognizer?.Parts ?? [];
        Relations = recognizer?.Relations;

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

        // Build relation lookup before processing parts (parts may reference relations to skip static check)
        if (recognizer.Relations is { Length: > 0 })
        {
            RelationsByPart = new Dictionary<string, List<RecognizerRelationSchema>>(StringComparer.OrdinalIgnoreCase);
            foreach (var rel in recognizer.Relations)
            {
                var funcType = await context.GetSchemaTypeAsync<FunctionType>(rel.Func, preload: preload);
                if (funcType == null)
                {
                    rel.Status = SchemaNodeStatus.RecognizerWrongRelation;
                    Status = SchemaNodeStatus.RecognizerWrongRelation;
                    continue;
                }
                rel.Status = null;
                rel.FuncNode = funcType;
                funcType.AddRef(this);
                if (!RelationsByPart.TryGetValue(rel.Part, out var list))
                {
                    list = [];
                    RelationsByPart[rel.Part] = list;
                }
                list.Add(rel);
            }
        }

        // Resolve per-part references
        bool hasPrefix = false;
        for (int pi = 0; pi < Parts.Length; pi++)
        {
            var part = Parts[pi];
            AnySchemaType? partType = null;
            ArrayType? arrType = null;
            switch (part.Type)
            {
                case RecognizerPartType.Literal:
                    continue;
                case RecognizerPartType.Self:
                    if (SourceSchemaType is ScalarType or EnumType)
                        partType = SourceSchemaType;
                    break;
                case RecognizerPartType.Field:
                    var field = string.IsNullOrWhiteSpace(part.Field) ? null : (SourceSchemaType as StructType)?.GetField(part.Field);
                    partType = field?.SchemaType;
                    if (partType is ArrayType)
                    {
                        arrType = (ArrayType)partType;
                        partType = arrType.ElementSchemaType;
                    }
                    break;
                case RecognizerPartType.Elements:
                    if (SourceSchemaType is ArrayType elemArrayType)
                    {
                        arrType = elemArrayType;
                        partType = arrType.ElementSchemaType;
                    }
                    break;
            }

            if (partType == null)
            {
                Status = SchemaNodeStatus.RecognizerWrongSourceType;
                return;
            }

            // For complex types (non-scalar/enum), a static recognizer OR a RecognizerRelation must be provided
            bool needsRecognizer = part.Type != RecognizerPartType.Self && partType is not (ScalarType or EnumType);
            bool hasExplicitRecognizer = part.Extensions?.ContainsKey("recognizer") == true;
            bool hasRelation = RelationsByPart?.ContainsKey(part.Field ?? "") == true;

            if (needsRecognizer && !hasExplicitRecognizer && !hasRelation)
            {
                Status = SchemaNodeStatus.RecognizerWrongSourceType;
                return;
            }

            // Load convert properties (RecognizerProperty is created inside if Recognizer is set)
            await part.LoadRecognizerPart(context, this, partType, part.Field);

            if (part.Status.HasValue && part.Status != SchemaNodeStatus.Ready)
            {
                Status = part.Status.Value;
                return;
            }

            // Check prefix property ordering
            if (!string.IsNullOrWhiteSpace(part.Prefix))
            {
                hasPrefix = true;
            }
            else if (hasPrefix)
            {
                Status = SchemaNodeStatus.RecognizerWrongParts;
                return;
            }
        }

        // Add ref
        SourceSchemaType.AddRef(this);
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
        if (RelationsByPart != null)
            foreach (var rels in RelationsByPart.Values)
                foreach (var rel in rels)
                    if (rel.FuncNode != null) yield return rel.FuncNode;
        foreach (var part in Parts)
        {
            if (part.RefTypes != null)
                foreach (var refType in part.RefTypes)
                    yield return refType;
        }
    }

    /// <inheritdoc />
    public override void Release()
    {
        SourceSchemaType?.RemoveRef(this);
        if (RelationsByPart != null)
        {
            foreach (var rels in RelationsByPart.Values)
            {
                foreach (var rel in rels)
                {
                    rel.FuncNode?.RemoveRef(this);
                    rel.FuncNode = null;
                }
            }
            RelationsByPart = null;
        }
        foreach (var part in Parts)
            part.UnloadRecognizerPart(this);
    }

    #endregion

    #region Recognize

    /// <summary>
    /// Parse the input string into a structured value by iterating Parts directly.
    /// Parse order per part: Phase 1 Prefix → Phase 2 Suffix → Phase 3 Content converters (reversed).
    /// </summary>
    public async Task<RecognizeOutput> RecognizeAsync(SchemaContext context, string input)
    {
        if (Status != SchemaNodeStatus.Ready || SourceSchemaType == null)
            return RecognizeOutput.Fail(0);

        // No Parts: entire input is the value (scalar/enum only)
        if (Parts.Length == 0)
        {
            if (SourceSchemaType is ScalarType or EnumType)
            {
                var node = await ParseSelfAsync(context, input, null);
                if (node == null) return RecognizeOutput.Fail(0);
                return new RecognizeOutput { Success = true, Position = input.Length, Value = node };
            }
            return RecognizeOutput.Fail(0);
        }

        // Find first prefix-mode part
        int prefixStart = Array.FindIndex(Parts, p => p.Type != RecognizerPartType.Literal && !string.IsNullOrEmpty(p.Prefix));

        int pos = 0;
        AnySchemaNode? result = null;
        Dictionary<string, AnySchemaNode?>? fields = null;

        // Sequential match for parts before prefix mode
        int sequentialEnd = prefixStart >= 0 ? prefixStart : Parts.Length;
        for (int i = 0; i < sequentialEnd; i++)
        {
            var part = Parts[i];
            switch (part.Type)
            {
                case RecognizerPartType.Literal:
                    if (part.Text == null) break;
                    if (pos + part.Text.Length > input.Length) return RecognizeOutput.Fail(pos);
                    if (!input.AsSpan(pos, part.Text.Length).Equals(part.Text.AsSpan(), StringComparison.OrdinalIgnoreCase))
                        return RecognizeOutput.Fail(pos);
                    pos += part.Text.Length;
                    break;

                case RecognizerPartType.Self:
                {
                    string selfValue = ExtractSegment(input, pos, i, out pos);
                    result = await ParseSelfAsync(context, selfValue, part);
                    if (result == null) return RecognizeOutput.Fail(0);
                    break;
                }

                case RecognizerPartType.Field:
                {
                    if (string.IsNullOrWhiteSpace(part.Field)) return RecognizeOutput.Fail(pos);
                    bool isArrayField = IsArrayField(part.Field);
                    string fieldValue = ExtractSegment(input, pos, i, out pos, !isArrayField);
                    fields ??= new(StringComparer.OrdinalIgnoreCase);
                    var fieldNode = await ParseFieldAsync(context, part, fieldValue, fields);
                    fields[part.Field] = fieldNode;
                    break;
                }

                case RecognizerPartType.Elements:
                {
                    string elemValue = ExtractSegment(input, pos, i, out pos, usePartSuffix: false);
                    result = await ParseElementsAsync(context, part, elemValue);
                    if (result == null) return RecognizeOutput.Fail(0);
                    break;
                }
            }
        }

        // Unordered prefix-mode match
        if (prefixStart >= 0)
        {
            // Separate parts into those with RecognizerRelation (deferred) and independent
            var independentParts = new List<int>();
            var deferredParts = new List<int>();
            for (int i = prefixStart; i < Parts.Length; i++)
            {
                var p = Parts[i];
                if (p.Type == RecognizerPartType.Literal) continue;
                bool hasRelation = p.Field != null && RelationsByPart?.ContainsKey(p.Field) == true;
                if (hasRelation)
                    deferredParts.Add(i);
                else
                    independentParts.Add(i);
            }

            // First pass: independent parts (no RecognizerRelation)
            foreach (int pi in independentParts)
            {
                var part = Parts[pi];
                if (string.IsNullOrEmpty(part.Prefix)) return RecognizeOutput.Fail(pos);

                int searchFrom = pos;
                while (true)
                {
                    int prefixIdx = input.IndexOf(part.Prefix, searchFrom, StringComparison.OrdinalIgnoreCase);
                    if (prefixIdx < 0) break;

                    int valueStart = prefixIdx + part.Prefix.Length;
                    int valueEnd = FindPrefixValueEnd(input, valueStart, part.Suffix, pi);

                    int nextSamePrefixIdx = input.IndexOf(part.Prefix, valueStart, StringComparison.OrdinalIgnoreCase);
                    if (nextSamePrefixIdx >= 0 && nextSamePrefixIdx < valueEnd)
                        valueEnd = nextSamePrefixIdx;

                    string segValue = input[valueStart..valueEnd];

                    if (!string.IsNullOrEmpty(part.Suffix) &&
                        valueEnd + part.Suffix.Length <= input.Length &&
                        input.AsSpan(valueEnd, part.Suffix.Length).Equals(part.Suffix.AsSpan(), StringComparison.OrdinalIgnoreCase))
                        searchFrom = valueEnd + part.Suffix.Length;
                    else
                        searchFrom = valueEnd;

                    switch (part.Type)
                    {
                        case RecognizerPartType.Self:
                            result = await ParseSelfAsync(context, segValue, part);
                            break;
                        case RecognizerPartType.Field when !string.IsNullOrWhiteSpace(part.Field):
                        {
                            fields ??= new(StringComparer.OrdinalIgnoreCase);
                            var fieldNode = await ParseFieldAsync(context, part, segValue, fields);
                            if (fields.TryGetValue(part.Field, out var existing) && existing is ArrayTypeNode existingArr)
                            {
                                if (fieldNode is ArrayTypeNode newArr) existingArr.AddRange(newArr);
                                else if (fieldNode != null) existingArr.Add(fieldNode);
                            }
                            else
                            {
                                fields[part.Field] = fieldNode;
                            }
                            break;
                        }
                        case RecognizerPartType.Elements:
                        {
                            if (result is ArrayTypeNode existingElems)
                            {
                                var newElems = await ParseElementsAsync(context, part, segValue);
                                if (newElems is ArrayTypeNode newElemsArr) existingElems.AddRange(newElemsArr);
                            }
                            else
                            {
                                result = await ParseElementsAsync(context, part, segValue);
                            }
                            break;
                        }
                    }
                }
            }

            // Second pass: deferred parts with RecognizerRelation
            foreach (int pi in deferredParts)
            {
                var part = Parts[pi];
                if (string.IsNullOrEmpty(part.Prefix) || string.IsNullOrWhiteSpace(part.Field)) continue;

                var relations = RelationsByPart![part.Field];
                var dynamicRecognizer = await ResolveRelationRecognizerAsync(context, relations, fields);

                int searchFrom = pos;
                while (true)
                {
                    int prefixIdx = input.IndexOf(part.Prefix, searchFrom, StringComparison.OrdinalIgnoreCase);
                    if (prefixIdx < 0) break;

                    int valueStart = prefixIdx + part.Prefix.Length;
                    int valueEnd = FindPrefixValueEnd(input, valueStart, part.Suffix, pi);

                    int nextSamePrefixIdx = input.IndexOf(part.Prefix, valueStart, StringComparison.OrdinalIgnoreCase);
                    if (nextSamePrefixIdx >= 0 && nextSamePrefixIdx < valueEnd)
                        valueEnd = nextSamePrefixIdx;

                    string segValue = input[valueStart..valueEnd];

                    if (!string.IsNullOrEmpty(part.Suffix) &&
                        valueEnd + part.Suffix.Length <= input.Length &&
                        input.AsSpan(valueEnd, part.Suffix.Length).Equals(part.Suffix.AsSpan(), StringComparison.OrdinalIgnoreCase))
                        searchFrom = valueEnd + part.Suffix.Length;
                    else
                        searchFrom = valueEnd;

                    fields ??= new(StringComparer.OrdinalIgnoreCase);
                    AnySchemaNode? fieldNode;
                    if (dynamicRecognizer != null)
                    {
                        var subResult = await dynamicRecognizer.RecognizeAsync(context, segValue);
                        fieldNode = subResult.Success ? subResult.Value : null;
                    }
                    else
                    {
                        fieldNode = await ParseFieldAsync(context, part, segValue, fields);
                    }
                    if (fields.TryGetValue(part.Field, out var existing) && existing is ArrayTypeNode existingArr)
                    {
                        if (fieldNode is ArrayTypeNode newArr) existingArr.AddRange(newArr);
                        else if (fieldNode != null) existingArr.Add(fieldNode);
                    }
                    else
                    {
                        fields[part.Field] = fieldNode;
                    }
                }
            }
            pos = input.Length;
        }

        if (pos != input.Length) return RecognizeOutput.Fail(pos);

        // Build result for struct source type
        if (fields != null && SourceSchemaType is StructType structType)
        {
            var node = new StructTypeNode(structType);
            foreach (var field in structType.Fields)
            {
                if (fields.TryGetValue(field.Name, out var val) && val != null)
                    node.SetField(field.Name, val);
            }
            return new RecognizeOutput { Success = true, Position = pos, Value = node };
        }

        if (result != null)
            return new RecognizeOutput { Success = true, Position = pos, Value = result };

        return RecognizeOutput.Fail(pos);
    }

    /// <summary>
    /// Returns true if the named field on SourceSchemaType has an ArrayType.
    /// </summary>
    private bool IsArrayField(string fieldName)
        => (SourceSchemaType as StructType)?.GetField(fieldName)?.SchemaType is ArrayType;

    /// <summary>
    /// Resolve a RecognizerRelation to a RecognizerType by finding the recognizer-specific relation
    /// from a list of relations for a part and calling its function with the currently parsed fields.
    /// </summary>
    private static async Task<RecognizerType?> ResolveRelationRecognizerAsync(
        SchemaContext context, List<RecognizerRelationSchema> relations, Dictionary<string, AnySchemaNode?>? fields)
    {
        var relation = relations.FirstOrDefault(r => r.Prop.Equals("recognizer", StringComparison.OrdinalIgnoreCase));
        if (relation == null) return null;
        var result = await ResolveRelationNodeAsync(context, relation, fields);
        if (result?.ToString() is not { Length: > 0 } recognizerName) return null;
        return await context.GetSchemaTypeAsync<RecognizerType>(recognizerName) is { Status: SchemaNodeStatus.Ready } rt ? rt : null;
    }

    /// <summary>
    /// Resolve a relation function call to get an override value as AnySchemaNode, using already-parsed field values as arguments.
    /// </summary>
    private static async Task<AnySchemaNode?> ResolveRelationNodeAsync(
        SchemaContext context, RecognizerRelationSchema relation, Dictionary<string, AnySchemaNode?>? fields)
    {
        if (relation.FuncNode == null) return null;

        // Build function arguments from parsed fields
        var args = new object?[relation.Args.Length];
        for (int a = 0; a < relation.Args.Length; a++)
        {
            var arg = relation.Args[a];
            if (!string.IsNullOrEmpty(arg.Name))
                args[a] = fields != null && fields.TryGetValue(arg.Name, out var v) ? v : null;
            else
                args[a] = arg.Value;
        }

        return await relation.FuncNode.CallAsync<AnySchemaNode>(context, args);
    }

    /// <summary>
    /// Resolve converter property overrides from relations for a given part, returning a dictionary of property name → override value.
    /// </summary>
    private async Task<Dictionary<string, AnySchemaNode?>?> ResolvePropertyOverridesAsync(
        SchemaContext context, RecognizerPartSchema part, Dictionary<string, AnySchemaNode?>? fields)
    {
        if (string.IsNullOrWhiteSpace(part.Field) ||
            RelationsByPart?.TryGetValue(part.Field, out var relations) != true)
            return null;

        Dictionary<string, AnySchemaNode?>? overrides = null;
        foreach (var relation in relations)
        {
            if (relation.Prop.Equals("recognizer", StringComparison.OrdinalIgnoreCase)) continue;

            var overrideVal = await ResolveRelationNodeAsync(context, relation, fields);
            if (overrideVal != null)
            {
                overrides ??= new(StringComparer.OrdinalIgnoreCase);
                overrides[relation.Prop] = overrideVal;
            }
        }
        return overrides;
    }

    /// <summary>
    /// Extract a value segment from input starting at pos, bounded by the current part's suffix
    /// (which is consumed) or the next literal/prefix part.
    /// For array fields (usePartSuffix=false) the suffix is NOT used as a boundary since it acts
    /// as an element delimiter; the array extent is determined by the next literal instead.
    /// </summary>
    private string ExtractSegment(string input, int pos, int currentPartIndex, out int endPos, bool usePartSuffix = true)
    {
        var currentPart = Parts[currentPartIndex];

        if (usePartSuffix && !string.IsNullOrEmpty(currentPart.Suffix))
        {
            int idx = input.IndexOf(currentPart.Suffix, pos, StringComparison.Ordinal);
            if (idx >= 0)
            {
                endPos = idx + currentPart.Suffix.Length;
                return input[pos..idx];
            }
        }

        for (int j = currentPartIndex + 1; j < Parts.Length; j++)
        {
            var nextPart = Parts[j];
            if (nextPart.Type == RecognizerPartType.Literal && nextPart.Text is { } boundary)
            {
                int idx = input.IndexOf(boundary, pos, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    endPos = idx;
                    return input[pos..idx];
                }
                break;
            }
            else if (nextPart.Type != RecognizerPartType.Literal && !string.IsNullOrEmpty(nextPart.Prefix))
            {
                int idx = input.IndexOf(nextPart.Prefix, pos, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    endPos = idx;
                    return input[pos..idx];
                }
                break;
            }
        }
        endPos = input.Length;
        return input[pos..];
    }

    /// <summary>
    /// Find the end position of a value in prefix-mode, bounded by the part's suffix or the
    /// nearest prefix of any sibling part.
    /// </summary>
    private int FindPrefixValueEnd(string input, int valueStart, string? suffix, int currentPartIndex)
    {
        if (!string.IsNullOrEmpty(suffix))
        {
            int idx = input.IndexOf(suffix, valueStart, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) return idx;
        }

        int end = input.Length;
        for (int j = 0; j < Parts.Length; j++)
        {
            if (j == currentPartIndex) continue;
            var other = Parts[j];
            if (other.Type == RecognizerPartType.Literal || string.IsNullOrEmpty(other.Prefix)) continue;
            int idx = input.IndexOf(other.Prefix, valueStart, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && idx < end) end = idx;
        }
        return end;
    }

    /// <summary>
    /// Apply the phase-based parse pipeline to a segment string, yielding a final AnySchemaNode
    /// or a processed string to be converted to a node by the caller.
    /// Phase 1: Prefix parse. Phase 2: Suffix parse. Phase 3: Content converts (reversed).
    /// </summary>
    private static async Task<(AnySchemaNode? node, string? str)> ApplyParsePhases(
        SchemaContext context, RecognizerPartSchema part, string segment)
    {
        string current = segment;

        // Phase 1: Prefix parse (strip leading prefix)
        if (part.PrefixProperty is IConvertProperty prefixConvert)
        {
            string? stripped = await prefixConvert.ParseAsync(context, current, current);
            if (stripped == null) return (null, null);
            current = stripped;
        }

        // Phase 2: Suffix parse (strip trailing suffix — no-op when suffix was already consumed by ExtractSegment)
        if (part.SuffixProperty is IConvertProperty suffixConvert)
        {
            string? stripped = await suffixConvert.ParseAsync(context, current, current);
            if (stripped == null) return (null, null);
            current = stripped;
        }

        // Phase 3: Content converts in reverse order
        var converts = part.ConvertProperties;
        if (converts is { Length: > 0 })
        {
            string original = current;
            for (int i = converts.Length - 1; i >= 0; i--)
            {
                var convert = converts[i];
                var node = await convert.ParseNodeAsync(context, current);
                if (node != null) return (node, null);
                string? parsed = await convert.ParseAsync(context, original, current);
                if (parsed == null) return (null, null);
                current = parsed;
            }
        }

        return (null, current);
    }

    /// <summary>
    /// Parse a Self part value using the phased pipeline, then create the final node.
    /// </summary>
    private async Task<AnySchemaNode?> ParseSelfAsync(SchemaContext context, string value, RecognizerPartSchema? part)
    {
        string current = value;
        if (part != null)
        {
            var (node, str) = await ApplyParsePhases(context, part, value);
            if (node != null) return node;
            if (str == null) return null;
            current = str;
        }

        if (SourceSchemaType is ScalarType scalar)
            return ParseScalarValue(scalar, current);

        if (SourceSchemaType is EnumType enumType)
        {
            var info = await enumType.LoadEnumValueInfo(context, current);
            if (info == null) return null;
            return new EnumTypeNode(enumType, info.Value);
        }

        return null;
    }

    /// <summary>
    /// Parse a struct field value using the phased pipeline.
    /// If the field is an array type and the recognizer targets elements, splits by part suffix.
    /// For sequential relations: a dynamic recognizer can be passed via the fields context.
    /// </summary>
    private async Task<AnySchemaNode?> ParseFieldAsync(
        SchemaContext context, RecognizerPartSchema part, string segment,
        Dictionary<string, AnySchemaNode?>? fields, RecognizerType? overrideRecognizer = null)
    {
        if (string.IsNullOrWhiteSpace(part.Field)) return null;

        var fieldSchema = (SourceSchemaType as StructType)?.GetField(part.Field);
        if (fieldSchema?.SchemaType == null) return null;
        var fieldType = fieldSchema.SchemaType;

        // Check for RecognizerRelation override for sequential mode
        if (overrideRecognizer == null && RelationsByPart?.TryGetValue(part.Field, out var rels) == true)
            overrideRecognizer = await ResolveRelationRecognizerAsync(context, rels, fields);

        // Dynamic recognizer override: bypass the part's convert pipeline
        if (overrideRecognizer != null)
        {
            if (fieldType is ArrayType arrType2 && overrideRecognizer.SourceSchemaType?.CanBeUseAs(arrType2) == true)
            {
                var r = await overrideRecognizer.RecognizeAsync(context, segment);
                return r.Success ? r.Value : null;
            }
            if (fieldType is ArrayType arrType3)
                return await ParseArrayFieldAsync(context, arrType3, overrideRecognizer, part, segment);

            var res = await overrideRecognizer.RecognizeAsync(context, segment);
            return res.Success ? res.Value : null;
        }

        // Array field: check for RecognizerProperty
        if (fieldType is ArrayType arrayType)
        {
            var recConv = part.RecognizerProperty;
            /*if (recConv != null && recConv.TargetsArray)
            {
                var r = await recConv.RecognizeAsync(context, segment);
                return r.Success ? r.Value : null;
            }
            return await ParseArrayFieldAsync(context, arrayType, recConv?.Recognizer, part, segment);*/
        }

        // Non-array: phased pipeline
        var (phasedNode, phasedStr) = await ApplyParsePhases(context, part, segment);
        if (phasedNode != null) return phasedNode;
        if (phasedStr == null) return null;
        return fieldType.CreateNode(phasedStr);
    }

    /// <summary>
    /// Parse an array-typed struct field by splitting on the part's suffix and recognizing each element.
    /// </summary>
    private async Task<AnySchemaNode?> ParseArrayFieldAsync(
        SchemaContext context, ArrayType arrayType, RecognizerType? elementRecognizer,
        RecognizerPartSchema part, string segment)
    {
        string suffix = part.Suffix ?? "";
        var arr = new ArrayTypeNode(arrayType);
        var segments = SplitByDelimiter(segment, suffix);

        foreach (var elemSeg in segments)
        {
            if (string.IsNullOrEmpty(elemSeg)) continue;

            if (elementRecognizer != null)
            {
                var elemResult = await elementRecognizer.RecognizeAsync(context, elemSeg);
                if (!elemResult.Success) return null;
                if (elemResult.Value != null) arr.Add(elemResult.Value);
            }
            else
            {
                var elemNode = arrayType.ElementSchemaType?.CreateNode(elemSeg);
                if (elemNode != null) arr.Add(elemNode);
                else arr.Add(elemSeg);
            }
        }
        return arr;
    }

    /// <summary>
    /// Parse array elements by splitting on the part's suffix and processing each segment.
    /// </summary>
    private async Task<AnySchemaNode?> ParseElementsAsync(SchemaContext context, RecognizerPartSchema part, string value)
    {
        if (SourceSchemaType is not ArrayType arrayType) return null;

        string suffix = part.Suffix ?? "";
        var segments = SplitByDelimiter(value, suffix);
        var arr = new ArrayTypeNode(arrayType);

        var recConv = part.ConvertProperties?.OfType<RecognizerProperty>().FirstOrDefault();

        foreach (var segment in segments)
        {
            if (string.IsNullOrEmpty(segment)) continue;

            if (recConv != null)
            {
                /*var elemResult = await recConv.Recognizer.RecognizeAsync(context, segment);
                if (!elemResult.Success) return null;
                if (elemResult.Value != null) arr.Add(elemResult.Value);*/
            }
            else if (arrayType.ElementSchemaType is ScalarType elemScalar)
            {
                arr.Add(elemScalar.CreateNode(segment) ?? new ScalarTypeNode(elemScalar, segment));
            }
            else
            {
                arr.Add(segment);
            }
        }
        return arr;
    }

    private static AnySchemaNode? ParseScalarValue(ScalarType scalar, string parsed)
    {
        object? value;
        if (scalar.IsBool)
        {
            if (!bool.TryParse(parsed, out var b)) return null;
            value = b;
        }
        else if (scalar.IsInt)
        {
            if (!long.TryParse(parsed, out var n)) return null;
            value = n;
        }
        else if (scalar.IsNumber || scalar.IsDouble || scalar.IsSingle)
        {
            if (!decimal.TryParse(parsed, CultureInfo.InvariantCulture, out var d)) return null;
            value = d;
        }
        else
        {
            value = parsed;
        }
        return new ScalarTypeNode(scalar, value);
    }

    private static IEnumerable<string> SplitByDelimiter(string input, string delimiter)
    {
        if (string.IsNullOrEmpty(delimiter))
        {
            yield return input;
            yield break;
        }
        int pos = 0;
        while (pos <= input.Length)
        {
            int idx = input.IndexOf(delimiter, pos, StringComparison.Ordinal);
            if (idx < 0)
            {
                yield return input[pos..];
                break;
            }
            yield return input[pos..idx];
            pos = idx + delimiter.Length;
        }
    }

    #endregion

    #region Emit

    /// <summary>
    /// Generate a string from a structured value by iterating Parts directly.
    /// Emit order per part: Phase 1 Content converters → Phase 2 Prefix → Phase 3 Suffix.
    /// </summary>
    public async Task<string?> EmitAsync(SchemaContext context, AnySchemaNode value)
    {
        if (Status != SchemaNodeStatus.Ready || SourceSchemaType == null)
            return null;

        if (Parts.Length == 0)
            return SourceSchemaType is ScalarType or EnumType ? value.ToString() : null;

        var sb = new StringBuilder();

        for (int i = 0; i < Parts.Length; i++)
        {
            var part = Parts[i];
            switch (part.Type)
            {
                case RecognizerPartType.Literal:
                    sb.Append(part.Text);
                    break;

                case RecognizerPartType.Self:
                    if (!await EmitSelfAsync(context, value, sb, part))
                        return null;
                    break;

                case RecognizerPartType.Field:
                    if (!await EmitFieldAsync(context, value, sb, part))
                        return null;
                    break;

                case RecognizerPartType.Elements:
                    if (!await EmitElementsAsync(context, value, sb, part))
                        return null;
                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Apply the phased emit pipeline to a node/string for a given part:
    /// Phase 1: run content converts (node→string first, then string pipeline);
    /// Phase 2: apply Prefix emit;
    /// Phase 3: apply Suffix emit.
    /// Returns the final content string or null on failure.
    /// </summary>
    private static async Task<string?> ApplyEmitPhases(SchemaContext context, RecognizerPartSchema part, AnySchemaNode value)
    {
        string raw = value.ToString();
        string? pipelineResult = null;

        var converts = part.ConvertProperties;

        // Phase 1a: node-to-string (first converter that can produce a string from the node wins)
        if (converts is { Length: > 0 })
        {
            foreach (var convert in converts)
            {
                string? nodeResult = await convert.EmitNodeAsync(context, value);
                if (nodeResult != null) { pipelineResult = nodeResult; break; }
            }
        }

        // Phase 1b: string pipeline (forward; transforms the result from 1a or value.ToString())
        if (converts is { Length: > 0 })
        {
            foreach (var convert in converts)
            {
                pipelineResult = await convert.EmitAsync(context, raw, pipelineResult);
                if (pipelineResult == null) return null;
            }
        }

        string content = pipelineResult ?? raw;

        // Phase 2: Prefix emit
        if (part.PrefixProperty is IConvertProperty prefixConvert)
        {
            string? withPrefix = await prefixConvert.EmitAsync(context, raw, content);
            if (withPrefix != null) content = withPrefix;
        }

        // Phase 3: Suffix emit
        if (part.SuffixProperty is IConvertProperty suffixConvert)
        {
            string? withSuffix = await suffixConvert.EmitAsync(context, raw, content);
            if (withSuffix != null) content = withSuffix;
        }

        return content;
    }

    /// <summary>
    /// Emit a Self part using the phased pipeline.
    /// </summary>
    private static async Task<bool> EmitSelfAsync(SchemaContext context, AnySchemaNode value, StringBuilder sb, RecognizerPartSchema part)
    {
        string? content = await ApplyEmitPhases(context, part, value);
        if (content == null) return false;
        sb.Append(content);
        return true;
    }

    /// <summary>
    /// Emit a struct field value using the phased pipeline, with array-element support.
    /// </summary>
    private async Task<bool> EmitFieldAsync(SchemaContext context, AnySchemaNode value, StringBuilder sb, RecognizerPartSchema part)
    {
        if (value is not StructTypeNode obj || string.IsNullOrWhiteSpace(part.Field)) return false;
        var fieldNode = obj.GetField(part.Field);

        // Optional field (prefix-mode): missing is allowed
        if (fieldNode == null) return !string.IsNullOrEmpty(part.Prefix);

        // Build fields dictionary for relation resolution
        Dictionary<string, AnySchemaNode?>? fieldsDict = null;
        if (RelationsByPart?.ContainsKey(part.Field) == true)
        {
            fieldsDict = new Dictionary<string, AnySchemaNode?>(StringComparer.OrdinalIgnoreCase);
            if (SourceSchemaType is StructType structType2)
                foreach (var f in structType2.Fields)
                    fieldsDict[f.Name] = obj.GetField(f.Name);
        }

        // Check for RecognizerRelation override
        RecognizerType? dynamicRec = null;
        if (fieldsDict != null && RelationsByPart?.TryGetValue(part.Field, out var rels) == true)
            dynamicRec = await ResolveRelationRecognizerAsync(context, rels, fieldsDict);

        string raw;

        if (fieldNode is ArrayTypeNode arrNode)
        {
            var fieldArrayType = (SourceSchemaType as StructType)?.GetField(part.Field)?.SchemaType as ArrayType;
            if (fieldArrayType == null) return false;

            var recConv = dynamicRec == null
                ? part.RecognizerProperty
                : null;

            // Determine which recognizer to use: dynamic override takes priority
            RecognizerType? elementRec = dynamicRec;
            bool targetsArray = false;

            if (elementRec == null && recConv != null)
            {
                //elementRec = recConv.Recognizer;
                //targetsArray = recConv.TargetsArray;
            }
            else if (elementRec != null)
            {
                targetsArray = elementRec.SourceSchemaType?.CanBeUseAs(fieldArrayType) == true;
            }

            if (elementRec != null && targetsArray)
            {
                var emitted = await elementRec.EmitAsync(context, fieldNode);
                if (emitted == null) return !string.IsNullOrEmpty(part.Prefix);
                raw = emitted;
            }
            else
            {
                string suffix = part.Suffix ?? "";
                var inner = new StringBuilder();
                for (int ei = 0; ei < arrNode.Count; ei++)
                {
                    var elem = arrNode[ei];
                    if (elem == null) continue;
                    if (elementRec != null && elem is AnySchemaNode elemNode)
                    {
                        var emitted = await elementRec.EmitAsync(context, elemNode);
                        if (emitted == null) return !string.IsNullOrEmpty(part.Prefix);
                        inner.Append(emitted);
                    }
                    else
                    {
                        inner.Append(elem.ToString());
                    }
                    if (!string.IsNullOrEmpty(suffix)) inner.Append(suffix);
                }
                // Remove trailing suffix (container removes last element's suffix)
                raw = inner.ToString();
                if (!string.IsNullOrEmpty(suffix) && raw.EndsWith(suffix, StringComparison.Ordinal))
                    raw = raw[..^suffix.Length];
            }
        }
        else if (dynamicRec != null)
        {
            var emitted = await dynamicRec.EmitAsync(context, fieldNode);
            if (emitted == null) return !string.IsNullOrEmpty(part.Prefix);
            raw = emitted;
        }
        else
        {
            // Use the phased pipeline
            string? content = await ApplyEmitPhases(context, part, fieldNode);
            if (content == null) return !string.IsNullOrEmpty(part.Prefix);
            sb.Append(content);
            return true;
        }

        // Apply non-recognizer string converts and prefix/suffix phases to the raw array string
        var stringConverts = part.ConvertProperties?
            .Where(c => c is not RecognizerProperty)
            .ToArray();
        string? pipelineResult = raw;
        if (stringConverts is { Length: > 0 })
        {
            foreach (var convert in stringConverts)
            {
                pipelineResult = await convert.EmitAsync(context, raw, pipelineResult);
                if (pipelineResult == null) return !string.IsNullOrEmpty(part.Prefix);
            }
        }

        string fieldContent = pipelineResult ?? raw;

        if (part.PrefixProperty is IConvertProperty prefixConvert)
        {
            string? withPrefix = await prefixConvert.EmitAsync(context, raw, fieldContent);
            if (withPrefix != null) fieldContent = withPrefix;
        }
        // NOTE: suffix phase skipped for array fields — suffix already served as element delimiter above

        sb.Append(fieldContent);
        return true;
    }

    /// <summary>
    /// Emit array elements with the part's suffix as element delimiter.
    /// The last element's suffix is removed (container responsibility).
    /// The suffix phase is NOT re-applied to the combined string (it acts as element delimiter only).
    /// </summary>
    private async Task<bool> EmitElementsAsync(SchemaContext context, AnySchemaNode value, StringBuilder sb, RecognizerPartSchema part)
    {
        if (value is not ArrayTypeNode arr) return !string.IsNullOrEmpty(part.Prefix);

        string suffix = part.Suffix ?? "";
        var recConv = part.RecognizerProperty;
        var inner = new StringBuilder();

        for (int i = 0; i < arr.Count; i++)
        {
            var elem = arr[i];
            if (elem == null) continue;

            if (recConv != null && elem is AnySchemaNode elemNode)
            {
                //var emitted = await recConv.Recognizer.EmitAsync(context, elemNode);
                //if (emitted == null) return false;
                //inner.Append(emitted);
            }
            else
            {
                inner.Append(elem.ToString());
            }

            if (!string.IsNullOrEmpty(suffix)) inner.Append(suffix);
        }

        // Remove trailing suffix from last element (container responsibility)
        string raw = inner.ToString();
        if (!string.IsNullOrEmpty(suffix) && raw.EndsWith(suffix, StringComparison.Ordinal))
            raw = raw[..^suffix.Length];

        // Apply non-recognizer string converts
        var stringConverts = part.ConvertProperties?
            .Where(c => c is not RecognizerProperty)
            .ToArray();
        string? pipelineResult = raw;
        if (stringConverts is { Length: > 0 })
        {
            foreach (var convert in stringConverts)
            {
                pipelineResult = await convert.EmitAsync(context, raw, pipelineResult);
                if (pipelineResult == null) return !string.IsNullOrEmpty(part.Prefix);
            }
        }

        string elemContent = pipelineResult ?? raw;

        // Apply prefix phase (structural navigation marker)
        if (part.PrefixProperty is IConvertProperty prefixConvert)
        {
            string? withPrefix = await prefixConvert.EmitAsync(context, raw, elemContent);
            if (withPrefix != null) elemContent = withPrefix;
        }
        // NOTE: suffix phase is intentionally skipped here — suffix already served as element delimiter above

        sb.Append(elemContent);
        return true;
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
            Relations = schema.Relations,
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
