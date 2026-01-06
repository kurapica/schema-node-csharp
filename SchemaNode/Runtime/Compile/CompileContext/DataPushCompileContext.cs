using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using static SchemaNode.Utility.Constant;
// ReSharper disable NotAccessedPositionalProperty.Global

namespace SchemaNode.Runtime;

/// <summary>
/// The data push compile context
/// </summary>
public class DataPushCompileContext(SchemaContext context, FunctionType pushFuncType, AppType appType, AppFieldType to) : CompileContext(context, pushFuncType)
{
    #region Push Info
    
    private readonly List<DataPushThirdFieldInfo> _thirdFields = [];
    
    /// <summary>
    /// The data push third fields
    /// </summary>
    public DataPushThirdFieldInfo[] ThirdFields => _thirdFields.ToArray();
    
    #endregion
    
    #region Implementation of CompileContext
    
    // <inheritdoc/>
    public override async Task<FunctionTypeSchema> VisitFunctionType()
    {
        FunctionTypeSchema schema = await base.VisitFunctionType();

        // No third app field involved
        if (to.SchemaType is not ArrayType array || array.Primary == null || array.Primary.Length == 0)
            return _thirdFields.Any() ? throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc, APP_PUSH_DATA_WRONG_FUNC) : schema;
        
        // Add third app field arguments
        ArgumentExpression[] args = new ArgumentExpression[_thirdFields.Count + 1];
        args[0] = schema.Args[0];
        for (int i = 0; i < _thirdFields.Count; i++)
            args[i + 1] = _thirdFields[i].Arg;
        
