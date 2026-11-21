using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property.Common;
using SchemaNode.Runtime;
using SchemaNode.Schema;

namespace SchemaNode.Node;

public class WorkflowNode : StructNode
{
    /// <summary>
    /// The return type field
    /// </summary>
    public IValueAccess? PayloadField { get; }

    /// <summary>
    /// The arguments field
    /// </summary>
    public FuncArgsNode? ArgsField { get; }

    /// <summary>
    /// The expressions field
    /// </summary>
    public ArrayNode? StepsFields { get; }

    public WorkflowNode(ISchemaContext? context, IValueTypeAccess type, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders)
        : base(context, type as Runtime.StructType ?? throw new InvalidCastException($"The type '{type.Name}' is not a valid function schema type."), value, parent, propertyProviders)
    {
        PayloadField = GetAccessValue(nameof(WorkflowSchema.Payload));
        ArgsField = GetAccessValue(nameof(WorkflowSchema.Args)) as FuncArgsNode;
        StepsFields = GetAccessValue(nameof(WorkflowSchema.Steps)) as ArrayNode;

        if (FullMode)
            _ = RefreshStateAsync();
    }

    private async Task RefreshStateAsync()
    {
        if (!FullMode || Context is not SchemaContext ctx) return;

        string? ns = Parent?.GetAccessValue("namespace")?.GetValue<string>();
        string? name = Parent?.GetAccessValue("name")?.GetValue<string>();
        string? funcName = !string.IsNullOrWhiteSpace(ns) && !string.IsNullOrWhiteSpace(name) ? $"{ns}.{name}" : name;
        if (string.IsNullOrWhiteSpace(funcName)) return;

        // variadic only for system functions
        var schema = (ctx.Runtime as SchemaRuntime)?.GetSystemSchema(funcName);
        ArgsField?.SetShowVariadic((((schema?.LoadState ?? 0) & SchemaLoadState.System) > 0));

        // the return and argument type are immutable if the function is used
        var nodeType = await ctx.GetNodeTypeAsync<FunctionType>(funcName).ConfigureAwait(false);
        if (nodeType?.IsUsed == true)
        {
            PayloadField?.SetPropertyValue<ReadOnly, bool>(true, this, null);
            ArgsField?.SetUnModifiable(true);
        }
    }
}

