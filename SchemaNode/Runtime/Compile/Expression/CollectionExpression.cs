using SchemaNode.Node;
using SchemaNode.Utility;
using System.Linq.Expressions;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The loop argument expression
/// </summary>
public record LoopArgExpression(AnySchemaType SchemaType) : SchemaExpression(SchemaType);

/// <summary>
/// The collection expression
/// </summary>
public record CollectionExpression(Enum.ExpressionType Type, SchemaExpression Iterator, LoopArgExpression LoopArg, SchemaExpression Expression, AnySchemaType SchemaType) : SchemaExpression(SchemaType);

/// <summary>
/// The reduce sum argument expression
/// </summary>
public record ReduceSumExpression(SchemaExpression Init, AnySchemaType SchemaType) : SchemaExpression(SchemaType);

/// <summary>
/// The reduce collection expression
/// </summary>
public record ReduceCollectionExpression(Enum.ExpressionType Type, SchemaExpression Iterator, LoopArgExpression LoopArg, ReduceSumExpression Sum, SchemaExpression Expression, AnySchemaType SchemaType): CollectionExpression(Type, Iterator, LoopArg, Expression, SchemaType);

/// <summary>
/// The collection expression visitor
/// </summary>
public class CollectionExpressionVisitor : IExpressionVisitor
{
    // <inheritdoc/>
    public int Priority => EXP_COLLECTION_PRIORITY;

