using SchemaNode.Node;
using SchemaNode.Utility;
using System.Collections;
using System.Linq.Expressions;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;
using ExpressionType = SchemaNode.Enum.ExpressionType;

namespace SchemaNode.Runtime;

#region Collection Expressions

/// <summary>
/// The iterator expression represents an iteration over an array within a schema expression tree.
/// </summary>
public record CollectionRootExp(SchemaExp Collection, AnySchemaType SchemaType) : SchemaExp(SchemaType);

/// <summary>
/// The loop argument exp used to replace the collection source in the function call
/// </summary>
public record CollectionItemExp(CollectionRootExp Root, AnySchemaType SchemaType) : SchemaExp(SchemaType);

/// <summary>
/// The collection result expression
/// </summary>
public abstract record CollectionResult(CollectionRootExp Root, AnySchemaType SchemaType) : SchemaExp(SchemaType);

/// <summary>
/// The collection expression
/// </summary>
public abstract record CollectionOperator(CollectionRootExp Root, AnySchemaType SchemaType): CollectionRootExp(Root, SchemaType);

/// <summary>
/// The predicate collection expression
/// </summary>
public record PredicateCollectionOperator(CollectionRootExp Root, CollectionItemExp Item, SchemaExp Predicate, AnySchemaType SchemaType)
    : CollectionOperator(Root, SchemaType);

/// <summary>
/// Order by collection expression
/// </summary>
public record OrderByCollectionOperator(CollectionRootExp Root,  string OrderField, bool Descending, AnySchemaType SchemaType)
    : CollectionOperator(Root, SchemaType);

/// <summary>
/// The take collection expression
/// </summary>
public record TakeCollectionOperator(CollectionRootExp Root, SchemaExp Take, AnySchemaType SchemaType)
    : CollectionOperator(Root, SchemaType);

/// <summary>
/// The skip collection expression
/// </summary>
public record SkipCollectionOperator(CollectionRootExp Root, SchemaExp Skip, AnySchemaType SchemaType)
    : CollectionOperator(Root, SchemaType);

public abstract record PredicateCollectionResult(CollectionRootExp Root, AnySchemaType SchemaType, CollectionItemExp? Item = null, SchemaExp? Predicate = null)
    : CollectionResult(Root, SchemaType);

/// <summary>
/// The count collection expression
/// </summary>
public record CountCollectionResult(CollectionRootExp Root, AnySchemaType SchemaType, CollectionItemExp? Item = null, SchemaExp? Predicate = null)
    : PredicateCollectionResult(Root, SchemaType, Item, Predicate);

/// <summary>
/// The any collection expression
/// </summary>
public record AnyCollectionResult(CollectionRootExp Root, AnySchemaType SchemaType, CollectionItemExp? Item = null, SchemaExp? Predicate = null)
    : PredicateCollectionResult(Root, SchemaType, Item, Predicate);

/// <summary>
/// The all collection expression
/// </summary>
public record AllCollectionResult(CollectionRootExp Root, AnySchemaType SchemaType, CollectionItemExp? Item = null, SchemaExp? Predicate = null)
    : PredicateCollectionResult(Root, SchemaType, Item, Predicate);

/// <summary>
/// The first collection expression
/// </summary>
public record FirstCollectionResult(CollectionRootExp Root, AnySchemaType SchemaType, CollectionItemExp? Item = null, SchemaExp? Predicate = null)
    : PredicateCollectionResult(Root, SchemaType, Item, Predicate);

/// <summary>
/// The last collection expression
/// </summary>
public record LastCollectionResult(CollectionRootExp Root, AnySchemaType SchemaType, CollectionItemExp? Item = null, SchemaExp? Predicate = null)
    : PredicateCollectionResult(Root, SchemaType, Item, Predicate);

/// <summary>
/// The fields collection expression
/// </summary>
public record FieldsCollectionResult(CollectionRootExp Root, string Field, AnySchemaType SchemaType): CollectionResult(Root, SchemaType);

/// <summary>
/// The reduce sum argument expression to be used as the accumulator in reduce expression
/// </summary>
public record ReduceSumExp(SchemaExp Init, AnySchemaType SchemaType) : SchemaExp(SchemaType);

/// <summary>
/// The reduce collection expression
/// </summary>
public record ReduceCollectionResult(CollectionRootExp Root, CollectionItemExp Item, ReduceSumExp Sum, SchemaExp Expression, AnySchemaType SchemaType)
    : CollectionResult(Root, SchemaType);

