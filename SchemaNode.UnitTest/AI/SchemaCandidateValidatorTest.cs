using System.Text.Json.Nodes;
using SchemaNode.AI.Chat;
using SchemaNode.Schema;
using SchemaNode.Schema.Provider;
using SchemaNode.Schema.Validation;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.UnitTest.AI;

/// <summary>
/// Tests for the shared server side candidate validator used by the HTTP API, the MCP tool and the
/// AI chat assistant.
/// </summary>
[TestClass]
public class SchemaCandidateValidatorTest : Base.AppTestBase
{
    private static JsonNode Json(string json) => JsonNode.Parse(json)!;

    [TestMethod]
    public async Task ValidStructCandidate_IsValid_AndCreates()
    {
        var result = await Context.ValidateNodeSchemaAsync(Json("""
        {
          "namespace": "",
          "name": "ai_address",
          "kind": "struct",
          "display": "AI Address",
          "struct": {
            "fields": [
              { "name": "street", "type": "system.string" },
              { "name": "zipCode", "type": "system.int" }
            ]
          }
        }
        """));

        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.AreEqual(SchemaCandidateValidator.OperationCreate, result.Operation);
        Assert.AreEqual("ai_address", result.TargetName);
        Assert.IsNotNull(result.Schema);
        Assert.IsNotNull(result.Normalized);
        Assert.IsNotNull(result.Normalized!["struct"]);
        Assert.IsInstanceOfType(result.Normalized["display"], typeof(JsonObject));
        Assert.AreEqual("AI Address", result.Normalized["display"]!["key"]!.GetValue<string>());
        Assert.IsNull(await Context.GetNodeTypeAsync("ai_address"), "Validation must never persist the candidate.");
    }

    [TestMethod]
    public async Task ExistingSchemaUpdate_KeepsIdentity_AndIsUpdate()
    {
        NodeSchema existing = new()
        {
            Namespace = "",
            Name = "ai_customer",
            Kind = NODE_KIND_STRUCT,
        };
        existing.SetProperty<StructProperty, StructSchema>(new StructSchema
        {
            Fields =
            [
                new StructFieldSchema { Name = "name", Type = NS_SYSTEM_STRING },
                new StructFieldSchema { Name = "age", Type = NS_SYSTEM_INT },
            ],
        });
        Assert.IsTrue(await Context.SaveSchemaAsync(existing));

        var result = await Context.ValidateNodeSchemaAsync(Json("""
        {
          "namespace": "",
          "name": "ai_customer",
          "kind": "struct",
          "struct": {
            "fields": [
              { "name": "name", "type": "system.string" },
              { "name": "age", "type": "system.int" },
              { "name": "email", "type": "system.string" }
            ]
          }
        }
        """), expectedName: "ai_customer");

        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.AreEqual(SchemaCandidateValidator.OperationUpdate, result.Operation);
        Assert.IsNotNull(result.Normalized);
        var fields = result.Normalized!["struct"]!["fields"]!.AsArray();
        CollectionAssert.AreEquivalent(
            new[] { "name", "age", "email" },
            fields.Select(f => f!["name"]!.GetValue<string>()).ToArray());
    }

