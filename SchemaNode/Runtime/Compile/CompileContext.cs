using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using ExpressionType = SchemaNode.Enum.ExpressionType;

// ReSharper disable UnusedMember.Local

namespace SchemaNode.Runtime;

/// <summary>
/// The function compile context
/// </summary>
public class CompileContext(SchemaContext context, FunctionType funcType)
{
    #region Private Fields

    // The expression visitors
    readonly IExpressionVisitor[] _visitors = context.ServiceProvider.GetServices<IExpressionVisitor>()
        .OrderBy(v => v.Priority)
        .ToArray();
    
    // the argument parameter expression map
    readonly Dictionary<string, ParameterExpression> _paramExpMap = new ();
    
    // the variable parameter expression map
    readonly Dictionary<string, ParameterExpression> _variableExpMap = new ();

    // The compile exp cache for pre-set
    readonly Dictionary<SchemaExpression, Expression> _compiledExpCache = new ();

    // the return label
    private LabelTarget? _returnLabel;
    
    /// <summary>
    /// The context exp
    /// </summary>
    private ParameterExpression? _contextExp;
    
    #endregion

    /// <summary>
    /// Visit the function type for compile schema
    /// </summary>
    public virtual async Task<FunctionTypeSchema> VisitFunctionType()
    {
        return await context.VisitFunctionType(funcType)
            ?? throw new FunctionVisitException(SchemaNodeStatus.FunctionExpsHasCompileError, TYPE_FUNC_COMPILE_ERROR);
    }
    
    /// <summary>
    /// Compile the function to dynamic method
    /// </summary>
    public async Task<Delegate?> CompileAsync()
    {
        // Prepare
        try
        {
            FunctionTypeSchema funcSchema = await VisitFunctionType();
            
            // Prepare
            var paramExps = new ParameterExpression[funcType.Args.Length + 1];

            // Build the parameters, no generic type for custom methods
            // Always add SchemaContext as the first parameters for inner call
            _contextExp = Expression.Parameter(typeof(SchemaContext));
            paramExps[0] = _contextExp; 
            for (int i = 0; i < funcType.Args.Length; i++)
            {
                FunctionNodeArgument arg = funcType.Args[i];
                ParameterExpression paramExp = Expression.Parameter(arg.SchemaType?.ToCSharpType(arg.Nullable) 
                    ?? throw new Exception($"The {funcType.Name} can't be compiled - expression compile failed"));
                paramExps[i + 1] = paramExp;
                _paramExpMap[arg.Name] = paramExp;
            }

            // Expression Tree -> Function Body
            List<Expression> expBlocks = [];
            _returnLabel = funcSchema.Exps.Any(e => e.Value is BreakExpression) 
                ? Expression.Label(funcSchema.Return) : null;
            
            ParameterExpression? finalVar = null;
            foreach (VariableExpression exp in funcSchema.Exps)
            {
                Expression? result = CompileSchemaExpression(exp.Value);
                if (result == null) throw new Exception($"The {funcType.Name} can't be compiled - expression {exp.Name} compile failed");

                // exp = result
                ParameterExpression expRes = Expression.Parameter(result.Type);
                _variableExpMap.Add(exp.Name, expRes);
                
                // Logger @TODO
                //if (exp is FunctionNodeExpression callexp)
                //    expBlocks.Add(Expression.Call(paramExps[0], typeof(SchemaContext).GetMethod(nameof(SchemaContext.LogInformation))!, Expression.Constant($"Calling expression {callexp.Name}")));
                expBlocks.Add(Expression.Assign(expRes, result));
                finalVar = expRes;
            }
            
            if (finalVar == null) throw new Exception($"The {funcType.Name} can't be compiled - no expression found");

            // Conversion last type
            Type lastType = funcSchema.Return;
            if (lastType != finalVar.Type)
            {
                ParameterExpression convExp = Expression.Variable(lastType, "_final");
                _variableExpMap.Add("_final", convExp);
                expBlocks.Add(Expression.Assign(convExp, ConvertExp(lastType, finalVar)));
                finalVar = convExp;
            }

            // Handle return label if existed
            if (_returnLabel != null)
            {
                expBlocks.Add(Expression.Return(_returnLabel, finalVar));
                expBlocks.Add(Expression.Label(_returnLabel, Expression.Default(_returnLabel.Type)));
            }

            // Build block
            BlockExpression blockExpr = Expression.Block(_variableExpMap.Values.ToArray(), expBlocks);

            // Build the dynamic method
            return CompileMethod(lastType, paramExps, blockExpr);
        }
        catch (Exception ex)
        {
            context.LogError(ex, "Failed to compile function {FunctionName}", funcType.Name);
        }

        return null;
    }
    
