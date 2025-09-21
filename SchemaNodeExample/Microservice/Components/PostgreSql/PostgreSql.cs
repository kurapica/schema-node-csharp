using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage;

namespace SchemaNode.Example
{
    /// <summary>
    /// Contains the base implementation of a PostgreSQL access facade.
    /// </summary>
    public abstract class PostgreSql : DbContext
    {
        #region Constructors

        /// <summary>
        /// The PostgreSql constructor
        /// </summary>=
        protected PostgreSql(PostgreSqlConfig config, IServiceProvider serviceProvider)
        {
            this.Config = config;
            this.serviceProvider = serviceProvider;
            loggerThunk = new Lazy<ILogger>(() =>
            {
                ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                return loggerFactory.CreateLogger(GetType());
            });
            //cacheServiceThunk = new Lazy<IFusionCache>(serviceProvider.GetRequiredService<IFusionCache>);
        }

        #endregion

        #region Initialize

        /// <summary>
        /// Initializes the access facade.
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            // Delete the database for rebuild.
            if (Config.Rebuild && !await DeleteSchemaAsync())
            {
                return false;
            }

            // Create the database.
            if (!await CreateSchemaAsync())
            {
                return false;
            }

            // Check the schema version.
            if (!await VerifySchemaVersionAsync())
            {
                return false;
            }

            // Finish.
            return true;
        }

