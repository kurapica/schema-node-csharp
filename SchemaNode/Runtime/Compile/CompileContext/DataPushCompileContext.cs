using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Node;
using static SchemaNode.Utility.Constant;
// ReSharper disable NotAccessedPositionalProperty.Global

namespace SchemaNode.Runtime;

/// <summary>
/// The data push compile context
/// </summary>
public class DataPushCompileContext(SchemaContext context, FunctionType function) : CompileContext(context, function)
{
    #region Push Info
    
    private readonly List<DataPushThirdFieldInfo> _thirdFields = [];
    
    /// <summary>
    /// The data push third fields
    /// </summary>
    public DataPushThirdFieldInfo[] ThirdFields => _thirdFields.ToArray();

    /// <summary>
    /// The application type
    /// </summary>
    private AppType? _appType;
    
    #endregion
    
    #region Implementation of CompileContext
    
    // <inheritdoc/>
    public override async Task<FunctionTypeSchema> VisitFunctionType()
    {
        if (Function.TryGetRuntimeFuncCache<DataPushCompileContext, FunctionTypeSchema>(out FunctionTypeSchema? schema))
            return schema!;
        
        schema = await base.VisitFunctionType();
        
        // No third app field involved
        AppFieldType? to = _appType?.Fields?.FirstOrDefault(f =>
            f.SchemaType is ArrayType arrayType && arrayType.ElementSchemaType == Function.ReturnNode);
        
        if (to?.SchemaType is ArrayType { Primary.Length: > 0 } array && _thirdFields.Count > 0)
        {
            // Add third app field arguments
            ArgumentExpression[] args = new ArgumentExpression[_thirdFields.Count + 1];
            args[0] = schema.Args[0];
            for (int i = 0; i < _thirdFields.Count; i++) args[i + 1] = _thirdFields[i].Arg;

            // Validate the struct build, all primary keys must be covered, all value must not from third field
            StructResultExpression toStruct = schema.Exps.LastOrDefault()?.Value as StructResultExpression
                                              ?? throw new FunctionVisitException(
                                                  SchemaNodeStatus.ApplicationPushDataWrongFunc,
                                                  APP_PUSH_DATA_WRONG_FUNC);

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
                                SchemaExpression owner = fieldAccessExp.Owner;
                                if (owner is VariableExpression vExp) owner = vExp.Value;
                                if (owner is ArgumentExpression argExp)
                                {
                                    keyExp = null;
                                    fromValid = true;

                                    // Record the thrid field's push key for later compare
                                    if (argExp.Index > 0)
                                    {
                                        DataPushThirdFieldInfo fldInfo = _thirdFields.First(a => a.Arg == argExp);
                                        if (!fldInfo.PushKeys.Contains(fieldAccessExp.FieldName))
                                            fldInfo.PushKeys.Add(fieldAccessExp.FieldName);
                                    }
                                }
                                else
                                {
                                    throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc,
                                        APP_PUSH_DATA_WRONG_FUNC);
                                }

                                break;

                            case ConstantExpression:
                                // Constant is valid
                                keyExp = null;
                                fromValid = true;
                                break;

                            default:
                                throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc,
                                    APP_PUSH_DATA_WRONG_FUNC);
                        }
                    }

                    if (!fromValid)
                        throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc,
                            APP_PUSH_DATA_WRONG_FUNC);
                }
                else if (FromThirdField(field.Expression))
                {
                    throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc,
                        APP_PUSH_DATA_WRONG_FUNC);
                }
            }

            schema = new FunctionTypeSchema(args, schema.Exps, schema.Return);
        }

        // Return new function type schema, adjusted arguments
        return Function.SetRuntimeFuncCache<DataPushCompileContext, FunctionTypeSchema>(schema)!;
    }

    /// <summary>
    /// Check Get App data function call
    /// </summary>
    public override async Task<SchemaExpression> VisitSchemaExpAsync(SchemaExpression exp)
    {
        switch (exp)
        {
            // Record the push keys from the third field arguments
            case FieldAccessExpression { Owner: ArgumentExpression { Index: > 0 } arg } fldAcces:
            {
                var info = _thirdFields.First(a => a.Arg == arg);
                if (!info.PushKeys.Contains(fldAcces.FieldName))
                    info.PushKeys.Add(fldAcces.FieldName);
                break;
            }
            
            // Check the third field function call
            case FuncCallExpression funcCallExp:
                switch (funcCallExp.Function.Name)
                {
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdata)}":
                        // parameter fetch should not be used in data push
                        throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc,
                            APP_PUSH_DATA_WRONG_FUNC);

                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdatabyonekey)}":
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdatabythreekey)}":
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdatabyfourkey)}":
                    {
                        if (funcCallExp.ExpType == ExpressionType.Call &&
                            funcCallExp.Args[0] is ConstantExpression { Value.IsEmpty: false } appExp &&
                            funcCallExp.Args[1] is ConstantExpression { Value.IsEmpty: false } fieldExp &&
                            funcCallExp.Args[2] is ConstantExpression { Value.IsEmpty: false } dataFieldExp
                           )
                        {
                            _appType ??= await Context.GetAppTypeAsync(appExp.Value.ToValue<string>()!);
                            if (_appType == null || _appType.Name != appExp.Value.ToValue<string>()) break;
                            var thirdField = _appType.GetField(fieldExp.Value.ToValue<string>());
                            if (thirdField?.SchemaType is not ArrayType { ElementSchemaType: StructType arrayStruct, Primary: { Length: > 0 } } arrayType) break;
                            var dataField = arrayStruct.GetField(dataFieldExp.Value.ToValue<string>()!);
                            if (dataField == null) break;
                            
                            DataPushThirdFieldInfo? thirdFieldInfo = _thirdFields.FirstOrDefault(a => a.Field == thirdField.Name);

                            // Combine the third field query
                            if (thirdFieldInfo == null)
                            {
                                List<DataPushPrimaryMap> primaryMap = [];

                                if (arrayType.Primary.Length != funcCallExp.Args.Length - 4)
                                    throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc,
                                        APP_PUSH_DATA_WRONG_FUNC);

                                // Key must be field access from argument, constant that generated before, otherwise we can't figure out the 
                                // primary key mapping, just fail it and leave it for further handling
                                for (int i = 0; i < arrayType.Primary.Length; i++)
                                {
                                    SchemaExpression? keyExp = funcCallExp.Args[i + 3];
                                    while (keyExp != null)
                                    {
                                        switch (keyExp)
                                        {
                                            case VariableExpression varExp:
                                                keyExp = varExp.Value;
                                                break;

                                            case FieldAccessExpression fieldAccessExp:
                                                // The owner must be argument type generated before
                                                SchemaExpression owner = fieldAccessExp.Owner;
                                                if (owner is VariableExpression vExp) owner = vExp.Value;
                                                
                                                if (owner is not ArgumentExpression arg)
                                                    throw new FunctionVisitException(
                                                        SchemaNodeStatus.ApplicationPushDataWrongFunc,
                                                        APP_PUSH_DATA_WRONG_FUNC);

                                                primaryMap.Add(new DataPushPrimaryFieldAccess(arrayType.Primary[i], arg.Index == 0 ? null : _thirdFields[arg.Index - 1].Field, arg.Index, fieldAccessExp.FieldName));
                                                keyExp = null;
                                                break;

                                            case ConstantExpression constExp:
                                                primaryMap.Add(new DataPushPrimaryConstant(arrayType.Primary[i], constExp.Value));
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
                                ArgumentExpression newArgExp = new($"__third_{thirdField.Name}", _thirdFields.Count + 1, true, arrayStruct);
                                thirdFieldInfo = new DataPushThirdFieldInfo(newArgExp, thirdField.Name, primaryMap.ToArray(),[dataField.Name]);
                                _thirdFields.Add(thirdFieldInfo);
                            }

                            return new FieldAccessExpression(thirdFieldInfo.Arg, dataField.Name, dataField.SchemeType!);
                        }

                        // Other app could be system parameters, leave it to the user
                        break;
                    }
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdata)}":
                        // parameter fetch should not be used in data push
                        throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc,
                            APP_PUSH_DATA_WRONG_FUNC);

                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdatabyonekey)}":
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdatabytwokey)}":
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdatabythreekey)}":
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdatabyfourkey)}":
                    {
                        if (funcCallExp.ExpType == ExpressionType.Call &&
                            funcCallExp.Args[0] is ConstantExpression { Value.IsEmpty: false }  appExp &&
                            funcCallExp.Args[1] is ConstantExpression { Value.IsEmpty: false }  fieldExp
                           )
                        {
                            _appType ??= await Context.GetAppTypeAsync(appExp.Value.ToValue<string>()!);
                            if (_appType == null || _appType.Name != appExp.Value.ToValue<string>()) break;
                            var thirdField = _appType.GetField(fieldExp.Value.ToValue<string>());
                            if (thirdField?.SchemaType is not ArrayType { ElementSchemaType: StructType arrayStruct, Primary: { Length: > 0 } } arrayType) break;
                            
                            DataPushThirdFieldInfo? thirdFieldInfo =
                                _thirdFields.FirstOrDefault(a => a.Field == thirdField.Name);

                            // Combine the third field query
                            if (thirdFieldInfo == null)
                            {
                                List<DataPushPrimaryMap> primaryMap = [];

                                if (arrayType.Primary.Length != funcCallExp.Args.Length - 3)
                                    throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc,
                                        APP_PUSH_DATA_WRONG_FUNC);

                                // Key must be field access from argument, constant that generated before, otherwise we can't figure out the 
                                // primary key mapping, just fail it and leave it for further handling
                                for (int i = 0; i < arrayType.Primary.Length; i++)
                                {
                                    SchemaExpression? keyExp = funcCallExp.Args[i + 2];
                                    while (keyExp != null)
                                    {
                                        switch (keyExp)
                                        {
                                            case VariableExpression varExp:
                                                keyExp = varExp.Value;
                                                break;

                                            case FieldAccessExpression fieldAccessExp:
                                                // The owner must be argument generated before
                                                SchemaExpression owner = fieldAccessExp.Owner;
                                                if (owner is VariableExpression vExp) owner = vExp.Value;
                                                if (owner is not ArgumentExpression arg)
                                                    throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc, APP_PUSH_DATA_WRONG_FUNC);

                                                primaryMap.Add(new DataPushPrimaryFieldAccess(arrayType.Primary[i], arg.Index == 0 ? null : _thirdFields[arg.Index - 1].Field, arg.Index, fieldAccessExp.FieldName));
                                                keyExp = null;
                                                break;

                                            case ConstantExpression constExp:
                                                primaryMap.Add(new DataPushPrimaryConstant(arrayType.Primary[i], constExp.Value));
                                                keyExp = null;
                                                break;

                                            default:
                                                throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc, APP_PUSH_DATA_WRONG_FUNC);
                                        }
                                    }
                                }

                                // Create new argument expression for the third field data
                                ArgumentExpression newArgExp = new($"__third_{thirdField.Name}", _thirdFields.Count + 1, true, arrayStruct);
                                thirdFieldInfo = new DataPushThirdFieldInfo(newArgExp, thirdField.Name, primaryMap.ToArray(), []);
                                _thirdFields.Add(thirdFieldInfo);
                            }

                            return thirdFieldInfo.Arg;
                        }

                        // Other app could be system parameters, leave it to the user
                        break;
                    }
                }
                break;
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
                                                  FromThirdField(collectionExp.Expression),
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
public record DataPushThirdFieldInfo(ArgumentExpression Arg, string Field, DataPushPrimaryMap[] PrimaryMap, List<string> PushKeys);

public abstract record DataPushPrimaryMap(string Key);

public record DataPushPrimaryConstant(string Key, AnySchemaNode Value) : DataPushPrimaryMap(Key);

public record DataPushPrimaryFieldAccess(string Key, string? AppField, int ArgIndex, string DataField) : DataPushPrimaryMap(Key);
