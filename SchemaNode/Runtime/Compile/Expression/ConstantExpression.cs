using SchemaNode.Node;
using System.Reflection;
using SchemaNode.Enum;

namespace SchemaNode.Runtime;


/// <summary>
/// Represents a schema expression that evaluates to a constant value.
/// </summary>
/// <param name="Value">The schema node representing the constant value for this expression. Cannot be null.</param>
public record ConstantExpression(AnySchemaNode Value) : SchemaExpression(Value.SchemaType);

/// <summary>
/// The attribute to mark a method as constant expression
/// </summary>
/// <param name="value"></param>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class ConstantExpAttribute(object value): System.Attribute
{
    public object Value { get; } = value;
}

/// <summary>
/// The constant expression visitor
/// </summary>
public class ConstantExpressionVisitor : IExpressionVisitor
{
    // <inheritdoc/>
    public int Priority => Utility.Constant.EXP_CONSTANT_PRIORITY;

    // <inheritdoc/>
    public Task<SchemaExpression?> VisitExpAsync(CompileContext context, SchemaExpression exp)
    {
        if (exp is not FuncCallExpression { ExpType: ExpressionType.Call } callExp) return Task.FromResult<SchemaExpression?>(null);
        var attr = callExp.Function.FuncInfo?.Method?.GetCustomAttribute<ConstantExpAttribute>();
        return Task.FromResult<SchemaExpression?>(attr != null ? new ConstantExpression(callExp.SchemaType.CreateNode(attr.Value)!) : null);
    }
}