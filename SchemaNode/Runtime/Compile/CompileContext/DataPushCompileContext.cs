using System.Linq.Expressions;
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
            
            // Check the field if related to third app field
            bool isThirdRelated = CheckThirdRelation(field.Expression);
            
            // only primary key field could be third app field related, if not the push data design is wrong
            if (primaryKeys.Contains(field.Name))
            {
                if (isThirdRelated)
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
                if (isThirdRelated)
                    throw new FunctionVisitException(SchemaNodeStatus.ApplicationPushDataWrongFunc, APP_PUSH_DATA_WRONG_FUNC);
                
                fields[i] = field;
            }
        }
        
        // No third app field related, used it directly
        if (!changed) return schema;
    }
    
    bool CheckThirdRelation(SchemaExpression expression)
    {
        switch (expression)
        {
            case VariableExpression varExp:
                bool result = CheckThirdRelation(varExp.Value);
                if (result) removeExps.Add(varExp.Name);
                return result;
            
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
}