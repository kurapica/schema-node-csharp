using SchemaNode.Context;
using SchemaNode.Enum;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The collection expression type
/// </summary>
public enum CollectionExpressionType
{
    Map,
    Reduce,
    Filter,
    First,
    Last,

    Any, 
    All,
    Count,
}

/// <summary>
/// Represents a collection operation expression, such as mapping, filtering, or aggregating, applied to a data set
/// within a schema expression tree.
/// </summary>
/// <remarks>Use this record to represent higher-order collection operations in expression trees, enabling complex
/// data transformations over structured data. The specific behavior depends on the combination of the collection
/// operation type and the provided function.</remarks>
/// <param name="Type">The type of collection operation to perform, such as map, filter, or reduce.</param>
/// <param name="Function">The function to apply as part of the collection operation. This defines the transformation or predicate logic.</param>
/// <param name="Args">The arguments to pass to the function as part of the collection operation. Each argument is a schema expression.</param>
/// <param name="Array">The index or identifier of the array or collection to which the operation is applied.</param>
/// <param name="SchemeType">The scheme type that describes the structure or type information of the resulting collection.</param>
public record CollectionExpression(CollectionExpressionType Type, FunctionType Function, SchemaExpression[] Args, int Array, AnySchemeType SchemeType)
    : FuncCallExpression(Function, Args, SchemeType);


/// <summary>
/// The collection expression visitor
/// </summary>
public class CollectionExpressionVisitor: IExpressionVisitor
{
    public int Priority => EXP_COLLECTION_PRIORITY;

    /// <inheritdoc />
    public SchemaExpression? VisitExpression(SchemaContext context, SchemaExpression exp)
    {
        if (exp is not FuncCallExpression funcCallExp) return null;
        if (funcCallExp.ExpType == Enum.ExpressionType.Call) return null;

        var func = funcCallExp.Function;

        // validate call type with function here, not in the main function visitor
        switch (funcCallExp.ExpType)
        {
            case ExpressionType.Reduce when func.Args.Length is 0 or > 2:
                throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncForReduce, TYPE_FUNC_CANT_USE_AS_REDUCE);

            case ExpressionType.First when func.ReturnNode is not ScalarType { IsBool: true }:
                throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncForFirst, TYPE_FUNC_CANT_USE_AS_FIRST);

            case ExpressionType.Last when func.ReturnNode is not ScalarType { IsBool: true }:
                throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncForLast, TYPE_FUNC_CANT_USE_AS_LAST);

            case ExpressionType.Filter when func.ReturnNode is not ScalarType { IsBool: true }:
                throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncForFilter, TYPE_FUNC_CANT_USE_AS_FILTER);
            
            case ExpressionType.Count when func.ReturnNode is not ScalarType { IsBool: true }:
                throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncForCount, TYPE_FUNC_CANT_USE_AS_COUNT);
            
            case ExpressionType.All when func.ReturnNode is not ScalarType { IsBool: true }:
                throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncForAll, TYPE_FUNC_CANT_USE_AS_ALL);
            
            case ExpressionType.Any when func.ReturnNode is not ScalarType { IsBool: true }:
                throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncForAll, TYPE_FUNC_CANT_USE_AS_ANY);
        }
        
        // Index array argument
        int arrayIndex = -1;
        for (int i = 0; i < funcCallExp.Args.Length; i++)
        {
            SchemaExpression arg = funcCallExp.Args[i];
            if (arg.SchemaType is not ArrayType) continue;
            
        }

        return null;
    }
}