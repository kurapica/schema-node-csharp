using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;

namespace SchemaNode.Runtime;

/// <summary>
/// The schema expression
/// </summary>
public abstract record SchemaExpression(AnySchemeType SchemaType);

/// <summary>
/// The expression visitor interface
/// </summary>
public interface IExpressionVisitor
{
    SchemaExpression? VisitExpression(SchemaContext context, FuncCallExpression funcCallExp);

    int Priorty { get; }
}

/// <summary>
/// The variable expression
/// </summary>
/// <param name="Name"></param>
/// <param name="SchemeType"></param>
public abstract record VariableExpression(string Name, SchemaExpression Value) : SchemaExpression(Value.SchemaType);

/// <summary>
/// Represents an argument expression with a specified name, index, and associated scheme type.
/// </summary>
/// <param name="Name">The name of the argument represented by this expression. Cannot be null or empty.</param>
/// <param name="Index">The zero-based index of the argument within the containing context. Must be greater than or equal to 0.</param>
/// <param name="SchemeType">The scheme type associated with the argument. Determines the type information for the argument expression.</param>
public record ArgumentExpression(string Name, int Index, AnySchemeType SchemeType) : SchemaExpression(SchemeType);

/// <summary>
/// Represents a function call expression with a specified function, argument expressions, and result type within a
/// schema.
/// </summary>
/// <param name="Function">The function to be invoked by this expression.</param>
/// <param name="Args">An array of expressions representing the arguments to pass to the function. The order of expressions corresponds to
/// the function's parameter order.</param>
/// <param name="SchemeType">The schema type that describes the result of the function call.</param>
public record FuncCallExpression(FunctionType Function, SchemaExpression[] Args, AnySchemeType SchemeType, ExpressionType ExpType = ExpressionType.Call) : SchemaExpression(SchemeType);

/// <summary>
/// The default expression
/// </summary>
public record DefaultExpression(SchemaExpression Inner, AnySchemaNode Default) : SchemaExpression(Default.SchemeType);

/// <summary>
/// The null expression
/// </summary>
public record NullExpression(AnySchemeType SchemeType) : SchemaExpression(SchemeType);