        /// <summary>
        /// Called to seed the database with initial data.
        /// </summary>
        protected abstract Task SeedAsync();

        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            if (Config.IsQueryLoggingEnabled)
            {
                optionsBuilder.EnableSensitiveDataLogging();
                optionsBuilder.UseLoggerFactory(serviceProvider.GetRequiredService<ILoggerFactory>());
            }
            string connectionString = Config.GetConnectionString(microservice.Environment);
            optionsBuilder.UseNpgsql(connectionString);
        }

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            Config.Register<GlobalDataEntity>();
            foreach (Type entityType in Config.Types)
            {
                // Register entity.
                EntityTypeBuilder entityTypeBuilder = modelBuilder.Entity(entityType);
                DescriptionAttribute entityTypeDescriptionAttribute = entityType.GetCustomAttribute<DescriptionAttribute>();
                if (entityTypeDescriptionAttribute != null)
                {
                    entityTypeBuilder.ToTable(tb => tb.HasComment(entityTypeDescriptionAttribute.Description));
                }

                // Register entity properties.
                foreach (PropertyInfo entityProperty in entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    DescriptionAttribute entityPropertyDescriptionAttribute = entityProperty.GetCustomAttribute<DescriptionAttribute>();
                    if (entityPropertyDescriptionAttribute != null)
                    {
                        entityTypeBuilder.Property(entityProperty.Name).HasComment(entityPropertyDescriptionAttribute.Description);
                    }
                }

                // Register entity unique keys.
                RegisterUniqueKeys(entityTypeBuilder);

                // Register entity indexes.
                RegisterIndices(entityTypeBuilder);

                // Register entity default values.
                RegisterDefaultValues(entityTypeBuilder);
            }
        }

        static void RegisterUniqueKeys(EntityTypeBuilder entityTypeBuilder)
        {
            IMutableEntityType entityType = entityTypeBuilder.Metadata;
            foreach (IMutableProperty property in entityType.GetProperties())
            {
                IEnumerable<UniqueKeyAttribute> uniqueKeys = GetUniqueKeyAttributes(entityType, property);
                if (uniqueKeys != null)
                {
                    foreach (UniqueKeyAttribute uniqueKey in uniqueKeys.Where(x => x.Order == 0))
                    {
                        // Single column Unique Key
                        if (string.IsNullOrWhiteSpace(uniqueKey.Group))
                        {
                            if (uniqueKey.Primary)
                            {
                                entityTypeBuilder.HasKey(property.Name);
                            }
                            else
                            {
                                entityType.AddIndex(property).IsUnique = true;
                            }
                        }
                        // Multiple column Unique Key
                        else
                        {
                            List<(IMutableProperty, int)> mutableProperties = new();
                            foreach (IMutableProperty x in entityType.GetProperties())
                            {
                                IEnumerable<UniqueKeyAttribute> uniqueKeyAttributes = GetUniqueKeyAttributes(entityType, x);
                                if (uniqueKeyAttributes != null)
                                {
                                    foreach (UniqueKeyAttribute uniqueKeyAttribute in uniqueKeyAttributes)
                                    {
                                        if (uniqueKeyAttribute.Group == uniqueKey.Group)
                                        {
                                            mutableProperties.Add((x, uniqueKeyAttribute.Order));
                                        }
                                    }
                                }
                            }
                            mutableProperties.Sort((a, b) =>
                            {
                                (IMutableProperty _, int ao) = a;
                                (IMutableProperty _, int bo) = b;
                                return ao.CompareTo(bo);
                            });
                            if (uniqueKey.Primary)
                            {
                                entityTypeBuilder.HasKey(mutableProperties.Select(p =>
                                {
                                    (IMutableProperty a, int _) = p;
                                    return a.Name;
                                }).ToArray());
                            }
                            else
                            {
                                entityType.AddKey(mutableProperties.Select(p =>
                                {
                                    (IMutableProperty a, int _) = p;
                                    return a;
                                }).ToList());
                            }
                        }
                    }
                }
            }
        }

        static void RegisterIndices(EntityTypeBuilder entityTypeBuilder)
        {
            IMutableEntityType entityType = entityTypeBuilder.Metadata;
            foreach (IMutableProperty property in entityType.GetProperties())
            {
                IEnumerable<IndexAttribute> indexKeys = GetIndexAttributes(entityType, property);
                if (indexKeys != null)
                {
                    foreach (IndexAttribute uniqueKey in indexKeys.Where(x => x.or == 0))
                    {
                        // Single column Unique Key
                        if (string.IsNullOrWhiteSpace(uniqueKey.Name))
                        {
                            entityType.AddIndex(property).IsUnique = uniqueKey.IsUnique;
                        }
                        // Multiple column Unique Key
                        else
                        {
                            List<(IMutableProperty, int)> mutableProperties = new();
                            foreach (IMutableProperty x in entityType.GetProperties())
                            {
                                IEnumerable<IndexAttribute> uniqueKeyAttributes = GetIndexAttributes(entityType, x);
                                if (uniqueKeyAttributes != null)
                                {
                                    foreach (IndexAttribute uniqueKeyAttribute in uniqueKeyAttributes)
                                    {
                                        if (uniqueKeyAttribute.Name == uniqueKey.Name)
                                        {
                                            mutableProperties.Add((x, uniqueKeyAttribute.Order));
                                        }
                                    }
                                }
                            }
                            mutableProperties.Sort((a, b) =>
                            {
                                (IMutableProperty _, int ao) = a;
                                (IMutableProperty _, int bo) = b;
                                return ao.CompareTo(bo);
                            });
                            entityType.AddIndex(mutableProperties.Select(p =>
                            {
                                (IMutableProperty a, int _) = p;
                                return a;
                            }).ToList()).IsUnique = uniqueKey.IsUnique;
                        }
                    }
                }
            }
        }

        static IEnumerable<UniqueKeyAttribute> GetUniqueKeyAttributes(IMutableEntityType entityType, IMutableProperty property)
        {
            if (entityType == null)
            {
                throw new ArgumentNullException(nameof(entityType));
            }
            if (entityType.ClrType == null)
            {
                throw new ArgumentNullException(nameof(entityType.ClrType));
            }
            if (property == null)
            {
                throw new ArgumentNullException(nameof(property));
            }
            if (property.Name == null)
            {
                throw new ArgumentNullException(nameof(property.Name));
            }
            PropertyInfo propInfo = entityType.ClrType.GetProperty(property.Name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            return propInfo?.GetCustomAttributes<UniqueKeyAttribute>();
        }

        static IEnumerable<IndexAttribute> GetIndexAttributes(IMutableEntityType entityType, IMutableProperty property)
        {
            if (entityType == null)
            {
                throw new ArgumentNullException(nameof(entityType));
            }
            if (entityType.ClrType == null)
            {
                throw new ArgumentNullException(nameof(entityType.ClrType));
            }
            if (property == null)
            {
                throw new ArgumentNullException(nameof(property));
            }
            if (property.Name == null)
            {
                throw new ArgumentNullException(nameof(property.Name));
            }
            PropertyInfo propInfo = entityType.ClrType.GetProperty(property.Name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            return propInfo?.GetCustomAttributes<IndexAttribute>();
        }

        static void RegisterDefaultValues(EntityTypeBuilder entityTypeBuilder)
        {
            foreach (IMutableProperty property in entityTypeBuilder.Metadata.GetProperties())
            {
                DefaultValueAttribute defaultValueAttribute = property.PropertyInfo?.GetCustomAttribute<DefaultValueAttribute>();
                if (defaultValueAttribute != null)
                {
                    if (defaultValueAttribute.Value is string value && value.StartsWith("sql:", StringComparison.OrdinalIgnoreCase))
                    {
                        property.SetDefaultValueSql(value.Substring(0, 4));
                    }
                    else
                    {
                        property.SetDefaultValue(defaultValueAttribute.Value);
                    }
                }
            }
        }

        #endregion

        #region Global Data

        /// <summary>
        /// Gets the typed value of a global data.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="name">The name of the global data.</param>
        public async Task<T> GetGlobalDataValueAsync<T>(string name)
        {
            string valueString = await GetGlobalDataValueStringAsync(name);
            Type type = typeof(T);
            if (type == typeof(string))
            {
                return (T)(object)valueString;
            }
            if (type == typeof(int))
            {
                if (!string.IsNullOrEmpty(valueString) && int.TryParse(valueString, out int resultInt))
                {
                    return (T)(object)resultInt;
                }
            }
            else if (type == typeof(decimal))
            {
                if (!string.IsNullOrEmpty(valueString) && decimal.TryParse(valueString, out decimal resultDecimal))
                {
                    return (T)(object)resultDecimal;
                }
            }
            else if (type == typeof(TimeSpan))
            {
                if (!string.IsNullOrEmpty(valueString) && int.TryParse(valueString, out int resultInt) && resultInt >= 0)
                {
                    return (T)(object)TimeSpan.FromSeconds(resultInt);
                }
            }
            else if (type == typeof(Guid))
            {
                if (!string.IsNullOrEmpty(valueString) && Guid.TryParse(valueString, out Guid resultGuid))
                {
                    return (T)(object)resultGuid;
                }
            }
            else if (type == typeof(bool))
            {
                if (!string.IsNullOrEmpty(valueString))
                {
                    return (T)(object)(string.Equals("true", valueString, StringComparison.OrdinalIgnoreCase) || string.Equals("yes", valueString, StringComparison.OrdinalIgnoreCase) || string.Equals("y", valueString, StringComparison.OrdinalIgnoreCase));
                }
            }
            else if (type.IsClass)
            {
                return valueString.FromJson<T>();
            }
            else
            {
                throw new PostgreSqlException($"The GlobalData type \"{type.FullName}\" is not supported.");
            }
            throw new PostgreSqlException($"The GlobalData \"{name}\" is in a wrong format.");
        }

        /// <summary>
        /// Sets the value of a global data.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="name">The name of the global data.</param>
        /// <param name="value">The value.</param>
        public async Task SetGlobalDataValueAsync<T>(string name, T value)
        {
            string valueString;
            Type type = typeof(T);
            if (type == typeof(string))
            {
                valueString = (string)(object)value;
            }
            else if (type == typeof(int))
            {
                valueString = ((int)(object)value).ToString();
            }
            else if (type == typeof(TimeSpan))
            {
                valueString = ((int)((TimeSpan)(object)value).TotalSeconds).ToString();
            }
            else if (type == typeof(Guid))
            {
                valueString = ((Guid)(object)value).ToString();
            }
            else if (type == typeof(bool))
            {
                valueString = (bool)(object)value ? "true" : "false";
            }
            else
            {
                throw new PostgreSqlException($"The GlobalData type \"{type.FullName}\" is not supported.");
            }
            await SetGlobalDataValueStringAsync(name, valueString);
        }

        /// <summary>
        /// Sets the value of a global data.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="name">The name of the global data.</param>
        /// <param name="value">The value.</param>
        /// <param name="description">The description.</param>
        public async Task<GlobalDataEntity> AddGlobalDataValueAsync<T>(string name, T value, string description)
        {
            string valueString;
            Type type = typeof(T);
            if (type == typeof(string))
            {
                valueString = (string)(object)value;
            }
            else if (type == typeof(int))
            {
                valueString = ((int)(object)value).ToString();
            }
            else if (type == typeof(decimal))
            {
                valueString = ((decimal)(object)value).ToString(CultureInfo.InvariantCulture);
            }
            else if (type == typeof(TimeSpan))
            {
                valueString = ((int)((TimeSpan)(object)value).TotalSeconds).ToString();
            }
            else if (type == typeof(Guid))
            {
                valueString = ((Guid)(object)value).ToString();
            }
            else if (type == typeof(bool))
            {
                valueString = (bool)(object)value ? "true" : "false";
            }
            else if (type.IsClass)
            {
                valueString = value?.ToJson() ?? "null";
            }
            else
            {
                throw new PostgreSqlException($"The GlobalData value type \"{type.FullName}\" is not supported.");
            }
            return await AddGlobalDataValueStringAsync(name, valueString, description);
        }

        async Task<GlobalDataEntity> AddGlobalDataValueStringAsync(string name, string value, string description)
        {
            GlobalDataEntity result = new()
            {
                Gd_Name = name,
                Gd_Value = value,
                Gd_Description = description
            };
            await AddAsync(result);
            return result;
        }

        async Task<string> GetGlobalDataValueStringAsync(string name)
        {
            GlobalDataEntity entity = await Set<GlobalDataEntity>().Where(gde => gde.Gd_Name == name).FirstOrDefaultAsync();
            if (entity == null)
            {
                throw new PostgreSqlException($"\"{name}\" does not exist in GlobalData.");
            }
            return entity.Gd_Value;
        }

        async Task SetGlobalDataValueStringAsync(string name, string value)
        {
            GlobalDataEntity entity = await Set<GlobalDataEntity>().Where(gde => gde.Gd_Name == name).FirstOrDefaultAsync();
            if (entity == null)
            {
                throw new PostgreSqlException($"\"{name}\" does not exist in GlobalData.");
            }
            entity.Gd_Value = value;
        }

        #endregion

        #region Schema

        RelationalDatabaseCreator DatabaseCreator => (RelationalDatabaseCreator)Database.GetService<IDatabaseCreator>();

        async Task<bool> DeleteSchemaAsync()
        {
            bool result = true;
            if (await DatabaseCreator.ExistsAsync())
            {
                try
                {
                    await DatabaseCreator.DeleteAsync();
                    Logger.LogDebug("数据库已删除。");
                }
                catch
                {
                    Logger.LogDebug("无法删除数据库。");
                    result = false;
                }
            }
            return result;
        }

        async Task<bool> CreateSchemaAsync()
        {
            if (!await DatabaseCreator.ExistsAsync())
            {
                try
                {
                    await DatabaseCreator.CreateAsync();
                    await Database.ExecuteSqlRawAsync("create extension \"uuid-ossp\";");
                    await DatabaseCreator.CreateTablesAsync();
                    Logger.LogDebug("The database is created.");
                }
                catch
                {
                    Logger.LogError("Failed to create database.");
                    return false;
                }
                try
                {
                    await AddSchemaVersionAsync();
                    await SeedAsync();
                    await SaveChangesAsync();
                    Logger.LogInformation("The database is seeded.");
                }
                catch
                {
                    Logger.LogError("Failed to seed the database.");
                    return false;
                }
            }
            else
            {
                // Add non-exist tables
                string script = DatabaseCreator.GenerateCreateScript()
                    .Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ")
                    .Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS ")
                    .Replace("CREATE UNIQUE INDEX ", "CREATE UNIQUE INDEX IF NOT EXISTS ");

                await Database.ExecuteSqlRawAsync(script);
            }
            return true;
        }

        async Task AddSchemaVersionAsync()
        {
            await AddGlobalDataValueAsync(GLOBAL_DATA_NAME_SCHEMA_VERSION, Config.SchemaVersion, "数据库版本");
        }

        async Task<bool> VerifySchemaVersionAsync()
        {
            int schemaVersion = await GetGlobalDataValueAsync<int>(GLOBAL_DATA_NAME_SCHEMA_VERSION);
            if (schemaVersion != Config.SchemaVersion)
            {
                Logger.LogError($"The current database version <{schemaVersion}> does not match the required version <{Config.SchemaVersion}>.");
                return false;
            }
            Logger.LogInformation($"The current database version is <{schemaVersion}>.");
            return true;
        }

        const string GLOBAL_DATA_NAME_SCHEMA_VERSION = "SCHEMA_VERSION";

        #endregion

        #region Logger

        /// <summary>
        /// The PostgreSql config
        /// </summary>
        public PostgreSqlConfig Config { get; init; }

        //public Dictionary<object, (EntityState, Dictionary<string, object>)> ModifiedEntities { get; } = new();

        /// <summary>
        /// Gets the logger.
        /// </summary>
        public ILogger Logger => loggerThunk.Value;

        /// <summary>
        /// The cache service
        /// </summary>
        //public IFusionCache Cache => cacheServiceThunk.Value;

        readonly Lazy<ILogger> loggerThunk;
        //readonly Lazy<IFusionCache> cacheServiceThunk;

        #endregion

        #region Constants

        /// <summary>
        /// The SQL value of default PK.
        /// </summary>
        public const string DEFAULT_PK_VALUE = "sql:uuid_generate_v4()";

        /// <summary>
        /// The SQL value of default now.
        /// </summary>
        public const string DEFAULT_NOW_VALUE = "sql:now()";

        #endregion

        #region Helpers

        /// <summary>
        /// Gets the queryable dataset
        /// </summary>
        protected IQueryable<T> QueryableSet<T>() where T : class
        {
            return Set<T>().AsQueryable();
        }

        #endregion

        #region Entity Track

        /// <summary>
        /// Save changed entities and release the entity caches
        /// </summary>
        public override async Task<int> SaveChangesAsync(CancellationToken token = default)
        {
            /*foreach (EntityEntry entry in ChangeTracker.Entries().Where(p => p.State != EntityState.Unchanged))
            {
                if (!ModifiedEntities.ContainsKey(entry.Entity))
                {
                    Dictionary<string, object> origin = null;

                    // Need the original value if modified
                    if (entry.State == EntityState.Modified)
                    {
                        // Need get the original value
                        origin = entry.OriginalValues.Properties.ToDictionary(property => property.Name, property => entry.Property(property.Name).OriginalValue);
                    }
                    ModifiedEntities.Add(entry.Entity, (entry.State, origin));
                }
            }*/
            int result = await base.SaveChangesAsync(token);
            base.ChangeTracker.Clear();
            return result;
        }

        /// <summary>
        /// Refreshes all cached entities.
        /// </summary>
        public void Refresh()
        {
            List<EntityEntry> entitiesList = ChangeTracker.Entries().ToList();
            foreach (EntityEntry entity in entitiesList)
            {
                entity.Reload();
            }
        }

        #endregion

        #region Implementations

        /// <inheritdoc />
        public override void Dispose()
        {
            base.Dispose();
            DisposableObjectMonitor.Remove((IDisposable)this);
        }

        /// <inheritdoc />
        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            DisposableObjectMonitor.Remove((IDisposable)this);
        }

        readonly IServiceProvider serviceProvider;

        #endregion
    }
}