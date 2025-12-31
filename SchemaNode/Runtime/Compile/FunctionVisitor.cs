using SchemaNode.Context;
using SchemaNode.Enum;
using System.Text.RegularExpressions;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;
// ReSharper disable NotAccessedPositionalProperty.Global

namespace SchemaNode.Runtime;

/// <summary>
/// The function expression visitor to generate schema expression trees
/// </summary>
public static class FunctionVisitor
{
    /// <summary>
    /// Generate Semantic Analysis Schema Expressions for the given function type
    /// </summary>
    public static async Task<FunctionTypeSchema?> VisitFunctionType(this SchemaContext context, FunctionType func)
    {
        #region Pre-checks

        // C# function, no need to generate expression trees
        if (func.IsSystemCall) return null;

        if (func.TryGetRuntimeFuncCache(out FunctionTypeSchema? cache))
            return cache;

        // Require exps
        if (func.Exps.Length == 0)
        {
            func.Status = SchemaNodeStatus.FunctionNoExps;
            throw new FunctionVisitException(SchemaNodeStatus.FunctionNoExps, TYPE_FUNC_NEED_EXPS);
        }

        // Validate return type
        AnySchemaType? returnType = func.ReturnNode ?? (!string.IsNullOrWhiteSpace(func.Return) ? await context.GetSchemaTypeAsync(func.Return) : null);
        if (returnType is not { IsValueType: true })
        {
            func.Status = SchemaNodeStatus.FunctionWrongReturnType;
            throw new FunctionVisitException(SchemaNodeStatus.FunctionWrongReturnType, TYPE_FUNC_RETURN_NOT_VALID);
        }
        func.ReturnNode = returnType;

        #endregion

        // Expression cache
        ArgumentExpression[] argExps = new ArgumentExpression[func.Args.Length];
        List<VariableExpression> results = [];
        Dictionary<string, VariableExpression> expMaps = [];
        Dictionary<string, int> accessCount = [];

        #region Function Exp build

        // Get expression with visit count++ & Field Access support
        bool GetExpression(string name, out SchemaExpression? value)
        {
            string[] access = name.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (expMaps.TryGetValue(access[0], out VariableExpression? varExp))
            {
                SchemaExpression exp = varExp;
                if (access.Length > 1)
                {
                    // replace with field access expression
                    AnySchemaType? type = exp.SchemaType;
                    if (type is ArrayType arrayType)
                        type = arrayType.ElementSchemaType;

                    for (int i = 1; i < access.Length; i++)
                    {
                        if (type is StructType structType && structType.Fields.FirstOrDefault(f => f.Name.Equals(access[i], StringComparison.OrdinalIgnoreCase)) is { } field)
                        {
                            type = field.SchemeType;
                        }
                        else
                        {
                            value = null;
                            return false;
                        }
                    }
                    exp = new FieldAccessExpression(exp, string.Join(".", access.Skip(1)), type!);
                }

                accessCount[access[0]] = (accessCount.GetValueOrDefault(access[0], 0)) + 1;
                value = exp;
                return true;
            }

            value = null;
            return false;
        }

        // Process arguments
        for (int i = 0; i < func.Args.Length; i++)
        {
            FunctionNodeArgument arg = func.Args[i];

            // Require argument name
            if (string.IsNullOrWhiteSpace(arg.Name))
            {
                arg.Status = SchemaNodeStatus.FunctionArgumentNoName;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionArgumentNoName, TYPE_FUNC_ARG_NAME_REQUIRED);
            }
            // No duplicate name
            else if (expMaps.ContainsKey(arg.Name))
            {
                arg.Status = SchemaNodeStatus.FunctionArgumentDuplicateName;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionArgumentDuplicateName, TYPE_FUNC_ARG_NAME_DUPLICATE);
            }
            // Require type, only system function support generic type like T1, T2...
            else if (string.IsNullOrEmpty(arg.Type) || Regex.IsMatch(arg.Type, @"^[tT]\d*$"))
            {
                arg.Status = SchemaNodeStatus.FunctionArgumentNoType;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionArgumentNoType, TYPE_FUNC_ARG_NO_TYPE);
            }

            // Validate the argument type
            AnySchemaType? argTypeNode = arg.SchemaType ?? await context.GetSchemaTypeAsync(arg.Type);
            if (argTypeNode is not { IsValueType: true })
            {
                arg.Status = SchemaNodeStatus.FunctionArgumentWrongType;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionArgumentWrongType, TYPE_FUNC_ARG_TYPE_NOT_VALID);
            }
            arg.SchemaType = argTypeNode;
            arg.Type = argTypeNode.Name; // adjust name

            // Create argument expression
            argExps[i] = new ArgumentExpression(arg.Name, i, argTypeNode);
            expMaps[arg.Name] = new VariableExpression(arg.Name, argExps[i]);
        }

        // Process exps
        foreach (FunctionNodeExpression exp in func.Exps)
        {
            // Require name
            if (string.IsNullOrWhiteSpace(exp.Name))
            {
                exp.Status = SchemaNodeStatus.FunctionExpNoName;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionExpNoName, TYPE_FUNC_EXP_NAME_REQUIRED);
            }
            // No duplicate name
            else if (expMaps.ContainsKey(exp.Name))
            {
                exp.Status = SchemaNodeStatus.FunctionExpDuplicateName;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionExpDuplicateName, TYPE_FUNC_EXP_NAME_CONFLICT_ARG);
            }

            // Validate func
            if ((exp.FuncNode ?? (!string.IsNullOrWhiteSpace(exp.Func) ? await context.GetSchemaTypeAsync(exp.Func) : null)) is not FunctionType funcType)
            {
                exp.Status = SchemaNodeStatus.FunctionExpWrongFunc;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFunc, TYPE_FUNC_EXP_CALL_FUNC_NOT_VALID);
            }
            exp.FuncNode = funcType;

            // Generic types
            AnySchemaType?[] genericTypes = funcType.Generic.ToArray();

            // Sets generic type
            var exp1 = exp;

            AnySchemaType? ParseGenericType(AnySchemaType? origin, AnySchemaType? genType = null, bool isReturn = false)
            {
                if (origin is not GenericTypeNode generic)
                {
                    if (origin == null || genType == null || genType.CanBeUseAs(origin)) return origin ?? genType;
                    if (isReturn)
                    {
                        exp1.Status = SchemaNodeStatus.FunctionWrongReturnType;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionWrongReturnType, TYPE_FUNC_EXP_CALL_RETURN_NOT_VALID);
                    }
                    else
                    {
                        exp1.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                    }
                }
                if (genType != null && genType is not GenericTypeNode)
                    genericTypes[generic.GenericIndex - 1] ??= genType;
                return genericTypes[generic.GenericIndex - 1] ?? genType;
            }

            // Validate return value
            exp.SchemaType ??= (!string.IsNullOrWhiteSpace(exp.Return) ? await context.GetSchemaTypeAsync(exp.Return) : null);
            if (exp.SchemaType  is not { IsValueType: true })
            {
                exp.Status = SchemaNodeStatus.FunctionWrongReturnType;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionWrongReturnType, TYPE_FUNC_EXP_CALL_RETURN_NOT_VALID);
            }

            // Match types
            AnySchemaType funcRetType = exp.SchemaType;
            AnySchemaType? arrayEleType = null;
            bool isCollectionExp = (exp.Type ?? ExpressionType.Call) != ExpressionType.Call;
            int arrayIndex = -1;

            // Check call type for return value
            switch (exp.Type)
            {
                case ExpressionType.Map:
                {
                    if (exp.SchemaType is ArrayType { ElementSchemaType: not null } arrayType)
                    {
                        funcRetType = arrayType.ElementSchemaType;
                    }
                    else
                    {
                        exp.Status = SchemaNodeStatus.FunctionExpWrongReturn;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongReturn, TYPE_FUNC_EXP_CALL_RETURN_NOT_VALID);
                    }
                    break;
                }
                case ExpressionType.Reduce:
                {
                    if (funcType.Args.Length is 0 or > 2)
                    {
                        exp.Status = SchemaNodeStatus.FunctionExpWrongFunc;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFunc, TYPE_FUNC_EXP_CALL_FUNC_NOT_VALID);
                    }
                    break;
                }
                case ExpressionType.First:
                case ExpressionType.Last:
                {
                    if (exp.SchemaType is ArrayType)
                    {
                        exp.Status = SchemaNodeStatus.FunctionExpWrongReturn;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongReturn, TYPE_FUNC_EXP_CALL_RETURN_NOT_VALID);
                    }
                    arrayEleType = exp.SchemaType;
                    funcRetType = (await context.GetSchemaTypeAsync(NS_SYSTEM_BOOL))!;
                    break;
                }
                case ExpressionType.Filter:
                {
                    if (exp.SchemaType is ArrayType { ElementSchemaType: not null } arrayType)
                    {
                        arrayEleType = arrayType.ElementSchemaType;
                    }
                    else
                    {
                        exp.Status = SchemaNodeStatus.FunctionExpWrongReturn;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongReturn, TYPE_FUNC_EXP_CALL_RETURN_NOT_VALID);
                    }
                    funcRetType = (await context.GetSchemaTypeAsync(NS_SYSTEM_BOOL))!;
                    break;
                }
                case ExpressionType.Count:
                {
                    if (exp.SchemaType is not ScalarType { IsInt: true })
                    {
                        exp.Status = SchemaNodeStatus.FunctionExpWrongReturn;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongReturn, TYPE_FUNC_EXP_CALL_RETURN_NOT_VALID);
                    }
                    funcRetType = (await context.GetSchemaTypeAsync(NS_SYSTEM_BOOL))!;
                    break;
                }
                case ExpressionType.All:
                case ExpressionType.Any:
                {
                    if (exp.SchemaType is not ScalarType { IsBool: true })
                    {
                        exp.Status = SchemaNodeStatus.FunctionExpWrongReturn;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongReturn, TYPE_FUNC_EXP_CALL_RETURN_NOT_VALID);
                    }
                    funcRetType = (await context.GetSchemaTypeAsync(NS_SYSTEM_BOOL))!;
                    break;
                }
            }

            // Parse generic return type
            ParseGenericType(funcType.ReturnNode, funcRetType);

            // build call arguments
            // check exp call first, get schema type for generic type
            SchemaExpression[] args = new SchemaExpression[funcType.Args.Length];

            // Set argument expression
            async Task SetArgExp(int index, SchemaExpression? argExp = null)
            {
                // Gets the argument definition
                FunctionNodeArgument? argDef = funcType.Args.ElementAtOrDefault(index);
                if (argDef == null)
                {
                    argDef = funcType.Args.LastOrDefault();
                    if (argDef?.Params != true) return;
                }
                
                // Params type check
                AnySchemaType? argType = argDef.SchemaType;
                if (argDef.Params == true && argType is ArrayType arrayType)
                    argType = arrayType.ElementSchemaType;
                
                // Collection expression check
                if (isCollectionExp && arrayIndex == -1 && argType is not ArrayType && (
                        argExp?.SchemaType is ArrayType { ElementSchemaType: not null } || 
                        argExp is FieldAccessExpression { Owner.SchemaType: ArrayType }))
                {
                    if (argDef.Params == true)
                    {
                        exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                    }
                    arrayIndex = index;
                    argExp = new IteratorExpression(argExp);
                    argType = arrayEleType ?? argType;
                }

                // Gets the argument type
                argType = ParseGenericType(argType, argExp?.SchemaType);
                if (argType is not { IsValueType: true })
                {
                    exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                }
                if (argDef.Params == true && argExp == null) return;

                // Default expression & not iterator exp
                if (argDef.Default != null && argExp is not IteratorExpression)
                {
                    if (argExp == null)
                    {
                        argExp = new ConstantExpression(argType.CreateNode(argDef.Default)!);
                    }
                    else
                    {
                        argExp = new DefaultExpression(argExp, argType.CreateNode(argDef.Default)!);
                    }
                }

                // Combine params
                if (argDef.Params ?? false)
                {
                    if (argExp == null) return;
                    var old = args[funcType.Args.Length - 1] as ParamsExpression;
                    args[funcType.Args.Length - 1] = new ParamsExpression(old?.Exps.Append(argExp).ToArray() ?? [argExp], old?.SchemaType ?? (await context.GetArraySchemaTypeAsync(argType))!);
                }

                // Nullable check
                else if (argExp == null && !(argDef.Nullable ?? false))
                {
                    exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                }

                // Record argument expression
                else
                {
                    args[index] = argExp ?? new NullExpression(argType);
                }
            }

            // Check exp access first for generic types
            for (int j = 0; j < funcType.Args.Length; j++)
            {
                FuncCallArg? arg = exp.Args.ElementAtOrDefault(j);
                if (string.IsNullOrWhiteSpace(arg?.Name)) continue;
                if (!GetExpression(arg.Name, out SchemaExpression? argExp))
                {
                    exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                }
                arg.SchemeType ??= !string.IsNullOrWhiteSpace(arg.Type) ? await context.GetSchemaTypeAsync(arg.Type) : argExp!.SchemaType;
                await SetArgExp(j, argExp);
            }

            // Check const value
            for (int j = 0; j < funcType.Args.Length; j++)
            {
                FunctionNodeArgument argDef = funcType.Args[j];
                FuncCallArg? arg = exp.Args.ElementAtOrDefault(j);
                if (!string.IsNullOrWhiteSpace(arg?.Name)) continue;

                // Validate type
                if (arg != null)
                {
                    arg.SchemeType ??= !string.IsNullOrWhiteSpace(arg.Type) ? await context.GetSchemaTypeAsync(arg.Type) : ParseGenericType(argDef.SchemaType);
                    if (arg.SchemeType is not { IsValueType: true })
                    {
                        exp.Status = SchemaNodeStatus.FunctionArgumentWrongType;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionArgumentWrongType, TYPE_FUNC_ARG_TYPE_NOT_VALID);
                    }
                }

                // Const expression
                await SetArgExp(j, arg?.Value == null ? null : new ConstantExpression(arg.SchemeType!.CreateNode(arg.Value)!));
            }

            // For params
            {
                var argDef = funcType.Args.LastOrDefault();
                if (argDef?.Params == true)
                {
                    var paramType = ParseGenericType(argDef.SchemaType);
                    if (paramType is ArrayType arrayType)
                        paramType = arrayType.ElementSchemaType;

                    for (int j = funcType.Args.Length; j < exp.Args.Length; j++)
                    {
                        var arg = exp.Args[j];

                        if (!string.IsNullOrWhiteSpace(arg.Name))
                        {
                            if (!GetExpression(arg.Name, out SchemaExpression? argExp))
                            {
                                exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                                throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                            }
                            arg.SchemeType ??= !string.IsNullOrWhiteSpace(arg.Type) ? await context.GetSchemaTypeAsync(arg.Type) : paramType;
                            await SetArgExp(j, argExp);
                        }
                        else
                        {
                            if (arg.Value == null) continue;

                            arg.SchemeType ??= !string.IsNullOrWhiteSpace(arg.Type) ? await context.GetSchemaTypeAsync(arg.Type) : paramType;
                            if (arg.SchemeType is not { IsValueType: true })
                            {
                                exp.Status = SchemaNodeStatus.FunctionArgumentWrongType;
                                throw new FunctionVisitException(SchemaNodeStatus.FunctionArgumentWrongType,
                                    TYPE_FUNC_ARG_TYPE_NOT_VALID);
                            }
                            await SetArgExp(j, new ConstantExpression(arg.SchemeType.CreateNode(arg.Value)!));
                        }
                    }
                }
            }

            // build function call expression
            VariableExpression callVarExp = new VariableExpression(exp.Name, new FuncCallExpression(funcType, args, exp.SchemaType!, exp.Type ?? ExpressionType.Call));

            // Add to maps
            expMaps[exp.Name] = callVarExp;
            results.Add(callVarExp); // reduce later
        }

        // struct build
        if (!results.Last().SchemaType.CanBeUseAs(returnType))
        {
            if (returnType is StructType { Fields: {  Length: > 0 } } @struct)
            {
                List<StructFieldExpression> fields = [];
                foreach (var f in @struct.Fields.Where(f => !(f.DisplayOnly ?? false)))
                {
                    if (GetExpression(f.Name, out SchemaExpression? fieldExp))
                    {
                        if (!fieldExp!.SchemaType.CanBeUseAs(f.SchemeType!))
                        {
                            func.Status = SchemaNodeStatus.FunctionReturnMemberNotValid;
                            throw new FunctionVisitException(SchemaNodeStatus.FunctionReturnMemberNotValid, TYPE_FUNC_RETURN_STRUCT_MEMBER_NOT_VALID);
                        }
                        fields.Add(new StructFieldExpression(f.Name, fieldExp));
                    }
                    else if (f.Require ?? false)
                    {
                        func.Status = SchemaNodeStatus.FunctionReturnMemberNotValid;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionReturnMemberNotValid, TYPE_FUNC_RETURN_STRUCT_MEMBER_NOT_VALID);
                    }
                }
                results.Add(new VariableExpression(StructResultExpName, new StructResultExpression(fields.ToArray(), @struct)));
            }
            else
            {
                func.Status = SchemaNodeStatus.FunctionWrongReturnType;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionWrongReturnType, TYPE_FUNC_RETURN_NOT_VALID);
            }
        }

        #endregion

        #region Semantic analysis

        // Get visitors
        IExpressionVisitor[] visitors = context.GetServices<IExpressionVisitor>().OrderByDescending(p => p.Priority).ToArray();

        // Inline function
        SchemaExpression Inline(SchemaExpression exp) => exp switch 
        {
            // Inline single access variable expression
            VariableExpression v => accessCount.TryGetValue(v.Name, out int vc) && vc == 1 ? expMaps[v.Name].Value : expMaps[v.Name],
            
            // Inline field access expression
            FieldAccessExpression f => new FieldAccessExpression(Inline(f.Owner), f.FieldName, f.SchemaType),
            
            // Inline iterator expression
            IteratorExpression i => new IteratorExpression(Inline(i.Array)),
            
            // Default expression
            DefaultExpression de => new DefaultExpression(Inline(de.Inner), de.Default),
            
            // Inline params expression
            ParamsExpression pe => new ParamsExpression(pe.Exps.Select(Inline).ToArray(), pe.SchemaType),
            
            // Already inline
            _ => exp
        };
        
        // Convert single access variable expression to inline expression
        List<VariableExpression> final = [];
        foreach (VariableExpression t in results)
        {
            VariableExpression varExp = t;
            SchemaExpression inner = varExp.Value;

            switch (inner)
            {
                // Function analysis and inline conversion
                case FuncCallExpression callExp:
                {
                    // Rebuild call expression
                    inner = new FuncCallExpression(callExp.Function, callExp.Args.Select(Inline).ToArray(), callExp.SchemaType, callExp.ExpType);
                
                    // Apply visitors
                    inner = visitors.Aggregate(inner, (current, visitor) => visitor.VisitExpression(context, current) ?? current);

                    // Replace
                    varExp = new VariableExpression(t.Name, inner);
                    expMaps[varExp.Name] = varExp;

                    break;
                }
                
                // Struct result expression
                case StructResultExpression resultExp:
                {
                    varExp = new VariableExpression(t.Name, new StructResultExpression(resultExp.Fields.Select(f 
                        => new StructFieldExpression(f.Name, Inline(f.Expression))).ToArray(), resultExp.SchemaType));
                    break;
                }
            }

            // Remove the one time access variables
            if (!accessCount.TryGetValue(varExp.Name, out int count) || count == 0 || count > 0)
                final.Add(varExp);
        }

        #endregion

        // Done
        cache = new FunctionTypeSchema(argExps, final.ToArray());
        func.SetRuntimeFuncCache(cache);
        return cache;
    }
    
    /// <summary>
    /// Visit schema expression with all visitors
    /// </summary>
    public static SchemaExpression VisitSchemaExpression(this SchemaContext context, SchemaExpression exp)
    {
        // Apply visitors
        return context.GetServices<IExpressionVisitor>().OrderByDescending(p => p.Priority)
            .Aggregate(exp, (current, visitor) => visitor.VisitExpression(context, current) ?? current);
    }
    
    #region Utility

    private const string StructResultExpName = "_structResult";

    #endregion
}

/// <summary>
/// The function visit result schema
/// </summary>
public record FunctionTypeSchema(ArgumentExpression[] Args, VariableExpression[] Exps);

/// <summary>
/// The function visit exception
/// </summary>
public class FunctionVisitException(SchemaNodeStatus status, string message) : Exception(message)
{
    public SchemaNodeStatus Status { get; } = status;
}
