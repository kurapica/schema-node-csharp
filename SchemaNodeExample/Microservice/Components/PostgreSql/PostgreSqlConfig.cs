using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;

namespace SchemaNode.Example
{
    /// <summary>
    /// Contains the configurations for PostgreSQL used in microservice.
    /// </summary>
    public class PostgreSqlConfig
    {
        #region Metadata

        /// <summary>
        /// The factory to create a new instance of <see cref="PostgreSql" />. NULL to disable PostgreSQL.
        /// </summary>
        public Func<PostgreSql> Factory { get; set; }

        /// <summary>
        /// The scope type to create a new instance of <see cref="PostgreSql" /> with scope service provider. NULL to disable MySQL
        /// </summary>
        public Type ScopeType { get; set; }

        /// <summary>
        /// The current schema version.
        /// </summary>
        public int SchemaVersion { get; set; }

        #endregion

        #region Connection

        /// <summary>
        /// The host of the PostgreSQL server.
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// The port of the PostgreSQL server.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// The username for login.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// The password for login.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// The database name.
        /// </summary>
        public string Database { get; set; }

        /// <summary>
        /// Gets the connection string for a specific environment.
        /// </summary>
        public string GetConnectionString(MicroserviceEnvironment environment)
        {
            return $"Host={Host};Port={Port};Username={Username};Password={Password};MinPoolSize=128;MaxPoolSize=512;Timeout=5;Database={(environment == MicroserviceEnvironment.Production ? Environment.MachineName + "_" : string.Empty)}{Database}";
        }

        #endregion

        #region Cache

        /// <summary>
        /// The prefix applied to the cache key
        /// </summary>
        public string Prefix { get; set; } = "ONION";

        /// <summary>
        /// The expire time in second
        /// </summary>
        public int ExpireSecond { get; set; } = 3600;

        /// <summary>
        /// The fake entity expire time in second
        /// </summary>
        public int FakeEntityExpireSecond { get; set; } = 60;

        #endregion

        #region Options

        /// <summary>
        /// Whether to log queries.
        /// </summary>
        public bool IsQueryLoggingEnabled { get; set; }

        /// <summary>
        /// Whether to rebuild the database upon startup.
        /// </summary>
        public bool Rebuild { get; set; }

        #endregion

        #region Registration

        /// <summary>
        /// Gets or sets the assembly that contains the entity types, which will all be registered.
        /// </summary>
        public void RegisterAllInAssembly(Assembly assembly)
        {
            Type[] assemblyTypes = assembly.GetTypes();
            foreach (Type type in assemblyTypes.Where(t => t.IsClass && t.GetCustomAttribute<TableAttribute>() != null && !t.IsAbstract))
            {
                types.Add(type);
            }
        }

        /// <summary>
        /// Registers a new entity type.
        /// </summary>
        public void Register<TEntity>() where TEntity : class
        {
            Register(typeof(TEntity));
        }

        /// <summary>
        /// Registers a new entity type.
        /// </summary>
        public void Register(Type entityType)
        {
            if (!entityType.IsClass || entityType.IsAbstract || entityType.GetCustomAttribute<TableAttribute>() == null)
            {
                throw new InvalidOperationException($"{entityType} is not a valid entity type.");
            }
            types.Add(entityType);
        }

        /// <summary>
        /// Gets all registered entity types.
        /// </summary>
        public Type[] Types => types.ToArray();

        readonly HashSet<Type> types = new();

        #endregion
    }
}