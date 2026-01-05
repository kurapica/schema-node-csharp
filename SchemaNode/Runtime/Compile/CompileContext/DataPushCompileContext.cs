using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The data push compile context
/// </summary>
public class DataPushCompileContext(SchemaContext context, FunctionType funcType, AppType appType, AppFieldType from, AppFieldType to) : CompileContext(context, funcType)
{
    private HashSet<string> removeExps = [];
    
    // <inheritdoc/>
    public override async Task<FunctionTypeSchema> VisitFunctionType()
    {
        FunctionTypeSchema schema = await base.VisitFunctionType();

        // No third app field for data push
        if (to.SchemaType is not ArrayType array || array.Primary == null || array.Primary.Length == 0) return schema;
        
        // Gets the struct build expression
        string[] primaryKeys = array.Primary;
        if (schema.Exps.LastOrDefault()?.Value is not StructResultExpression structExp)
            throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc, APP_PUSH_DATA_WRONG_FUNC);

        StructFieldExpression[] fields = new  StructFieldExpression[structExp.Fields.Length];
        bool changed = false;
        for(int i = 0; i < structExp.Fields.Length; i++)
        {
            StructFieldExpression field = structExp.Fields[i];
                        
            // only primary key field could be third app field related, if not the push data design is wrong
            if (primaryKeys.Contains(field.Name))
            {
                if (CheckThirdField(field.Expression))
                {
                    changed = true;
                }
                else
                {
                    fields[i] = field;
                }
            }
            else
            {
                // if related to thrid app field, fail the visit
                if (!AvoidThirdField(field.Expression))
                    throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc, APP_PUSH_DATA_WRONG_FUNC);
                
                fields[i] = field;
            }
        }
        
        // No third app field related, used it directly
        if (!changed) return schema;
    }

    /// <summary>
    /// Check if require third app field
    /// </summary>
    /// <param name="expression"></param>
    /// <returns></returns>
    bool CheckThirdField(SchemaExpression expression)
    {
        switch (expression)
        {
            case VariableExpression varExp:
                break;
            
            case ArgumentExpression argExp:
                break;
            case ConstantExpression constExp:
                break;
            case DefaultExpression defExp:
                break;
            case NullExpression nullExp:
                break;
            case ParamsExpression paramsExp:
                break;
            case IteratorExpression iterExp:
                break;
            case FuncCallExpression funcCallExp:
                switch (funcCallExp.Function.Name)
                {
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdata)}":
                        break;
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdatabyonekey)}":
                        break;
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdatabythreekey)}":
                        break;
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdatabyfourkey)}":
                        break;
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdata)}":
                        break;
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdatabyonekey)}":
                        break;
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdatabytwokey)}":
                        break;
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdatabythreekey)}":
                        break;
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdatabyfourkey)}":
                        break;
                }
                break;
            case UnaryLogicExpression unaryLogicExp:
                break;
            case BinaryLogicExpression binaryLogicExp:
                break;
            case ConditionalExpression condExp:
                break;
            case FieldAccessExpression fieldAccessExp:
                break;
            case DataSourceExpression dataSourceExp:
                break;
            case DataResultExpression dataResultExp:
                break;
            case CollectionExpression collectionExp:
                break;
            case BreakExpression breakExp:
                break;
            case UnaryArithmeticExpression unaryArithmeticExp:
                break;
            case BinaryArithmeticExpression binaryArithmeticExp:
                break;
        }
    }

    /// <summary>
    /// No third app field involved
    /// </summary>
    /// <param name="expression"></param>
    /// <returns></returns>
    bool NoThirdField(SchemaExpression expression)
    {
        switch (expression)
        {
            case VariableExpression varExp:
                return NoThirdField(varExp.Value);

            case ArgumentExpression:
            case ConstantExpression:
            case NullExpression:
                return true;

            case DefaultExpression defExp:
                return NoThirdField(defExp.Inner);

            case ParamsExpression paramsExp:
                return paramsExp.Exps.All(NoThirdField);

            case IteratorExpression iterExp:
                return NoThirdField(iterExp.Array);

            case FuncCallExpression funcCallExp:
                // @TODO: more check
                switch (funcCallExp.Function.Name)
                {
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdata)}":
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdatabyonekey)}":
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdatabythreekey)}":
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappfdatabyfourkey)}":
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdata)}":
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdatabyonekey)}":
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdatabytwokey)}":
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdatabythreekey)}":
                    case $"{NS_SYSTEM_DATA}.{nameof(SystemData.getappdatabyfourkey)}":
                        return false;
                }
                return true;
            case UnaryLogicExpression unaryLogicExp:
                return NoThirdField(unaryLogicExp.Inner);

            case BinaryLogicExpression binaryLogicExp:
                return NoThirdField(binaryLogicExp.Left) && NoThirdField(binaryLogicExp.Right);

            case ConditionalExpression condExp:
                return NoThirdField(condExp.Condition) && NoThirdField(condExp.TrueExp) && NoThirdField(condExp.FalseExp);

            case FieldAccessExpression fieldAccessExp:
                return NoThirdField(fieldAccessExp.Owner);

            case DataSourceExpression dataSourceExp:
            case DataResultExpression dataResultExp:
                return false;

            case CollectionExpression collectionExp:
                return NoThirdField(collectionExp.Iterator) && NoThirdField(collectionExp.Loop);

            case BreakExpression breakExp:
                // No break exp in data push
                throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc, APP_PUSH_DATA_WRONG_FUNC);

            case UnaryArithmeticExpression unaryArithmeticExp:
                return NoThirdField(unaryArithmeticExp.Inner);

            case BinaryArithmeticExpression binaryArithmeticExp:
                return NoThirdField(binaryArithmeticExp.Left) && NoThirdField(binaryArithmeticExp.Right);

            default:
                return true;
        }
    }
}