        // Validate the struct build, all primary keys must be covered, all value must not from third field
        StructResultExpression toStruct = schema.Exps.LastOrDefault()?.Value as StructResultExpression 
            ?? throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc, APP_PUSH_DATA_WRONG_FUNC);

        foreach (StructFieldExpression field in toStruct.Fields)
        {
            if (array.Primary.Contains(field.Name, StringComparer.OrdinalIgnoreCase))
            {
                // Primary key field, must be field access from argument or constant
                bool fromValid = false;
                SchemaExpression? keyExp = field.Expression;
                while (keyExp != null)
                {
                    switch (keyExp)
                    {
                        case VariableExpression varExp:
                            keyExp = varExp.Value;
                            break;

                        case FieldAccessExpression fieldAccessExp:
                            if (fieldAccessExp.Owner is ArgumentExpression)
                            {
                                keyExp = null;
                                fromValid = true;
                            }
                            else
                            {
                                throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc, APP_PUSH_DATA_WRONG_FUNC);
                            }
                            break;

                        case ConstantExpression:
                            // Constant is valid
                            keyExp = null;
                            fromValid = true;
                            break;

                        default:
                            throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc, APP_PUSH_DATA_WRONG_FUNC);
                    }
                }

                if (!fromValid)
                    throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc, APP_PUSH_DATA_WRONG_FUNC);
            }
            else if (FromThirdField(field.Expression))
            {
                throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc, APP_PUSH_DATA_WRONG_FUNC);
            }
        }
        
        // Return new function type schema, adjusted arguments
        return new FunctionTypeSchema(args, schema.Exps, schema.Return);
    }

    /// <summary>
    /// Check Get App data function call
    /// </summary>
    public override async Task<SchemaExpression> VisitSchemaExpAsync(SchemaExpression exp)
    {
        if (exp is FuncCallExpression funcCallExp)
        {
            switch (funcCallExp.Function.Name)
            {
                case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdata)}":
                    // parameter fetch should not be used in data push
                    throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc, APP_PUSH_DATA_WRONG_FUNC);
                
                case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdatabyonekey)}":
                case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdatabythreekey)}":
                case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdatabyfourkey)}":
                {
                    if (funcCallExp.ExpType == ExpressionType.Call &&
                        funcCallExp.Args[0] is ConstantExpression appExp && appExp.Value.ToValue<string>() == appType.Name &&
                        funcCallExp.Args[1] is ConstantExpression fieldExp && appType.GetField(fieldExp.Value.ToValue<string>()) is { SchemaType: ArrayType { ElementSchemaType: StructType arrayStruct} arrayType } thirdField &&
                        funcCallExp.Args[2] is ConstantExpression dataFieldExp && arrayStruct.GetField(dataFieldExp.Value.ToValue<string>() ?? string.Empty) is { SchemeType: {} } dataField
                    ){
                        DataPushThirdFieldInfo? thirdFieldInfo = _thirdFields.FirstOrDefault(a => a.Field == thirdField.Name);
                        
                        // Combine the third field query
                        if (thirdFieldInfo == null)
                        {
                            Dictionary<string, SchemaExpression> primaryMap = [];
                            string[] primaryKeys = arrayType.Primary ?? [];

                            if (primaryKeys.Length != funcCallExp.Args.Length - 4)
                                throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc,
                                    APP_PUSH_DATA_WRONG_FUNC);

                            // Key must be field access from argument, constant that generated before, otherwise we can't figure out the 
                            // primary key mapping, just fail it and leave it for further handling
                            for (int i = 0; i < primaryKeys.Length; i++)
                            {
                                SchemaExpression? keyExp = funcCallExp.Args[i + 4];
                                while (keyExp != null)
                                {
                                    switch (keyExp)
                                    {
                                        case VariableExpression varExp:
                                            keyExp = varExp.Value;
                                            break;

                                        case FieldAccessExpression fieldAccessExp:
                                            // The owner must be the from type or other third app field argument generated before
                                            if (fieldAccessExp.Owner is not ArgumentExpression)
                                                throw new FunctionVisitException(
                                                    SchemaNodeStatus.ApplicationPushDataWrongFunc,
                                                    APP_PUSH_DATA_WRONG_FUNC);

                                            primaryMap[primaryKeys[i]] = fieldAccessExp;
                                            keyExp = null;
                                            break;

                                        case ConstantExpression constExp:
                                            primaryMap[primaryKeys[i]] = constExp;
                                            keyExp = null;
                                            break;

                                        default:
                                            throw new FunctionVisitException(
                                                SchemaNodeStatus.ApplicationPushDataWrongFunc,
                                                APP_PUSH_DATA_WRONG_FUNC);
                                    }
                                }
                            }

                            // Create new argument expression for the third field data
                            ArgumentExpression newArgExp = new($"__third_{thirdField.Name}",_thirdFields.Count + 1, arrayStruct);
                            thirdFieldInfo = new DataPushThirdFieldInfo(newArgExp, thirdField.Name, primaryMap);
                            _thirdFields.Add(thirdFieldInfo);
                        }

                        return new FieldAccessExpression(thirdFieldInfo.Arg, dataField.Name, dataField.SchemeType);
                    }

                    // Other app could be system parameters, leave it to the user
                    break;
                }
                case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdata)}":
                    // parameter fetch should not be used in data push
                    throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc, APP_PUSH_DATA_WRONG_FUNC);
                
                case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdatabyonekey)}":
                case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdatabytwokey)}":
                case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdatabythreekey)}":
                case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdatabyfourkey)}":
                {
                    if (funcCallExp.ExpType == ExpressionType.Call &&
                        funcCallExp.Args[0] is ConstantExpression appExp && appExp.Value.ToValue<string>() == appType.Name &&
                        funcCallExp.Args[1] is ConstantExpression fieldExp && appType.GetField(fieldExp.Value.ToValue<string>()) is { SchemaType: ArrayType { ElementSchemaType: StructType arrayStruct} arrayType } thirdField
                    ){
                        DataPushThirdFieldInfo? thirdFieldInfo = _thirdFields.FirstOrDefault(a => a.Field == thirdField.Name);
                        
                        // Combine the third field query
                        if (thirdFieldInfo == null)
                        {
                            Dictionary<string, SchemaExpression> primaryMap = [];
                            string[] primaryKeys = arrayType.Primary ?? [];

                            if (primaryKeys.Length != funcCallExp.Args.Length - 4)
                                throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc,
                                    APP_PUSH_DATA_WRONG_FUNC);

                            // Key must be field access from argument, constant that generated before, otherwise we can't figure out the 
                            // primary key mapping, just fail it and leave it for further handling
                            for (int i = 0; i < primaryKeys.Length; i++)
                            {
                                SchemaExpression? keyExp = funcCallExp.Args[i + 4];
                                while (keyExp != null)
                                {
                                    switch (keyExp)
                                    {
                                        case VariableExpression varExp:
                                            keyExp = varExp.Value;
                                            break;

                                        case FieldAccessExpression fieldAccessExp:
                                            // The owner must be the from type or other third app field argument generated before
                                            if (fieldAccessExp.Owner is not ArgumentExpression)
                                                throw new FunctionVisitException(
                                                    SchemaNodeStatus.ApplicationPushDataWrongFunc,
                                                    APP_PUSH_DATA_WRONG_FUNC);

                                            primaryMap[primaryKeys[i]] = fieldAccessExp;
                                            keyExp = null;
                                            break;

                                        case ConstantExpression constExp:
                                            primaryMap[primaryKeys[i]] = constExp;
                                            keyExp = null;
                                            break;

                                        default:
                                            throw new FunctionVisitException(
                                                SchemaNodeStatus.ApplicationPushDataWrongFunc,
                                                APP_PUSH_DATA_WRONG_FUNC);
                                    }
                                }
                            }

                            // Create new argument expression for the third field data
                            ArgumentExpression newArgExp = new($"__third_{thirdField.Name}",_thirdFields.Count + 1, arrayStruct);
                            thirdFieldInfo = new DataPushThirdFieldInfo(newArgExp, thirdField.Name, primaryMap);
                            _thirdFields.Add(thirdFieldInfo);
                        }

                        return thirdFieldInfo.Arg;
                    }

                    // Other app could be system parameters, leave it to the user
                    break;
                }
            }
        }
        
        return await base.VisitSchemaExpAsync(exp);
    }
    
    /// <summary>
    /// No third app field involved
    /// </summary>
    private static bool FromThirdField(SchemaExpression expression)
    {
        return expression switch
        {
            ArgumentExpression argExp => argExp.Index > 0, // Only allow first argument
            VariableExpression varExp => FromThirdField(varExp.Value),
            DefaultExpression defExp => FromThirdField(defExp.Inner),
            ParamsExpression paramsExp => paramsExp.Exps.Any(FromThirdField),
            IteratorExpression iterExp => FromThirdField(iterExp.Array),
            FuncCallExpression funcCallExp => funcCallExp.Function.Name switch
            {
                $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdata)}"
                    or $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdatabyonekey)}"
                    or $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdatabythreekey)}"
                    or $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdatabyfourkey)}"
                    or $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdata)}"
                    or $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdatabyonekey)}"
                    or $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdatabytwokey)}"
                    or $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdatabythreekey)}"
                    or $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdatabyfourkey)}" => false, // allow parameters
                _ => funcCallExp.Args.Any(FromThirdField)
            },
            UnaryLogicExpression unaryLogicExp => FromThirdField(unaryLogicExp.Inner),
            BinaryLogicExpression binaryLogicExp => FromThirdField(binaryLogicExp.Left) || 
                                                    FromThirdField(binaryLogicExp.Right),
            ConditionalExpression condExp => FromThirdField(condExp.Condition) || 
                                             FromThirdField(condExp.TrueExp) ||
                                             FromThirdField(condExp.FalseExp),
            FieldAccessExpression fieldAccessExp => FromThirdField(fieldAccessExp.Owner),
            CollectionExpression collectionExp => FromThirdField(collectionExp.Iterator) ||
                                                  FromThirdField(collectionExp.Loop),
            UnaryArithmeticExpression unaryArithmeticExp => FromThirdField(unaryArithmeticExp.Inner),
            BinaryArithmeticExpression binaryArithmeticExp => FromThirdField(binaryArithmeticExp.Left) ||
                                                              FromThirdField(binaryArithmeticExp.Right),
            // No break/source exp in data push
            DataSourceExpression or DataResultExpression or BreakExpression =>
                throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc, APP_PUSH_DATA_WRONG_FUNC),
            _ => false,
        };
    }
    
    #endregion
}

/// <summary>
/// The third field info 
/// </summary>
public record DataPushThirdFieldInfo(ArgumentExpression Arg, String Field, Dictionary<string, SchemaExpression> Primarys);
