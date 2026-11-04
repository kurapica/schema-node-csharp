using SchemaNode.WPF.AppView;
using SchemaNode.WPF.View;

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
///     so the array is <c>system.schema.app.field.schemas</c>.
///
/// Views that vue registers but have no C# Core counterpart (namespaceView,
/// funcView, callArgsView, waterFallView, workflownodesView) are not
/// registered here.
/// </summary>
public static class ManSchemaViews
{
    /// <summary>struct field table: element type system.schema.struct.field</summary>
    public const string StructFieldsArray = "system.schema.struct.fields";

    /// <summary>relation table: element type system.schema.relation.schema</summary>
    public const string RelationSchemasArray = "system.schema.relation.schemas";

    /// <summary>enum values table: element type system.entry</summary>
    public const string EnumValuesArray = "system.entrys";

    /// <summary>app field table: element type system.schema.app.field.schema</summary>
    public const string AppFieldSchemasArray = "system.schema.app.field.schemas";

    /// <summary>
    /// Creates the registry shared by the Man screens: the built-in defaults
    /// plus the table registrations above. "system.identifier" cells use a
    /// plain-text template (hosts provide the DataTemplate under the same
    /// resource key): identifier fields are immutable primary keys
    /// (StructFieldSchema.Name, AppFieldSchema.Name), so editing them means
    /// delete + add. "system.string" is intentionally NOT overridden: it is a
    /// general editable type (e.g. RelationSchema.Target / Property), and the
    /// immutable enum entry value simply falls back to the default disabled
    /// cell.
    /// </summary>
    public static SchemaViewRegistry CreateRegistry()
    {
        SchemaViewRegistry registry = SchemaViewRegistry.CreateDefault();
        registry.RegisterTable(StructFieldsArray);
        registry.RegisterTable(RelationSchemasArray);
        registry.RegisterTable(EnumValuesArray);
        registry.RegisterTable(AppFieldSchemasArray);

        registry.RegisterType("system.identifier", "SchemaImmutableTextCell", role: SchemaViewRole.Cell);

        // Node-specific hook: an array node that a host page source was attached to renders as the
        // pageable table instead of the plain table. The dispatch stays in the registry, so no
        // screen needs to test "is this pageable" itself.
        PageTableSchemaViews.Enable(registry);
        return registry;
    }
}
