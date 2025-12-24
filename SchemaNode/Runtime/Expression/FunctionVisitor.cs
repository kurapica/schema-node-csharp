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
    public static async Task<List<SchemaExpression>> GenerateSchemaExpressions(this SchemaContext context, FunctionType func)
    {
        // C# function, no need to generate expression trees
        if (func.IsSystemCall) return [];
        if (func.Exps.Length == 0)
        {
            func.Status = SchemaNodeStatus.FunctionNoExps;
            throw new FunctionVisitException(SchemaNodeStatus.FunctionNoExps, TYPE_FUNC_NEED_EXPS);
        }

        AnySchemeType? returnType = func.ReturnNode ?? (!string.IsNullOrWhiteSpace(func.Return) ? await context.GetSchemaTypeAsync(func.Return) : null);
        if (returnType is not { IsValueType: true })
        {
            func.Status = SchemaNodeStatus.FunctionWrongReturnType;
            throw new FunctionVisitException(SchemaNodeStatus.FunctionWrongReturnType, TYPE_FUNC_RETURN_NOT_VALID);
        }
        func.ReturnNode = returnType;

        List<SchemaExpression> results = [];
        Dictionary<string, SchemaExpression> expMaps = [];
        Dictionary<string, int> expAccessCount = [];
        IExpressionVisitor[] visitors = context.GetServices<IExpressionVisitor>().OrderByDescending(p => p.Priorty).ToArray();

        // Get expression with visit count++
        bool GetExpression(string name, out SchemaExpression? value)
        {
            string[] access = name.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (expMaps.TryGetValue(access[0], out SchemaExpression? exp))
            {
                if (access.Length > 1)
                {
                    // repace with field access expression
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

                if (expAccessCount.TryGetValue(access[0], out int count))
                    expAccessCount[access[0]] = count + 1;
                else
                    expAccessCount[access[0]] = 1;                

                value = exp;
                return true;
            }

            value = null;
            return false;
        }

        // Process arguments
        for (int i = 0; i < func.Args.Length; i++)
        {
            var arg = func.Args[i];

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
            // Require type
            else if(string.IsNullOrEmpty(arg.Type) || Regex.IsMatch(arg.Type, @"^[tT]\d*$"))
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
            expMaps[arg.Name] = new ArgumentExpression(arg.Name, i, argTypeNode);
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
            FunctionType? funcType = (exp.FuncNode ?? (!string.IsNullOrWhiteSpace(exp.Func) ? await context.GetSchemaTypeAsync(exp.Func) : null)) as FunctionType;
            if (funcType == null)
            {
                exp.Status = SchemaNodeStatus.FunctionExpWrongFunc;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFunc, TYPE_FUNC_EXP_CALL_FUNC_NOT_VALID);
            }
            exp.FuncNode = funcType;

            // Generic types
            AnySchemeType?[] genericTypes = funcType.Generic.ToArray();

            // Sets generic type
            AnySchemeType? SetGenericType(AnySchemeType? origin, AnySchemeType? genType)
            {
                if (origin is not GenericTypeNode generic) return origin ?? genType;
                if (genType != null && genType is not GenericTypeNode)
                    genericTypes[generic.GenericIndex - 1] ??= genType;
                return genericTypes[generic.GenericIndex - 1];
            }

            // Gets type if generic
            AnySchemeType? GetGenericType(AnySchemeType? origin) => origin is not GenericTypeNode generic ? origin : (genericTypes[generic.GenericIndex - 1] ?? generic);

            // validate return value
            AnySchemeType? expRetType = !string.IsNullOrWhiteSpace(exp.Return) ? await context.GetSchemaTypeAsync(exp.Return) : null;
            if (expRetType is not { IsValueType: true })
            {
                exp.Status = SchemaNodeStatus.FunctionWrongReturnType;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionWrongReturnType, TYPE_FUNC_EXP_CALL_RETURN_NOT_VALID);
            }
            exp.SchemaType = expRetType;
            SetGenericType(funcType.ReturnNode, expRetType);

            // build call arguments
            // check exp call first, get schema type for generic type
            SchemaExpression[] args = new SchemaExpression[funcType.Args.Length];

            // Set argument expression
            async Task SetArgExp(int index, SchemaExpression? argExp = null)
            {
                try
                {
                    var argDef = funcType.Args.ElementAtOrDefault(index);
                    if (argDef == null)
                    {
                        argDef = funcType.Args.LastOrDefault();
                        if (argDef?.Params != true) return;
                    }

                    var argType = SetGenericType(argDef.SchemaType, argExp?.SchemaType);

                    // build argument expression
                    if (argDef.Params ?? false)
                    {
                        if (argExp == null) return;
                        var old = args[funcType.Args.Length - 1] as ParamsExpression;
                        args[funcType.Args.Length - 1] = new ParamsExpression(old?.Exps?.Append(argExp)?.ToArray() ?? [argExp], old?.SchemaType ?? (await context.GetArraySchemaTypeAsync(argType))!);
                    }
                    else
                    {
                        args[index] = argExp ?? new NullExpression(argType!);
                    }
                }
                catch (Exception e)
                {
                    context.LogError(e, $"FunctionVisitor.SetArgExp {func.Name} - {exp.Name} - ArgIndex:{index}");
                }
            }

            // check exp access first for generic types
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

            // check const value
            for (int j = 0; j < funcType.Args.Length; j++)
            {
                var argDef = funcType.Args[j];
                var arg = exp.Args.ElementAtOrDefault(j);
                if (!string.IsNullOrWhiteSpace(arg?.Name)) continue;
                if (arg?.Value == null)
                {
                    if (!(argDef.Nullable ?? false))
                    {
                        exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                        throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                    }
                    await SetArgExp(j, null);
                    continue;
                }
                arg.SchemeType ??= !string.IsNullOrWhiteSpace(arg.Type) ? await context.GetSchemaTypeAsync(arg.Type) : GetGenericType(argDef.SchemaType);
                if (arg.SchemeType is not { IsValueType: true })
                {
                    exp.Status = SchemaNodeStatus.FunctionArgumentWrongType;
                    throw new FunctionVisitException(SchemaNodeStatus.FunctionArgumentWrongType, TYPE_FUNC_ARG_TYPE_NOT_VALID);
                }
                await SetArgExp(j, new ConstantExpression(arg.SchemeType.CreateNode(arg.Value)!));
            }

            // For params
            {
                var argDef = funcType.Args.LastOrDefault();
                if (argDef?.Params == true)
                {
                    var paramType = GetGenericType(argDef.SchemaType);
                    if (paramType is ArrayType arrayType)
                        paramType = arrayType.ElementSchemaType;

                    for (int j = funcType.Args.Length; j < exp.Args.Length; j++)
                    {
                        if (argDef?.Params != true) break; // skip rest

                        var arg = exp.Args[j];

                        if (!string.IsNullOrWhiteSpace(arg?.Name))
                        {
                            if (!GetExpression(arg.Name, out SchemaExpression? argExp))
                            {
                                exp.Status = SchemaNodeStatus.FunctionExpWrongFuncArgs;
                                throw new FunctionVisitException(SchemaNodeStatus.FunctionExpWrongFuncArgs, TYPE_FUNC_EXP_ARGS_NOT_VALID);
                            }
                            arg.SchemeType ??= !string.IsNullOrWhiteSpace(arg.Type) ? await context.GetSchemaTypeAsync(arg.Type) : argExp!.SchemaType;
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
            SchemaExpression callExp = new FuncCallExpression(funcType, args, exp.SchemaType!);

            // Apply visitors
            foreach (var visitor in visitors)
            {
                callExp = visitor.VisitExpression(context, callExp) ?? callExp;
            }

            // Validate func call exp, others will be done in visitors
            if (callExp is FuncCallExpression funcCallExp)
            {
                
            }

            // Add to maps
            expMaps[exp.Name] = callExp;
            results.Add(new VariableExpression(exp.Name, callExp)); // reduce later
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
                results.Add(new StructResultExpression(fields.ToArray(), @struct));
            }
            else
            {
                func.Status = SchemaNodeStatus.FunctionWrongReturnType;
                throw new FunctionVisitException(SchemaNodeStatus.FunctionWrongReturnType, TYPE_FUNC_RETURN_NOT_VALID);
            }
        }

        // only keep not one time used exps
        results = results.Where(e => e is not VariableExpression v || !expAccessCount.TryGetValue(v.Name, out int c) || c <= 1).ToList();

        return results;
    }

    /// <summary>
    /// The function visit exception
    /// </summary>
    public class FunctionVisitException(SchemaNodeStatus status, string message) : Exception(message)
    {
        public SchemaNodeStatus Status { get; } = status;
    }
}