    [TestMethod]
    public async Task UnknownKind_IsRejected()
    {
        var result = await Context.ValidateNodeSchemaAsync(Json("""
        { "namespace": "", "name": "bad", "kind": "nope" }
        """));

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Code == SchemaValidationErrorCodes.InvalidKind || e.Code == SchemaValidationErrorCodes.ConstraintViolated));
    }

    [TestMethod]
    public async Task MissingName_IsRejected()
    {
        var result = await Context.ValidateNodeSchemaAsync(Json("""
        { "namespace": "", "kind": "struct", "struct": { "fields": [] } }
        """));

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Code == SchemaValidationErrorCodes.InvalidName));
    }

    [TestMethod]
    public async Task UnknownFieldType_IsRejected()
    {
        var result = await Context.ValidateNodeSchemaAsync(Json("""
        {
          "namespace": "",
          "name": "bad_field",
          "kind": "struct",
          "struct": { "fields": [ { "name": "a", "type": "system.not_a_type" } ] }
        }
        """));

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Code == SchemaValidationErrorCodes.UnknownType && e.Path == "struct.fields[0].type"));
    }

    [TestMethod]
    public async Task TargetNameMismatch_IsRejected()
    {
        var result = await Context.ValidateNodeSchemaAsync(Json("""
        {
          "namespace": "",
          "name": "new_name",
          "kind": "struct",
          "struct": { "fields": [] }
        }
        """), expectedName: "old_name");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Code == SchemaValidationErrorCodes.TargetNameMismatch));
    }

    [TestMethod]
    public async Task SystemSchema_IsRejected()
    {
        var result = await Context.ValidateNodeSchemaAsync(Json("""
        {
          "namespace": "system",
          "name": "hacked",
          "kind": "struct",
          "struct": { "fields": [] }
        }
        """));

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Code == SchemaValidationErrorCodes.SystemSchemaProtected));
    }

    [TestMethod]
    public async Task MissingNamespace_IsRejected()
    {
        var result = await Context.ValidateNodeSchemaAsync(Json("""
        {
          "namespace": "no.such.namespace",
          "name": "x",
          "kind": "struct",
          "struct": { "fields": [] }
        }
        """));

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.Code == SchemaValidationErrorCodes.NamespaceNotFound));
    }

    [TestMethod]
    public async Task ValidEnum_IsValid_AndDuplicateValuesAreRejected()
    {
        var valid = await Context.ValidateNodeSchemaAsync(Json("""
        {
          "namespace": "",
          "name": "ai_status",
          "kind": "enum",
          "enum": { "type": "string", "values": [ { "value": "active" }, { "value": "inactive" } ] }
        }
        """));
        Assert.IsTrue(valid.IsValid, string.Join("; ", valid.Errors.Select(e => e.Message)));

        var duplicate = await Context.ValidateNodeSchemaAsync(Json("""
        {
          "namespace": "",
          "name": "ai_status",
          "kind": "enum",
          "enum": { "type": "string", "values": [ { "value": "active" }, { "value": "active" } ] }
        }
        """));
        Assert.IsFalse(duplicate.IsValid);
        Assert.IsTrue(duplicate.Errors.Any(e => e.Code == SchemaValidationErrorCodes.InvalidDefinition));
    }

    [TestMethod]
    public async Task ArrayElement_IsValidated()
    {
        var valid = await Context.ValidateNodeSchemaAsync(Json("""
        {
          "namespace": "",
          "name": "ai_strings",
          "kind": "array",
          "array": { "element": "system.string" }
        }
        """));
        Assert.IsTrue(valid.IsValid, string.Join("; ", valid.Errors.Select(e => e.Message)));

        var nestedArray = await Context.ValidateNodeSchemaAsync(Json("""
        {
          "namespace": "",
          "name": "ai_nested",
          "kind": "array",
          "array": { "element": "system.list<system.string>" }
        }
        """));
        Assert.IsFalse(nestedArray.IsValid);
    }

    [TestMethod]
    public async Task FunctionCandidate_IsValidated()
    {
        var valid = await Context.ValidateNodeSchemaAsync(Json("""
        {
          "namespace": "",
          "name": "ai_length",
          "kind": "function",
          "func": {
            "return": "system.int",
            "args": [ { "name": "text", "type": "system.string" } ]
          }
        }
        """));
        Assert.IsTrue(valid.IsValid, string.Join("; ", valid.Errors.Select(e => e.Message)));

        var invalid = await Context.ValidateNodeSchemaAsync(Json("""
        {
          "namespace": "",
          "name": "ai_length",
          "kind": "function",
          "func": {
            "return": "system.not_a_type",
            "args": []
          }
        }
        """));
        Assert.IsFalse(invalid.IsValid);
        Assert.IsTrue(invalid.Errors.Any(e => e.Code == SchemaValidationErrorCodes.UnknownType));
    }

    [TestMethod]
    public async Task UnknownProperty_IsReportedAsWarning_NotError()
    {
        var result = await Context.ValidateNodeSchemaAsync(Json("""
        {
          "namespace": "",
          "name": "ai_warn",
          "kind": "struct",
          "struct": { "fields": [] },
          "madeUpProperty": true
        }
        """));

        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.IsTrue(result.Warnings.Any(w => w.Contains("madeUpProperty")));
    }

    [TestMethod]
    public async Task ShortTypeAliases_AreNormalizedToCanonicalNames()
    {
        var result = await Context.ValidateNodeSchemaAsync(Json("""
        {
          "namespace": "",
          "name": "ai_aliases",
          "kind": "struct",
          "struct": {
            "fields": [
              { "name": "name", "type": "string" },
              { "name": "age", "type": "int" },
              { "name": "tags", "type": "string[]" }
            ]
          }
        }
        """));

        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
        var fields = result.Normalized!["struct"]!["fields"]!.AsArray();
        Assert.AreEqual(NS_SYSTEM_STRING, fields[0]!["type"]!.GetValue<string>());
        Assert.AreEqual(NS_SYSTEM_INT, fields[1]!["type"]!.GetValue<string>());
        Assert.AreEqual($"{NS_SYSTEM_LIST}<{NS_SYSTEM_STRING}>", fields[2]!["type"]!.GetValue<string>());
    }

    [TestMethod]
    public async Task FlattenedStructFields_AreNestedUnderStruct()
    {
        // Models sometimes put "fields" at the top level. It is normalized into struct.fields.
        var result = await Context.ValidateNodeSchemaAsync(Json("""
        {
          "namespace": "",
          "name": "IntegerRange",
          "kind": "struct",
          "display": "Integer Range",
          "fields": [
            { "name": "min", "type": "system.int", "display": "Minimum", "lowLimit": 0, "upLimit": 100 },
            { "name": "max", "type": "system.int", "display": "Maximum", "lowLimit": 0, "upLimit": 100 }
          ]
        }
        """));

        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
        var normalized = result.Normalized!;
        Assert.IsNull(normalized["fields"], "The flattened fields property must be removed.");
        var fields = normalized["struct"]!["fields"]!.AsArray();
        Assert.AreEqual(2, fields.Count);
        Assert.AreEqual("min", fields[0]!["name"]!.GetValue<string>());
        Assert.AreEqual(0, fields[0]!["lowLimit"]!.GetValue<int>());
        Assert.AreEqual(100, fields[0]!["upLimit"]!.GetValue<int>());
        Assert.IsInstanceOfType(fields[0]!["display"], typeof(JsonObject));
        Assert.AreEqual("Minimum", fields[0]!["display"]!["key"]!.GetValue<string>());
        Assert.AreEqual("Integer Range", normalized["display"]!["key"]!.GetValue<string>());
    }

    [TestMethod]
    public async Task DisplayTranslationAliases_AreNormalized()
    {
        var result = await Context.ValidateNodeSchemaAsync(Json("""
        {
          "namespace": "",
          "name": "LocalizedRange",
          "kind": "struct",
          "display": { "key": "Localized Range", "trans": [ { "language": "zhCN", "text": "本地化范围" } ] },
          "struct": {
            "fields": [
              {
                "name": "min",
                "type": "system.int",
                "display": { "key": "Minimum", "trans": [ { "language": "zhCN", "text": "最小值" } ] }
              }
            ]
          }
        }
        """));

        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
        var normalized = result.Normalized!;
        var displayTranslation = normalized["display"]!["trans"]!.AsArray()[0]!.AsObject();
        Assert.AreEqual("zhCN", displayTranslation["lang"]!.GetValue<string>());
        Assert.AreEqual("本地化范围", displayTranslation["tran"]!.GetValue<string>());
        Assert.IsFalse(displayTranslation.ContainsKey("language"));
        Assert.IsFalse(displayTranslation.ContainsKey("text"));

        var fieldTranslation = normalized["struct"]!["fields"]!.AsArray()[0]!["display"]!["trans"]!.AsArray()[0]!.AsObject();
        Assert.AreEqual("zhCN", fieldTranslation["lang"]!.GetValue<string>());
        Assert.AreEqual("最小值", fieldTranslation["tran"]!.GetValue<string>());
    }

    [TestMethod]
    public async Task LocalizeDisplays_ProducesEnglishKeysAndZhTranslations()
    {
        JsonNode node = JsonNode.Parse("""
        {
          "namespace": "",
          "name": "IntegerRange",
          "kind": "struct",
          "display": { "key": "整数范围", "trans": [] },
          "struct": {
            "fields": [
              { "name": "min", "type": "system.int", "display": { "key": "最小值", "trans": [] } },
              { "name": "max", "type": "system.int", "display": { "key": "最大值", "trans": [] } }
            ]
          }
        }
        """)!;

        JsonNode localized = SchemaChatJson.LocalizeDisplays(node)!;
        Assert.AreEqual("Integer Range", localized["display"]!["key"]!.GetValue<string>());
        Assert.AreEqual("整数范围", localized["display"]!["trans"]!.AsArray()[0]!["tran"]!.GetValue<string>());

        var result = await Context.ValidateNodeSchemaAsync(localized);
        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [TestMethod]
    public async Task UnknownFunctionReference_IsRejected()
    {
        var result = await Context.ValidateNodeSchemaAsync(Json("""
        {
          "namespace": "",
          "name": "ai_valid_func",
          "kind": "struct",
          "struct": {
            "fields": [
              {
                "name": "value",
                "type": "system.string",
                "valid": { "mode": "call", "func": "system.no_such_function", "args": [] }
              }
            ]
          }
        }
        """));

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e =>
            e.Code == SchemaValidationErrorCodes.UnknownType &&
            e.Path != null && e.Path.Contains("valid.func")));
    }

    [TestMethod]
    public async Task Candidate_DoesNotPersistOrModifyExistingSchema()
    {
        NodeSchema existing = new()
        {
            Namespace = "",
            Name = "ai_untouched",
            Kind = NODE_KIND_STRUCT,
        };
        existing.SetProperty<StructProperty, StructSchema>(new StructSchema
        {
            Fields = [new StructFieldSchema { Name = "keep", Type = NS_SYSTEM_STRING }],
        });
        Assert.IsTrue(await Context.SaveSchemaAsync(existing));

        var result = await Context.ValidateNodeSchemaAsync(Json("""
        {
          "namespace": "",
          "name": "ai_untouched",
          "kind": "struct",
          "struct": { "fields": [ { "name": "keep", "type": "system.string" }, { "name": "extra", "type": "system.int" } ] }
        }
        """));

        Assert.IsTrue(result.IsValid);
        var type = await Context.GetNodeTypeAsync("ai_untouched");
        Assert.IsNotNull(type);
        var loaded = type!.GetNodeSchema();
        var fields = loaded.GetProperty<StructProperty>()!.Value!.Fields;
        Assert.AreEqual(1, fields.Length, "Validating a candidate must not update the stored schema.");
        Assert.AreEqual("keep", fields[0].Name);
    }
}
