using System.Linq.Expressions;
using ExpressionType = SchemaNode.Enum.ExpressionType;

// ReSharper disable NotAccessedPositionalProperty.Global

namespace SchemaNode.Runtime;

/// <summary>
/// The schema expression
/// </summary>
public abstract record SchemaExp(NodeType NodeType);

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
public record VariableExp(string Name, SchemaExp Value) : SchemaExp(Value.NodeType);

/// <summary>
/// Represents an argument expression with a specified name, index, and associated scheme type.
/// </summary>
/// <param name="Index">The zero-based index of the argument within the containing context. Must be greater than or eq to 0.</param>
/// <param name="SchemaType">The scheme type associated with the argument. Determines the type information for the argument expression.</param>
public record ArgumentExp(string Name, int Index, bool Nullable, NodeType NodeType) : SchemaExp(NodeType);

/// <summary>
/// The params expression type
/// </summary>
/// <param name="Exps"></param>
/// <param name="SchemaType"></param>
public record ParamsExp(SchemaExp[] Exps, NodeType NodeType) : SchemaExp(NodeType);

/// <summary>
/// The struct field expression
/// </summary>
/// <param name="Name">The struct field name</param>
/// <param name="Expression">The expression</param>
public record StructFieldExp(string Name, SchemaExp Expression): SchemaExp(Expression.NodeType);

/// <summary>
/// The struct result expression
/// </summary>
/// <param name="Fields">The struct field members</param>
/// <param name="SchemaType">The struct schema type</param>
public record StructResultExp(StructFieldExp[] Fields, NodeType NodeType) : SchemaExp(NodeType);

/// <summary>
/// Represents a function call expression with a specified function, argument expressions, and result type within a
/// schema.
/// </summary>
/// <param name="Function">The function to be invoked by this expression.</param>
/// <param name="Args">An array of expressions representing the arguments to pass to the function. The order of expressions corresponds to
/// the function's parameter order.</param>
/// <param name="SchemaType">The schema type that describes the result of the function call.</param>
/// <param name="ExpType">The collection expression type</param>
public record FuncCallExp(FunctionType Function, SchemaExp[] Args, NodeType NodeType, ExpressionType ExpType = ExpressionType.Call) : SchemaExp(NodeType);
