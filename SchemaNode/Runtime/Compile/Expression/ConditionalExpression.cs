using SchemaNode.Enum;
using SchemaNode.Function;
using static SchemaNode.Utility.Constant;
// ReSharper disable NotAccessedPositionalProperty.Global

namespace SchemaNode.Runtime;

/// <summary>
/// The conditional expression
/// </summary>
/// <param name="Condition">The condition</param>
/// <param name="TrueExp">The true value</param>
/// <param name="FalseExp">The false value</param>
public record ConditionalExpression(SchemaExpression Condition, SchemaExpression TrueExp, SchemaExpression FalseExp, AnySchemaType SchemaType) : SchemaExpression(SchemaType);

public class ConditionalExpressionVisitor : IExpressionVisitor
{
    public int Priority => EXP_CONDITIONAL_PRIORITY;

    // <inheritdoc/>
    public Task<SchemaExpression?> VisitExpAsync(CompileContext context, SchemaExpression exp)
    {
        if (exp is not FuncCallExpression { ExpType: ExpressionType.Call } callExp ||
            !callExp.Function.Name.Equals($"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.cond)}")) return Task.FromResult<SchemaExpression?>(null);
      
        if (callExp.Args.Length != 3 || 
            !(callExp.Args[0].SchemaType is ScalarType { IsBool: true }) ||
            !callExp.Args[1].SchemaType.CanBeUseAs(exp.SchemaType) ||
            !callExp.Args[2].SchemaType.CanBeUseAs(exp.SchemaType))
            throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
        return Task.FromResult<SchemaExpression?>(new ConditionalExpression(callExp.Args[0], callExp.Args[1], callExp.Args[2], exp.SchemaType));
    }
}