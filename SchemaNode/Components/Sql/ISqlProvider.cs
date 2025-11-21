namespace SchemaNode.Components;

/// <summary>
/// SQL provider abstraction. Each database (MySQL, SQLServer, PostgreSQL, Oracle, SQLite)
/// supplies its own implementation to handle SQL syntax differences.
/// 
/// This provider is used by the expression translator to build WHERE clauses and SQL fragments.
/// </summary>
public interface ISqlProvider
{
    /* ===========================
     *  Basics: Identifiers / Parameters / Literals
     * =========================== */

    /// <summary>
    /// Quotes a field/column name.
    /// Example:
    /// - MySQL: `UserName`
    /// - SQLServer: [UserName]
    /// - PostgreSQL: "UserName"
    /// </summary>
    string QuoteField(string fieldName);

    /// <summary>
    /// Quotes a table name.
    /// </summary>
    string QuoteTable(string tableName);

    /// <summary>
    /// Generates a provider-specific parameter name based on an index.
    /// Example:
    /// - MySQL: @p0
    /// - PostgreSQL: $1
    /// - Oracle: :p0
    /// </summary>
    string GenParameterName(int index);

    /// <summary>
    /// Converts a constant value into an SQL literal.
    /// Used only as a fallback when a parameter cannot be used.
    /// </summary>
    string Literal(object? value);

    /* ===========================
     *   Operators: Unary / Binary
     * =========================== */

    /// <summary>
    /// Formats a binary expression: (left OP right)
    /// Example: (a = b), (a > b)
    /// </summary>
    string Binary(BinaryExpType type, string left, string right);

    /// <summary>
    /// Formats a unary expression such as NOT (...)
    /// </summary>
    string Unary(UnaryExpType type, string operand);

    /* ===========================
     *      Text / Pattern Matching
     * =========================== */

    /// <summary>
    /// String concatenation.
    /// Example:
    /// - MySQL: CONCAT(a, b)
    /// - SQLServer: a + b
    /// </summary>
    string Concat(string left, string right);

    /// <summary>
    /// Builds a LIKE '%xxx%' pattern.
    /// </summary>
    string LikeContains(string field, string param);

    /// <summary>
    /// Builds a LIKE 'xxx%' pattern.
    /// </summary>
    string LikeStartsWith(string field, string param);

    /// <summary>
    /// Builds a LIKE '%xxx' pattern.
    /// </summary>
    string LikeEndsWith(string field, string param);

    /* ===========================
     *       IN / NOT IN
     * =========================== */

    /// <summary>
    /// field IN (param1, param2, ...)
    /// </summary>
    string In(string field, IReadOnlyList<string> paramNames);

    /// <summary>
    /// field NOT IN (param1, param2, ...)
    /// </summary>
    string NotIn(string field, IReadOnlyList<string> paramNames);

    /* ===========================
     *       NULL handling
     * =========================== */

    string IsNull(string field);
    string IsNotNull(string field);

    /* ===========================
     *       Final SQL hook
     * =========================== */

    /// <summary>
    /// Gives the provider a chance to modify the final SQL expression
    /// (e.g., add optimization hints, adjust syntax).
    /// </summary>
    string FinalizeExpression(string whereSql);
}
