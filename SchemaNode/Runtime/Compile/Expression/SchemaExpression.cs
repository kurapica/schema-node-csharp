using System.Linq.Expressions;
using SchemaNode.Context;
using SchemaNode.Node;
using ExpressionType = SchemaNode.Enum.ExpressionType;

// ReSharper disable NotAccessedPositionalProperty.Global

namespace SchemaNode.Runtime;

/// <summary>
/// The schema expression
/// </summary>
public abstract record SchemaExpression(AnySchemaType SchemaType);

/// <summary>
/// The expression visitor interface
/// </summary>
public interface IExpressionVisitor
{
    /// <summary>
    /// The visitor priority
    /// </summary>
    int Priority { get; }
    
    /// <summary>
    /// Visit the expression and re-write
    /// </summary>
    SchemaExpression? VisitExpression(SchemaContext context, SchemaExpression exp);

    /// <summary>
    /// Compile the expression to Expression
    /// </summary>
    virtual Expression? CompileExpression(CompileContext context, SchemaExpression exp) => null;
}

/// <summary>
/// The variable expression
/// </summary>
/// <param name="Name"></param>
/// <param name="Value"></param>
public record VariableExpression(string Name, SchemaExpression Value) : SchemaExpression(Value.SchemaType);

/// <summary>
/// Represents an argument expression with a specified name, index, and associated scheme type.
/// </summary>
/// <param name="Index">The zero-based index of the argument within the containing context. Must be greater than or equal to 0.</param>
/// <param name="SchemaType">The scheme type associated with the argument. Determines the type information for the argument expression.</param>
public record ArgumentExpression(string Name, int Index, AnySchemaType SchemaType) : SchemaExpression(SchemaType);

/// <summary>
/// The default expression
/// </summary>
/// <param name="Inner"></param>
/// <param name="Default"></param>
public record DefaultExpression(SchemaExpression Inner, AnySchemaNode Default) : SchemaExpression(Default.SchemaType);

/// <summary>
/// The null expression
/// </summary>
public record NullExpression(AnySchemaType SchemaType) : SchemaExpression(SchemaType);

/// <summary>
/// The params expression type
/// </summary>
/// <param name="Exps"></param>
/// <param name="SchemaType"></param>
public record ParamsExpression(SchemaExpression[] Exps, AnySchemaType SchemaType) : SchemaExpression(SchemaType);

/// <summary>
/// The iterator expression represents an iteration over an array within a schema expression tree.
/// </summary>
/// <param name="Array">The array expression</param>
public record IteratorExpression(SchemaExpression Array) : SchemaExpression((Array as FieldAccessExpression)?.SchemaType ?? (Array.SchemaType as ArrayType)!.ElementSchemaType!);

/// <summary>
/// The struct field expression
/// </summary>
/// <param name="Name">The struct field name</param>
/// <param name="Expression">The expression</param>
public record StructFieldExpression(string Name, SchemaExpression Expression): SchemaExpression(Expression.SchemaType);

/// <summary>
/// The struct result expression
/// </summary>
/// <param name="Fields">The struct field members</param>
/// <param name="SchemaType">The struct schema type</param>
public record StructResultExpression(StructFieldExpression[] Fields, AnySchemaType SchemaType) : SchemaExpression(SchemaType);

/// <summary>
/// Represents a function call expression with a specified function, argument expressions, and result type within a
/// schema.
/// </summary>
/// <param name="Function">The function to be invoked by this expression.</param>
/// <param name="Args">An array of expressions representing the arguments to pass to the function. The order of expressions corresponds to
/// the function's parameter order.</param>
/// <param name="SchemaType">The schema type that describes the result of the function call.</param>
public record FuncCallExpression(FunctionType Function, SchemaExpression[] Args, AnySchemaType SchemaType, ExpressionType ExpType = ExpressionType.Call) : SchemaExpression(SchemaType);