/// <summary>
/// The map collection expression
/// </summary>
public record MapCollectionResult(CollectionRootExp Root, CollectionItemExp Item, SchemaExp Expression, AnySchemaType SchemaType)
    : CollectionResult(Root, SchemaType);

#endregion

/// <summary>
/// The collection expression visitor
/// </summary>
public class CollectionExpVisitor : IExpVisitor
{
    // <inheritdoc/>
    public int Priority => EXP_COLLECTION_PRIORITY;

    // <inheritdoc/>
    public async Task<SchemaExp?> VisitExpAsync(CompileContext context, SchemaExp exp)
    {
        if (exp is not FuncCallExp funcExp) return null;

        // Handle final collection call
        if (funcExp.ExpType == ExpressionType.Call)
        {
            CollectionRootExp? sourceExp = funcExp.Args.FirstOrDefault(a => a is CollectionRootExp) as CollectionRootExp;
            if (sourceExp == null) return null;
            
            switch (funcExp.Function.Name)
            {
                // getFields(source)
                case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.getfields)}":
                {
                    string fieldName = funcExp.Args.ElementAtOrDefault(1) is ConstantExp fieldExp ? fieldExp.Value.ToValue<string>() ?? "" : "";
                    if (string.IsNullOrEmpty(fieldName) || sourceExp.SchemaType is not ArrayType { ElementSchemaType: StructType structType })
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
                    AnySchemaType type = structType;
                    string[] paths = fieldName.Split('.', StringSplitOptions.RemoveEmptyEntries);
                    foreach (string path in paths)
                    {
                        StructFieldSchema field = (type as StructType)?.GetField(path) ?? throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
                        type = field.SchemeType ?? throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
                    }
                    
                    return new FieldsCollectionResult(sourceExp, fieldName, context.GetArrayType(type) ?? throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs));
                }
            
