using System.Text.Json.Nodes;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema.Validation;

/// <summary>
/// The error codes produced by <see cref="SchemaCandidateValidator"/>.
/// </summary>
public static class SchemaValidationErrorCodes
{
    /// <summary>The candidate is not a JSON object.</summary>
    public const string InvalidPayload = "invalid_payload";
    /// <summary>The candidate cannot be deserialized as a node schema.</summary>
    public const string InvalidJson = "invalid_json";
    /// <summary>The candidate does not satisfy the node schema type definition.</summary>
    public const string StructureInvalid = "structure_invalid";
    /// <summary>A constraint declared by the node schema type was violated.</summary>
    public const string ConstraintViolated = "constraint_violated";
    /// <summary>The schema name is missing or invalid.</summary>
    public const string InvalidName = "invalid_name";
    /// <summary>The schema namespace does not exist.</summary>
    public const string NamespaceNotFound = "namespace_not_found";
    /// <summary>The schema kind is missing or not registered.</summary>
    public const string InvalidKind = "invalid_kind";
    /// <summary>System schemas cannot be generated or modified by the assistant.</summary>
    public const string SystemSchemaProtected = "system_schema_protected";
    /// <summary>The candidate identity differs from the editor target.</summary>
    public const string TargetNameMismatch = "target_name_mismatch";
    /// <summary>The candidate kind-specific payload is missing or malformed.</summary>
    public const string MissingDefinition = "missing_definition";
    /// <summary>A referenced type could not be resolved.</summary>
    public const string UnknownType = "unknown_type";
    /// <summary>A referenced type exists but is not a value type.</summary>
    public const string NotValueType = "not_value_type";
    /// <summary>The kind-specific definition is malformed.</summary>
    public const string InvalidDefinition = "invalid_definition";
    /// <summary>The user is not allowed to create or update the target schema.</summary>
    public const string NotAuthorized = "not_authorized";
    /// <summary>The candidate is identical to the current schema.</summary>
    public const string NoChange = "no_change";
}

/// <summary>
/// A single problem found while validating a candidate node schema.
/// </summary>
public sealed class SchemaValidationError
{
    /// <summary>The machine readable error code, see <see cref="SchemaValidationErrorCodes"/>.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>The human readable error message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>The JSON path inside the candidate, e.g. <c>struct.fields[0].type</c>.</summary>
    public string? Path { get; set; }
}

/// <summary>
/// The result of validating a candidate node schema.
/// </summary>
public sealed class SchemaValidationResult
{
    /// <summary>Whether the candidate is valid.</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>The blocking problems found in the candidate.</summary>
    public List<SchemaValidationError> Errors { get; set; } = [];

    /// <summary>Non blocking findings, e.g. unknown extension properties.</summary>
    public List<string> Warnings { get; set; } = [];

    /// <summary>The parsed node schema, available even when semantic errors were found.</summary>
    public NodeSchema? Schema { get; set; }

    /// <summary>The normalized schema JSON (runtime-only fields removed).</summary>
    public JsonNode? Normalized { get; set; }

    /// <summary>The full name of the candidate schema.</summary>
    public string? TargetName { get; set; }

    /// <summary>Whether the candidate creates a new schema or updates an existing one.</summary>
    public string? Operation { get; set; }

    /// <summary>Adds an error to the result.</summary>
    public void AddError(string code, string message, string? path = null)
        => Errors.Add(new SchemaValidationError { Code = code, Message = message, Path = path });

    /// <summary>Adds a warning to the result.</summary>
    public void AddWarning(string warning) => Warnings.Add(warning);
}