    /// <summary>
    /// Compile the schema expression to Expression
    /// </summary>
    public virtual Expression CompileSchemaExpression(SchemaExpression exp, Type? expectedType = null)
    {
        expectedType ??= exp.SchemaType.ToCSharpType();
        if (_compiledExpCache.TryGetValue(exp, out Expression? cachedExp))
            return ConvertExp(expectedType, cachedExp);
        
        #region Apply visitors
        
        foreach (var visitor in _visitors)
        {
            Expression? resExp = visitor.CompileExpression(this, exp);
            if (resExp != null) return ConvertExp(expectedType, resExp);
        }
        
        #endregion 
        
        #region specific expressions
        
        switch (exp)
        {
            // Argument
            case ArgumentExpression ae:
                return GetParameterExpression(ae.Name);
            
            // Variable
            case VariableExpression ve:
                return GetParameterExpression(ve.Name);
            
            // Params
            case ParamsExpression ps:
            {
                expectedType = expectedType.GetElementType() ?? expectedType; // make sure it's element type
                return Expression.NewArrayInit(expectedType, ps.Exps
                    .Select(e => ConvertExp(expectedType, CompileSchemaExpression(e, expectedType)))
                    .ToArray());
            }
            
            // Iterator
            case IteratorExpression iter:
                throw new NotImplementedException("IteratorExpression compilation not implemented yet.");
            
            // Constant
            case ConstantExpression constExp:
            {
                // For reduce
                if (constExp.Value.IsEmpty && !expectedType.IsNullable())
                    return Expression.Default(expectedType);

                object? value = expectedType.GetNotNullType().TryConvert(constExp.Value);
                return value != null && value.GetType().IsSafeConstantValue()
                    ? Expression.Constant(value, expectedType)
                    : Expression.Default(expectedType);
            }
            
            // a ?? b
            case DefaultExpression defaultExp:
            {
                return Expression.Coalesce(CompileSchemaExpression(defaultExp.Inner, expectedType),
                    Expression.Constant(expectedType.TryConvert(defaultExp.Default.Value), expectedType));
            }
            
            // null
            case NullExpression:
                return Expression.Constant(null, expectedType);
            
            // a[b]
            case FieldAccessExpression fldAccess:
            {
                return CompileSchemaExpression(new FuncCallExpression(
                    (context.GetSchemaTypeAsync($"{NS_SYSTEM_COLLECTION}.{nameof(SystemCollection.getfield)}").GetAwaiter().GetResult() as FunctionType)!,
                    [fldAccess.Owner, new ConstantExpression(context.GetSchemaTypeAsync(NS_SYSTEM_STRING).GetAwaiter().GetResult()!.CreateNode(fldAccess.FieldName)!)],
                    fldAccess.SchemaType
                ), expectedType);
            }
            
            // a ? b : c
            case ConditionalExpression condExp:
                return Expression.Condition(CompileSchemaExpression(condExp.Condition),
                    CompileSchemaExpression(condExp.TrueExp, expectedType),
                    CompileSchemaExpression(condExp.FalseExp, expectedType));
            
            // { a, b, c }
            case StructResultExpression structExp:
            {
                var resultVar = Expression.Variable(typeof(StructTypeNode));
                List<Expression> blockExps = [
                    Expression.Assign(resultVar, Expression.New(typeof(StructTypeNode).GetConstructors()[0], Expression.Constant(structExp.SchemaType), Expression.Constant(null)))
                ];
                MethodInfo objectAdd = typeof(StructTypeNode).GetMethod(nameof(StructTypeNode.SetField))!;
                
                foreach (var fieldExp in structExp.Fields)
                {
                    blockExps.Add(Expression.Call(resultVar, objectAdd, Expression.Constant(fieldExp.Name, typeof(string)), 
                        ConvertExp(typeof(Object), CompileSchemaExpression(fieldExp.Expression))));
                }
                
                blockExps.Add(resultVar); // as the result
                
                return Expression.Block([resultVar], blockExps);
            }
        }
        
        #endregion

        #region Function Arguments

        // Default Function Call
        if (exp is not FuncCallExpression funcCallExp) return Expression.Empty();

        // function validate
        SchemaFuncInfo callFuncInfo = funcCallExp.Function?.GetSchemaFuncInfo(context) ?? throw new Exception($"The function call {funcCallExp.Function?.Name} can't be compiled");
        int useContext = (callFuncInfo.Sign & FUNC_SIGN_CONTEXT) == FUNC_SIGN_CONTEXT ? 1 : 0;

        // Prepare the call arguments
        Expression[] callArgs = new Expression[funcCallExp.Args.Length + useContext];
        if (useContext > 0) callArgs[0] = _contextExp!;

        // Prepare the call arguments
        for (int i = 0; i < funcCallExp.Args.Length; i++)
        {
            SchemaExpression leaf = funcCallExp.Args[i];
            Type callType = (funcCallExp.Function.Args[i].SchemaType is GenericTypeNode ? funcCallExp.Args[i].SchemaType : funcCallExp.Function.Args[i].SchemaType)
                            ?.ToCSharpType(funcCallExp.Function.Args[i].Nullable) ?? throw new Exception($"The expression {i} argument type not valid.");

            if (leaf is IteratorExpression)
                throw new Exception("IteratorExpression must be used in non-call exp.");
            
            // default
            callArgs[useContext + i] = ConvertExp(callType, CompileSchemaExpression(leaf, callType));
        }

        // Prepare the function
        MethodInfo callMethod = callFuncInfo.Method!;
        // bool hasClosure = callFuncInfo.DynamicMethod != null && callFuncInfo.DynamicMethod.HasClosure();
        Type expReturnType = exp.SchemaType.ToCSharpType((callFuncInfo.Sign & FUNC_SIGN_NULLABLE_RET) > 0);
        Type expRetElement = funcCallExp.ExpType is ExpressionType.Map && exp.SchemaType is ArrayType arr
            ? arr.ElementSchemaType!.ToCSharpType()
            : expReturnType;

        // Make generic method for system defined methods
        Type?[] genTypes = callFuncInfo.Generics.Select(p =>
        {
            Utility.Schema.SchemaParamTypeInfo? info = null;
            Type? type = null;
            if (callFuncInfo.Return.Generic == p.Generic)
            {
                info = callFuncInfo.Return;
                type = expRetElement.GetNotNullType();
            }
            else
            {
                for (int j = 0; j < callFuncInfo.Args.Length; j++)
                {
                    Utility.Schema.SchemaParamTypeInfo sinfo = callFuncInfo.Args[j];
                    if (sinfo.Generic == p.Generic)
                    {
                        info = sinfo;
                        type = callArgs[j + useContext].Type.GetNotNullType();
                        break;
                    }
                }
            }

            if (info == null || type == null) throw new InvalidOperationException($"The function call {funcCallExp.Function?.Name} can't be compiled");
            if (info.Array && type.IsSZArray) return type.GetElementType();
            if ((info.List || info.Enumerable) && type.GetGenericArguments() is { Length: > 0} args) return args[0];
            return type;
        }).ToArray();
        
        // Generate generic method
        if ((callFuncInfo.Sign & FUNC_SIGN_IMMUTABLE) == FUNC_SIGN_IMMUTABLE && (callFuncInfo.Sign & FUNC_SIGN_GENERIC) == FUNC_SIGN_GENERIC)
        {
            // Generate the generic method
            string genSign = string.Join('|', genTypes.Select(p => (Nullable.GetUnderlyingType(p!) ?? p!).FullName));
            callMethod = callFuncInfo.GenericMethods.GetOrAdd(genSign, _ => callFuncInfo.Method!.MakeGenericMethod(genTypes!));
        }
        
        // Use remote call
        else if ((callFuncInfo.Sign & FUNC_SIGN_REMOTE_CALL) == FUNC_SIGN_REMOTE_CALL)
        {
            // Generate the call
            var convCallArgs = new Expression[callArgs.Length + (useContext > 0 ? 2 : 3)];
            convCallArgs[0] = _contextExp!;
            convCallArgs[1] = Expression.Constant(callFuncInfo.Name);
            convCallArgs[2] = Expression.Constant(genTypes);
            if (useContext > 0)
            {
                for (int i = 1; i < callArgs.Length; i++)
                    convCallArgs[i + 2] = callArgs[i];
            }
            else
            {
                for (int i = 0; i < callArgs.Length; i++)
                    convCallArgs[i + 3] = callArgs[i];
            }

            callArgs = convCallArgs;
            int count = callArgs.Length - 3;
            callMethod = typeof(FunctionType).GetMethod($"CallRemoteFunction{count}", BindingFlags.Static | BindingFlags.NonPublic)!;

            // Make generic type
            callMethod = count > 0 ? callMethod.MakeGenericMethod(callArgs.Skip(3).Select(e => e.Type).Prepend(expRetElement).ToArray()) : callMethod;
        }
        
        #endregion

        #region Function Call Build
        
        // Direct call
        if (funcCallExp.ExpType == ExpressionType.Call)
            return GenMethodCallExp(callFuncInfo, callMethod, callArgs, expRetElement);
        
        return Expression.Empty();
    }

