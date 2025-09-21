using System;

namespace SchemaNode.Example
{
    /// <summary>
    /// Represents a database error thrown by PostgreSQL.
    /// </summary>
    public class PostgreSqlException : Exception
    {
        #region Constructors

        /// <inheritdoc />
        public PostgreSqlException()
        {
        }

        /// <inheritdoc />
        public PostgreSqlException(string message) : base(message)
        {
        }

        /// <inheritdoc />
        public PostgreSqlException(string message, Exception innerException) : base(message, innerException)
        {
        }

        #endregion
    }
}