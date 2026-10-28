using System.Text.Json;
using System.Text.Json.Nodes;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property.App;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using RuntimeNamespaceType = SchemaNode.Runtime.NamespaceType;
using NodeType = SchemaNode.Runtime.NodeType;
using RuntimeValueType = SchemaNode.Runtime.ValueType;
using RuntimeArrayType = SchemaNode.Runtime.ArrayType;

namespace SchemaNode.Schema.Validation;

/// <summary>
/// Validates a candidate <see cref="NodeSchema"/> against the SchemaNode runtime.
/// <para>
/// The validator reuses the runtime schema definitions and value types; it does not implement a
/// second schema parser. It is used by the AI chat API, the MCP validation tool and the
/// <c>validate-schema</c> HTTP API, so the same rules apply on the server for every caller.
/// </para>
/// </summary>
public static class SchemaCandidateValidator
{
    /// <summary>The node schema meta type used for structural validation.</summary>
    public const string NodeSchemaTypeName = $"{NS_SYSTEM_SCHEMA_NODE}.schema";

    /// <summary>Operation value for a new schema.</summary>
    public const string OperationCreate = "create";

    /// <summary>Operation value for an existing schema.</summary>
    public const string OperationUpdate = "update";

    private const int MaxDepth = 8;

    /// <summary>
    /// Common short type names produced by models, mapped to the canonical SchemaNode value type
    /// names. Only type-reference positions are normalized; enum value types are left untouched.
    /// </summary>
    private static readonly Dictionary<string, string> TypeAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["string"] = NS_SYSTEM_STRING,
        ["text"] = NS_SYSTEM_STRING,
        ["int"] = NS_SYSTEM_INT,
        ["integer"] = NS_SYSTEM_INT,
        ["long"] = NS_SYSTEM_INT,
        ["decimal"] = NS_SYSTEM_NUMBER,
        ["number"] = NS_SYSTEM_NUMBER,
        ["float"] = NS_SYSTEM_FLOAT,
        ["double"] = NS_SYSTEM_DOUBLE,
        ["bool"] = NS_SYSTEM_BOOL,
        ["boolean"] = NS_SYSTEM_BOOL,
        ["date"] = NS_SYSTEM_DATE,
        ["datetime"] = NS_SYSTEM_DATE,
        ["object"] = NS_SYSTEM_OBJECT,
    };

    /// <summary>
    /// Validates a candidate node schema.
    /// </summary>
    /// <param name="context">The schema context.</param>
    /// <param name="candidate">The candidate node schema JSON.</param>
    /// <param name="expectedName">
    /// When set, the candidate full name must match this name. This binds the candidate to the
    /// editor target so an assistant cannot silently replace a different schema.
    /// </param>
    /// <param name="checkAuthorization">
    /// Whether to enforce the existing <c>SchemaCreate</c>/<c>SchemaUpdate</c> policies.
    /// When the caller catches validation failures instead (the chat tools), keep this
    /// <see langword="true"/> so an unauthorized caller still cannot obtain a usable candidate.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The validation result.</returns>
    public static async Task<SchemaValidationResult> ValidateNodeSchemaAsync(
        this SchemaContext context,
        JsonNode? candidate,
        string? expectedName = null,
        bool checkAuthorization = true,
        CancellationToken cancellationToken = default)
    {
        var result = new SchemaValidationResult();

        // Locale strings (display) are accepted as plain strings by the model, but the editor and
        // the serializer expect a locale string object. Normalize them before parsing so the
        // returned candidate is directly loadable by the frontend.
        JsonNode? normalized = candidate?.DeepClone();
        NormalizeLocaleStrings(normalized, 0);
        NormalizeTypeAliases(normalized, 0);
        NormalizeFlattenedDefinitions(normalized, 0);

        await ValidateCoreAsync(context, normalized, expectedName, checkAuthorization, result, 0, cancellationToken);
        return result;
    }

    private static void NormalizeTypeAliases(JsonNode? node, int depth)
    {
        if (node is not JsonObject root || depth > MaxDepth) return;

        if (root["struct"] is JsonObject structDef && structDef["fields"] is JsonArray fields)
        {
            foreach (JsonNode? field in fields)
            {
                if (field is not JsonObject fieldObject) continue;
                NormalizeTypeValue(fieldObject, "type");
                NormalizeTypeValue(fieldObject, "overrideType");
            }
        }

        if (root["array"] is JsonObject arrayDef)
            NormalizeTypeValue(arrayDef, "element");

        if (root["func"] is JsonObject funcDef)
        {
            NormalizeTypeValue(funcDef, "return");
            if (funcDef["args"] is JsonArray args)
            {
                foreach (JsonNode? arg in args)
                    if (arg is JsonObject argObject) NormalizeTypeValue(argObject, "type");
            }
            if (funcDef["exps"] is JsonArray exps)
            {
                foreach (JsonNode? exp in exps)
                {
                    if (exp is not JsonObject expObject) continue;
                    NormalizeTypeValue(expObject, "return");
                    if (expObject["call"] is JsonObject call)
                    {
                        NormalizeTypeValue(call, "return");
                        NormalizeTypeValue(call, "funcReturn");
                    }
                }
            }
        }

        if (root["schemas"] is JsonArray schemas)
        {
            foreach (JsonNode? subSchema in schemas)
                NormalizeTypeAliases(subSchema, depth + 1);
        }
    }

    private static void NormalizeTypeValue(JsonObject owner, string key)
    {
        if (!owner.TryGetPropertyValue(key, out JsonNode? node) || node is not JsonValue value) return;
        if (!value.TryGetValue<string>(out string? typeName) || string.IsNullOrWhiteSpace(typeName)) return;

        string trimmed = typeName.Trim();
        if (TypeAliases.TryGetValue(trimmed, out string? canonical))
        {
            owner[key] = canonical;
            return;
        }

        // Also accept X[] / List<X> shorthand for a single element array usage.
        if (trimmed.EndsWith("[]", StringComparison.Ordinal))
        {
            string element = trimmed[..^2].Trim();
            if (TypeAliases.TryGetValue(element, out string? elementType))
            {
                owner[key] = $"{NS_SYSTEM_LIST}<{elementType}>";
                return;
            }
        }

        if (trimmed.StartsWith("List<", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith('>'))
        {
            string element = trimmed[5..^1].Trim();
            if (TypeAliases.TryGetValue(element, out string? elementType))
                owner[key] = $"{NS_SYSTEM_LIST}<{elementType}>";
        }
    }

    private static void NormalizeFlattenedDefinitions(JsonNode? node, int depth)
    {
        if (node is not JsonObject root || depth > MaxDepth) return;

        switch (GetString(root, "kind")?.ToLowerInvariant())
        {
            case NODE_KIND_STRUCT:
                if (root["struct"] == null) root["struct"] = new JsonObject();
                if (root["struct"] is JsonObject structDef
                    && !structDef.ContainsKey("fields")
                    && root["fields"] is JsonArray fields)
                {
                    structDef["fields"] = fields.DeepClone();
                    root.Remove("fields");
                }
                break;

            case NODE_KIND_ENUM:
                if (root["enum"] == null) root["enum"] = new JsonObject();
                if (root["enum"] is JsonObject enumDef)
                {
                    if (!enumDef.ContainsKey("values") && root["values"] is JsonArray values)
                    {
                        enumDef["values"] = values.DeepClone();
                        root.Remove("values");
                    }
                    if (!enumDef.ContainsKey("type")
                        && root["type"] is JsonValue typeValue
                        && typeValue.TryGetValue<string>(out string? enumType)
                        && System.Enum.TryParse<EnumValueType>(enumType, true, out _))
                    {
                        enumDef["type"] = enumType;
                        root.Remove("type");
                    }
                    if (!enumDef.ContainsKey("cascade") && root["cascade"] is JsonArray cascade)
                    {
                        enumDef["cascade"] = cascade.DeepClone();
                        root.Remove("cascade");
                    }
                }
                break;

            case NODE_KIND_ARRAY:
                if (root["array"] == null) root["array"] = new JsonObject();
                if (root["array"] is JsonObject arrayDef
                    && !arrayDef.ContainsKey("element")
                    && root["element"] is JsonValue elementValue
                    && elementValue.TryGetValue<string>(out string? element))
                {
                    arrayDef["element"] = element;
                    root.Remove("element");
                }
                break;

            case NODE_KIND_FUNCTION:
                if (root["func"] == null) root["func"] = new JsonObject();
                if (root["func"] is JsonObject funcDef)
                {
                    if (!funcDef.ContainsKey("return")
                        && root["return"] is JsonValue returnValue
                        && returnValue.TryGetValue<string>(out string? returnType))
                    {
                        funcDef["return"] = returnType;
                        root.Remove("return");
                    }
                    if (!funcDef.ContainsKey("args") && root["args"] is JsonArray args)
                    {
                        funcDef["args"] = args.DeepClone();
                        root.Remove("args");
                    }
                    if (!funcDef.ContainsKey("exps"))
                    {
                        JsonArray? exps = root["exps"] as JsonArray ?? root["exprs"] as JsonArray;
                        if (exps != null)
                        {
                            funcDef["exps"] = exps.DeepClone();
                            root.Remove("exps");
                            root.Remove("exprs");
                        }
                    }
                }
                break;
        }

        if (root["schemas"] is JsonArray schemas)
        {
            foreach (JsonNode? subSchema in schemas)
                NormalizeFlattenedDefinitions(subSchema, depth + 1);
        }
    }

    private static void NormalizeDisplayObject(JsonObject display)
    {
        if (!display.ContainsKey("key") && GetString(display, "text") is { } text)
            display["key"] = text;

        if (display["trans"] is JsonArray translations)
        {
            foreach (JsonNode? item in translations)
            {
                if (item is not JsonObject entry) continue;

                string? lang = GetString(entry, "lang") ?? GetString(entry, "language") ?? GetString(entry, "locale");
                string? tran = GetString(entry, "tran") ?? GetString(entry, "text") ?? GetString(entry, "translation");

                if (!string.IsNullOrWhiteSpace(lang)) entry["lang"] = lang;
                if (!string.IsNullOrWhiteSpace(tran)) entry["tran"] = tran;

                entry.Remove("language");
                entry.Remove("text");
                entry.Remove("translation");
                entry.Remove("locale");
            }
        }

        // The display object itself only knows key/trans.
        display.Remove("language");
        display.Remove("text");
        display.Remove("translation");
        display.Remove("locale");
    }

    private static void NormalizeLocaleStrings(JsonNode? node, int depth)
    {
        if (node == null || depth > MaxDepth) return;

        if (node is JsonObject obj)
        {
            foreach (string key in obj.Select(p => p.Key).ToArray())
            {
                JsonNode? value = obj[key];
                if (key.Equals("display", StringComparison.OrdinalIgnoreCase)
                    && value is JsonValue stringValue
                    && stringValue.TryGetValue<string>(out string? text))
                {
                    obj[key] = new JsonObject
                    {
                        ["key"] = text,
                        ["trans"] = new JsonArray(),
                    };
                    continue;
                }

                if (key.Equals("display", StringComparison.OrdinalIgnoreCase) && value is JsonObject displayObject)
                {
                    NormalizeDisplayObject(displayObject);
                }

                NormalizeLocaleStrings(value, depth + 1);
            }
            return;
        }

        if (node is JsonArray array)
        {
            foreach (JsonNode? item in array)
                NormalizeLocaleStrings(item, depth + 1);
        }
    }

    private static async Task ValidateCoreAsync(
        SchemaContext context,
        JsonNode? candidate,
        string? expectedName,
        bool checkAuthorization,
        SchemaValidationResult result,
        int depth,
        CancellationToken cancellationToken)
    {
        if (depth > MaxDepth)
        {
            result.AddError(SchemaValidationErrorCodes.StructureInvalid, "The schema hierarchy is too deep to validate.");
            return;
        }

        if (candidate is not JsonObject root)
        {
            result.AddError(SchemaValidationErrorCodes.InvalidPayload, "The candidate schema must be a JSON object.");
            return;
        }

        // 1. Structural parse. The PropertyOwner converter preserves kind specific definitions
        // (struct/enum/array/func/display/...) in NodeSchema.Extensions.
        NodeSchema? schema;
        try
        {
            schema = context.FromJsonRequest<NodeSchema>(root.ToJsonString());
        }
        catch (Exception ex)
        {
            result.AddError(SchemaValidationErrorCodes.InvalidJson, $"The candidate schema could not be parsed: {ex.Message}");
            return;
        }

        if (schema == null)
        {
            result.AddError(SchemaValidationErrorCodes.InvalidJson, "The candidate schema could not be parsed.");
            return;
        }

        // Keep the first parsed schema as the response payload. For nested namespace schemas the
        // caller gets the whole tree through NodeSchema.Schemas.
        result.Schema ??= schema;
        result.TargetName ??= schema.FullName;

        // 2. Identity checks.
        ValidateIdentity(context, schema, expectedName, result);
        if (result.Errors.Count == 0)
            await ValidateNamespaceAsync(context, schema, result);

        // 3. Structural validation against system.schema.node.schema (kind enum, field types).
        try
        {
            var metaType = await context.GetNodeTypeAsync<RuntimeValueType>(NodeSchemaTypeName);
            if (metaType == null)
            {
                result.AddError(SchemaValidationErrorCodes.StructureInvalid, $"The schema type '{NodeSchemaTypeName}' is not available.");
            }
            else
            {
                var value = await metaType.ValidateValueAsync(context, root);
                if (value == null)
                    result.AddError(SchemaValidationErrorCodes.StructureInvalid, "The candidate schema does not match the node schema structure.");
                else if (!value.IsValid && value.Violated is { } violated)
                    CollectViolations(violated, string.Empty, result);
            }
        }
        catch (Exception ex)
        {
            result.AddError(SchemaValidationErrorCodes.StructureInvalid, $"The candidate schema could not be validated: {ex.Message}");
        }

        // Do not run semantic checks on an already broken structure; they would only add noise.
        if (result.Errors.Count > 0) return;

        // 4. Kind specific validation.
        var genericNames = GetGenericNames(root);
        switch (schema.Kind.ToLowerInvariant())
        {
            case NODE_KIND_STRUCT:
                await ValidateStructAsync(context, root, genericNames, result, cancellationToken);
                break;
            case NODE_KIND_ENUM:
                ValidateEnum(root, result);
                break;
            case NODE_KIND_ARRAY:
                await ValidateArrayAsync(context, root, genericNames, result, cancellationToken);
                break;
            case NODE_KIND_FUNCTION:
                await ValidateFunctionAsync(context, root, genericNames, result, cancellationToken);
                break;
            case NODE_KIND_NAMESPACE:
                // namespace sub schemas are validated recursively below
                break;
            case NODE_KIND_STRING:
            case NODE_KIND_INT:
            case NODE_KIND_DECIMAL:
            case NODE_KIND_DATE:
            case NODE_KIND_BOOL:
            case NODE_KIND_OBJECT:
                break;
            default:
                result.AddError(
                    SchemaValidationErrorCodes.InvalidKind,
                    $"The schema kind '{schema.Kind}' is not registered in this runtime.",
                    "kind");
                break;
        }

        // 5. Unknown top-level extension properties are reported, not rejected: existing schemas
        // may legitimately carry dialect specific extension data.
        var allowedKeys = GetAllowedKeys(context, schema.Kind);
        foreach (var (key, _) in root)
        {
            if (!allowedKeys.Contains(key))
                result.AddWarning($"Unknown schema property '{key}' is not declared by kind '{schema.Kind}' and will be preserved as extension data.");
        }

        // Reject references to functions that do not exist (valid, entrySource, default, ...).
        if (result.Errors.Count == 0)
            await ValidateFunctionReferencesAsync(context, root, string.Empty, genericNames, result, 0, cancellationToken);

        // 6. Namespace sub schemas.
        if (schema.Kind.Equals(NODE_KIND_NAMESPACE, StringComparison.OrdinalIgnoreCase)
            && root.TryGetPropertyValue("schemas", out var schemasNode) && schemasNode is JsonArray schemas)
        {
            foreach (var subSchema in schemas)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ValidateCoreAsync(context, subSchema, null, false, result, depth + 1, cancellationToken);
            }
        }

        // 7. Authorization is checked last so malformed candidates do not reach the policy system.
        if (checkAuthorization && result.Errors.Count == 0)
            await CheckAuthorizationAsync(context, schema, result);

        // 8. Normalized representation used by the response protocol and the frontend apply step.
        try
        {
            result.Normalized = schema.ToJsonNode();
        }
        catch
        {
            result.Normalized = root.DeepClone();
        }
    }

    private static void ValidateIdentity(
        SchemaContext context,
        NodeSchema schema,
        string? expectedName,
        SchemaValidationResult result)
    {
        // Name
        if (string.IsNullOrWhiteSpace(schema.Name))
        {
            result.AddError(SchemaValidationErrorCodes.InvalidName, "The schema name is required.", "name");
        }
        else
        {
            if (schema.Name.Length > PRIMARY_KEY_MAX_LEN)
                result.AddError(SchemaValidationErrorCodes.InvalidName, $"The schema name exceeds {PRIMARY_KEY_MAX_LEN} characters.", "name");
            if (schema.Name.Contains('.') || schema.Name.Any(char.IsWhiteSpace))
                result.AddError(SchemaValidationErrorCodes.InvalidName, "The schema name must be a single identifier without dots or whitespace.", "name");
        }

        // Namespace path
        if (!string.IsNullOrWhiteSpace(schema.Namespace))
        {
            if (schema.Namespace.Split('.').Any(string.IsNullOrWhiteSpace))
                result.AddError(SchemaValidationErrorCodes.InvalidName, "The schema namespace contains an empty segment.", "namespace");
        }

        // Kind
        if (string.IsNullOrWhiteSpace(schema.Kind))
        {
            result.AddError(SchemaValidationErrorCodes.InvalidKind, "The schema kind is required.", "kind");
        }
        else if ((context.Runtime as SchemaRuntime)?.GetNodeType(schema.Kind) == null)
        {
            result.AddError(SchemaValidationErrorCodes.InvalidKind, $"The schema kind '{schema.Kind}' is not registered.", "kind");
        }

        if (result.Errors.Count > 0) return;

        var fullName = schema.FullName;

        // The system namespace defines the public semantics; the assistant must not generate
        // candidates that modify or replace it.
        if (fullName.Equals(NS_SYSTEM, StringComparison.OrdinalIgnoreCase)
            || fullName.StartsWith($"{NS_SYSTEM}.", StringComparison.OrdinalIgnoreCase))
        {
            result.AddError(
                SchemaValidationErrorCodes.SystemSchemaProtected,
                $"The system namespace is read-only for assistant generated candidates ('{fullName}').",
                "name");
        }

        // The candidate must stay bound to the editor target so it cannot replace another schema.
        if (!string.IsNullOrWhiteSpace(expectedName)
            && !fullName.Equals(expectedName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            result.AddError(
                SchemaValidationErrorCodes.TargetNameMismatch,
                $"The candidate targets '{fullName}' but the current editor edits '{expectedName.Trim()}'. Keep the same namespace and name when modifying an existing schema.",
                "name");
        }
    }

    private static async Task ValidateNamespaceAsync(
        SchemaContext context,
        NodeSchema schema,
        SchemaValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(schema.Namespace)) return;

        NodeType? namespaceType = await TryGetNodeTypeAsync(context, schema.Namespace);
        if (namespaceType is not RuntimeNamespaceType)
        {
            result.AddError(
                SchemaValidationErrorCodes.NamespaceNotFound,
                $"The namespace '{schema.Namespace}' does not exist.",
                "namespace");
        }
    }

    private static async Task CheckAuthorizationAsync(SchemaContext context, NodeSchema schema, SchemaValidationResult result)
    {
        try
        {
            NodeType? existing = await context.GetNodeTypeAsync(schema.FullName);
            if (existing != null)
            {
                // Enforce the same authorization as SaveSchemaAsync for an update.
                await context.AuthorizeAsync(existing, PolicyScope.SchemaUpdate);
                result.Operation = OperationUpdate;
                return;
            }

            // Enforce the same authorization as SaveSchemaAsync for a creation: the closest
            // existing parent namespace must allow SchemaCreate.
            string name = schema.Namespace ?? string.Empty;
            NodeType? parent = null;
            while (!string.IsNullOrWhiteSpace(name) && (parent = await context.GetNodeTypeAsync(name)) == null)
                name = name.GetNamespace();

            parent ??= (context.Runtime as SchemaRuntime)?.RootNamespace;
            if (parent != null)
                await context.AuthorizeAsync(parent, PolicyScope.SchemaCreate);

            result.Operation = OperationCreate;
        }
        catch (UnauthorizedAccessException)
        {
            result.AddError(
                SchemaValidationErrorCodes.NotAuthorized,
                "The current user is not allowed to create or update this schema.",
                null);
        }
        catch (Exception ex)
        {
            result.AddError(
                SchemaValidationErrorCodes.NotAuthorized,
                $"The authorization check failed: {ex.Message}",
                null);
        }
    }

    private static async Task ValidateStructAsync(
        SchemaContext context,
        JsonObject root,
        HashSet<string> genericNames,
        SchemaValidationResult result,
        CancellationToken cancellationToken)
    {
        if (root["struct"] is not JsonObject structDef)
        {
            result.AddError(SchemaValidationErrorCodes.MissingDefinition, "A struct schema requires the 'struct' definition.", "struct");
            return;
        }

        if (structDef["fields"] is not JsonArray fields)
        {
            result.AddError(SchemaValidationErrorCodes.InvalidDefinition, "The 'struct.fields' array is required.", "struct.fields");
            return;
        }

        if (fields.Count == 0)
            result.AddWarning("The struct definition has no fields.");

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < fields.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = $"struct.fields[{i}]";
            if (fields[i] is not JsonObject field)
            {
                result.AddError(SchemaValidationErrorCodes.InvalidDefinition, "A struct field must be an object.", path);
                continue;
            }

            string? fieldName = GetString(field, "name");
            if (string.IsNullOrWhiteSpace(fieldName))
                result.AddError(SchemaValidationErrorCodes.InvalidDefinition, "The struct field name is required.", $"{path}.name");
            else if (!names.Add(fieldName))
                result.AddError(SchemaValidationErrorCodes.InvalidDefinition, $"Duplicate struct field name '{fieldName}'.", $"{path}.name");

            string? type = GetString(field, "type");
            if (string.IsNullOrWhiteSpace(type))
                result.AddError(SchemaValidationErrorCodes.InvalidDefinition, "The struct field type is required.", $"{path}.type");
            else
                await RequireValueTypeAsync(context, type, genericNames, $"{path}.type", "struct field", result);
        }
    }

    private static void ValidateEnum(JsonObject root, SchemaValidationResult result)
    {
        if (root["enum"] is not JsonObject enumDef)
        {
            result.AddError(SchemaValidationErrorCodes.MissingDefinition, "An enum schema requires the 'enum' definition.", "enum");
            return;
        }

        string? type = GetString(enumDef, "type");
        if (string.IsNullOrWhiteSpace(type))
            result.AddError(SchemaValidationErrorCodes.InvalidDefinition, "The enum value type is required (string, int or flags).", "enum.type");
        else if (!System.Enum.TryParse<EnumValueType>(type, true, out var enumValueType))
            result.AddError(SchemaValidationErrorCodes.InvalidDefinition, $"Unknown enum value type '{type}'.", "enum.type");
        else if (enumValueType == EnumValueType.Flags && enumDef["cascade"] is not JsonArray)
            result.AddWarning("A flags enum usually declares the 'enum.cascade' levels.");

        if (enumDef["values"] is not JsonArray values)
        {
            result.AddError(SchemaValidationErrorCodes.InvalidDefinition, "The 'enum.values' array is required.", "enum.values");
            return;
        }

        if (values.Count == 0)
            result.AddError(SchemaValidationErrorCodes.InvalidDefinition, "An enum must contain at least one value.", "enum.values");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < values.Count; i++)
        {
            string path = $"enum.values[{i}]";
            if (values[i] is not JsonObject entry)
            {
                result.AddError(SchemaValidationErrorCodes.InvalidDefinition, "An enum value must be an object.", path);
                continue;
            }

            string? value = GetString(entry, "value");
            if (string.IsNullOrWhiteSpace(value))
                result.AddError(SchemaValidationErrorCodes.InvalidDefinition, "The enum value is required.", $"{path}.value");
            else if (!seen.Add(value))
                result.AddError(SchemaValidationErrorCodes.InvalidDefinition, $"Duplicate enum value '{value}'.", $"{path}.value");
        }
    }

    private static async Task ValidateArrayAsync(
        SchemaContext context,
        JsonObject root,
        HashSet<string> genericNames,
        SchemaValidationResult result,
        CancellationToken cancellationToken)
    {
        if (root["array"] is not JsonObject arrayDef)
        {
            result.AddError(SchemaValidationErrorCodes.MissingDefinition, "An array schema requires the 'array' definition.", "array");
            return;
        }

        string? element = GetString(arrayDef, "element");
        if (string.IsNullOrWhiteSpace(element))
        {
            result.AddError(SchemaValidationErrorCodes.InvalidDefinition, "The array element type is required.", "array.element");
            return;
        }

        var elementType = await RequireValueTypeAsync(context, element, genericNames, "array.element", "array element", result);
        if (elementType is RuntimeArrayType)
            result.AddError(SchemaValidationErrorCodes.InvalidDefinition, "An array element type cannot be an array.", "array.element");
    }

    private static async Task ValidateFunctionAsync(
        SchemaContext context,
        JsonObject root,
        HashSet<string> genericNames,
        SchemaValidationResult result,
        CancellationToken cancellationToken)
    {
        if (root["func"] is not JsonObject funcDef)
        {
            result.AddError(SchemaValidationErrorCodes.MissingDefinition, "A function schema requires the 'func' definition.", "func");
            return;
        }

        string? returnType = GetString(funcDef, "return");
        if (string.IsNullOrWhiteSpace(returnType))
            result.AddError(SchemaValidationErrorCodes.InvalidDefinition, "The function return type is required.", "func.return");
        else
            await RequireValueTypeAsync(context, returnType, genericNames, "func.return", "function return", result);

        if (funcDef["args"] is JsonArray args)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Count; i++)
            {
                string path = $"func.args[{i}]";
                if (args[i] is not JsonObject arg)
                {
                    result.AddError(SchemaValidationErrorCodes.InvalidDefinition, "A function argument must be an object.", path);
                    continue;
                }

                string? argName = GetString(arg, "name");
                if (string.IsNullOrWhiteSpace(argName))
                    result.AddError(SchemaValidationErrorCodes.InvalidDefinition, "The function argument name is required.", $"{path}.name");
                else if (!names.Add(argName))
                    result.AddError(SchemaValidationErrorCodes.InvalidDefinition, $"Duplicate function argument name '{argName}'.", $"{path}.name");

                string? argType = GetString(arg, "type");
                if (string.IsNullOrWhiteSpace(argType))
                    result.AddError(SchemaValidationErrorCodes.InvalidDefinition, "The function argument type is required.", $"{path}.type");
                else
                    await RequireValueTypeAsync(context, argType, genericNames, $"{path}.type", "function argument", result);
            }
        }

        if (funcDef["exps"] is JsonArray exps)
        {
            for (int i = 0; i < exps.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string path = $"func.exps[{i}]";
                if (exps[i] is not JsonObject exp)
                {
                    result.AddError(SchemaValidationErrorCodes.InvalidDefinition, "A function expression must be an object.", path);
                    continue;
                }

                string? expType = GetString(exp, "return");
                if (!string.IsNullOrWhiteSpace(expType))
                    await RequireValueTypeAsync(context, expType, genericNames, $"{path}.return", "function expression return", result);

                if (exp["call"] is JsonObject call)
                {
                    string? funcName = GetString(call, "func");
                    if (!string.IsNullOrWhiteSpace(funcName))
                    {
                        NodeType? funcType = await TryGetNodeTypeAsync(context, funcName);
                        if (funcType is not FunctionType)
                            result.AddError(SchemaValidationErrorCodes.UnknownType, $"The function '{funcName}' does not exist.", $"{path}.call.func");
                    }
                }
            }
        }
    }

    private static async Task ValidateFunctionReferencesAsync(
        SchemaContext context,
        JsonNode? node,
        string path,
        HashSet<string> genericNames,
        SchemaValidationResult result,
        int depth,
        CancellationToken cancellationToken)
    {
        if (node == null || depth > MaxDepth) return;

        if (node is JsonObject obj)
        {
            foreach (var (key, value) in obj)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string childPath = string.IsNullOrEmpty(path) ? key : $"{path}.{key}";

                if (key.Equals("func", StringComparison.OrdinalIgnoreCase)
                    && value is JsonValue funcValue
                    && funcValue.TryGetValue<string>(out string? funcName)
                    && !string.IsNullOrWhiteSpace(funcName)
                    && !IsGenericType(funcName, genericNames))
                {
                    NodeType? funcType = await TryGetNodeTypeAsync(context, funcName);
                    if (funcType is not FunctionType)
                        result.AddError(
                            SchemaValidationErrorCodes.UnknownType,
                            $"The function '{funcName}' does not exist.",
                            childPath);
                }

                await ValidateFunctionReferencesAsync(context, value, childPath, genericNames, result, depth + 1, cancellationToken);
            }
            return;
        }

        if (node is JsonArray array)
        {
            for (int i = 0; i < array.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ValidateFunctionReferencesAsync(context, array[i], $"{path}[{i}]", genericNames, result, depth + 1, cancellationToken);
            }
        }
    }

    private static async Task<RuntimeValueType?> RequireValueTypeAsync(
        SchemaContext context,
        string typeName,
        HashSet<string> genericNames,
        string path,
        string role,
        SchemaValidationResult result)
    {
        if (IsGenericType(typeName, genericNames)) return null;

        NodeType? nodeType = await TryGetNodeTypeAsync(context, typeName);
        if (nodeType == null)
        {
            result.AddError(SchemaValidationErrorCodes.UnknownType, $"The {role} type '{typeName}' does not exist.", path);
            return null;
        }

        if (nodeType is not RuntimeValueType valueType)
        {
            result.AddError(SchemaValidationErrorCodes.NotValueType, $"The {role} type '{typeName}' is not a value type.", path);
            return null;
        }

        return valueType;
    }

    private static bool IsGenericType(string typeName, HashSet<string> genericNames)
    {
        if (genericNames.Count == 0) return false;
        if (genericNames.Contains(typeName)) return true;

        // e.g. system.list<T> or List<T, T1> on a generic function/struct.
        int start = typeName.IndexOf('<');
        if (start < 0 || !typeName.EndsWith('>')) return false;
        string[] parameters = typeName[(start + 1)..^1]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parameters.Length > 0 && parameters.All(genericNames.Contains);
    }

    private static async Task<NodeType?> TryGetNodeTypeAsync(SchemaContext context, string typeName)
    {
        try
        {
            return await context.GetNodeTypeAsync(typeName);
        }
        catch
        {
            return null;
        }
    }

    private static HashSet<string> GetGenericNames(JsonObject root)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetPropertyValue("generics", out var generics) || generics == null) return names;

        if (generics is JsonValue value && value.TryGetValue<string>(out var text))
        {
            string name = text.Split('|')[0].Trim();
            if (name.Length > 0) names.Add(name);
        }
        else if (generics is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is JsonValue itemValue && itemValue.TryGetValue<string>(out var itemText))
                {
                    string name = itemText.Split('|')[0].Trim();
                    if (name.Length > 0) names.Add(name);
                }
                else if (item is JsonObject itemObject && GetString(itemObject, "name") is { Length: > 0 } itemName)
                {
                    names.Add(itemName);
                }
            }
        }

        return names;
    }

    private static HashSet<string> GetAllowedKeys(SchemaContext context, string kind)
    {
        HashSet<string> keys = new(StringComparer.OrdinalIgnoreCase)
        {
            "namespace", "name", "kind", "schemas", "error", "compatibles", "usedBy", "generics",
        };

        foreach (Type propType in context.Runtime.GetSchemaKindPropertyTypes(SCHEMA_KIND_NODE))
            keys.Add(propType.GetPropertyName());

        string schemaKind = (context.Runtime as SchemaRuntime)?.GetSchemaKindByNodeKind(kind) ?? kind;
        foreach (Type propType in context.Runtime.GetSchemaKindPropertyTypes(schemaKind))
            keys.Add(propType.GetPropertyName());

        return keys;
    }

    private static void CollectViolations(JsonNode? node, string path, SchemaValidationResult result)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, child) in obj)
                    CollectViolations(child, string.IsNullOrEmpty(path) ? key : $"{path}.{key}", result);
                break;
            case JsonArray array:
            {
                string constraints = string.Join(", ", array.Select(v => v?.ToString() ?? "constraint"));
                result.AddError(
                    SchemaValidationErrorCodes.ConstraintViolated,
                    string.IsNullOrEmpty(path)
                        ? $"The candidate violates: {constraints}."
                        : $"The property '{path}' violates: {constraints}.",
                    string.IsNullOrEmpty(path) ? null : path);
                break;
            }
            case null:
                break;
            default:
                result.AddError(
                    SchemaValidationErrorCodes.ConstraintViolated,
                    string.IsNullOrEmpty(path)
                        ? $"The candidate violates: {node}."
                        : $"The property '{path}' violates: {node}.",
                    string.IsNullOrEmpty(path) ? null : path);
                break;
        }
    }

    private static string? GetString(JsonObject obj, string key)
    {
        if (!obj.TryGetPropertyValue(key, out var node) || node is not JsonValue value) return null;
        return value.TryGetValue<string>(out var text) ? text : node.ToString();
    }
}
