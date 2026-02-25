using SchemaNode.Runtime;

namespace SchemaNode.Components;

/// <summary>
/// The default implementation of ISqlProvider, which uses minimal SQL syntax.
/// </summary>
public class DefaultSqlProvider : ISqlProvider
{
    public string QuoteField(string fieldName) => fieldName;
    public string QuoteTable(string tableName) => tableName;
    public string QuoteIndex(string indexName) => indexName;
    public string GenParameterName(int index) => $"@p{index}";
    public string Concat(string left, string right) => $"CONCAT({left}, {right})";
    public string LikeContains(string field, string param) => $"{field} LIKE CONCAT('%', {param}, '%')";
    public string LikeStartsWith(string field, string param) => $"{field} LIKE CONCAT({param}, '%')";
    public string LikeEndsWith(string field, string param) => $"{field} LIKE CONCAT('%', {param})";

    public string NotLikeContains(string field, string param) => $"{field} NOT LIKE CONCAT('%', {param}, '%')";

    public string NotLikeStartsWith(string field, string param) => $"{field} NOT LIKE CONCAT({param}, '%')";

    public string NotLikeEndsWith(string field, string param) => $"{field} NOT LIKE CONCAT('%', {param})";

    public string In(string field, IEnumerable<object> paramNames) => $"{QuoteField(field)} IN ({string.Join(", ", paramNames.Select(Literal))})";
    public string NotIn(string field, IEnumerable<object> paramNames) => $"{QuoteField(field)} NOT IN ({string.Join(", ", paramNames.Select(Literal))})";
    public string IsNull(string field) => $"{field} IS NULL";
    public string IsNotNull(string field) => $"{field} IS NOT NULL";
    public string FinalizeExpression(string whereSql) => whereSql;
    
    public string Literal(object? value)
    {
        return value switch
        {
            null => "NULL",
            bool b => b ? "1" : "0",
            int or long or float or double or decimal => value.ToString()!,
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
            _ => $"'{value}'"
        };
    }

    public string Binary(LogicType type, string left, string right)
    {
        var op = type switch
        {
            LogicType.Equal => "=",
            LogicType.NotEqual => "<>",
            LogicType.GreaterThan => ">",
            LogicType.GreaterEqual => ">=",
            LogicType.LessThan => "<",
            LogicType.LessEqual => "<=",
            LogicType.AndAlso => "AND",
            LogicType.OrElse => "OR",
            _ => throw new NotSupportedException($"Unsupported BinaryExpType: {type}")
        };

        return $"({left} {op} {right})";
    }

    public string Arithmetic(ArithmeticType type, string left, string right)
    {
        var op = type switch
        {
            ArithmeticType.Add => "+",
            ArithmeticType.Subtract => "-",
            ArithmeticType.Multiply => "*",
            _ => throw new NotSupportedException($"Unsupported ArithmeticType: {type}")
        };

        return $"({left} {op} {right})";
    }
}