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
            ArgumentExp[] args = new ArgumentExp[_thirdFields.Count + 1];
            args[0] = schema.Args[0];
            for (int i = 0; i < _thirdFields.Count; i++) args[i + 1] = _thirdFields[i].Arg;

            // Validate the struct build, all primary keys must be covered, all value must not from third field
            StructResultExp toStruct = schema.Exps.LastOrDefault()?.Value as StructResultExp
                                              ?? throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc);

            foreach (StructFieldExp field in toStruct.Fields)
            {
                if (array.Primary.Contains(field.Name, StringComparer.OrdinalIgnoreCase))
                {
                    // Primary key field, must be field access from argument or constant
                    bool fromValid = false;
                    SchemaExp? keyExp = field.Expression;
                    while (keyExp != null)
                    {
                        switch (keyExp)
                        {
                            case VariableExp varExp:
                                keyExp = varExp.Value;
                                break;

                            case FieldAccessExp fieldAccessExp:
                                SchemaExp owner = fieldAccessExp.Owner;
                                if (owner is VariableExp vExp) owner = vExp.Value;
                                if (owner is ArgumentExp argExp)
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
                                    throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc);
                                }

                                break;

                            case ConstantExp:
                                // Constant is valid
                                keyExp = null;
                                fromValid = true;
                                break;

                            default:
                                throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc);
                        }
                    }

                    if (!fromValid)
                        throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc);
                }
                else if (FromThirdField(field.Expression))
                {
                    throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc);
                }
            }
            
            // Check break logic for third field push keys
            foreach (BreakExp breakExp in schema.Exps.Where(e => e.Value is BreakExp).Select(e => e.Value).Cast<BreakExp>())
            {
                FromThirdField(breakExp.Cond, true);
            }
            
            schema = new FunctionTypeSchema(args, schema.Exps, schema.Return);
        }

        // Return new function type schema, adjusted arguments
        return Function.SetRuntimeFuncCache<DataPushCompileContext, FunctionTypeSchema>(schema)!;
    }

    /// <summary>
    /// Check Get App data function call
    /// </summary>
    public override async Task<SchemaExp> VisitSchemaExpAsync(SchemaExp exp)
    {
        SchemaExp curr = exp;
        if (curr is DefaultExp defaultExp) curr = defaultExp.Inner;
        
        switch (curr)
        {
            // Record the push keys from the third field arguments
            case FieldAccessExp { Owner: ArgumentExp { Index: > 0 } arg } fldAccess:
            {
                var info = _thirdFields.First(a => a.Arg == arg);
                if (!info.PushKeys.Contains(fldAccess.FieldName))
                    info.PushKeys.Add(fldAccess.FieldName);
                break;
            }
            
            // Check the third field function call
            case FuncCallExp funcCallExp:
                switch (funcCallExp.Function.Name)
                {
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getfield)}":
                    {
                        if (funcCallExp.ExpType == ExpressionType.Call &&
                            funcCallExp.Args[0] is ConstantExp { Value.IsEmpty: false } appExp &&
                            funcCallExp.Args[1] is ConstantExp { Value.IsEmpty: false } fieldExp &&
                            funcCallExp.Args[2] is ConstantExp { Value.IsEmpty: false } dataFieldExp
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
                                    throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc);

                                // Key must be field access from argument, constant that generated before, otherwise we can't figure out the 
                                // primary key mapping, just fail it and leave it for further handling
                                for (int i = 0; i < arrayType.Primary.Length; i++)
                                {
                                    SchemaExp? keyExp = funcCallExp.Args[i + 3];
                                    while (keyExp != null)
                                    {
                                        switch (keyExp)
                                        {
                                            case VariableExp varExp:
                                                keyExp = varExp.Value;
                                                break;

                                            case FieldAccessExp fieldAccessExp:
                                                // The owner must be argument type generated before
                                                SchemaExp owner = fieldAccessExp.Owner;
                                                if (owner is VariableExp vExp) owner = vExp.Value;
                                                if (owner is not ArgumentExp arg)
                                                    throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc);

                                                primaryMap.Add(new DataPushPrimaryFieldAccess(arrayType.Primary[i], arg.Index == 0 ? null : _thirdFields[arg.Index - 1].Field, arg.Index, fieldAccessExp.FieldName));
                                                keyExp = null;
                                                break;

                                            case ConstantExp constExp:
                                                primaryMap.Add(new DataPushPrimaryConstant(arrayType.Primary[i], constExp.Value));
                                                keyExp = null;
                                                break;

                                            default:
                                                throw new FunctionVisitException(
                                                    SchemaNodeStatus.ApplicationPushDataWrongFunc);
                                        }
                                    }
                                }

                                // Create new argument expression for the third field data
                                ArgumentExp newArgExp = new($"__third_{thirdField.Name}", _thirdFields.Count + 1, true, arrayStruct);
                                thirdFieldInfo = new DataPushThirdFieldInfo(newArgExp, thirdField.Name, primaryMap.ToArray(),[dataField.Name]);
                                _thirdFields.Add(thirdFieldInfo);
                            }

                            return new FieldAccessExp(thirdFieldInfo.Arg, dataField.Name, dataField.SchemaType!);
                        }

                        // Other app could be system parameters, leave it to the user
                        break;
                    }
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.get)}":
                    {
                        if (funcCallExp.ExpType == ExpressionType.Call &&
                            funcCallExp.Args[0] is ConstantExp { Value.IsEmpty: false }  appExp &&
                            funcCallExp.Args[1] is ConstantExp { Value.IsEmpty: false }  fieldExp
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
                                    throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc);

                                // Key must be field access from argument, constant that generated before, otherwise we can't figure out the 
                                // primary key mapping, just fail it and leave it for further handling
                                for (int i = 0; i < arrayType.Primary.Length; i++)
                                {
                                    SchemaExp? keyExp = funcCallExp.Args[i + 2];
                                    while (keyExp != null)
                                    {
                                        switch (keyExp)
                                        {
                                            case VariableExp varExp:
                                                keyExp = varExp.Value;
                                                break;

                                            case FieldAccessExp fieldAccessExp:
                                                // The owner must be argument generated before
                                                SchemaExp owner = fieldAccessExp.Owner;
                                                if (owner is VariableExp vExp) owner = vExp.Value;
                                                if (owner is not ArgumentExp arg)
                                                    throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc);

                                                primaryMap.Add(new DataPushPrimaryFieldAccess(arrayType.Primary[i], arg.Index == 0 ? null : _thirdFields[arg.Index - 1].Field, arg.Index, fieldAccessExp.FieldName));
                                                keyExp = null;
                                                break;

                                            case ConstantExp constExp:
                                                primaryMap.Add(new DataPushPrimaryConstant(arrayType.Primary[i], constExp.Value));
                                                keyExp = null;
                                                break;

                                            default:
                                                throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc);
                                        }
                                    }
                                }

                                // Create new argument expression for the third field data
                                ArgumentExp newArgExp = new($"__third_{thirdField.Name}", _thirdFields.Count + 1, true, arrayStruct);
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

    private bool NotCondVisit(FieldAccessExp fieldAccessExp)
    {
        SchemaExp owner = fieldAccessExp.Owner;
        if (owner is VariableExp vExp) owner = vExp.Value;
        if (owner is ArgumentExp argExp)
        {
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
            throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc);
        }

        return false;
    }
    
    /// <summary>
    /// No third app field involved
    /// </summary>
    private bool FromThirdField(SchemaExp expression, bool isCond = false)
    {
        return expression switch
        {
            ArgumentExp argExp => argExp.Index > 0, // Only allow first argument
            VariableExp varExp => FromThirdField(varExp.Value, isCond),
            DefaultExp defExp => FromThirdField(defExp.Inner, isCond),
            ParamsExp paramsExp => paramsExp.Exps.Any(e => FromThirdField(e, isCond)),
            CollectionOperator collectionExp => FromThirdField(collectionExp.Root),
            CollectionRootExp iterExp => FromThirdField(iterExp.Collection),
            FuncCallExp funcCallExp => funcCallExp.Function.Name switch
            {
                $"{NS_SYSTEM_DATA}.{nameof(SystemData.getfield)}"
                    or $"{NS_SYSTEM_DATA}.{nameof(SystemData.get)}" => false, // allow parameters
                _ => funcCallExp.Args.Any(e => FromThirdField(e, isCond))
            },
            UnaryLogicExp unaryLogicExp => FromThirdField(unaryLogicExp.Inner, isCond),
            BinaryLogicExp binaryLogicExp => FromThirdField(binaryLogicExp.Left, isCond) || 
                                                    FromThirdField(binaryLogicExp.Right, isCond),
            ConditionalExp condExp => FromThirdField(condExp.Condition, true) ||
                                             FromThirdField(condExp.TrueExp) ||
                                             FromThirdField(condExp.FalseExp),
            FieldAccessExp fieldAccessExp => isCond ? NotCondVisit(fieldAccessExp) : FromThirdField(fieldAccessExp.Owner),
            ArithmeticExp arithmeticExp => arithmeticExp.Args.Any(e => FromThirdField(e)),
            
            // No break/source exp in data push
            DataSourceExp or BreakExp => throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc),
            _ => false,
        };
    }
    
    #endregion
}

/// <summary>
/// The third field info 
/// </summary>
public record DataPushThirdFieldInfo(ArgumentExp Arg, string Field, DataPushPrimaryMap[] PrimaryMap, List<string> PushKeys);

public abstract record DataPushPrimaryMap(string Key);

public record DataPushPrimaryConstant(string Key, AnySchemaNode Value) : DataPushPrimaryMap(Key);

public record DataPushPrimaryFieldAccess(string Key, string? AppField, int ArgIndex, string DataField) : DataPushPrimaryMap(Key);
