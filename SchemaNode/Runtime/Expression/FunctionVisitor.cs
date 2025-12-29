using SchemaNode.Context;
using SchemaNode.Enum;
using System.Text.RegularExpressions;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The function expression visitor to generate schema expression trees
/// </summary>
public static class FunctionVisitor
{
    /// <summary>
    /// Generate schema expressions for the given function type
    /// <remarks>The schema expression provide the structure of the function expressions, can be used for compiling, sql-conversion and etc.</remarks>
    /// </summary>
    public static async Task<List<VariableExpression>> GenerateSchemaExpressions(this SchemaContext context, FunctionType func)
    {
        // C# function, no need to generate expression trees
        if (func.IsSystemCall) return [];

        // Require exps
        if (func.Exps.Length == 0)
        {
            func.Status = SchemaNodeStatus.FunctionNoExps;
            throw new FunctionVisitException(SchemaNodeStatus.FunctionNoExps, TYPE_FUNC_NEED_EXPS);
        }

        // Validate return type
        AnySchemeType? returnType = func.ReturnNode ?? (!string.IsNullOrWhiteSpace(func.Return) ? await context.GetSchemaTypeAsync(func.Return) : null);
        if (returnType is not { IsValueType: true })
        {
            func.Status = SchemaNodeStatus.FunctionWrongReturnType;
            throw new FunctionVisitException(SchemaNodeStatus.FunctionWrongReturnType, TYPE_FUNC_RETURN_NOT_VALID);
        }
        func.ReturnNode = returnType;

        // Expression cache
        List<VariableExpression> results = [];
        Dictionary<string, VariableExpression> expMaps = [];
        Dictionary<string, int> accessCount = [];

        // Get visitors
        IExpressionVisitor[] visitors = context.GetServices<IExpressionVisitor>().OrderByDescending(p => p.Priority).ToArray();

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
                    AnySchemeType? type = exp.SchemaType;
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
            AnySchemeType? argTypeNode = arg.SchemaType ?? await context.GetSchemaTypeAsync(arg.Type);
            if (argTypeNode is not { IsValueType: true })
            {
                arg.Status = SchemaNodeStatus.FunctionArgumentWrongType;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionArgumentWrongType, TYPE_FUNC_ARG_TYPE_NOT_VALID);
            }
            arg.SchemaType = argTypeNode;
            arg.Type = argTypeNode.Name; // adjust name

