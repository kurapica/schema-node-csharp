using SchemaNode.Context;
using static SchemaNode.Utility.Constant;

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

public class DataSourceVisitor : IExpressionVisitor
{
    public int Priority { get; set; } = EXP_DATA_SOURCE_PRIORITY;

    /// <inheritdoc />
    public SchemaExpression? VisitExpression(SchemaContext context, SchemaExpression exp)
    {
        if (exp is not DataSourceExpression dataSourceExp) return null;

        // Additional validation or processing can be added here if needed

        return null;
    }
}