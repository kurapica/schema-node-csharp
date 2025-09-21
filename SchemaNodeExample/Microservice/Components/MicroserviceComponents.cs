namespace SchemaNode.Example
{
    /// <summary>
    /// Contains the re-usable components for a microservice.
    /// </summary>
    public class MicroserviceComponents
    {
        #region Constructors

        /// <summary>
        /// The microservice components
        /// </summary>
        public MicroserviceComponents(object owner, IServiceProvider serviceProvider)
        {
            criticalRegionProviderThunk = new Lazy<ICriticalRegionProvider>(serviceProvider.GetRequiredService<ICriticalRegionProvider>);
            loggerThunk = new Lazy<ILogger>(() =>
            {
                ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                return loggerFactory.CreateLogger(owner.GetType());
            });
            //postgreSqlThunk = new Lazy<PostgreSql>(serviceProvider.GetRequiredService<PostgreSql>);
        }

        #endregion

        #region Critical Region

        /// <summary>
        /// Gets the <see cref="CriticalRegion" /> provider.
        /// </summary>
        public ICriticalRegionProvider CriticalRegionProvider => criticalRegionProviderThunk.Value;

        readonly Lazy<ICriticalRegionProvider> criticalRegionProviderThunk;

        #endregion

        #region Logger

        /// <summary>
        /// Gets the logger.
        /// </summary>
        public ILogger Logger => loggerThunk.Value;

        readonly Lazy<ILogger> loggerThunk;

        #endregion

        #region PostgreSQL

        /// <summary>
        /// Gets the PostgreSQL instance.
        /// </summary>
        //public TPostgreSql? GetPostgreSql<TPostgreSql>() where TPostgreSql : PostgreSql
        //{
        //    return postgreSqlThunk.Value as TPostgreSql;
        //}

        //readonly Lazy<PostgreSql> postgreSqlThunk;

        #endregion
    }
}