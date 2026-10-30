using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using SchemaNode.AI.Mcp;
using SchemaNode.Context;
using SchemaNode.Schema.Validation;

namespace SchemaNode.AI.Chat;

/// <summary>
/// Builds the tool set exposed to the schema assistant.
/// <para>
/// Read tools are created from the existing MCP tool methods in <see cref="SchemaTools"/>, so the
/// tool implementations, parameter descriptions and authorization checks are shared with the MCP
/// server. Only read-only tools are exposed; the save/delete MCP tools are never registered here.
/// </para>
/// </summary>
internal static class SchemaChatToolset
{
    private const string GetSchemaDescription =
        "Read a schema type or namespace definition from the SchemaNode runtime. " +
        "Use it to inspect prototypes, existing schemas, their real properties and type names before constructing a candidate.";

    private const string ValidateSchemaDescription =
        "Validate a candidate node schema on the server without saving it. " +
        "Returns blocking errors and warnings. Fix the errors and validate again before proposing.";

    private const string ProposeSchemaDescription =
        "Submit the final complete node schema as a change candidate for the current editor. " +
        "This does not persist anything; the user applies the candidate explicitly. " +
        "Call it exactly once with the complete schema, not a JSON string or a fragment.";

    /// <summary>Creates the tool list for one request.</summary>
    public static IList<AITool> Create(SchemaContext context, SchemaChatSession session, string? target, bool isNew,
        JsonNode? currentJson)
    {
        var tools = new List<AITool>();

        // For a brand new schema the model tends to query the target that does not exist yet.
        // The prompt already contains the complete format, so only expose the read tool for
        // existing schemas.
        if (!isNew)
        {
            MethodInfo getSchema = typeof(SchemaChatToolset).GetMethod(nameof(GetSchemaSafe), BindingFlags.NonPublic | BindingFlags.Static)!;
            tools.Add(CreateMcpFunction(getSchema, context, SchemaChatPrompt.ToolGetSchema, GetSchemaDescription));
        }

        MethodInfo validateSchema = typeof(SchemaTools).GetMethod(nameof(SchemaTools.ValidateSchema))!;
        tools.Add(CreateMcpFunction(validateSchema, context, SchemaChatPrompt.ToolValidateSchema, ValidateSchemaDescription));

        var proposalTool = new ProposalTool(context, session, target, currentJson);
        tools.Add(CreateFunction(
            typeof(ProposalTool).GetMethod(nameof(ProposalTool.ProposeSchemaChange))!,
            proposalTool,
            SchemaChatPrompt.ToolProposeChange,
            ProposeSchemaDescription));

        return tools;
    }

    private static async Task<object> GetSchemaSafe(
        SchemaContext context,
        [Description("Schema type or namespace name to read. Use a real existing name, not the schema being created.")]
        string name)
    {
        try
        {
            return await SchemaTools.GetSchema(context, name);
        }
        catch (Exception ex)
        {
            // Return a normal tool result instead of throwing. A failing tool call often makes
            // small local models stop instead of recovering.
            return new Dictionary<string, string>
            {
                ["error"] = ex.Message,
                ["hint"] = "The schema type was not found. Do not query the schema you are about to create; " +
                           "call get_schema on system.schema.node.schema or an existing type instead.",
            };
        }
    }

    private static AIFunction CreateMcpFunction(
        MethodInfo method,
        SchemaContext context,
        string name,
        string description)
    {
        return CreateFunction(method, null, name, description, context);
    }

    private static AIFunction CreateFunction(
        MethodInfo method,
        object? instance,
        string name,
        string description,
        SchemaContext? context = null)
    {
        var options = new AIFunctionFactoryOptions
        {
            Name = name,
            Description = description,
            SerializerOptions = SchemaChatJson.Options,
            ConfigureParameterBinding = parameter =>
            {
                if (parameter.ParameterType != typeof(SchemaContext))
                    return default;

                // The schema context is a request scoped service; it must not appear in the
                // function JSON schema the model sees.
                return new AIFunctionFactoryOptions.ParameterBindingOptions
                {
                    ExcludeFromSchema = true,
                    BindParameter = (_, arguments) =>
                        arguments.Services?.GetService(typeof(SchemaContext))
                        ?? context
                        ?? throw new InvalidOperationException("The schema context is not available for the tool invocation."),
                };
            },
        };

        return AIFunctionFactory.Create(method, instance, options);
    }

