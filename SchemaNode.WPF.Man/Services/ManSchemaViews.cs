using SchemaNode.Node;
using SchemaNode.Utility;
using SchemaNode.WPF.AppView;
using SchemaNode.WPF.Man.NodeWrapper;
using SchemaNode.WPF.View;
using SchemaNode.WPF.View.NodeWrapper;

namespace SchemaNode.WPF.Man.Services;

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
/// The Man-only special views are registered here (never in the view library):
/// <c>callArgs.vue</c> as a read-only argument list and <c>func.vue</c> as the
/// function definition card layout. <c>namespace.vue</c> is the library's
/// type-selector registration, and <c>waterFallView.vue</c> lives inside the
/// function cards as the expression preview.
/// </summary>
public static class ManSchemaViews
{
    /// <summary>struct field array</summary>
    public const string StructFieldsArray = "system.schema.struct.fields";

    /// <summary>relation table: element type system.schema.relation.schema</summary>
    public const string RelationSchemasArray = "system.schema.relation.schemas";

    /// <summary>enum values table: element type system.entry</summary>
    public const string EnumValuesArray = "system.entrys";

    /// <summary>app field table: element type system.schema.app.field.schema</summary>
    public const string AppFieldSchemasArray = "system.schema.app.field.schemas";

    /// <summary>
    /// function call arguments: <c>CallArg</c> has no primary index, so Core does not emit a
    /// companion array schema and the field keeps the generic list type name.
    /// </summary>
    public const string CallArgsArray = "system.list<system.schema.func.callarg>";

    /// <summary>function definition schema</summary>
    public const string FunctionSchemaType = "system.schema.func.schema";

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
            ViewColumns = ["name", "type", "fielddefine.display"],
        });
        registry.RegisterTable(RelationSchemasArray, new SchemaTableOptions
        {
            ViewColumns = ["target", "property", "kind"],
        });
        // Enum values render as the ordinary table plus a "move up" action: entry order is
        // meaningful for enums, but that is a Man editing concern, so the wrapper lives here.
        registry.RegisterTable(EnumValuesArray, wrapperFactory: node =>
            new EnumValuesTableWrapper((DataNode)node, registry, null, SchemaViewRole.Field));
        registry.RegisterTable(AppFieldSchemasArray);

        // Function call arguments are pushed by Core and never added/removed by the user.
        // Non-Field roles fall back to the Field registration, so one entry covers form and
        // row-editor usage (the list is never rendered as a table cell).
        registry.RegisterType(CallArgsArray, ManTemplateKeys.CallArgs, SchemaViewRole.Field, wrapperFactory: node =>
            new FuncCallArgsWrapper((DataNode)node, registry, null, SchemaViewRole.Field));

        // Function definitions use the card layout (return / argument cards / expression cards).
        registry.RegisterType(FunctionSchemaType, ManTemplateKeys.Function, SchemaViewRole.Field);

        registry.RegisterType(Constant.NS_SYSTEM_IDENTIFIER, "SchemaImmutableTextCell", role: SchemaViewRole.Cell);

        registry.NodeRegistrationResolver = (node, role, skin) =>
        {
            // system.schema.struct.field.name has the identifier type but is editable, unlike the
            // app-field primary key. Resolve the cell by owner so the two never share a template.
            if (role == SchemaViewRole.Cell
                && string.Equals(node.Type.Name, Constant.NS_SYSTEM_IDENTIFIER, StringComparison.OrdinalIgnoreCase)
                && node is DataNode { Parent: { } parent }
                && string.Equals(parent.Type.Name, Constant.NS_SYSTEM_SCHEMA_STRUCT_FIELD, StringComparison.OrdinalIgnoreCase))
                return new SchemaViewRegistration(SchemaTemplateKeys.Input, null);

            // Core merges the function fields into the node schema struct, so the type name stays
            // system.schema.node.schema and the function card view cannot be selected by type.
            // Identify it structurally: a struct that owns return / args / exps.
            if (role is SchemaViewRole.Field or SchemaViewRole.RowEditor
                && node is StructNode structNode
                && structNode.GetAccessValue("return") is not null
                && structNode.GetAccessValue("args") is FuncArgsNode
                && structNode.GetAccessValue("exps") is ArrayNode)
            {
                return new SchemaViewRegistration(ManTemplateKeys.Function,
                    value => new StructWrapper((DataNode)value, registry, null, role));
            }

            return null;
        };

        // Node-specific hook: an array node that a host page source was attached to renders as the
        // pageable table instead of the plain table. The dispatch stays in the registry, so no
        // screen needs to test "is this pageable" itself.
        PageTableSchemaViews.Enable(registry);
        return registry;
    }
}