    // <inheritdoc/>
    public async Task<SchemaExpression?> VisitExpAsync(CompileContext context, SchemaExpression exp)
    {
        if (exp is not FuncCallExpression funcExp || funcExp.ExpType == Enum.ExpressionType.Call) return null;

        IteratorExpression iter = funcExp.Args.FirstOrDefault(a => a is IteratorExpression) as IteratorExpression
            ?? throw new FunctionVisitException(Enum.SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
        
        SchemaExpression iterSource = iter.Array is FieldAccessExpression fieldAccess
            ? fieldAccess.Owner
            : iter.Array;

        // Replace all collection iterator with loop argument
        LoopArgExpression loopExp = new LoopArgExpression((iterSource.SchemaType as ArrayType)?.ElementSchemaType
            ?? throw new FunctionVisitException(Enum.SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID));
        
        if (funcExp.ExpType != Enum.ExpressionType.Reduce)
        {
            return new CollectionExpression(
                funcExp.ExpType,
                iterSource,
                loopExp,
                // Map the function call to schema expression if possible
                await context.VisitSchemaExpAsync(new FuncCallExpression(
                    funcExp.Function,
                    // replace iterator to loop argument
                    funcExp.Args.Select(a => a switch
                    {
                        IteratorExpression it =>  it.Array switch
                        {
                            FieldAccessExpression fldAcc => new FieldAccessExpression(loopExp, fldAcc.FieldName, fldAcc.SchemaType),
                            _ => loopExp
                        },
                        _ => a
                    }).ToArray(),
                    funcExp.ExpType switch
                    {
                        Enum.ExpressionType.Map => (funcExp.SchemaType as ArrayType)!.ElementSchemaType!,
                        Enum.ExpressionType.Reduce => funcExp.SchemaType,
                        // Filter, First, Last, Count, All, Any
                        _ => (await context.GetSchemaTypeAsync(NS_SYSTEM_BOOL))!,
                    }
                )),
                funcExp.SchemaType
            );
        }

        // Handle reduce expression
        ReduceSumExpression sumExp = new ReduceSumExpression(funcExp.Args.FirstOrDefault(a => a != iter) ?? new NullExpression(funcExp.SchemaType), funcExp.SchemaType);
        return new ReduceCollectionExpression(
            funcExp.ExpType,
            iterSource,
            loopExp,
            sumExp,
            // Map the function call to schema expression if possible
            await context.VisitSchemaExpAsync(new FuncCallExpression(
                funcExp.Function,
                funcExp.Args.Select(a => (SchemaExpression)(a switch
                {
                    IteratorExpression it =>  it.Array switch
                    {
                        FieldAccessExpression fldAcc => new FieldAccessExpression(loopExp, fldAcc.FieldName, fldAcc.SchemaType),
                        _ => loopExp
                    },
                    _ => sumExp
                })).ToArray(),
                funcExp.SchemaType
            )),
            funcExp.SchemaType
        );
    }

    // <inheritdoc/>
    public async Task<Expression?> CompileExpAsync(CompileContext context, SchemaExpression exp)
    {
        if (exp is not CollectionExpression colExp) return null;

        ParameterExpression start = Expression.Variable(typeof(int), "_start");
        ParameterExpression stop = Expression.Variable(typeof(int), "_stop");
        LabelTarget forLabel = Expression.Label(typeof(int));
        Expression arrayLen;

        Expression indexExp = colExp.Type == Enum.ExpressionType.Last ? Expression.PreDecrementAssign(start) : Expression.PostIncrementAssign(start);
        Expression iteratorExp = await context.CompileSchemaExpAsync(colExp.Iterator);
        ParameterExpression iterExp = Expression.Variable(iteratorExp.Type, "_iter");
        Expression iterator = iterExp;

        if (iterator.Type.IsSZArray)
        {
            // array[start++]
            arrayLen = Expression.ArrayLength(iterExp);
            iterator = Expression.ArrayIndex(iterExp, indexExp);
        }
        else
        {
            // array.get_item(start++)
            arrayLen = Expression.Property(iterExp, "Count");
            iterator = Expression.MakeIndex(iterExp, iterExp.Type.GetProperty("Item", [typeof(int)])!, [indexExp]);
        }

        Type expReturnType = exp.SchemaType.ToCSharpType();
        context.SetCompiledExpression(colExp.LoopArg, iterator);

        // Handle different collection expression types
        switch (colExp.Type)
        {
            case Enum.ExpressionType.Map:
            {
                // Compile loop
                Expression callMethod = await context.CompileSchemaExpAsync(colExp.Expression);

                // Generate result expression
                ParameterExpression resultExp = Expression.Variable(expReturnType.IsArrayType() ? expReturnType : typeof(ArrayTypeNode));

                return Expression.Block(
                    [iterExp, resultExp, start, stop],
                    Expression.Assign(iterExp, iteratorExp),
                    Expression.Assign(resultExp, resultExp.Type == typeof(ArrayTypeNode)
                        ? Expression.New(resultExp.Type.GetConstructors()[0], Expression.Constant(exp.SchemaType), Expression.Constant(null))
                        : Expression.New(resultExp.Type)),
                    Expression.Assign(start, Expression.Constant(0, typeof(int))),
                    Expression.Assign(stop, arrayLen),
                    Expression.Loop(
                        Expression.IfThenElse(
                            Expression.LessThan(start, stop),
                            resultExp.Type.IsArrayType()
                                ? callMethod.Type.IsArrayType()
                                    ? Expression.Call(resultExp, resultExp.Type.GetMethod("AddRange", [typeof(IEnumerable<>).MakeGenericType(expReturnType)
                                    ])!, callMethod)
                                    : Expression.Call(resultExp, resultExp.Type.GetMethod("Add")!, callMethod)
                                : callMethod.Type == typeof(ArrayTypeNode)
                                    ? Expression.Call(resultExp, typeof(ArrayTypeNode).GetMethod(nameof(ArrayTypeNode.AddRange))!, callMethod)
                                    : Expression.Call(resultExp, typeof(ArrayTypeNode).GetMethod(nameof(ArrayTypeNode.Add))!, callMethod),
                            Expression.Break(forLabel, stop)
                        ),
                        forLabel
                    ),
                    resultExp
                );
            }

            case Enum.ExpressionType.Filter:
            {
                // Compile loop
                Expression temp = iterator;
                ParameterExpression curr = Expression.Parameter(temp.Type, "_curr");
                context.SetCompiledExpression(colExp.LoopArg, curr);
                Expression callMethod = await context.CompileSchemaExpAsync(colExp.Expression);

                // Generate result expression
                ParameterExpression resultExp = Expression.Variable(expReturnType.IsArrayType() ? expReturnType : typeof(ArrayTypeNode));

                return Expression.Block(
                    [iterExp, resultExp, start, stop, curr],
                    Expression.Assign(iterExp, iteratorExp),
                    Expression.Assign(resultExp, resultExp.Type == typeof(ArrayTypeNode)
                        ? Expression.New(resultExp.Type.GetConstructors()[0], Expression.Constant(exp.SchemaType), Expression.Constant(null))
                        : Expression.New(resultExp.Type)),
                    Expression.Assign(start, Expression.Constant(0, typeof(int))),
                    Expression.Assign(stop, arrayLen),
                    Expression.Loop(
                        Expression.IfThenElse(
                            Expression.LessThan(start, stop),
                            Expression.Block(new List<Expression>()
                            {
                                Expression.Assign(curr, temp),
                                Expression.IfThen(callMethod,
                                    Expression.Call(resultExp, resultExp.Type.GetMethod("Add")!, curr)
                                )
                            }),
                            Expression.Break(forLabel, stop)
                        ),
                        forLabel
                    ),
                    resultExp
                );
            }

            case Enum.ExpressionType.Reduce:
            {
                ReduceCollectionExpression reduceExp = (colExp as ReduceCollectionExpression)!;

                // Compile loop
                ParameterExpression resultExp = Expression.Variable(expReturnType);

                // Replace the sum exp
                context.SetCompiledExpression(reduceExp.Sum, resultExp);
                Expression callMethod = await context.CompileSchemaExpAsync(colExp.Expression);

                // Compile
                return Expression.Block(
                    [iterExp, resultExp, start, stop],
                    Expression.Assign(iterExp, iteratorExp),
                    Expression.Assign(start, Expression.Constant(0, typeof(int))),
                    Expression.Assign(stop, arrayLen),
                    Expression.Assign(resultExp, Expression.Coalesce(await context.CompileSchemaExpAsync(reduceExp.Sum), reduceExp.Sum.Init is NullExpression 
                        ? Expression.Default(expReturnType)
                        : await context.CompileSchemaExpAsync(reduceExp.Sum.Init))),
                    Expression.Loop(
                        Expression.IfThenElse(
                            Expression.LessThan(start, stop),
                            Expression.Assign(resultExp, callMethod),
                            Expression.Break(forLabel, stop)
                        ),
                        forLabel
                    ),
                    resultExp
                );
            }

            case Enum.ExpressionType.First:
            {
                ParameterExpression resultExp = Expression.Variable(iterator.Type);

                // Replace the call args
                Expression temp = iterator;
                context.SetCompiledExpression(colExp.LoopArg, resultExp);
                Expression callMethod = await context.CompileSchemaExpAsync(colExp.Expression);

                // New init parameter
                ParameterExpression init = Expression.Parameter(resultExp.Type, "_init");

                // Compile
                return Expression.Block(
                    [iterExp, resultExp, start, stop, init],
                    Expression.Assign(iterExp, iteratorExp),
                    Expression.Assign(start, Expression.Constant(0, typeof(int))),
                    Expression.Assign(stop, arrayLen),
                    Expression.Assign(init, Expression.Default(iterator.Type)),
                    Expression.Assign(resultExp, init),
                    Expression.Loop(
                        Expression.IfThenElse(
                            Expression.LessThan(start, stop),
                            Expression.Block(
                                Expression.Assign(resultExp, temp),
                                Expression.IfThenElse(callMethod,
                                    Expression.Break(forLabel, stop),
                                    Expression.Assign(resultExp, init))
                            ),
                            Expression.Break(forLabel, stop)
                        ),
                        forLabel
                    ),
                    resultExp
                );
            }
            case Enum.ExpressionType.Last:
            {
                ParameterExpression resultExp = Expression.Variable(iterator.Type);

                // Replace the call args
                Expression temp = iterator;
                context.SetCompiledExpression(colExp.LoopArg, resultExp);
                Expression callMethod = await context.CompileSchemaExpAsync(colExp.Expression);

                // New init parameter
                ParameterExpression init = Expression.Parameter(resultExp.Type, "_init");

                // Compile
                return Expression.Block(
                    [iterExp, resultExp, start, stop, init],
                    Expression.Assign(iterExp, iteratorExp),
                    Expression.Assign(stop, Expression.Constant(0, typeof(int))),
                    Expression.Assign(start, arrayLen),
                    Expression.Assign(init, Expression.Default(iterator.Type)),
                    Expression.Assign(resultExp, init),
                    Expression.Loop(
                        Expression.IfThenElse(
                            Expression.GreaterThan(start, stop),
                            Expression.Block(new List<Expression>()
                            {
                                Expression.Assign(resultExp, temp),
                                Expression.IfThenElse(callMethod,
                                    Expression.Break(forLabel, stop),
                                    Expression.Assign(resultExp, init))
                            }),
                            Expression.Break(forLabel, stop)
                        ),
                        forLabel
                    ),
                    resultExp
                );
            }

            case Enum.ExpressionType.Count:
            {
                ParameterExpression resultExp = Expression.Variable(typeof(int));
                Expression callMethod = await context.CompileSchemaExpAsync(colExp.Expression);

                return Expression.Block(
                    [iterExp, resultExp, start, stop],
                    Expression.Assign(iterExp, iteratorExp),
                    Expression.Assign(resultExp, Expression.Constant(0, typeof(int))),
                    Expression.Assign(start, Expression.Constant(0, typeof(int))),
                    Expression.Assign(stop, arrayLen),
                    Expression.Loop(
                        Expression.IfThenElse(
                            Expression.LessThan(start, stop),
                            Expression.Block(new List<Expression>()
                            {
                                Expression.IfThen(callMethod,
                                    Expression.PostIncrementAssign(resultExp)
                                )
                            }),
                            Expression.Break(forLabel, stop)
                        ),
                        forLabel
                    ),
                    resultExp
                );
            }
            case Enum.ExpressionType.All:
            {
                ParameterExpression resultExp = Expression.Variable(typeof(bool));
                Expression callMethod = await context.CompileSchemaExpAsync(colExp.Expression);

                // Compile
                return Expression.Block(
                    [iterExp, resultExp, start, stop],
                    Expression.Assign(iterExp, iteratorExp),
                    Expression.Assign(start, Expression.Constant(0, typeof(int))),
                    Expression.Assign(stop, arrayLen),
                    Expression.Assign(resultExp, Expression.Constant(true, typeof(bool))),
                    Expression.Loop(
                        Expression.IfThenElse(
                            Expression.LessThan(start, stop),
                            Expression.Block(new List<Expression>()
                            {
                                Expression.Assign(resultExp, callMethod),
                                Expression.IfThen(Expression.Not(resultExp),
                                    Expression.Break(forLabel, stop))
                            }),
                            Expression.Break(forLabel, stop)
                        ),
                        forLabel
                    ),
                    resultExp
                );
            }
            case Enum.ExpressionType.Any:
            {
                ParameterExpression resultExp = Expression.Variable(typeof(bool));
                Expression callMethod = await context.CompileSchemaExpAsync(colExp.Expression);

                // Compile
                return Expression.Block(
                    [iterExp, resultExp, start, stop],
                    Expression.Assign(iterExp, iteratorExp),
                    Expression.Assign(start, Expression.Constant(0, typeof(int))),
                    Expression.Assign(stop, arrayLen),
                    Expression.Assign(resultExp, Expression.Constant(false, typeof(bool))),
                    Expression.Loop(
                        Expression.IfThenElse(
                            Expression.LessThan(start, stop),
                            Expression.Block(new List<Expression>()
                            {
                                Expression.Assign(resultExp, callMethod),
                                Expression.IfThen(resultExp,
                                    Expression.Break(forLabel, stop))
                            }),
                            Expression.Break(forLabel, stop)
                        ),
                        forLabel
                    ),
                    resultExp
                );
            }
        }

        return null;
    }
}