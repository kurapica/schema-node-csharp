using System.Text.Json.Nodes;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;


public abstract record AppSchemaDataFilter;

public enum AppSchemaDataResult
{
    List,
    Count,
    Exist,
    First,
    Last,
    Field,
}

public record AppSchemaDataFilterField(string Field): AppSchemaDataFilter;

public record AppSchemaDataFilterUnary(LogicType Type, AppSchemaDataFilter Operand) : AppSchemaDataFilter;

public record AppSchemaDataFilterBinary(LogicType Type, AppSchemaDataFilter Left, AppSchemaDataFilter Right) : AppSchemaDataFilter;

public record AppSchemaDataFilterValue(object Value) : AppSchemaDataFilter;

public record AppSchemaDataOrder(string Field, bool Desc);

public static class AppSchemaDataFilterExtensions
{
    /// <summary>
    /// Combine two filters with AND ALSO
    /// </summary>
    public static AppSchemaDataFilter AndAlso(this AppSchemaDataFilter left, AppSchemaDataFilter right)
        => left is AppSchemaDataFilterValue ? right
                : right is AppSchemaDataFilterValue ? left
                : new AppSchemaDataFilterBinary(LogicType.AndAlso, left, right);

    public static bool IsValid(this AppSchemaDataFilter filter)
    {
        switch (filter)
        {
            case AppSchemaDataFilterField access:
            {
                return true;
            }
            case AppSchemaDataFilterUnary unary:
                switch (unary.Type)
                {
                    case LogicType.IsNull:
                    case LogicType.IsEmpty:
                    case LogicType.NotNull:
                    case LogicType.NotEmpty:
                        return unary.Operand.IsValid();
                    default:
                        throw new NotSupportedException($"The unary expression type not supported: {unary.Type}");
                }
            case AppSchemaDataFilterBinary binary:
                switch (binary.Type)
                {
                    case LogicType.AndAlso:
                    case LogicType.OrElse:
                    case LogicType.Equal:
                    case LogicType.NotEqual:
                    case LogicType.GreaterThan:
                    case LogicType.GreaterEqual:
                    case LogicType.LessThan:
                    case LogicType.LessEqual:
                        return binary.Left.IsValid() && binary.Right.IsValid();
                    case LogicType.Contains:
                    case LogicType.NotContains:
                        if (binary.Left is not AppSchemaDataFilterValue val || SystemLogic.isempty(val.Value)) return false;
                        return binary.Right.IsValid();  
                    case LogicType.StartsWith:
                    case LogicType.NotStartsWith:
                    case LogicType.EndsWith:
                    case LogicType.NotEndsWith:
                    case LogicType.Match:
                    case LogicType.NotMatch:
                        if (binary.Right is not AppSchemaDataFilterValue swVal || SystemLogic.isempty(swVal.Value)) return false;
                        return binary.Left.IsValid();
                    default:
                        throw new NotSupportedException($"The binary expression type not supported: {binary.Type}");
                }
            case AppSchemaDataFilterValue value:
                return !SystemLogic.isempty(value.Value);
        }

    return false;
    }

