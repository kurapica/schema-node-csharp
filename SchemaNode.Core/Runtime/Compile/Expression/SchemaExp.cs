using System.Linq.Expressions;
using ExpressionType = SchemaNode.Enum.ExpType;

// ReSharper disable NotAccessedPositionalProperty.Global

namespace SchemaNode.Runtime;

/// <summary>
/// The schema expression
/// </summary>
public abstract record SchemaExp(ValueType ValueType);

/// <summary>
/// The expression visitor interface
/// </summary>
public interface IExpVisitor
{
    /// <summary>
    /// The visitor priority
    /// </summary>
    int Priority { get; }
    
    /// <summary>
    /// Visit the expression and re-write
    /// </summary>
    Task<SchemaExp?> VisitExpAsync(CompileContext context, SchemaExp exp);

    /// <summary>
    /// Compile the expression to Expression
    /// </summary>
    Task<Expression?> CompileExpAsync(CompileContext context, SchemaExp exp, Type expectedType) => Task.FromResult<Expression?>(null);
}

/// <summary>
/// The variable expression
/// </summary>
/// <param name="Name"></param>
/// <param name="Value"></param>
public record VariableExp(string Name, SchemaExp Value) : SchemaExp(Value.ValueType);

/// <summary>
/// Represents an argument expression with a specified name, index, and associated scheme type.
/// </summary>
/// <param name="Index">The zero-based index of the argument within the containing context. Must be greater than or eq to 0.</param>
/// <param name="ValueType">The scheme type associated with the argument. Determines the type information for the argument expression.</param>
public record ArgumentExp(string Name, int Index, bool Require, ValueType ValueType) : SchemaExp(ValueType);

/// <summary>
/// The params expression type
/// </summary>
/// <param name="Exps"></param>
/// <param name="ValueType"></param>
public record ParamsExp(SchemaExp[] Exps, ValueType ValueType) : SchemaExp(ValueType);

/// <summary>
/// The struct field expression
/// </summary>
/// <param name="Name">The struct field name</param>
/// <param name="Expression">The expression</param>
public record StructFieldExp(string Name, SchemaExp Expression): SchemaExp(Expression.ValueType);

/// <summary>
/// The struct result expression
/// </summary>
/// <param name="Fields">The struct field members</param>
/// <param name="ValueType">The struct schema type</param>
public record StructResultExp(StructFieldExp[] Fields, ValueType ValueType) : SchemaExp(ValueType);

/// <summary>
/// Represents a function call expression with a specified function, argument expressions, and result type within a
/// schema.
/// </summary>
/// <param name="Function">The function to be invoked by this expression.</param>
/// <param name="Args">An array of expressions representing the arguments to pass to the function. The order of expressions corresponds to
/// the function's parameter order.</param>
/// <param name="ValueType">The schema type that describes the result of the function call.</param>
/// <param name="ExpType">The collection expression type</param>
public record FuncCallExp(FunctionType Function, SchemaExp[] Args, ValueType ValueType, ExpressionType ExpType = ExpressionType.Call) : SchemaExp(ValueType);
