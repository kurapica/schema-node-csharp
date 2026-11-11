namespace SchemaNode.WPF.Man.Services;

/// <summary>
/// Template keys for views that only the Man host registers. They are looked up in the
/// application resources (see Templates/ManSchemaTemplates.xaml), never in the view library,
/// because they depend on the function/administration node shapes.
/// </summary>
public static class ManTemplateKeys
{
    /// <summary>Read-only function call argument list (callArgs.vue).</summary>
    public const string CallArgs = "ManCallArgs";

    /// <summary>Function definition card layout with expression previews (func.vue).</summary>
    public const string Function = "ManFunction";
}
