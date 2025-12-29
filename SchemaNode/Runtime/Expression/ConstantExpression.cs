using SchemaNode.Context;
using SchemaNode.Node;
using System.Reflection;

namespace SchemaNode.Runtime;


/// <summary>
/// Represents a schema expression that evaluates to a constant value.
/// </summary>
/// <param name="Value">The schema node representing the constant value for this expression. Cannot be null.</param>
public record ConstantExpression(AnySchemaNode Value) : SchemaExpression(Value.SchemeType);

/// <summary>
/// The attribute to mark a method as constant expression
/// </summary>
/// <param name="value"></param>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class ConstantExpAttribute(object value): System.Attribute
{
    public object Value { get; } = value;
}

/// <summary>
/// The constant expression visitor
/// </summary>
public class ConstantExpressionVisitor : IExpressionVisitor
{
    public int Priority { get; } = Utility.Constant.EXP_CONSTANT_PRIORITY;

    // <inheritdoc/>
    public SchemaExpression? VisitExpression(SchemaContext context, SchemaExpression exp)
    {
        if (exp is not FuncCallExpression funcCallExp) return null;
        MethodInfo? info = funcCallExp.Function?.FuncInfo?.Method;
        var attr = info?.GetCustomAttribute<ConstantExpAttribute>();
        return attr != null ? new ConstantExpression(funcCallExp.SchemaType.CreateNode(attr.Value)!) : null;
    }
}