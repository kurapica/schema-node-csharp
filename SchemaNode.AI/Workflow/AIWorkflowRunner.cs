using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SchemaNode.AI;
using SchemaNode.AI.Agents;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Relation;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
using ValueType = SchemaNode.Runtime.ValueType;

namespace SchemaNode.AI.Workflow;

/// <summary>
/// One model attempt, kept for diagnostics and tests.
/// </summary>
public sealed class AIWorkflowAttempt
{
    /// <summary>The 1-based attempt number.</summary>
    public required int Number { get; init; }

    /// <summary>The raw text returned by the provider.</summary>
    public required string RawOutput { get; init; }

    /// <summary>The validation or parsing error, null when the attempt was valid.</summary>
    public string? Error { get; init; }

    /// <summary>Whether this attempt produced a valid value.</summary>
    public bool Valid { get; init; }
}

/// <summary>
/// The result of one generate/validate/retry run.
/// </summary>
public sealed class AIWorkflowRunResult
{
    /// <summary>The validated value, null on failure.</summary>
    public IValueAccess? Value { get; init; }

    /// <summary>All attempts in order.</summary>
    public IReadOnlyList<AIWorkflowAttempt> Attempts { get; init; } = [];

    /// <summary>The last error. On validation exhaustion this is the real error of the last attempt.</summary>
    public string? Error { get; init; }

    /// <summary>Whether a valid value was produced.</summary>
    public bool Succeeded => Value is not null;

    /// <summary>The number of model calls.</summary>
    public int CallCount => Attempts.Count;
}

/// <summary>
/// The input for <see cref="AIWorkflowRunner.RunAsync"/>.
/// </summary>
public sealed class AIWorkflowRunRequest
{
    /// <summary>The target type the generated JSON must satisfy.</summary>
    public required ValueType TargetType { get; init; }

    /// <summary>The provider that generates the JSON.</summary>
    public required IAgentProvider Provider { get; init; }

    /// <summary>Additional generation attempts after the first validation failure.</summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>Optional system instruction.</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>Optional task instruction.</summary>
    public string? Instruction { get; init; }

    /// <summary>Prompt blocks already resolved from the configured prompt sources.</summary>
    public IReadOnlyList<string> PromptBlocks { get; init; } = [];

    /// <summary>Maximum completion tokens.</summary>
    public int MaxTokens { get; init; } = 512;

    /// <summary>Sampling temperature.</summary>
    public double? Temperature { get; init; } = 0.2;

    /// <summary>Maximum target schema description characters.</summary>
    public int MaxMetadataChars { get; init; } = 6000;
}

/// <summary>
/// Runs the generate → validate → retry-with-error loop. The loop is independent from the
/// workflow context, so it can be unit tested with a mock provider and reused by smoke tests.
/// </summary>
public static class AIWorkflowRunner
{
    /// <summary>
    /// Generates a value, validates it against <see cref="AIWorkflowRunRequest.TargetType"/> and
    /// feeds the concrete validation error back to the model until it succeeds or the retry budget
    /// is exhausted. Provider/transport failures are not retried; they escape as
    /// <see cref="AIAgentException"/> so the workflow can report an unavailable server immediately.
    /// </summary>
    public static async Task<AIWorkflowRunResult> RunAsync(
        SchemaContext context,
        AIWorkflowRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        int maxRetries = Math.Clamp(request.MaxRetries, 0, 10);
        string metadata = await BuildMetadataAsync(context, request.TargetType, request.MaxMetadataChars, cancellationToken)
            .ConfigureAwait(false);
        string systemPrompt = BuildSystemPrompt(request, metadata);
        string userPrompt = BuildUserPrompt(request, null, null);

        List<AIWorkflowAttempt> attempts = [];
        string? lastError = null;
        string? lastOutput = null;

        for (int index = 0; index <= maxRetries; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string prompt = index == 0 ? userPrompt : BuildUserPrompt(request, lastError, lastOutput);
            string raw = await request.Provider.CompleteAsync(
                new AIAgentRequest
                {
                    SystemPrompt = systemPrompt,
                    UserPrompt = prompt,
                    Temperature = request.Temperature,
                    MaxTokens = request.MaxTokens,
                },
                cancellationToken).ConfigureAwait(false);

            lastOutput = raw;

            JsonNode? value = TryExtractJson(raw, out string parseError);
            if (value is null)
            {
                lastError = $"The response was not valid JSON: {parseError}";
                attempts.Add(new AIWorkflowAttempt
                {
                    Number = index + 1,
                    RawOutput = raw,
                    Error = lastError,
                    Valid = false,
                });
                continue;
            }

            IValueAccess? node;
            try
            {
                node = await request.TargetType.ValidateValueAsync(context, value).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = $"The JSON could not be converted to '{request.TargetType.Name}': {ErrorMessage(ex)}";
                attempts.Add(new AIWorkflowAttempt
                {
                    Number = index + 1,
                    RawOutput = raw,
                    Error = lastError,
                    Valid = false,
                });
                continue;
            }

            if (node is { IsValid: true })
            {
                attempts.Add(new AIWorkflowAttempt
                {
                    Number = index + 1,
                    RawOutput = raw,
                    Error = null,
                    Valid = true,
                });
                return new AIWorkflowRunResult { Value = node, Attempts = attempts };
            }

            lastError = BuildValidationError(node, request.TargetType, value);
            attempts.Add(new AIWorkflowAttempt
            {
                Number = index + 1,
                RawOutput = raw,
                Error = lastError,
                Valid = false,
            });
        }

        return new AIWorkflowRunResult { Attempts = attempts, Error = lastError };
    }