                // source.length
                case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.length)}":
                    return new CountCollectionResult(sourceExp, SchemaContext.SystemInt);
            
                // source.OrderBy(field, desc)
                case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.orderby)}":
                {
                    string orderField = funcExp.Args.ElementAtOrDefault(1) is ConstantExp fieldExp ? fieldExp.Value.ToValue<string>() ?? "" : "";
                    bool descending = funcExp.Args.ElementAtOrDefault(2) is ConstantExp descExp && descExp.Value.ToValue<bool>();

                    if (string.IsNullOrEmpty(orderField) || sourceExp.SchemaType is not ArrayType { ElementSchemaType: StructType structType } || structType.GetField(orderField) == null)
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
                
                    return new OrderByCollectionOperator(sourceExp, orderField, descending, sourceExp.SchemaType);
                }
            
                // source.skip(n)
                case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.skip)}":
                    return new SkipCollectionOperator(sourceExp, funcExp.Args.ElementAtOrDefault(1)!, sourceExp.SchemaType);
            
                // source.take(n)
                case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.take)}":
                    return new TakeCollectionOperator(sourceExp, funcExp.Args.ElementAtOrDefault(1)!, sourceExp.SchemaType);
                
                default:
                    return null;
            }
        }
        
        // For non-call expression
        SchemaExp iterArg = funcExp.Args.FirstOrDefault(a => a is CollectionItemExp or FieldAccessExp{ Owner: CollectionItemExp})
            ?? throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
        
        CollectionItemExp item = iterArg is CollectionItemExp it ? it : (iterArg as FieldAccessExp)!.Owner as CollectionItemExp
            ?? throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);

        CollectionRootExp source = item.Root;

        switch (funcExp.ExpType)
        {
            // Try to merge the predicate
            case ExpressionType.Filter when source is PredicateCollectionOperator pre:
                return new PredicateCollectionOperator(pre.Root, pre.Item,
                    new BinaryLogicExp(LogicType.AndAlso, pre.Predicate, await context.VisitSchemaExpAsync(
                        // combine the iterator
                        new FuncCallExp(funcExp.Function, funcExp.Args.Select(a => a switch
                        {
                            CollectionItemExp => pre.Item,
                            FieldAccessExp { Owner: CollectionItemExp } fldAcc => new FieldAccessExp(pre.Item, fldAcc.FieldName, fldAcc.SchemaType),
                            _ => a
                        }).ToArray(), pre.Predicate.SchemaType)), pre.Predicate.SchemaType),
                    funcExp.SchemaType);
            
            // Default filter
            case ExpressionType.Filter:
                return new PredicateCollectionOperator(source, item,
                    await context.VisitSchemaExpAsync(new FuncCallExp(funcExp.Function, funcExp.Args, SchemaContext.SystemBool)), 
                    funcExp.SchemaType); 
            
            // Reduce the collection source
            case ExpressionType.Reduce:
            {
                ReduceSumExp sumExp = new ReduceSumExp(funcExp.Args.FirstOrDefault(a => a != iterArg) 
                                                       ?? new NullExp(funcExp.SchemaType), funcExp.SchemaType);
                return new ReduceCollectionResult(source, item, sumExp,
                    // Map the function call to schema expression if possible
                    await context.VisitSchemaExpAsync(new FuncCallExp(
                        funcExp.Function,
                        funcExp.Args.Select(a => a == iterArg ? iterArg : sumExp).ToArray(),
                        funcExp.SchemaType
                    )),
                    funcExp.SchemaType
                );
            }
            
            case ExpressionType.Map:
                switch (funcExp.Function.Name)
                {
                    // getField(source, field), cover the case to FieldsDataSourceExpression
                    case $"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.getfield)}":
                    {
                        string fieldName = funcExp.Args.ElementAtOrDefault(1) is ConstantExp fieldExp ? fieldExp.Value.ToValue<string>() ?? "" : "";
                        if (string.IsNullOrEmpty(fieldName) || source.SchemaType is not ArrayType { ElementSchemaType: StructType structType })
                            throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
                        AnySchemaType type = structType;
                        string[] paths = fieldName.Split('.', StringSplitOptions.RemoveEmptyEntries);
                        foreach (string path in paths)
                        {
                            StructFieldSchema field = (type as StructType)?.GetField(path) ?? throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
                            type = field.SchemeType ?? throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs);
                        }
                    
                        return new FieldsCollectionResult(source, fieldName, context.GetArrayType(type) ?? throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs));
                    }

                    // assign
                    case $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}":
                    case $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.@default)}":
                    {
                        if (iterArg is FieldAccessExp fieldExp)
                            return new FieldsCollectionResult(source, fieldExp.FieldName, context.GetArrayType(fieldExp.SchemaType) ?? throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs));
                        break;
                    }
                }
                
                return new MapCollectionResult(source, item,
                    // Map the function call to schema expression if possible
                    await context.VisitSchemaExpAsync(new FuncCallExp(
                        funcExp.Function,
                        funcExp.Args,
                        (funcExp.SchemaType as ArrayType)!.ElementSchemaType!
                    )),
                    funcExp.SchemaType
                );
            
            case ExpressionType.First:
                return new FirstCollectionResult(source, funcExp.SchemaType, item,
                    // Map the function call to schema expression if possible
                    await context.VisitSchemaExpAsync(new FuncCallExp(
                        funcExp.Function,
                        funcExp.Args,
                        SchemaContext.SystemBool
                    )) as LogicExp
                );
            
            case ExpressionType.Last:
                return new LastCollectionResult(source, funcExp.SchemaType, item,
                    // Map the function call to schema expression if possible
                    await context.VisitSchemaExpAsync(new FuncCallExp(
                        funcExp.Function,
                        funcExp.Args,
                        SchemaContext.SystemBool
                    )) as LogicExp
                );
            case ExpressionType.Count:
                return new CountCollectionResult(source, funcExp.SchemaType, item,
                    // Map the function call to schema expression if possible
                    await context.VisitSchemaExpAsync(new FuncCallExp(
                        funcExp.Function,
                        funcExp.Args,
                        SchemaContext.SystemBool
                    )) as LogicExp
                );
            case ExpressionType.All:
                return new AllCollectionResult(source, funcExp.SchemaType, item,
                    // Map the function call to schema expression if possible
                    await context.VisitSchemaExpAsync(new FuncCallExp(
                        funcExp.Function,
                        funcExp.Args,
                        SchemaContext.SystemBool
                    )) as LogicExp
                );
            case ExpressionType.Any:
                return new AnyCollectionResult(source, funcExp.SchemaType, item,
                    // Map the function call to schema expression if possible
                    await context.VisitSchemaExpAsync(new FuncCallExp(
                        funcExp.Function,
                        funcExp.Args,
                        SchemaContext.SystemBool
                    )) as LogicExp
                );
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    // <inheritdoc/>
    public async Task<Expression?> CompileExpAsync(CompileContext context, SchemaExp exp, Type expectedType)
    {
        if (exp is not CollectionOperator && exp is not CollectionResult)
        {
            // For collection root, compile the collection directly
            return exp is CollectionRootExp rootExp ? await context.CompileSchemaExpAsync(rootExp.Collection) : null;
        }

        // Use function call for some collection operators
        switch (exp)
        {
            // source.orderBy(field, desc)
            case OrderByCollectionOperator orderBy:
                return await context.CompileSchemaExpAsync(new FuncCallExp(
                    (await context.GetSchemaTypeAsync<FunctionType>($"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.orderby)}"))!,
                    [
                        orderBy.Root,
                        new ConstantExp(SchemaContext.SystemString.CreateNode(orderBy.OrderField)!),
                        new ConstantExp(SchemaContext.SystemBool.CreateNode(orderBy.Descending)!)
                    ],
                    exp.SchemaType));
            
            }
        
        // Others will be compiled here
        Expression sourceExp = await context.CompileSchemaExpAsync((exp as CollectionOperator)?.Root ?? (exp as CollectionResult)?.Root
            ?? throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs));
        
        // Prepare loop variables
        ParameterExpression start = Expression.Variable(typeof(int), "_start");
        ParameterExpression stop = Expression.Variable(typeof(int), "_stop");
        LabelTarget forLabel = Expression.Label(typeof(int));
        Expression indexExp = exp is LastCollectionResult ? Expression.PreDecrementAssign(start) : Expression.PostIncrementAssign(start);
        ParameterExpression arrExp = Expression.Variable(sourceExp.Type, "_array");
        Expression arrayLen;
        Expression iterator;

        if (arrExp.Type.IsSZArray)
        {
            // array[start++]
            arrayLen = Expression.ArrayLength(arrExp);
            iterator = Expression.ArrayIndex(arrExp, indexExp);
        }
        else
        {
            // array.get_item(start++)
            arrayLen = Expression.Property(arrExp, "Count");
            iterator = Expression.MakeIndex(arrExp, arrExp.Type.GetProperty("Item", [typeof(int)])!, [indexExp]);
        }

        Type expReturnType = exp.SchemaType.ToCSharpType();

        // Handle different collection expression types
        switch (exp)
        {
            case PredicateCollectionOperator predicateExp:
            {
                // Compile loop
                Expression temp = iterator;
                ParameterExpression curr = Expression.Parameter(temp.Type, "_curr");
                context.SetCompiledExpression(predicateExp.Item, curr); // replace iterator
                Expression callMethod = await context.CompileSchemaExpAsync(predicateExp.Predicate);

                // Generate result expression
                ParameterExpression resultExp = Expression.Variable(expReturnType.IsArrayType() ? expReturnType : typeof(ArrayTypeNode));

                return Expression.Block(
                    [arrExp, resultExp, start, stop, curr],
                    Expression.Assign(arrExp, sourceExp),
                    Expression.Assign(resultExp, resultExp.Type == typeof(ArrayTypeNode)
                        ? Expression.New(resultExp.Type.GetConstructors()[0], Expression.Constant(exp.SchemaType),
                            Expression.Constant(null))
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

            case TakeCollectionOperator takeExp:
            {
                // Generate result expression
                ParameterExpression resultExp = Expression.Variable(expReturnType.IsArrayType() ? expReturnType : typeof(ArrayTypeNode));
                Expression takeCount = await context.CompileSchemaExpAsync(takeExp.Take);

                return Expression.Block(
                    [arrExp, resultExp, start, stop],
                    Expression.Assign(arrExp, sourceExp),
                    Expression.Assign(resultExp, resultExp.Type == typeof(ArrayTypeNode)
                        ? Expression.New(resultExp.Type.GetConstructors()[0], Expression.Constant(exp.SchemaType),
                            Expression.Constant(null))
                        : Expression.New(resultExp.Type)),
                    Expression.Assign(start, Expression.Constant(0, typeof(int))),
                    Expression.Assign(stop,  Expression.Condition(
                        Expression.LessThan(takeCount, arrayLen),
                        takeCount,
                        arrayLen
                    )),
                    Expression.Loop(
                        Expression.IfThenElse(
                            Expression.LessThan(start, stop),
                            Expression.Call(resultExp, resultExp.Type.GetMethod("Add")!, iterator),
                            Expression.Break(forLabel, stop)
                        ),
                        forLabel
                    ),
                    resultExp
                );
            }
            
            case SkipCollectionOperator skipExp:
            {
                // Generate result expression
                ParameterExpression resultExp = Expression.Variable(expReturnType.IsArrayType() ? expReturnType : typeof(ArrayTypeNode));
                Expression takeCount = await context.CompileSchemaExpAsync(skipExp.Skip);

                return Expression.Block(
                    [arrExp, resultExp, start, stop],
                    Expression.Assign(arrExp, sourceExp),
                    Expression.Assign(resultExp, resultExp.Type == typeof(ArrayTypeNode)
                        ? Expression.New(resultExp.Type.GetConstructors()[0], Expression.Constant(exp.SchemaType),
                            Expression.Constant(null))
                        : Expression.New(resultExp.Type)),
                    Expression.Assign(start, takeCount),
                    Expression.Assign(stop,  arrayLen),
                    Expression.Loop(
                        Expression.IfThenElse(
                            Expression.LessThan(start, stop),
                            Expression.Call(resultExp, resultExp.Type.GetMethod("Add")!, iterator),
                            Expression.Break(forLabel, stop)
                        ),
                        forLabel
                    ),
                    resultExp
                );
            }
            
            case CountCollectionResult countExp:
            {
                if (countExp.Item == null) return arrayLen;

                context.SetCompiledExpression(countExp.Item, iterator);
                ParameterExpression resultExp = Expression.Variable(typeof(int));
                Expression callMethod = await context.CompileSchemaExpAsync(countExp.Predicate!);

                return Expression.Block(
                    [arrExp, resultExp, start, stop],
                    Expression.Assign(arrExp, sourceExp),
                    Expression.Assign(resultExp, Expression.Constant(0, typeof(int))),
                    Expression.Assign(start, Expression.Constant(0, typeof(int))),
                    Expression.Assign(stop, arrayLen),
                    Expression.Loop(
                        Expression.IfThenElse(
                            Expression.LessThan(start, stop),
                            Expression.IfThen(callMethod, Expression.PostIncrementAssign(resultExp)),
                            Expression.Break(forLabel, stop)
                        ),
                        forLabel
                    ),
                    resultExp
                );
            }
            
            case AnyCollectionResult anyExp:
            {
                if (anyExp.Item == null)
                    return Expression.GreaterThan(arrayLen, Expression.Constant(0, typeof(int)));

                context.SetCompiledExpression(anyExp.Item, iterator);
                ParameterExpression resultExp = Expression.Variable(typeof(bool));
                Expression callMethod = await context.CompileSchemaExpAsync(anyExp.Predicate!);

                // Compile
                return Expression.Block(
                    [arrExp, resultExp, start, stop],
                    Expression.Assign(arrExp, sourceExp),
                    Expression.Assign(start, Expression.Constant(0, typeof(int))),
                    Expression.Assign(stop, arrayLen),
                    Expression.Assign(resultExp, Expression.Constant(false, typeof(bool))),
                    Expression.Loop(
                        Expression.IfThenElse(
                            Expression.LessThan(start, stop),
                            Expression.Block(new List<Expression>()
                            {
                                Expression.Assign(resultExp, callMethod),
                                Expression.IfThen(callMethod, Expression.Break(forLabel, stop))
                            }),
                            Expression.Break(forLabel, stop)
                        ),
                        forLabel
                    ),
                    resultExp
                );
            }
            
            case AllCollectionResult allExp:
            {
                if (allExp.Item == null)
                    return Expression.Constant(true, typeof(bool));

                context.SetCompiledExpression(allExp.Item, iterator);
                ParameterExpression resultExp = Expression.Variable(typeof(bool));
                Expression callMethod = await context.CompileSchemaExpAsync(allExp.Predicate!);

                // Compile
                return Expression.Block(
                    [arrExp, resultExp, start, stop],
                    Expression.Assign(arrExp, sourceExp),
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
                        
            case FirstCollectionResult firstExp:
            {
                if (firstExp.Item == null) // no iterator, return first element directly
                {
                    // array[0]
                    Expression firstItem;
                    if (arrExp.Type.IsSZArray)
                    {
                        firstItem = Expression.ArrayIndex(arrExp, Expression.Constant(0, typeof(int)));
                    }
                    else
                    {
                        firstItem = Expression.MakeIndex(arrExp, arrExp.Type.GetProperty("Item", [typeof(int)])!,
                            [Expression.Constant(0, typeof(int))]);
                    }

                    return Expression.Condition(Expression.GreaterThan(arrayLen, Expression.Constant(0, typeof(int))),
                        firstItem, Expression.Default(iterator.Type));
                }

                ParameterExpression resultExp = Expression.Variable(iterator.Type);

                // Replace the call args
                Expression temp = iterator;
                context.SetCompiledExpression(firstExp.Item, resultExp);
                Expression callMethod = await context.CompileSchemaExpAsync(firstExp.Predicate!);

                // New init parameter
                ParameterExpression init = Expression.Parameter(resultExp.Type, "_init");

                // Compile
                return Expression.Block(
                    [arrExp, resultExp, start, stop, init],
                    Expression.Assign(arrExp, sourceExp),
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
            
            case LastCollectionResult lastExp:
            {
                if (lastExp.Item == null)
                {
                    // array[0]
                    Expression lastItem;
                    Expression lastIndex = Expression.Subtract(arrayLen, Expression.Constant(1, typeof(int)));
                    if (arrExp.Type.IsSZArray)
                    {
                        lastItem = Expression.ArrayIndex(arrExp, lastIndex);
                    }
                    else
                    {
                        lastItem = Expression.MakeIndex(arrExp, arrExp.Type.GetProperty("Item", [typeof(int)])!, [lastIndex]);
                    }

                    return Expression.Condition(Expression.GreaterThan(arrayLen, Expression.Constant(0, typeof(int))),
                        lastItem, Expression.Default(iterator.Type));
                }

                ParameterExpression resultExp = Expression.Variable(iterator.Type);

                // Replace the call args
                Expression temp = iterator;
                context.SetCompiledExpression(lastExp.Item, resultExp);
                Expression callMethod = await context.CompileSchemaExpAsync(lastExp.Predicate!);

                // New init parameter
                ParameterExpression init = Expression.Parameter(resultExp.Type, "_init");

                // Compile
                return Expression.Block(
                    [arrExp, resultExp, start, stop, init],
                    Expression.Assign(arrExp, sourceExp),
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

            case ReduceCollectionResult reduceExp:
            {
                // Compile loop
                context.SetCompiledExpression(reduceExp.Item, iterator);
                ParameterExpression resultExp = Expression.Variable(expReturnType);

                // Replace the sum exp
                context.SetCompiledExpression(reduceExp.Sum, resultExp);
                Expression callMethod = await context.CompileSchemaExpAsync(reduceExp.Expression);

                // Compile
                return Expression.Block(
                    [arrExp, resultExp, start, stop],
                    Expression.Assign(arrExp, sourceExp),
                    Expression.Assign(start, Expression.Constant(0, typeof(int))),
                    Expression.Assign(stop, arrayLen),
                    Expression.Assign(resultExp, Expression.Coalesce(await context.CompileSchemaExpAsync(reduceExp.Sum),
                        reduceExp.Sum.Init is NullExp
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

            case FieldsCollectionResult fieldsExp:
            {
                // Create a synthetic item placeholder for the current element
                AnySchemaType elementType = (fieldsExp.Root.SchemaType as ArrayType)!.ElementSchemaType!;
                CollectionItemExp syntheticItem = new CollectionItemExp(fieldsExp.Root, elementType);

                // Map the synthetic item to the actual iterator
                context.SetCompiledExpression(syntheticItem, iterator);

                // Compile getfield(item, fieldName) for each element
                AnySchemaType fieldType = (fieldsExp.SchemaType as ArrayType)!.ElementSchemaType!;
                Expression callMethod = await context.CompileSchemaExpAsync(new FieldAccessExp(syntheticItem, fieldsExp.Field, fieldType));

                // Generate result expression
                ParameterExpression resultExp = Expression.Variable(expReturnType.IsArrayType() ? expReturnType : typeof(ArrayTypeNode));

                return Expression.Block(
                    [arrExp, resultExp, start, stop],
                    Expression.Assign(arrExp, sourceExp),
                    Expression.Assign(resultExp, resultExp.Type == typeof(ArrayTypeNode)
                        ? Expression.New(resultExp.Type.GetConstructors()[0], Expression.Constant(exp.SchemaType),
                            Expression.Constant(null))
                        : Expression.New(resultExp.Type)),
                    Expression.Assign(start, Expression.Constant(0, typeof(int))),
                    Expression.Assign(stop, arrayLen),
                    Expression.Loop(
                        Expression.IfThenElse(
                            Expression.LessThan(start, stop),
                            resultExp.Type.IsArrayType()
                                ? callMethod.Type.IsArrayType()
                                    ? Expression.Call(
                                        typeof(CollectionExpVisitor).GetMethod(nameof(AddRangeIfNotEmpty))!,
                                        Expression.Convert(resultExp, typeof(IList)),
                                        Expression.Convert(callMethod, typeof(IEnumerable)))
                                    : Expression.Call(
                                        typeof(CollectionExpVisitor).GetMethod(nameof(AddIfNotEmpty))!,
                                        Expression.Convert(resultExp, typeof(IList)),
                                        Expression.Convert(callMethod, typeof(object)))
                                : callMethod.Type == typeof(ArrayTypeNode)
                                    ? Expression.Call(resultExp,
                                        typeof(ArrayTypeNode).GetMethod(nameof(ArrayTypeNode.AddRange))!, callMethod)
                                    : Expression.Call(resultExp,
                                        typeof(ArrayTypeNode).GetMethod(nameof(ArrayTypeNode.Add))!, callMethod),
                            Expression.Break(forLabel, stop)
                        ),
                        forLabel
                    ),
                    resultExp
                );
            }

            case MapCollectionResult mapExp:
            {
                // Compile loop
                context.SetCompiledExpression(mapExp.Item, iterator);
                Expression callMethod = await context.CompileSchemaExpAsync(mapExp.Expression);

                // Generate result expression
                ParameterExpression resultExp =
                    Expression.Variable(expReturnType.IsArrayType() ? expReturnType : typeof(ArrayTypeNode));

                return Expression.Block(
                    [arrExp, resultExp, start, stop],
                    Expression.Assign(arrExp, sourceExp),
                    Expression.Assign(resultExp, resultExp.Type == typeof(ArrayTypeNode)
                        ? Expression.New(resultExp.Type.GetConstructors()[0], Expression.Constant(exp.SchemaType),
                            Expression.Constant(null))
                        : Expression.New(resultExp.Type)),
                    Expression.Assign(start, Expression.Constant(0, typeof(int))),
                    Expression.Assign(stop, arrayLen),
                    Expression.Loop(
                        Expression.IfThenElse(
                            Expression.LessThan(start, stop),
                            resultExp.Type.IsArrayType()
                                ? callMethod.Type.IsArrayType()
                                    ? Expression.Call(
                                        typeof(CollectionExpVisitor).GetMethod(nameof(AddRangeIfNotEmpty))!,
                                        Expression.Convert(resultExp, typeof(IList)),
                                        Expression.Convert(callMethod, typeof(IEnumerable)))
                                    : Expression.Call(
                                        typeof(CollectionExpVisitor).GetMethod(nameof(AddIfNotEmpty))!,
                                        Expression.Convert(resultExp, typeof(IList)),
                                        Expression.Convert(callMethod, typeof(object)))
                                : callMethod.Type == typeof(ArrayTypeNode)
                                    ? Expression.Call(resultExp,
                                        typeof(ArrayTypeNode).GetMethod(nameof(ArrayTypeNode.AddRange))!, callMethod)
                                    : Expression.Call(resultExp,
                                        typeof(ArrayTypeNode).GetMethod(nameof(ArrayTypeNode.Add))!, callMethod),
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

    /// <summary>
    /// Adds an item to the list, skipping null or empty values.
    /// </summary>
    public static void AddIfNotEmpty(IList result, object? item)
    {
        if (item == null) return;
        if (item is AnySchemaNode { IsEmpty: true }) return;
        result.Add(item);
    }

    /// <summary>
    /// Adds items from a sequence to the list, skipping null or empty values.
    /// </summary>
    public static void AddRangeIfNotEmpty(IList result, IEnumerable? items)
    {
        if (items == null) return;
        foreach (var item in items)
        {
            if (item == null) continue;
            if (item is AnySchemaNode { IsEmpty: true }) continue;
            result.Add(item);
        }
    }
}