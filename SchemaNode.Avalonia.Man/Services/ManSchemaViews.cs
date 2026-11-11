using SchemaNode.Node;
using SchemaNode.Utility;
using SchemaNode.Avalonia.AppView;
using SchemaNode.Avalonia.View;

namespace SchemaNode.Avalonia.Man.Services;

/// <summary>
/// Central view registration for the Man host, mirroring the
/// <c>regSchemaTypeView</c> calls of schema-node-man/src/schema/index.ts.
///
/// The array type names below are the ones C# Core actually generates
/// (companion array schema = element schema name + "s", see
/// <c>Function.Reflect.Array.genarrayname</c>); they intentionally differ from
/// the vue names in two places:
///   - enum values: the element type is <c>system.entry</c>, so the array is
///     <c>system.entrys</c> (vue used "system.schema.enum.values", which does
///     not exist in the C# runtime);
///   - app fields: the element type is <c>system.schema.app.field.schema</c>,
///     so the array is <c>system.schema.app.field.schemas</c>;
///   - struct fields: <c>StructFieldSchema</c> has no primary/unique index, so Core does
///     not emit a companion array and the field keeps the generic list type name
///     <c>system.list&lt;system.schema.struct.field&gt;</c> (a "struct.fields" name does
///     not exist in the C# runtime).
///
/// Call arguments use the generic list name because no companion array schema
/// is generated for <c>CallArg</c>: <c>system.list&lt;system.schema.func.callarg&gt;</c>.
/// The remaining vue special views are covered outside this registry: the
/// node-type popover by the built-in type selector, the function definition
/// screen by <c>FunctionDefinitionView</c>, the function call list by the plain
/// array view (add/delete is legitimate there), and the workflow node list by
/// the canvas of <c>WorkflowConfigView</c>.
/// </summary>
public static class ManSchemaViews
{
    /// <summary>function call arguments: read-only, rebuilt by Core from the signature</summary>
    public const string FuncCallArgsArray = $"{Constant.NS_SYSTEM_LIST}<{Constant.NS_SYSTEM_SCHEMA_FUNC}.callarg>";

    /// <summary>Man template key for <see cref="FuncCallArgsArray"/>.</summary>
    public const string FuncCallArgsTemplate = "SchemaFuncCallArgs";

    /// <summary>Man template key for the function definition card layout.</summary>
    public const string FunctionDefinitionTemplate = "SchemaFunctionDefinition";

    /// <summary>function definition type: FunctionNode with return / args / exps</summary>
    public const string FunctionSchemaType = $"{Constant.NS_SYSTEM_SCHEMA_FUNC}.schema";

    /// <summary>struct field array</summary>
    public const string StructFieldsArray = "system.schema.struct.fields";

    /// <summary>relation table: element type system.schema.relation.schema</summary>
    public const string RelationSchemasArray = "system.schema.relation.schemas";

    /// <summary>enum values table: element type system.entry</summary>
    public const string EnumValuesArray = "system.entrys";

    /// <summary>app field table: element type system.schema.app.field.schema</summary>
    public const string AppFieldSchemasArray = "system.schema.app.field.schemas";

    /// <summary>
    /// Creates the registry shared by the Man screens: the built-in defaults
    /// plus the table registrations above. App-field names use a plain-text
    /// template (hosts provide the DataTemplate under the same resource key):
    /// they are immutable primary keys, so editing them means delete + add.
    /// Struct-field names are ordinary editable identifiers (vue has no
    /// identifier special case), so the node resolver keeps only the app-field
    /// cell on the immutable template.
    /// </summary>
    public static SchemaViewRegistry CreateRegistry()
    {
        SchemaViewRegistry registry = SchemaViewRegistry.CreateDefault();

        // Struct fields and relation schemas carry many more properties than fit side by side.
        // Mirror schema-node-man's structFields.vue / relations.vue: project only the whitelisted
        // columns, render the cells read-only, and edit the whole element through the operations
        // column as a vertical struct form.
        registry.RegisterTable(StructFieldsArray, new SchemaTableOptions
        {
            ViewColumns = ["name", "type", "fieldDefine.display"],
        });
        registry.RegisterTable(RelationSchemasArray, new SchemaTableOptions
        {
            ViewColumns = ["target", "property", "kind"],
        });
        registry.RegisterTable(EnumValuesArray,
            wrapperFactory: node => new NodeWrapper.EnumValuesTableWrapper((DataNode)node, registry, null, SchemaViewRole.Field));
        registry.RegisterTable(AppFieldSchemasArray);

        // Function call arguments are pushed by Core: the signature owns the collection, so the
        // Man view projects it read-only (no add/delete/operation column) and follows the
        // SubscribeArgs rebuilds. Registration is host-local; the generic array view is untouched.
        SchemaViewTemplates.Set(FuncCallArgsTemplate,
            wrapper => new Views.FuncCallArgsView { DataContext = wrapper });
        registry.RegisterType(FuncCallArgsArray, FuncCallArgsTemplate,
            wrapperFactory: node => new NodeWrapper.FuncCallArgsWrapper((DataNode)node, registry));

        // Function definition: return / args / exps become cards with a live expression result.
        // The type registration covers nodes whose type name is function.schema; the node resolver
        // also covers FunctionNode instances in case Core builds the definition under the node
        // schema name.
        SchemaViewTemplates.Set(FunctionDefinitionTemplate,
            wrapper => new Views.FunctionDefinitionCardsView { DataContext = wrapper });
        registry.RegisterType(FunctionSchemaType, FunctionDefinitionTemplate,
            wrapperFactory: node => new NodeWrapper.FunctionDefinitionWrapper(
                (DataNode)node, registry, null, SchemaViewRole.Field));

        registry.RegisterType(Constant.NS_SYSTEM_IDENTIFIER, "SchemaImmutableTextCell", role: SchemaViewRole.Cell);

        // system.schema.struct.field.name has the identifier type but is editable, unlike the
        // app-field primary key. Resolve the cell by owner so the two never share a template.
        registry.NodeRegistrationResolver = (node, role, skin) =>
        {
            // Core merges the function fields into the node schema struct (the type name stays
            // system.schema.node.schema), so the trigger is structural: a struct that owns the
            // return / args / exps triplet. Function calls and argument nodes do not match.
            if (role is SchemaViewRole.Field or SchemaViewRole.RowEditor
                && node is StructNode structNode
                && structNode.GetAccessValue("return") is not null
                && structNode.GetAccessValue("args") is FuncArgsNode
                && structNode.GetAccessValue("exps") is ArrayNode)
            {
                return new SchemaViewRegistration(
                    FunctionDefinitionTemplate,
                    value => new NodeWrapper.FunctionDefinitionWrapper(
                        (DataNode)value, registry, null, role));
            }

            return role == SchemaViewRole.Cell
                   && string.Equals(node.Type.Name, Constant.NS_SYSTEM_IDENTIFIER, StringComparison.OrdinalIgnoreCase)
                   && node is DataNode { Parent: { } parent }
                   && string.Equals(parent.Type.Name, Constant.NS_SYSTEM_SCHEMA_STRUCT_FIELD, StringComparison.OrdinalIgnoreCase)
                ? new SchemaViewRegistration(SchemaTemplateKeys.Input, null)
                : null;
        };

        // Node-specific hook: an array node that a host page source was attached to renders as the
        // pageable table instead of the plain table. The dispatch stays in the registry, so no
        // screen needs to test "is this pageable" itself.
        PageTableSchemaViews.Enable(registry);
        return registry;
    }
}