    private static string BuildSystemPrompt(AIWorkflowRunRequest request, string metadata)
    {
        StringBuilder sb = new();
        sb.AppendLine("You generate JSON data for a schema-driven workflow.");
        sb.AppendLine("Return exactly one JSON value. Do not add prose, comments or Markdown code fences.");
        sb.AppendLine($"Target schema type: {request.TargetType.Name}");
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            sb.AppendLine();
            sb.AppendLine("Additional system instruction:");
            sb.AppendLine(request.SystemPrompt.Trim());
        }

        sb.AppendLine();
        sb.AppendLine("Target schema description:");
        sb.AppendLine(metadata);
        return sb.ToString();
    }

    private static string BuildUserPrompt(AIWorkflowRunRequest request, string? retryError, string? previousOutput)
    {
        StringBuilder sb = new();
        foreach (string block in request.PromptBlocks)
        {
            if (string.IsNullOrWhiteSpace(block)) continue;
            sb.AppendLine(block.Trim());
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(request.Instruction))
        {
            sb.AppendLine(request.Instruction.Trim());
            sb.AppendLine();
        }

        if (retryError is not null)
        {
            sb.AppendLine("Your previous JSON was rejected: " + retryError);
            sb.AppendLine();
            sb.AppendLine("Previous answer:");
            sb.AppendLine(Truncate(previousOutput ?? string.Empty, 2000));
            sb.AppendLine();
            sb.AppendLine("Fix the JSON so it validates against the target schema. Return only the corrected JSON.");
        }
        else
        {
            sb.AppendLine("Return only the JSON value.");
        }

        return sb.ToString();
    }

    private static string BuildValidationError(IValueAccess? node, ValueType target, JsonNode value)
    {
        List<string> messages = [];
        if (node is DataNode data)
        {
            foreach (DataNode errorNode in data.GetErrorNodes())
            {
                if (string.IsNullOrWhiteSpace(errorNode.Error)) continue;
                string path = string.IsNullOrWhiteSpace(errorNode.Access) ? errorNode.Name ?? target.Name : errorNode.Access;
                messages.Add($"{path}: {errorNode.Error}");
            }
        }

        if (messages.Count == 0 && node?.Violated is { } violated)
            messages.Add("violated constraints: " + violated.ToJsonString());

        if (messages.Count == 0)
            messages.AddRange(DescribeMismatch(target, value));

        if (messages.Count == 0)
            messages.Add($"the JSON does not match the target schema '{target.Name}'. Expected {DescribeShape(target)}");

        return string.Join("; ", messages);
    }

    /// <summary>
    /// Produce concrete field-level errors when the core validator rejects the shape without
    /// exposing an error node (for example when a string is supplied for an integer field).
    /// </summary>
    private static IEnumerable<string> DescribeMismatch(ValueType target, JsonNode value)
    {
        if (target is not StructType st || value is not JsonObject obj)
        {
            if (target is StructType && value is not JsonObject)
                yield return $"expected an object for '{target.Name}' but received {KindName(value)}";
            yield break;
        }

        foreach (StructFieldType field in st.GetFields())
        {
            JsonNode? child = obj[field.Name];
            if (child is null)
            {
                if (field.Require == true)
                    yield return $"field '{field.Name}' is required";
                continue;
            }

            string? expected = field.Type?.Name;
            if (!IsCompatible(field.Type, child))
                yield return $"field '{field.Name}' expects {expected ?? "another type"} but received {KindName(child)}";
        }
    }

    private static bool IsCompatible(ValueType? type, JsonNode value)
    {
        if (type is null) return true;

        return type.Kind switch
        {
            NODE_KIND_STRING or NODE_KIND_DATE => value.GetValueKind() == JsonValueKind.String,
            NODE_KIND_INT or NODE_KIND_DECIMAL => value.GetValueKind() == JsonValueKind.Number,
            NODE_KIND_BOOL => value.GetValueKind() is JsonValueKind.True or JsonValueKind.False,
            NODE_KIND_STRUCT => value is JsonObject,
            NODE_KIND_ARRAY => value is JsonArray,
            NODE_KIND_ENUM => value.GetValueKind() is JsonValueKind.String or JsonValueKind.Number,
            _ => true,
        };
    }

    private static string KindName(JsonNode value)
        => value.GetValueKind().ToString().ToLowerInvariant();

    private static string DescribeShape(ValueType target)
    {
        if (target is StructType st)
        {
            string fields = string.Join(", ", st.GetFields().Select(field =>
                $"{field.Name}: {field.Type?.Name ?? "any"}{(field.Require == true ? " (required)" : string.Empty)}"));
            return fields.Length == 0 ? "an object" : $"an object with {{{fields}}}";
        }

        if (target is ArrayType array)
            return $"an array of {array.Element?.Name ?? "any"}";

        return $"a {target.Name} value";
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "...";

    private static string ErrorMessage(Exception exception)
    {
        Exception current = exception;
        while (current.InnerException is not null) current = current.InnerException;
        return current.Message;
    }

    /// <summary>
    /// Extracts a JSON value from a model response, tolerating Markdown fences and leading prose.
    /// </summary>
    internal static JsonNode? TryExtractJson(string raw, out string error)
    {
        error = string.Empty;
        string text = raw.Trim();
        if (text.Length == 0)
        {
            error = "the response was empty";
            return null;
        }

        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            int firstNewline = text.IndexOf('\n');
            if (firstNewline >= 0) text = text[(firstNewline + 1)..];
            int closing = text.LastIndexOf("```", StringComparison.Ordinal);
            if (closing >= 0) text = text[..closing];
            text = text.Trim();
        }

        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException ex)
        {
            error = ex.Message;
        }

        int start = text.IndexOfAny(['{', '[']);
        int end = text.LastIndexOfAny(['}', ']']);
        if (start >= 0 && end > start)
        {
            try
            {
                return JsonNode.Parse(text[start..(end + 1)]);
            }
            catch (JsonException ex)
            {
                error = ex.Message;
            }
        }

        return null;
    }

    /// <summary>
    /// Builds the target schema description: the existing ontology projection (struct fields,
    /// nested types and function signatures) plus a relation section describing field
    /// dependencies and computed relations.
    /// </summary>
    public static async Task<string> BuildMetadataAsync(
        SchemaContext context,
        ValueType target,
        int maxChars,
        CancellationToken cancellationToken = default)
    {
        string projection;
        try
        {
            projection = await context.RenderSchemaOntologyAsync(
                target.Name,
                OntologyTextTemplates.FormatSsp,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            projection = $"Schema type: {target.Name} (description unavailable: {ErrorMessage(ex)})";
        }

        if (string.IsNullOrWhiteSpace(projection))
            projection = $"Schema type: {target.Name}";

        StringBuilder relations = new();
        HashSet<object> visited = [];
        HashSet<string> functions = [];
        CollectRelations(target, visited, functions, relations);
        if (relations.Length > 0)
        {
            StringBuilder combined = new(projection.Trim());
            combined.AppendLine();
            combined.AppendLine();
            combined.AppendLine("Relations and function definitions:");
            combined.Append(relations);
            projection = combined.ToString();
        }

        projection = projection.Trim();
        if (maxChars > 0 && projection.Length > maxChars)
            projection = projection[..maxChars] + "\n... (schema description truncated)";
        return projection;
    }

    private static void CollectRelations(NodeType type, HashSet<object> visited, HashSet<string> functions, StringBuilder sb)
    {
        if (!visited.Add(type)) return;

        switch (type)
        {
            case StructType st:
                foreach (StructFieldType field in st.GetFields())
                {
                    if (field.Type is not null) CollectRelations(field.Type, visited, functions, sb);
                }

                foreach (RelationType relation in st.GetRelations())
                {
                    CallProcess? call = relation.Process as CallProcess;
                    string field = relation.Property?.Name ?? "(self)";
                    string args = call is null
                        ? string.Empty
                        : string.Join(", ", call.Args.Select(arg =>
                            string.IsNullOrWhiteSpace(arg.Source)
                                ? (arg.Value?.ToJsonString() ?? "null")
                                : arg.Source));
                    string function = call?.FuncType?.Name ?? call?.Func ?? relation.Target;
                    string dependency = call?.hasDepends == true ? "depends on sources" : "computed";
                    sb.AppendLine($"- {st.Name}.{field}: {function}({args}) [{dependency}]");
                    if (call?.FuncType is { } func) AppendFunction(func, functions, sb);
                }
                break;

            case ArrayType array when array.Element is not null:
                CollectRelations(array.Element, visited, functions, sb);
                break;

            case FunctionType function:
                AppendFunction(function, functions, sb);
                break;
        }
    }

    private static void AppendFunction(FunctionType function, HashSet<string> functions, StringBuilder sb)
    {
        if (!functions.Add(function.Name)) return;

        string args = string.Join(", ", function.Args.Select(arg =>
            $"{arg.Name}: {arg.Type}{(arg.Require ? " required" : string.Empty)}"));
        sb.AppendLine($"- function {function.Name}({args}) -> {function.Return?.Name ?? "void"}");
    }
}