    /// <summary>
    /// Gets the parameter expression by name
    /// </summary>
    public ParameterExpression GetParameterExpression(string name)
    {
        if (_paramExpMap.TryGetValue(name, out ParameterExpression? paramExp) || _variableExpMap.TryGetValue(name, out paramExp))
            return paramExp;
        throw new KeyNotFoundException($"Parameter '{name}' not found in function.");
    }

    /// <summary>
    /// Gets the return label
    /// </summary>
    public LabelTarget? GetReturnLabel() => _returnLabel;

    public ParameterExpression? GetContext() => _contextExp;

    /// <summary>
    /// Try get compiled expression from cache
    /// </summary>
    public Expression? GetCompiledExpression(SchemaExpression exp)
    {
        _compiledExpCache.TryGetValue(exp, out Expression? compiledExp);
        return compiledExp;
    }

    /// <summary>
    /// Set compiled expression to cache
    /// </summary>
    public void CacheCompiledExpression(SchemaExpression exp, Expression compiledExp)
    {
        _compiledExpCache[exp] = compiledExp;
    }

    #region Utility Methods

    // Compile the method to delegate
    static Delegate CompileMethod(Type retType, IReadOnlyList<ParameterExpression> paramExps, BlockExpression blockExpr)
    {
        Type[] funcTypes = new Type[paramExps.Count + 1];
        for (int i = 0; i < paramExps.Count; i++)
        {
            funcTypes[i] = paramExps[i].Type;
        }
        funcTypes[paramExps.Count] = retType;
        Type lambdaType = funcTypes.Length switch
        {
            2 => typeof(Func<,>).MakeGenericType(funcTypes),
            3 => typeof(Func<,,>).MakeGenericType(funcTypes),
            4 => typeof(Func<,,,>).MakeGenericType(funcTypes),
            5 => typeof(Func<,,,,>).MakeGenericType(funcTypes),
            6 => typeof(Func<,,,,,>).MakeGenericType(funcTypes),
            7 => typeof(Func<,,,,,,>).MakeGenericType(funcTypes),
            8 => typeof(Func<,,,,,,,>).MakeGenericType(funcTypes),
            9 => typeof(Func<,,,,,,,,>).MakeGenericType(funcTypes),
            10 => typeof(Func<,,,,,,,,,>).MakeGenericType(funcTypes),
            11 => typeof(Func<,,,,,,,,,,>).MakeGenericType(funcTypes),
            12 => typeof(Func<,,,,,,,,,,,>).MakeGenericType(funcTypes),
            13 => typeof(Func<,,,,,,,,,,,,>).MakeGenericType(funcTypes),
            14 => typeof(Func<,,,,,,,,,,,,,>).MakeGenericType(funcTypes),
            15 => typeof(Func<,,,,,,,,,,,,,,>).MakeGenericType(funcTypes),
            16 => typeof(Func<,,,,,,,,,,,,,,,>).MakeGenericType(funcTypes),
            17 => typeof(Func<,,,,,,,,,,,,,,,,>).MakeGenericType(funcTypes),
            _ => throw new ArgumentOutOfRangeException()
        };
        return (Delegate)typeof(FunctionType)
            .GetMethod(nameof(CompileDynamicMethod), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(lambdaType)
            .Invoke(null, [blockExpr, paramExps])!;
    }

    // Convert expression
    Expression ConvertExp(Type ctype, Expression exp)
    {
        if (ctype == exp.Type) return exp;
        if (ctype.IsAssignableFrom(exp.Type) || exp.Type == typeof(object)) return Expression.Convert(exp, ctype);

        Expression notNullExp = exp.Type.IsNullable() ? Expression.Call(exp, exp.Type.GetMethod("GetValueOrDefault", System.Type.EmptyTypes)!) : exp;
        Expression? resExp = null;
        Type rctype = ctype.GetNotNullType();
        
        // convert csharp type to schema type node
        if (ctype.IsAssignableTo(typeof(AnySchemaNode)))
        {
            string schema = exp.Type.GetSchemaType(true) ?? throw new Exception($"The type {exp.Type.FullName} can't be converted to schema type node");
            AnySchemaType schemaType = context.GetSchemaTypeAsync(schema).GetAwaiter().GetResult() ?? throw new Exception($"The schema type node {schema} not found");
            MethodInfo method = typeof(AnySchemaType).GetMethod(nameof(AnySchemaType.CreateNode))!;
            return Expression.Convert(Expression.Call(Expression.Constant(schemaType), method, notNullExp), ctype);
        }

        // simple type conversion
        if (!notNullExp.Type.IsAssignableTo(typeof(AnySchemaNode)))
        {
            resExp = Type.GetTypeCode(rctype) switch
            {
                TypeCode.Boolean => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToBoolean), [notNullExp.Type])!, notNullExp),
                TypeCode.Char => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToChar), [notNullExp.Type])!, notNullExp),
                TypeCode.SByte => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToSByte), [notNullExp.Type])!, notNullExp),
                TypeCode.Byte => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToByte), [notNullExp.Type])!, notNullExp),
                TypeCode.Int16 => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToInt16), [notNullExp.Type])!, notNullExp),
                TypeCode.UInt16 => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToUInt16), [notNullExp.Type])!, notNullExp),
                TypeCode.Int32 => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToInt32), [notNullExp.Type])!, notNullExp),
                TypeCode.UInt32 => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToUInt32), [notNullExp.Type])!, notNullExp),
                TypeCode.Int64 => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToInt64), [notNullExp.Type])!, notNullExp),
                TypeCode.UInt64 => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToUInt64), [notNullExp.Type])!, notNullExp),
                TypeCode.Single => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToSingle), [notNullExp.Type])!, notNullExp),
                TypeCode.Double => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToDouble), [notNullExp.Type])!, notNullExp),
                TypeCode.Decimal => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToDecimal), [notNullExp.Type])!, notNullExp),
                TypeCode.DateTime => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToDateTime), [notNullExp.Type])!, notNullExp),
                TypeCode.String => Expression.Call(null, typeof(Convert).GetMethod(nameof(Convert.ToString), [notNullExp.Type])!, notNullExp),
                _ => resExp
            };
        }

        // for complex types
        if (resExp == null)
        {
            MethodInfo method = typeof(Extension).GetMethod(nameof(Extension.TryConvert), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
            resExp = Expression.Convert(Expression.Call(null, method, Expression.Constant(ctype), notNullExp), ctype);
        }

        // nullable result
        return rctype == ctype
            ? resExp
            : Expression.Condition(Expression.NotEqual(exp, Expression.Constant(null, exp.Type)), resExp, Expression.Constant(null, ctype));
    }

    // Gen method call
    Expression GenMethodCallExp(SchemaFuncInfo callFuncInfo, MethodInfo callMethod, Expression[] callArgs, Type? returnType = null)
    {
        // Call the method
        Expression result;
        if ((callFuncInfo.Sign & FUNC_SIGN_ASYNC) == FUNC_SIGN_ASYNC)
        {
            // Gets the task result
            MethodCallExpression callExp = Expression.Call(null, callMethod, callArgs);
            callExp = Expression.Call(callExp, callExp.Type.GetMethod(nameof(Task.GetAwaiter), System.Type.EmptyTypes)!);
            result = Expression.Call(callExp, callExp.Type.GetMethod(nameof(TaskAwaiter.GetResult), System.Type.EmptyTypes)!);
        }
        else if ((callFuncInfo.Sign & FUNC_SIGN_IMMUTABLE) == FUNC_SIGN_IMMUTABLE)
        {
            result = Expression.Call(null, callMethod, callArgs);
        }
        else
        {
            result = GenDynamicCallExp(callMethod.ReturnType, callFuncInfo.DynamicMethod!, callArgs);
        }

        if (returnType != null && returnType != result.Type)
        {
            result = ConvertExp(returnType, result);
        }
        return result;
    }

    // Gen dynamic method call
    static Expression GenDynamicCallExp(Type retType, Delegate method, Expression[] callArgs)
    {
        // call the dynamic method, skip the mysql expression
        MethodInfo callDynamicMethod = GetCallDynamicFunc(retType, callArgs.Select(p => p.Type).ToArray());
        Expression[] newCallArgs = new Expression[callArgs.Length + 1];
        newCallArgs[0] = Expression.Constant(method);
        for (int i = 0; i < callArgs.Length; i++)
            newCallArgs[i + 1] = callArgs[i];
        return Expression.Call(null, callDynamicMethod, newCallArgs);
    }

    // Compile lambda to method
    static T CompileDynamicMethod<T>(Expression block, params ParameterExpression[] inputs)
        => Expression.Lambda<T>(block, inputs).Compile();


    // Gets the call dynamic func
    static MethodInfo GetCallDynamicFunc(Type ret, params Type[] inputs)
    {
        MethodInfo method = typeof(FunctionType).GetMethod($"CallDynamicFunc{inputs.Length}", BindingFlags.Static | BindingFlags.NonPublic)!;
        return method.MakeGenericMethod(inputs.Prepend(ret).ToArray());
    }
    
    #region CallDynamicFunc

    static TR? CallDynamicFunc<TR>(Delegate del, object[] args)
    {
        var method = del.Method;
        var target = del.Target;
        var parms = method.GetParameters();
        int parmCount = parms.Length;
        bool firstIsClosure = parmCount > 0 && parms[0].ParameterType.FullName == "System.Runtime.CompilerServices.Closure";

        // Case A: static method
        if (method.IsStatic)
        {
            // If static method expects a Closure as first parameter and we do have a closure target,
            // pass the closure as first arg and invoke with null target.
            if (firstIsClosure && target != null && parmCount == args.Length + 1)
            {
                var newArgs = new object?[args.Length + 1];
                newArgs[0] = target;
                Array.Copy(args, 0, newArgs, 1, args.Length);
                return (TR?)method.Invoke(null, newArgs);
            }

            // If parameter count exactly matches args, call as plain static
            if (parmCount == args.Length)
                return (TR?)method.Invoke(null, args);

            // otherwise fallback to DynamicInvoke (safer but slower)
            return (TR?)del.DynamicInvoke(args);
        }

        // Case B: instance method
        // Normal: instance method => call with target as instance, args match parmCount
        if (!method.IsStatic)
        {
            if (parmCount == args.Length)
                return (TR?)method.Invoke(target, args);

            // Special: open-instance-like delegate: target == null, but method expects instance as first parameter:
            // if parmCount == args.Length + 1, treat args[0] as the instance
            if (target == null && parmCount == args.Length + 1)
            {
                var newTarget = args[0];
                var remaining = new object?[args.Length - 1];
                Array.Copy(args, 1, remaining, 0, remaining.Length);
                return (TR?)method.Invoke(newTarget, remaining);
            }

            // Fallback to DynamicInvoke
            return (TR?)del.DynamicInvoke(args);
        }

        // Fallback (shouldn't reach here)
        return (TR?)del.DynamicInvoke(args);
    }

    // Call dynamic function
    static TR? CallDynamicFunc1<TR, T1>(Delegate method, T1 arg1)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!]);
    }
    static TR? CallDynamicFunc2<TR, T1, T2>(Delegate method, T1 arg1, T2 arg2)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!]);
    }
    static TR? CallDynamicFunc3<TR, T1, T2, T3>(Delegate method, T1 arg1, T2 arg2, T3 arg3)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!]);
    }
    static TR? CallDynamicFunc4<TR, T1, T2, T3, T4>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!]);
    }
    static TR? CallDynamicFunc5<TR, T1, T2, T3, T4, T5>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!, arg5!]);
    }
    static TR? CallDynamicFunc6<TR, T1, T2, T3, T4, T5, T6>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!, arg5!, arg6!]);
    }
    static TR? CallDynamicFunc7<TR, T1, T2, T3, T4, T5, T6, T7>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!]);
    }
    static TR? CallDynamicFunc8<TR, T1, T2, T3, T4, T5, T6, T7, T8>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!]);
    }
    static TR? CallDynamicFunc9<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!]);
    }
    static TR? CallDynamicFunc10<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!, arg10!]);
    }
    static TR? CallDynamicFunc11<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!, arg10!, arg11! ]);
    }
    static TR? CallDynamicFunc12<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!, arg10!, arg11!, arg12! ]);
    }
    static TR? CallDynamicFunc13<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!, arg10!, arg11!, arg12!, arg13! ]);
    }
    static TR? CallDynamicFunc14<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!, arg10!, arg11!, arg12!, arg13!, arg14! ]);
    }
    static TR? CallDynamicFunc15<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!, arg10!, arg11!, arg12!, arg13!, arg14!, arg15!
        ]);
    }
    static TR? CallDynamicFunc16<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15, T16 arg16)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!, arg10!, arg11!, arg12!, arg13!, arg14!, arg15!, arg16!]);
    }
    static TR? CallDynamicFunc17<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17>(Delegate method, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15, T16 arg16, T17 arg17)
    {
        // Invoke the dynamic method
        return CallDynamicFunc<TR>(method, [arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!, arg10!, arg11!, arg12!, arg13!, arg14!, arg15!, arg16!, arg17!]);
    }
    #endregion

    #region Call Server Func

    static TR? GetResult<TR>(JsonNode? token)
    {
        Type tr = typeof(TR);
        bool isNullable = tr.IsSubclassOfGenericType(typeof(Nullable<>));
        tr = isNullable ? tr.GetGenericArguments()[0] : tr;
        if (tr == typeof(JsonArray))
        {
            return token is JsonArray arr ? (TR)(object)arr : isNullable ? (TR?)(object?)null : default;
        }
        else if (tr == typeof(JsonObject))
        {
            return token is JsonObject obj ? (TR) (object) obj : isNullable ? (TR?)(object?)null : default;
        }
        else if (token is JsonValue val)
        {
            return tr == typeof(JsonValue) ? (TR)(object)val : val.GetValue<TR>();
        }
        return isNullable ? (TR?)(object?)null : default;
    }
    
    /// <summary>
    /// Call the data dict function with arguments
    /// </summary>
    static async Task<TR?> CallRemoteFunction0<TR>(SchemaContext context, string name, string[] generic) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray(), generic));
    static async Task<TR?> CallRemoteFunction1<TR, T1>(SchemaContext context, string name, string[] generic, T1 v1) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1 }, generic));
    static async Task<TR?> CallRemoteFunction2<TR, T1, T2>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2 }, generic));
    static async Task<TR?> CallRemoteFunction3<TR, T1, T2, T3>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3 }, generic));
    static async Task<TR?> CallRemoteFunction4<TR, T1, T2, T3, T4>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4 }, generic));
    static async Task<TR?> CallRemoteFunction5<TR, T1, T2, T3, T4, T5>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5 }, generic));
    static async Task<TR?> CallRemoteFunction6<TR, T1, T2, T3, T4, T5, T6>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6 }, generic));
    static async Task<TR?> CallRemoteFunction7<TR, T1, T2, T3, T4, T5, T6, T7>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7 }, generic));
    static async Task<TR?> CallRemoteFunction8<TR, T1, T2, T3, T4, T5, T6, T7, T8>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8 }, generic));
    static async Task<TR?> CallRemoteFunction9<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9 }, generic));
    static async Task<TR?> CallRemoteFunction10<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10 }, generic));
    static async Task<TR?> CallRemoteFunction11<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11 }, generic));
    static async Task<TR?> CallRemoteFunction12<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11, T12 v12) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12 }, generic));
    static async Task<TR?> CallRemoteFunction13<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11, T12 v12, T13 v13) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13 }, generic));
    static async Task<TR?> CallRemoteFunction14<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11, T12 v12, T13 v13, T14 v14) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14 }, generic));
    static async Task<TR?> CallRemoteFunction15<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11, T12 v12, T13 v13, T14 v14, T15 v15) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14, v15 }, generic));
    static async Task<TR?> CallRemoteFunction16<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11, T12 v12, T13 v13, T14 v14, T15 v15, T16 v16) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14, v15, v16 }, generic));
    static async Task<TR?> CallRemoteFunction17<TR, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17>(SchemaContext context, string name, string[] generic, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11, T12 v12, T13 v13, T14 v14, T15 v15, T16 v16, T17 v17) => GetResult<TR>(await context.CallFunctionAsync(name, new JsonArray { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14, v15, v16, v17 }, generic));

    #endregion

    #endregion

    #endregion
}