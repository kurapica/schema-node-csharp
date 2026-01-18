using MySqlConnector;
using SchemaNode.Components;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;

namespace SchemaNode.MySql;

/// <summary>
/// The MySQL implementation of ISqlProvider.
/// </summary>
public class MySqlProvider : ISqlProvider
{
    public string QuoteField(string fieldName) => $"`{fieldName}`";
    public string QuoteTable(string tableName) => $"`{tableName}`";
    public string QuoteIndex(string indexName) => $"`{indexName}`";
    public string GenParameterName(int index) => $"@p{index}";
    public string Concat(string left, string right) => $"CONCAT({left}, {right})";
    public string LikeContains(string field, string param) => $"{field} LIKE CONCAT('%', {Literal(param)}, '%')";
    public string LikeStartsWith(string field, string param) => $"{field} LIKE CONCAT({Literal(param)}, '%')";
    public string LikeEndsWith(string field, string param) => $"{field} LIKE CONCAT('%', {Literal(param)})";
    public string NotLikeContains(string field, string param) => $"{field} NOT LIKE CONCAT('%', {Literal(param)}, '%')";
    public string NotLikeStartsWith(string field, string param) => $"{field} NOT LIKE CONCAT({Literal(param)}, '%')";
    public string NotLikeEndsWith(string field, string param) => $"{field} NOT LIKE CONCAT('%', {Literal(param)})";

    public string In(string field, IEnumerable<object> paramNames) => $"{field} IN ({string.Join(", ", paramNames.Select(Literal))})";
    public string NotIn(string field, IEnumerable<object> paramNames) => $"{field} NOT IN ({string.Join(", ", paramNames.Select(Literal))})";
    public string IsNull(string field) => $"{field} IS NULL";
    public string IsNotNull(string field) => $"{field} IS NOT NULL";
    public string FinalizeExpression(string whereSql) => whereSql;

    public string Literal(object? value)
    {
        if (value is AnySchemaNode node) value = node.LiteralValue;

        return value switch
        {
            null => "NULL",
            bool b => b ? "1" : "0",
            int or long or float or double or decimal => value.ToString()!,
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
            string s => $"'{MySqlHelper.EscapeString(s)}'",
            Guid g => $"'{g}'",
            _ => $"'{MySqlHelper.EscapeString(value.ToString()!)}'"
        };
    }

    public string Binary(LogicType type, string left, string right)
    {
        var op = type switch
        {
            LogicType.AndAlso => "AND",
            LogicType.OrElse => "OR",
            LogicType.Equal => "=",
            LogicType.NotEqual => "<>",
            LogicType.GreaterThan => ">",
            LogicType.GreaterEqual => ">=",
            LogicType.LessThan => "<",
            LogicType.LessEqual => "<=",
            _ => throw new NotSupportedException($"Unsupported BinaryExpType: {type}")
        };

        return $"({left} {op} {right})";
    }
}