    internal static async Task<AppSchemaDataFilter?> ToAppSchemaDataFilterAsync(this JsonObject filter, SchemaContext context, StructType structType, FieldFilter[]? fieldFilters = null)
    {
        if (filter.IsEmpty()) return null;

        AppSchemaDataFilter? accessExp = null;
        foreach ((string key, JsonNode? value) in filter)
        {
            if (value == null || value.IsEmpty()) continue;
            
            // filter
            var filterMode = fieldFilters?.FirstOrDefault(f => f.Filter.Equals(key, StringComparison.OrdinalIgnoreCase))?.Mode 
                             ?? FieldFilterMode.Exactly;

            if (filterMode == FieldFilterMode.Filter)
            {
                // get the function
                FunctionType? funcType = await context.GetSchemaTypeAsync<FunctionType>(key);
                if (funcType != null && value is JsonArray { Count: > 0 } arr)
                {
                    // Call filter func with policy filter compile context
                    AppSchemaDataFilter? f = await funcType.CallAsync<AppSchemaDataFilter, QueryFilterCompileContext>(context, arr.Select(a => (object)a!).ToArray());
                    if (f != null)
                    {
                        accessExp = accessExp != null
                            ? new AppSchemaDataFilterBinary(LogicType.AndAlso, accessExp, f)
                            : f;
                    }
                }
                continue;
            }
            
            // get the field
            StructFieldConfig? field = structType.GetField(key);
            
            // only support scalar or locale string type
            if (field is not { SchemeType: ScalarType } && !NS_SYSTEM_LOCALE_STRING.Equals(field?.SchemeType?.Name)) continue;

            AnySchemaType valueType = field.SchemeType as ScalarType ?? (await context.GetSchemaTypeAsync<ScalarType>(NS_SYSTEM_STRING))!;
            
            var filterExp = value switch
            {
                JsonArray arr => new AppSchemaDataFilterBinary(LogicType.Contains,
                        new AppSchemaDataFilterValue(new ArrayTypeNode(field.SchemeType, arr)),
                        new AppSchemaDataFilterField(key)),
                JsonValue val => new AppSchemaDataFilterBinary(filterMode switch
                        {
                            FieldFilterMode.Exactly => LogicType.Equal,
                            FieldFilterMode.Prefix => LogicType.StartsWith,
                            FieldFilterMode.Suffix => LogicType.EndsWith,
                            FieldFilterMode.Contains => LogicType.Match,
                            _ => LogicType.Equal
                        }, 
                        new AppSchemaDataFilterField(key),
                        new AppSchemaDataFilterValue(valueType.CreateNode(val)!)),
                _ => null
            };
            
            accessExp = accessExp != null && filterExp != null
                ? new AppSchemaDataFilterBinary(LogicType.AndAlso, accessExp, filterExp)
                : filterExp;
        }

        return accessExp;
    }

    /// <summary>
    /// Try to convert the access exp to filter json object
    /// </summary>
    public static JsonObject? ToFilter(this AppSchemaDataFilter accessExp)
    {
        if (accessExp is not AppSchemaDataFilterBinary binaryAccessExp) return null;
        if (binaryAccessExp.Type == LogicType.AndAlso)
        {
            JsonObject? leftFilter = binaryAccessExp.Left.ToFilter();
            JsonObject? rightFilter = binaryAccessExp.Right.ToFilter();
            if (leftFilter == null) return null;
            if (rightFilter == null) return null;

            // merge
            foreach ((string key, JsonNode? value) in rightFilter)
                leftFilter[key] = value?.DeepClone();
            return leftFilter;
        }

        AppSchemaDataFilterField? accessNode = binaryAccessExp.Left as AppSchemaDataFilterField ??
                                               binaryAccessExp.Right as AppSchemaDataFilterField;
        if (accessNode == null) return null;
        AppSchemaDataFilterValue? valueAccess = (binaryAccessExp.Left == accessNode
            ? binaryAccessExp.Right
            : binaryAccessExp.Left) as AppSchemaDataFilterValue;
        if (valueAccess == null) return null;

        switch (binaryAccessExp.Type)
        {
            case LogicType.Equal:
                return new JsonObject
                {
                    [accessNode.Field] = valueAccess.Value.ToJson()
                };
            case LogicType.Contains:
                if (valueAccess.Value is ArrayTypeNode { Count: 1 } arrayNode)
                {
                    return new JsonObject
                    {
                        [accessNode.Field] = arrayNode[0]!.ToJson()
                    };
                }
                break;
        }

        return null;
    }

    /// <summary>
    /// Convert the exp tree to SQL
    /// </summary>
    public static string ToSql(this AppSchemaDataFilter accessExp, ISqlProvider sqlProvider, DynamicTableSchema tableSchema, string prefix = "")
        => ToSql(sqlProvider, accessExp, tableSchema, prefix);

