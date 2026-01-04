using SchemaNode.Enum;
using SchemaNode.Function;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The assign expression visitor
/// </summary>
public class AssignExpressionVisitor : IExpressionVisitor
{
    public int Priority => EXP_ASSIGN_PRIORITY;

    // <inheritdoc/>
    public async Task<SchemaExpression?> VisitExpAsync(CompileContext context, SchemaExpression exp)
    {
        await Task.Yield();
        
        if (exp is not FuncCallExpression { ExpType: ExpressionType.Call } callExp) return null;

        switch (callExp.Function.Name)
        {
            case $"{NS_SYSTEM_CONV}.{nameof(SystemConv.assign)}":
            {
                return callExp.Args.ElementAtOrDefault(0) 
                       ?? throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
            }
            case $"{NS_SYSTEM_CONV}.{nameof(SystemConv.@default)}":
            {
                ConstantExpression constantExp = callExp.Args.ElementAtOrDefault(1) as ConstantExpression
                                                 ?? throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                return new DefaultExpression(callExp.Args[0], constantExp.Value);
            }
            case $"{NS_SYSTEM_CONV}.{nameof(SystemConv.@null)}":
            {
                return new NullExpression(callExp.SchemaType);
            }
        }
        return null;
    }
}