    /// <summary>
    /// The terminal tool used to capture the structured schema change candidate. It validates the
    /// candidate with the shared server side validator and stores it in the request session.
    /// </summary>
    private sealed class ProposalTool(
        SchemaContext context,
        SchemaChatSession session,
        string? expectedTarget,
        JsonNode? currentJson)
    {
        [Description(
            "Submit the final complete node schema as a change candidate for the schema currently open in the editor. " +
            "The candidate must be the complete node schema object in the system.schema.node.schema format. " +
            "It is validated on the server; if validation fails, fix the errors and call this tool again. " +
            "Calling this tool does not save anything.")]
        public async Task<SchemaProposalToolResult> ProposeSchemaChange(
            [Description(
                "The complete node schema JSON object. Include namespace, name, kind and the kind specific definition.")]
            JsonElement schema,
            [Description(
                "A short natural language summary of what changed and why. This is shown to the user. Optional.")]
            string? summary = null,
            CancellationToken cancellationToken = default)
        {
            session.RecordToolCall(SchemaChatPrompt.ToolProposeChange);

            if (string.IsNullOrWhiteSpace(summary))
            {
                summary = string.IsNullOrWhiteSpace(expectedTarget)
                    ? "Proposed a schema change."
                    : $"Proposed a schema change for '{expectedTarget}'.";
            }

            JsonNode? candidateJson;
            try
            {
                candidateJson = JsonNode.Parse(schema.GetRawText());
            }
            catch (Exception ex)
            {
                session.Proposal = new SchemaChangeCandidate
                {
                    Operation = SchemaCandidateValidator.OperationUpdate,
                    TargetName = expectedTarget ?? string.Empty,
                    Summary = summary,
                    IsValid = false,
                    Errors =
                    [
                        new SchemaValidationError
                        {
                            Code = SchemaValidationErrorCodes.InvalidJson,
                            Message = $"The proposed schema is not valid JSON: {ex.Message}",
                        },
                    ],
                };
                session.ProposalCount++;
                return ToToolResult(session.Proposal);
            }

            SchemaValidationResult validation;
            try
            {
                validation = await context.ValidateNodeSchemaAsync(
                    candidateJson,
                    expectedTarget,
                    checkAuthorization: true,
                    cancellationToken);
            }
            catch (UnauthorizedAccessException)
            {
                validation = new SchemaValidationResult();
                validation.AddError(
                    SchemaValidationErrorCodes.NotAuthorized,
                    "The current user is not allowed to create or update this schema.");
            }
            catch (Exception ex)
            {
                validation = new SchemaValidationResult();
                validation.AddError(
                    SchemaValidationErrorCodes.StructureInvalid,
                    $"The candidate could not be validated: {ex.Message}");
            }

            if (validation.IsValid && currentJson != null)
            {
                JsonNode? normalizedCandidate = SchemaChatJson.NormalizeForPrompt(candidateJson);
                JsonNode? normalizedCurrent = SchemaChatJson.NormalizeForPrompt(currentJson);
                if (JsonNode.DeepEquals(normalizedCandidate, normalizedCurrent))
                {
                    validation.AddError(
                        SchemaValidationErrorCodes.NoChange,
                        "The candidate is identical to the schema currently open in the editor. " +
                        "Apply the change the user asked for, then propose the modified schema.");
                }
            }

            var candidate = new SchemaChangeCandidate
            {
                Operation = validation.Operation
                    ?? (string.IsNullOrWhiteSpace(expectedTarget)
                        ? SchemaCandidateValidator.OperationCreate
                        : SchemaCandidateValidator.OperationUpdate),
                TargetName = validation.TargetName ?? expectedTarget ?? string.Empty,
                Schema = validation.Schema,
                Normalized = validation.Normalized,
                Summary = summary,
                IsValid = validation.IsValid,
                Errors = validation.Errors.ToArray(),
                Warnings = validation.Warnings.ToArray(),
            };

            session.Proposal = candidate;
            session.ProposalCount++;
            return ToToolResult(candidate);
        }

        private static SchemaProposalToolResult ToToolResult(SchemaChangeCandidate candidate) => new()
        {
            IsValid = candidate.IsValid,
            Operation = candidate.Operation,
            TargetName = candidate.TargetName,
            Errors = candidate.Errors,
            Warnings = candidate.Warnings,
            Hint = candidate.IsValid
                ? null
                : candidate.Errors.Any(e => e.Code == SchemaValidationErrorCodes.NoChange)
                    ? "The candidate is identical to the current schema. " +
                      "Actually apply the requested change (for example rewrite every display key to English and add zhCN translations), then propose again."
                    : "The candidate is invalid. Fix the reported errors and call propose_schema_change again in this turn. " +
                      "Use canonical value type names such as system.string, system.int, system.number, system.bool and system.date; " +
                      "primitive types already exist under the system namespace.",
        };
    }
}