    // To sql
    static string ToSql(ISqlProvider sqlProvider, AppSchemaDataFilter accessExp, DynamicTableSchema tableSchema, string prefix)
    {
        switch (accessExp)
        {
            case AppSchemaDataFilterField access:
            {
                // Check if the field is complex field
                string fieldName = "";
                foreach (DynamicTableField field in  tableSchema.GetDynamicTableFields(access.Field))
                {
                    // Works for locale string key
                    if (field is { Complex: not null, SchemaType: not ScalarType }) continue;
                    fieldName = field.Name;
                    break;
                }
                if (string.IsNullOrWhiteSpace(fieldName))
                    throw new NotSupportedException($"The field not found in table schema: {access.Field}");
                return $"{prefix}{sqlProvider.QuoteField(fieldName)}";
            }
            case AppSchemaDataFilterUnary unary:
                switch (unary.Type)
                {
                    case LogicType.IsNull:
                    case LogicType.IsEmpty:
                        return sqlProvider.IsNull(ToSql(sqlProvider, unary.Operand, tableSchema, prefix));
                    case LogicType.NotNull:
                    case LogicType.NotEmpty:
                        return sqlProvider.IsNotNull(ToSql(sqlProvider, unary.Operand, tableSchema, prefix));
                    default:
                        throw new NotSupportedException($"The unary expression type not supported: {unary.Type}");
                }
            case AppSchemaDataFilterBinary binary:
                switch (binary.Type)
                {
                    case LogicType.AndAlso:
                    case LogicType.OrElse:
                    case LogicType.Equal:
                    case LogicType.NotEqual:
                    case LogicType.GreaterThan:
                    case LogicType.GreaterEqual:
                    case LogicType.LessThan:
                    case LogicType.LessEqual:
                        return sqlProvider.Binary(binary.Type,
                            ToSql(sqlProvider, binary.Left, tableSchema, prefix),
                            ToSql(sqlProvider, binary.Right, tableSchema, prefix));
                    case LogicType.Contains:
                        return sqlProvider.In(
                            ToSql(sqlProvider, binary.Right, tableSchema, prefix),
                            ((binary.Left as AppSchemaDataFilterValue)!.Value as IEnumerable<object>)!);
                    case LogicType.NotContains:
                        return sqlProvider.NotIn(
                            ToSql(sqlProvider, binary.Right, tableSchema, prefix),
                            ((binary.Left as AppSchemaDataFilterValue)!.Value as IEnumerable<object>)!);
                    case LogicType.StartsWith:
                        return sqlProvider.LikeStartsWith(
                            ToSql(sqlProvider, binary.Left, tableSchema, prefix),
                            (string)typeof(string).TryConvert((binary.Right as AppSchemaDataFilterValue)?.Value
                                ?? throw new NotSupportedException("The startsWith right value must be string"))!);
                    case LogicType.NotStartsWith:
                        return sqlProvider.NotLikeStartsWith(
                            ToSql(sqlProvider, binary.Left, tableSchema, prefix),
                            (string)typeof(string).TryConvert((binary.Right as AppSchemaDataFilterValue)?.Value
                                ?? throw new NotSupportedException("The notStartsWith right value must be string"))!);
                    case LogicType.EndsWith:
                        return sqlProvider.LikeEndsWith(
                            ToSql(sqlProvider, binary.Left, tableSchema, prefix),
                            (string)typeof(string).TryConvert((binary.Right as AppSchemaDataFilterValue)?.Value
                                ?? throw new NotSupportedException("The endsWith right value must be string"))!);
                    case LogicType.NotEndsWith:
                        return sqlProvider.NotLikeEndsWith(
                            ToSql(sqlProvider, binary.Left, tableSchema, prefix),
                            (string)typeof(string).TryConvert((binary.Right as AppSchemaDataFilterValue)?.Value
                                ?? throw new NotSupportedException("The notEndsWith right value must be string"))!);
                    case LogicType.Match:
                        return sqlProvider.LikeContains(
                            ToSql(sqlProvider, binary.Left, tableSchema, prefix),
                            (string)typeof(string).TryConvert((binary.Right as AppSchemaDataFilterValue)?.Value
                                ?? throw new NotSupportedException("The match right value must be string"))!);
                    case LogicType.NotMatch:
                        return sqlProvider.NotLikeContains(
                            ToSql(sqlProvider, binary.Left, tableSchema, prefix),
                            (string)typeof(string).TryConvert((binary.Right as AppSchemaDataFilterValue)?.Value
                                ?? throw new NotSupportedException("The notMatch right value must be string"))!);
                    default:
                        throw new NotSupportedException($"The binary expression type not supported: {binary.Type}");
                }
            case AppSchemaDataFilterValue value:
                return sqlProvider.Literal(value.Value);
        }

        throw new NotSupportedException("The expression type not supported");
    }

}