            // Create argument expression
            expMaps[arg.Name] = new VariableExpression(arg.Name, new ArgumentExpression(i, argTypeNode));
        }

        // Process exps
        for (int i = 0; i < func.Exps.Length; i++)
        {
            FunctionNodeExpression exp = func.Exps[i];

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
            AnySchemeType?[] genericTypes = funcType.Generic.ToArray();

            // Sets generic type
            AnySchemeType? ParseGenericType(AnySchemeType? origin, AnySchemeType? genType = null, bool isReturn = false)
            {
                if (origin is not GenericTypeNode generic)
                {
                    if (origin == null || genType == null || genType.CanBeUseAs(origin)) return origin ?? genType;
                    if (isReturn)
                    {
                        exp.Status = SchemaNodeStatus.FunctionWrongReturnType;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionWrongReturnType, TYPE_FUNC_EXP_CALL_RETURN_NOT_VALID);
                    }
                    else
                    {
                        exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
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
            AnySchemeType funcReturnType = exp.SchemaType;
            
            // Check call type for return value, can't do this in visitor, we still need handle the call type here
            switch (exp.Type)
            {
                case ExpressionType.Map:
                {
                    if (funcReturnType is not ArrayType { ElementSchemaType: null } arrayType)
                    {
                        exp.Status = SchemaNodeStatus.FunctionWrongReturnType;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionWrongReturnType, TYPE_FUNC_EXP_CALL_RETURN_NOT_VALID);
                    }
                    funcReturnType = arrayType.ElementSchemaType!;
                    break;
                }
                case ExpressionType.First:
                    break;
                case ExpressionType.Last:
                    break;
                case ExpressionType.Filter:
                    break;
                case ExpressionType.Count:
                    break;
                case ExpressionType.All:
                    break;
                case ExpressionType.Any:
                    break;
            }
            
            ParseGenericType(funcType.ReturnNode, exp.SchemaType);

            // build call arguments
            // check exp call first, get schema type for generic type
            SchemaExpression[] args = new SchemaExpression[funcType.Args.Length];

            // Set argument expression
            async Task SetArgExp(int index, SchemaExpression? argExp = null)
            {
                // Gets the argument definition
                var argDef = funcType.Args.ElementAtOrDefault(index);
                if (argDef == null)
                {
                    argDef = funcType.Args.LastOrDefault();
                    if (argDef?.Params != true) return;
                }

                // Gets the argument type
                var argType = ParseGenericType(argDef.SchemaType, argExp?.SchemaType);
                if (argType is not { IsValueType: true })
                {
                    exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                }
                if ((argDef.Params ?? false) && argExp == null) return;

                // Default expression
                if (argDef.Default != null)
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
                    args[funcType.Args.Length - 1] = new ParamsExpression(old?.Exps?.Append(argExp).ToArray() ?? [argExp], old?.SchemaType ?? (await context.GetArraySchemaTypeAsync(argType))!);
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
                var argDef = funcType.Args[j];
                var arg = exp.Args.ElementAtOrDefault(j);
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
                var argDef = funcType.Args[j];
                var arg = exp.Args.ElementAtOrDefault(j);
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

                        if (!string.IsNullOrWhiteSpace(arg?.Name))
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
                            if (arg?.Value == null) continue;

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
            VariableExpression callVarExp = new VariableExpression(exp.Name, new FuncCallExpression(funcType, args, exp.SchemaType!));

            // Add to maps
            expMaps[exp.Name] = callVarExp;
            results.Add(callVarExp); // reduce later
        }

        // struct build
        if (!results.Last().SchemaType.CanBeUseAs(returnType))
        {
            if (returnType is StructType { Fields: {  Length: > 0 } } @struct)
            {
                List<VariableExpression> fields = [];
                foreach (var f in @struct.Fields.Where(f => !(f.DisplayOnly ?? false)))
                {
                    if (GetExpression(f.Name, out SchemaExpression? fieldExp))
                    {
                        if (!fieldExp!.SchemaType.CanBeUseAs(f.SchemeType!))
                        {
                            func.Status = SchemaNodeStatus.FunctionReturnMemberNotValid;
                            throw new FunctionVisitException(SchemaNodeStatus.FunctionReturnMemberNotValid, TYPE_FUNC_RETURN_STRUCT_MEMBER_NOT_VALID);
                        }
                        fields.Add(fieldExp as VariableExpression ?? new VariableExpression(f.Name, fieldExp));
                    }
                    else if (f.Require ?? false)
                    {
                        func.Status = SchemaNodeStatus.FunctionReturnMemberNotValid;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionReturnMemberNotValid, TYPE_FUNC_RETURN_STRUCT_MEMBER_NOT_VALID);
                    }
                }
                results.Add(new VariableExpression(STRUCT_RESULT_EXP_NAME, new StructResultExpression(fields.ToArray(), @struct)));
            }
            else
            {
                func.Status = SchemaNodeStatus.FunctionWrongReturnType;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionWrongReturnType, TYPE_FUNC_RETURN_NOT_VALID);
            }
        }

        // Convert single access variable expression to inline expression
        List<VariableExpression> final = [];
        for (int i = 0; i < results.Count; i++)
        {
            VariableExpression varExp = results[i];
            SchemaExpression inner = varExp.Value;

            if (inner is FuncCallExpression callExp)
            {
                SchemaExpression[] args = new SchemaExpression[callExp.Args.Length];
                for (int j = 0; j < callExp.Args.Length; j++)
                {
                    var arg = callExp.Args[j];
                    if (arg is VariableExpression v && accessCount.TryGetValue(v.Name, out int vc) && vc == 1)
                    {
                        args[j] = expMaps[v.Name].Value;
                    }
                    else if (arg is FieldAccessExpression f && f.Owner is VariableExpression fv && accessCount.TryGetValue(fv.Name, out int fvc) && fvc == 1)
                    {
                        args[j] = new FieldAccessExpression(expMaps[fv.Name].Value, f.FieldName, f.SchemeType);
                    }
                    else
                    {
                        args[j] = arg;
                    }
                }

                inner = new FuncCallExpression(callExp.Function, args, callExp.SchemaType);
            }

            // Apply visitors
            for (int j = 0; j < results.Count; j++)
            {
                foreach (var visitor in visitors)
                    inner = visitor.VisitExpression(context, inner) ?? inner;
            }

            // Replace
            if (varExp.Value != inner)
            {
                varExp = new VariableExpression(results[i].Name, inner);
                expMaps[varExp.Name] = varExp;
            }

            // Remove the one time access variables
            if (!accessCount.TryGetValue(varExp.Name, out int count) || count == 0 || count > 0)
                final.Add(varExp);
        }

        // Done
        return final;
    }
    #region Utility

    const string STRUCT_RESULT_EXP_NAME = "_structResult";

    #endregion
}


/// <summary>
/// The function visit exception
/// </summary>
public class FunctionVisitException(SchemaNodeStatus status, string message) : Exception(message)
{
    public SchemaNodeStatus Status { get; } = status;
}
