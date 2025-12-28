namespace SchemaNode.Runtime;

/// <summary>
/// The data source expression type
/// </summary>
public enum DataSourceExpressionType
{
    List,
    First,
    Last,
    Count,
}

public record DataSourceExpression(DataSourceExpressionType Type, string App, string Field, AnySchemeType SchemeType)
    : SchemaExpression(SchemeType);

