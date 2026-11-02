using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Runtime;

namespace SchemaNode.Node;

/// <summary>
/// The function call node, used to apply the relations from the called function argument
/// </summary>
public class FuncCallNode : StructNode
{
    /// <summary>
    /// The apply mode field
    /// </summary>
    public EnumNode? ModeField { get; }

    /// <summary>
    /// The function field
    /// </summary>
    public StringNode? FuncField { get; }

    /// <summary>
    /// The arguments field
    /// </summary>
    public FuncCallArgsNode? ArgsField { get; }

    public FuncCallNode(ISchemaContext context, IValueTypeAccess type, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders)
        : base(context, type as StructType ?? throw new InvalidCastException($"The type '{type.Name}' is not a valid function call type."), value, parent, propertyProviders)
    {
        ModeField = GetAccessValue("mode") as EnumNode;
        FuncField = GetAccessValue("func") as StringNode;
        ArgsField = GetAccessValue("args") as FuncCallArgsNode;

        if (!FullMode) return;

        if (ModeField is not null)
            RecordSubscription(ModeField.Subscribe(_item => { _ = RefreshArgsAsync(); }), this);
        if (FuncField is not null)
            RecordSubscription(FuncField.Subscribe(_item => { _ = RefreshArgsAsync(); }, true), this);
    }

    /// <summary>
    /// Refresh the function call arguments
    /// </summary>
    private async Task RefreshArgsAsync()
    {
        if (!FullMode || Context is not SchemaContext ctx) return;

        string? func = FuncField?.GetValue<string>();
        FunctionType? funcType = !string.IsNullOrWhiteSpace(func) ? await ctx.GetNodeTypeAsync<FunctionType>(func) : null;
        ArgsField?.RefreshFuncCall(ModeField?.GetValue<ApplyMode>(), funcType);
    }
}
