using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;


public class AssignExpressionVisitor : IExpressionVisitor
{
    public int Priority => EXP_ASSIGN_PRIORITY;

    // <inheritdoc/>
    public SchemaExpression? VisitExpression(SchemaContext context, SchemaExpression exp)
    {
        if (exp is not FuncCallExpression callExp) return null;

        switch (callExp.Function.Name)
        {
            case $"{NS_SYSTEM_CONV}.{nameof(SystemConv.assign)}":
            {
                return callExp.Args.ElementAtOrDefault(0);
            }
            case $"{NS_SYSTEM_CONV}.{nameof(SystemConv.@default)}":
            {
                var constantExp = callExp.Args.ElementAtOrDefault(1) as ConstantExpression;
                if (constantExp == null)
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
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