using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using SchemaNode.Components.Provider;
using SchemaNode.Node;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.MySql;

/// <summary>
/// The implementation of IAppSchemaDataProvider for MySQL
/// </summary>
public class AppSchemaDataProvider: IAppSchemaDataProvider
{
    #region Constructors

    public AppSchemaDataProvider(IDbConnection dbConn, IServiceProvider serviceProvider)
    {
        _dbConnection = (MySqlConnection)dbConn;
        _loggerThunk = new Lazy<ILogger>(serviceProvider.GetRequiredService<ILogger<AppSchemaDataProvider>>);
    }

    #endregion
    
    #region IAppSchemaDataProvider implementation

    /// <inheritdoc />
    public async Task<bool> EnsureDynamicTableAsync(DynamicTableSchema schema)
    {
        await EnsureOpenConnectionAsync();
        
        // Check to update the data table
        try
        {
            // Gets the existed fields
            DbCommand command = GetDbCommand();
            command.CommandText = $"DESCRIBE `{schema.Name}`";
            Logger.LogInformation(command.CommandText);
            DbDataReader reader = await command.ExecuteReaderAsync();
            Dictionary<string, string> nameTypes = new();
            try
            {
                while (await reader.ReadAsync())
                    nameTypes.Add(reader.GetString(0), reader.GetString(1));
            }
            finally
            {
                await reader.CloseAsync();
            }

            // Check the new schema
            StringBuilder? sb = null;
            foreach (DynamicTableField dyFld in schema.Fields)
            {
                string dataType = DataType(dyFld);
                if (!nameTypes.ContainsKey(dyFld.Name))
                {
                    sb ??= new StringBuilder();
                    sb.Append($"ALTER TABLE `{schema.Name}` ADD `{dyFld.Name}` {dataType};");
                }
                else if (!nameTypes[dyFld.Name].Equals(dataType, StringComparison.OrdinalIgnoreCase))
                {
                    sb ??= new StringBuilder();
                    sb.Append($"ALTER TABLE `{schema.Name}` MODIFY COLUMN `{dyFld.Name}` {dataType};");
                }
            }

            // Check the existed indexes
            command = GetDbCommand();
            command.CommandText = $"SHOW INDEXES FROM `{schema.Name}`";
            reader = await command.ExecuteReaderAsync();
            Dictionary<string, bool> names = new(); // name => unique

            // Check indexes
            List<string> uniqueIndex = new();
            try
            {
                while (await reader.ReadAsync())
                {
                    string keyName = reader.GetString("Key_name");
                    if (keyName.Equals(DYNAMIC_UNIQUE_INDEX, StringComparison.OrdinalIgnoreCase))
                    {
                        uniqueIndex.Add(reader.GetString("Column_name"));
                    }
                    else if (!keyName.Equals("PRIMARY", StringComparison.OrdinalIgnoreCase) && !names.ContainsKey(keyName))
                    {
                        names.Add(keyName, reader.GetInt32("Non_unique") == 0);
                    }
                }
            }
            finally
            {
                await reader.CloseAsync();
            }

            // Check unique indexes
            if (!schema.Single)
            {
                List<string> chkUniqueIndex = new()
                {
                    DYNAMIC_TABLE_TARG_FIELD
                };
                foreach (DynamicTableField tableField in schema.Fields.Where(p => p.Primary))
                    chkUniqueIndex.Add(tableField.Name);

                // Compares the unique indexes
                if (chkUniqueIndex.Count != uniqueIndex.Count || chkUniqueIndex.Where((p, i) => !p.Equals(uniqueIndex[i])).Any())
                {
                    // Remove the old unique index
                    if (uniqueIndex.Count > 0)
                    {
                        sb ??= new StringBuilder();
                        sb.Append($"DROP INDEX `{DYNAMIC_UNIQUE_INDEX}` ON `{schema.Name}`;");
                    }

                    // Add the unique index
                    sb ??= new StringBuilder();
                    sb.Append($"ALTER TABLE `{schema.Name}` ADD UNIQUE INDEX `{DYNAMIC_UNIQUE_INDEX}`({string.Join(',', chkUniqueIndex.Select(e => $"`{e}`"))});");
                }
            }

            // Check new indexes
            if (schema.Indexes is { Length: > 0 })
            {
                foreach (var index in schema.Indexes)
                {
                    string key = $"IDX_{schema.Name}_{string.Join('_', index.Fields)}";
                    if (names.ContainsKey(key))
                    {
                        names.Remove(key);
                    }
                    else
                    {
                        sb ??= new StringBuilder();
                        sb.Append($"ALTER TABLE `{schema.Name}` ADD INDEX `{key}`({string.Join(',', index.Fields.Select(e => $"`{e}`"))});");
                    }
                }
            }

            // Remove no use indexes
            foreach (string name in names.Keys.Where(p => !p.Equals(DYNAMIC_UNIQUE_INDEX)))
            {
                sb ??= new StringBuilder();
                sb.Append($"DROP INDEX `{name}` ON `{schema.Name}`;");
            }

            // Update the table
            if (sb != null)
            {
                DbCommand updateCommand = GetDbCommand();
                updateCommand.CommandText = sb.ToString();
                Logger.LogInformation(updateCommand.CommandText);
                await updateCommand.ExecuteNonQueryAsync();
            }

            return true;
        }
        catch (MySqlException ex)
        {
            // Continue to create the table
            if (ex.ErrorCode != MySqlErrorCode.NoSuchTable)
                throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message);
            throw;
        }

        // Create the data table
        try
        {
            StringBuilder sb = new();

            // Create the data table
            sb.Append($"CREATE TABLE IF NOT EXISTS `{schema.Name}` (");

            // The primary key
            if (!schema.Single)
                sb.Append($"`{DYNAMIC_TABLE_SEQNO_FIELD}` BIGINT UNSIGNED AUTO_INCREMENT,");
            sb.Append($"`{DYNAMIC_TABLE_TARG_FIELD}` VARCHAR({DYNAMIC_TABLE_TARG_LEN}) NOT NULL, ");

            // Generate the column lists
            foreach (DynamicTableField tableField in schema.Fields)
            {
                // Name-Type
                sb.Append($"`{tableField.Name}` {DataType(tableField)}");

                // Not Null
                if (tableField.Primary)
                    sb.Append(" NOT NULL");

                // End
                sb.Append(", ");
            }

            // Append primary key
            if (schema.Single)
                sb.Append($"PRIMARY KEY(`{DYNAMIC_TABLE_TARG_FIELD}`)");
            else
            {
                // Use auto-incr seqno as primary key
                sb.Append($"PRIMARY KEY(`{DYNAMIC_TABLE_SEQNO_FIELD}`)");

                // Use target and other primary key as unique index
                sb.Append($", UNIQUE INDEX {DYNAMIC_UNIQUE_INDEX} (`{DYNAMIC_TABLE_TARG_FIELD}`");
                foreach (DynamicTableField tableField in schema.Fields.Where(p => p.Primary))
                    sb.Append($", `{tableField.Name}`");
                sb.Append(")");
            }

            // End the building
            sb.Append(") engine=InnoDB;");
            DbCommand command = GetDbCommand();
            command.CommandText = sb.ToString();
            Logger.LogInformation(command.CommandText);
            await command.ExecuteNonQueryAsync();
            
            // Create the indexes
            if (schema.Indexes is { Length: > 0 })
            {
                sb = new StringBuilder();
                foreach (var index in schema.Indexes)
                {
                    string key = $"IDX_{schema.Name}_{string.Join('_', index.Fields)}";
                    sb.Append($"ALTER TABLE `{schema.Name}` ADD INDEX `{key}`({string.Join(',', index.Fields.Select(e => $"`{e}`"))});");
                }
                
                command = GetDbCommand();
                command.CommandText = sb.ToString();
                Logger.LogInformation(command.CommandText);
                await command.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message);
            throw;
        }

        return true;
    }
    
    /// <inheritdoc />
    public async Task<(AnySchemaNode? result, int total)> QueryDynamicTableAsync(DynamicTableSchema schema, string target = "", 
        JsonNode? filter = null, int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, 
        bool forUpdate = false)
    {
        await EnsureOpenConnectionAsync();
        target = !string.IsNullOrWhiteSpace(target) ? MySqlHelper.EscapeString(target) : "";
        
        if (string.IsNullOrWhiteSpace(target)) return (null, -1);

        // single row
        if (schema.Single)
        {
            AnySchemaNode? value = null;
            
            // Gets the data from the database
            if (schema.Fields is [{ Name: DYNAMIC_TABLE_VALUE_FIELD }])
            {
                // Single value
                DbCommand command = GetDbCommand();
                command.CommandText = $"SELECT `{DYNAMIC_TABLE_VALUE_FIELD}` FROM `{schema.Name}` WHERE `{DYNAMIC_TABLE_TARG_FIELD}` = \"{target}\"";
                Logger.LogInformation(command.CommandText);
                DbDataReader reader = await command.ExecuteReaderAsync();
                try
                {
                    if (reader.HasRows)
                    {
                        await reader.ReadAsync();
                        value = schema.Fields[0].FromReader(reader);
                    }
                }
                finally
                {
                    await reader.CloseAsync();
                }
            }
            else
            {
                // Struct value
                StringBuilder sb = new();
                sb.Append($"SELECT ");
                schema.AppendFields(sb);
                sb.Append($" FROM `{schema.Name}` WHERE `{DYNAMIC_TABLE_TARG_FIELD}` = \"{target}\"");
                sb.Append(forUpdate ? " FOR UPDATE;" : ";");

                // Get data
                DbCommand command = GetDbCommand();
                command.CommandText = sb.ToString();
                Logger.LogInformation(command.CommandText);
                DbDataReader reader = await command.ExecuteReaderAsync();
                try
                {
                    if (reader.HasRows)
                    {
                        await reader.ReadAsync();
                        value = schema.GetFieldPack(reader);
                    }
                }
                finally
                {
                    await reader.CloseAsync();
                }
            }

            return (value, value == null ? 0 : 1);
        }

        // multi rows
        else
        {
            // Build sql
            bool fullFill = false;
            StringBuilder sb = new();
            sb.Append($" From `{schema.Name}` FORCE INDEX(`{DYNAMIC_UNIQUE_INDEX}`) ");

            // Conditions
            sb.Append($" WHERE `{DYNAMIC_TABLE_TARG_FIELD}` = \"{target}\"");
            switch (filter)
            {
                // Query based on the conditions
                case JsonObject pack:
                {
                    fullFill = true;
                    foreach ((string fld, string? v, bool isString, bool isList) in schema.GetFieldValues(pack, true))
                    {
                        if (v == null)
                        {
                            fullFill = false;
                        }
                        else
                        {
                            sb.Append(
                                isList
                                    ? $" AND `{fld}` IN {v}"
                                    : isString
                                        ? $" AND `{fld}` = \"{v}\""
                                        : $" AND `{fld}` = {v}");
                        }
                    }

                    if (!fullFill)
                    {
                        foreach ((string fld, string? v, bool isString, bool isList) in schema.GetFieldValues(pack, noPrimary:true))
                        {
                            if (v != null)
                            {
                                sb.Append(
                                    isList
                                        ? $" AND `{fld}` IN {v}"
                                        : isString
                                            ? $" AND `{fld}` = \"{v}\""
                                            : $" AND `{fld}` = {v}");
                            }
                        }
                    }

                    break;
                }
                case JsonArray array:
                {
                    if (array.Count == 0) break;

                    fullFill = true;
                    bool hasQuery = false;
                    sb.Append(" AND (");

                    // Only allow full-fill query
                    foreach (var token in array)
                    {
                        if (token is not JsonObject pack)
                            continue;

                        // Pre
                        fullFill = true;
                        bool appAnd = false;

                        // Build the query
                        if (hasQuery) sb.Append(" OR ");
                        hasQuery = true;
                        sb.Append("(");
                        foreach ((string fld, string? v, bool isString, bool isList) in schema.GetFieldValues(pack, true))
                        {
                            if (isList || v == null)
                            {
                                fullFill = false;
                                break;
                            }

                            if (appAnd) sb.Append(" AND ");
                            sb.Append(isString ? $"`{fld}` = \"{v}\"" : $"`{fld}` = {v}");
                            appAnd = true;
                        }

                        sb.Append(")");

                        // Only allow full query here
                        if (!fullFill)
                            return (null, 0);
                    }

                    // Tail
                    sb.Append(")");

                    // Continue
                    break;
                }
            }

            // Query Total
            int total = 0;
            if (!fullFill && !forUpdate)
            {
                DbCommand totalCommand = GetDbCommand();
                totalCommand.CommandText = $"SELECT COUNT(*) {sb};";
                Logger.LogInformation(totalCommand.CommandText);
                DbDataReader totalReader = await totalCommand.ExecuteReaderAsync();
                try
                {
                    if (totalReader.HasRows && await totalReader.ReadAsync())
                        total = totalReader.GetInt32(0);
                }
                finally
                {
                    await totalReader.CloseAsync();
                }
            }

            // for other
            _whereClause = $"{sb}";

            // Append the rest
            sb.Append(" ORDER BY ");
            bool first = false;
            foreach(var (field, d) in schema.GetOrderBys(desc, orderBy))
            {
                if (first) sb.Append(", ");
                first = true;
                sb.Append($"{field}");
                if (d) sb.Append(" DESC ");
            }
            if (take is > 0)
                sb.Append($" LIMIT {take}");
            if (skip is > 0)
                sb.Append($" OFFSET {skip}");
            
            // Query Data
            StringBuilder select = new();
            select.Append("SELECT ");
            schema.AppendFields(select, "o.");
            select.Append(" FROM ");
            select.Append(schema.Name);
            select.Append(" o JOIN (SELECT ");
            select.Append(DYNAMIC_TABLE_SEQNO_FIELD);
            select.Append(" ");
            select.Append(sb.ToString());
            select.Append(") t ON o.");
            select.Append(DYNAMIC_TABLE_SEQNO_FIELD);
            select.Append(" = t.");
            select.Append(DYNAMIC_TABLE_SEQNO_FIELD);
            select.Append(" ORDER BY ");
            first = false;
            foreach(var (field, d) in schema.GetOrderBys(desc, orderBy))
            {
                if (first) select.Append(", ");
                first = true;
                select.Append($"o.{field}");
                if (d) select.Append(" DESC ");
            }

            select.Append(forUpdate ? " FOR UPDATE;" : ";");
            ArrayTypeNode value = new ArrayTypeNode(schema.TypeNode);
            DbCommand command = GetDbCommand();
            command.CommandText = select.ToString();
            Logger.LogInformation(command.CommandText);
            DbDataReader reader = await command.ExecuteReaderAsync();
            try
            {
                if (reader.HasRows)
                {
                    while (await reader.ReadAsync())
                    {
                        AnySchemaNode? pack = schema.GetFieldPack(reader);
                        if (pack != null) value.Add(pack);
                    }
                }
            }
            finally
            {
                await reader.CloseAsync();
            }
            
            return (value, (fullFill || forUpdate) ? value.Count : total);
        }
    }

    /// <inheritdoc />
    public async Task<(bool result, AnySchemaNode? origin)> SaveDynamicTableDataAsync(DynamicTableSchema schema, string target = "", AnySchemaNode? value = null)
    {
        await EnsureOpenConnectionAsync();
        target = !string.IsNullOrWhiteSpace(target) ? MySqlHelper.EscapeString(target) : "";

        if (string.IsNullOrWhiteSpace(target)) return (false, null);
        
        // single row
        if (schema.Single)
        {
            // Gets the origin value
            (AnySchemaNode? origin, _) = await QueryDynamicTableAsync(schema, target);

            // Delete if null
            if (value == null)
            {
                if (origin != null)
                {
                    DbCommand command = GetDbCommand();
                    command.CommandText = $"DELETE FROM `{schema.Name}` WHERE `{DYNAMIC_TABLE_TARG_FIELD}` = \"{target}\"";
                    Logger.LogInformation(command.CommandText);
                    await command.ExecuteNonQueryAsync();
                    return (true, origin);
                }
                return (false, null);
            }

            // Check the value type
            if (schema.Fields is [{ Name: DYNAMIC_TABLE_VALUE_FIELD }])
            {
                // Convert the value
                string? result = schema.Fields[0].ToString(value);
                bool isInsert = false;

                // Insert the value
                if (origin == null)
                {
                    if (result == null) return (false, null);
                    try
                    {
                        DbCommand command = GetDbCommand();
                        command.CommandText = schema.Fields[0].IsString
                            ? $"INSERT INTO `{schema.Name}` (`{DYNAMIC_TABLE_TARG_FIELD}`, `{DYNAMIC_TABLE_VALUE_FIELD}`) VALUES ( \"{target}\", \"{MySqlHelper.EscapeString(result)}\" )"
                            : $"INSERT INTO `{schema.Name}` (`{DYNAMIC_TABLE_TARG_FIELD}`, `{DYNAMIC_TABLE_VALUE_FIELD}`) VALUES ( \"{target}\", {result} )";
                        Logger.LogInformation(command.CommandText);
                        await command.ExecuteNonQueryAsync();
                        isInsert = true;
                    }
                    catch (MySqlException ex)
                    {
                        if (ex.ErrorCode != MySqlErrorCode.DuplicateKeyEntry)
                            throw;
                    }
                }

                // Update the value
                if (!isInsert)
                {
                    DbCommand command = GetDbCommand();
                    command.CommandText = schema.Fields[0].IsString
                        ? $"UPDATE `{schema.Name}` SET `{DYNAMIC_TABLE_VALUE_FIELD}` = \"{MySqlHelper.EscapeString(result!)}\" WHERE `{DYNAMIC_TABLE_TARG_FIELD}` = \"{target}\""
                        : $"UPDATE `{schema.Name}` SET `{DYNAMIC_TABLE_VALUE_FIELD}` = {result!} WHERE `{DYNAMIC_TABLE_TARG_FIELD}` = \"{target}\"";
                    Logger.LogInformation(command.CommandText);
                    await command.ExecuteNonQueryAsync();
                }
                return (true, origin);
            }
            else if (value is StructTypeNode pack)
            {
                // Build the sql
                StringBuilder sb = new();
                bool isInsert = false;

                // Insert
                if (origin == null)
                {
                    // Header
                    sb.Append($"INSERT INTO `{schema.Name}` (`{DYNAMIC_TABLE_TARG_FIELD}`, ");
                    schema.AppendFields(sb);
                    sb.Append($") VALUES ( \"{target}\"");

                    // Body
                    foreach ((string _, string? val, bool isString, _) in schema.GetFieldValues(pack))
                        sb.Append($",{(val == null ? "null" : (isString ? $"\"{MySqlHelper.EscapeString(val)}\"" : val))}");

                    // Footer
                    sb.Append(");");
                    try
                    {
                        // Execute
                        DbCommand command = GetDbCommand();
                        command.CommandText = sb.ToString();
                        Logger.LogInformation(command.CommandText);
                        await command.ExecuteNonQueryAsync();
                        isInsert = true;
                    }
                    catch (MySqlException ex)
                    {
                        if (ex.ErrorCode != MySqlErrorCode.DuplicateKeyEntry)
                            throw;
                    }
                }

                // Update
                if (!isInsert)
                {
                    // Header
                    sb.Clear();
                    sb.Append($"UPDATE `{schema.Name}` SET ");

                    // Body
                    bool preCond = false;
                    foreach ((string fld, string? val, bool isString, _) in schema.GetFieldValues(pack))
                    {
                        sb.Append($"{(preCond ? "," : "")}`{fld}`={(val == null ? "null" : (isString ? $"\"{MySqlHelper.EscapeString(val)}\"" : val))}");
                        preCond = true;
                    }

                    // Footer
                    sb.Append($" WHERE `{DYNAMIC_TABLE_TARG_FIELD}` = \"{target}\"");

                    // Execute
                    DbCommand command = GetDbCommand();
                    command.CommandText = sb.ToString();
                    Logger.LogInformation(command.CommandText);
                    await command.ExecuteNonQueryAsync();
                }
                return (true, origin);
            }
            else
            {
                return (false, null);
            }
        }
        
        // multi rows
        else
        {
            JsonArray array;
            StringBuilder sb = new();

            // Prepare the data
            switch (value)
            {
                case ArrayTypeNode arr:
                    array = arr.ToJson()!;
                    break;
                case StructTypeNode obj:
                    array = [obj.ToJson()];
                    break;
                default:
                    return (false, null);
            }
            if (array.Count == 0) return (false, null);
            
            (AnySchemaNode? origin, _) = await QueryDynamicTableAsync(schema, target, array, forUpdate: true);

            // record exist rows
            HashSet<string> existKeys = [];
            List<string> keys = [];
            if (origin is ArrayTypeNode oArr && oArr.Count > 0)
            {
                foreach (AnySchemaNode item in oArr)
                {
                    if (item is StructTypeNode obj)
                    {
                        keys.Clear();
                        bool fullFill = true;
                        foreach ((_, string? v, _, _) in schema.GetFieldValues(obj, true))
                        {
                            // Check value
                            if (v == null)
                            {
                                fullFill = false;
                                break;
                            }
                            keys.Add(v);
                        }

                        if (!fullFill) return (false, null); // impossible
                        existKeys.Add(string.Join('|', keys));
                    }
                }
            }

            // Foreach
            foreach (JsonNode? val in array)
            {
                if (val is not JsonObject pack) continue;

                // Build where condition
                bool fullFill = true;
                keys.Clear();
                sb.Clear();
                sb.Append($" WHERE `{DYNAMIC_TABLE_TARG_FIELD}` = \"{target}\"");
                foreach ((string fld, string? v, bool isString, _) in schema.GetFieldValues(pack, true))
                {
                    // Check value
                    if (v == null)
                    {
                        fullFill = false;
                        break;
                    }
                    keys.Add(v);
                    sb.Append(isString
                        ? $" AND `{fld}` = \"{MySqlHelper.EscapeString(v)}\""
                        : $" AND `{fld}` = {v}"
                    );
                }
                if (!fullFill) continue;

                // Query the origin
                string where = sb.ToString();
                
                // Insert
                bool isInsert = false;
                if (!existKeys.Contains(string.Join('|', keys)))
                {
                    // Header
                    sb.Clear();
                    sb.Append($"INSERT INTO `{schema.Name}` (`{DYNAMIC_TABLE_TARG_FIELD}`, ");
                    schema.AppendFields(sb);
                    sb.Append($") VALUES ( \"{target}\"");

                    // Body
                    foreach ((string _, string? v, bool isString, _) in schema.GetFieldValues(pack))
                        sb.Append($",{(v == null ? "null" : (isString ? $"\"{MySqlHelper.EscapeString(v)}\"" : v))}");

                    // Footer
                    sb.Append(");");
                    try
                    {
                        // Execute
                        DbCommand command = GetDbCommand();
                        command.CommandText = sb.ToString();
                        Logger.LogInformation(command.CommandText);
                        await command.ExecuteNonQueryAsync();
                        isInsert = true;
                    }
                    catch (MySqlException ex)
                    {
                        if (ex.ErrorCode != MySqlErrorCode.DuplicateKeyEntry)
                            throw;
                    }
                }
                if (!isInsert)
                {
                    // Header
                    sb.Clear();
                    sb.Append($"UPDATE `{schema.Name}` SET ");

                    // Body
                    bool preCond = false;
                    foreach ((string fld, string? v, bool isString, _) in schema.GetFieldValues(pack, false, true))
                    {
                        sb.Append($"{(preCond ? "," : "")}`{fld}`={(v == null ? "null" : (isString ? $"\"{MySqlHelper.EscapeString(v)}\"" : v))}");
                        preCond = true;
                    }

                    // Footer
                    sb.Append(" ");
                    sb.Append(where);

                    // Execute
                    DbCommand command = GetDbCommand();
                    command.CommandText = sb.ToString();
                    Logger.LogInformation(command.CommandText);
                    await command.ExecuteNonQueryAsync();
                }
            }
            return (true, origin);
        }
    }

    /// <inheritdoc />
    public async Task<(bool result, AnySchemaNode? origin)> DeleteDynamicTableDataAsync(DynamicTableSchema schema, string target = "", JsonNode? filter = null)
    {
        await EnsureOpenConnectionAsync();
        target = !string.IsNullOrWhiteSpace(target) ? MySqlHelper.EscapeString(target) : "";

        if (string.IsNullOrWhiteSpace(target)) return (false, null);
        
        // single row
        if (schema.Single)
        {
            (AnySchemaNode? origin, _) = await QueryDynamicTableAsync(schema, target, forUpdate: true);
            if (origin is null) return (false, null);
            
            DbCommand command = GetDbCommand();
            command.CommandText = $"DELETE FROM `{schema.Name}` WHERE `{DYNAMIC_TABLE_TARG_FIELD}` = \"{target}\"";
            Logger.LogInformation(command.CommandText);
            await command.ExecuteNonQueryAsync();
            
            return (true, origin);
        }
        
        // multi rows
        else if (!schema.IncrUpdate || filter is JsonArray { Count: > 0 })
        {
            _whereClause = null;
            (AnySchemaNode? origin, _) = await QueryDynamicTableAsync(schema, target, filter, forUpdate: true);
            if (origin is not ArrayTypeNode arr || arr.Count == 0 || _whereClause == null) return (false, null);
            
            DbCommand command = GetDbCommand();
            command.CommandText = $"DELETE {_whereClause.Replace($"FORCE INDEX(`{DYNAMIC_UNIQUE_INDEX}`)", "")};"; // Can change to deleted flag controls
            Logger.LogInformation(command.CommandText);
            await command.ExecuteNonQueryAsync();
            
            return (true, origin);
        }

        return (false, null);
    }

    /// <inheritdoc />
    public async Task BeginTransactionAsync()
    {
        if (_transaction != null)
            throw new InvalidOperationException("There is already a transaction in progress.");

        await EnsureOpenConnectionAsync();
        _transaction = await _dbConnection.BeginTransactionAsync();
    }

    /// <inheritdoc />
    public async Task CommitTransactionAsync()
    {
        if (_transaction == null)
            throw new InvalidOperationException("There is no transaction in progress.");
        await _transaction.CommitAsync();
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    /// <inheritdoc />
    public async Task RollbackTransactionAsync()
    {
        if (_transaction == null)
            throw new InvalidOperationException("There is no transaction in progress.");
        await _transaction.RollbackAsync();
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    #endregion
    
    #region Utility
    
    // Get DbCommand
    DbCommand GetDbCommand()
    {
        DbCommand command = _dbConnection.CreateCommand();
        command.Transaction = _transaction;
        return command;
    }
    
    // Ensure the database connection is open
    private Task EnsureOpenConnectionAsync() =>
        _dbConnection.State != ConnectionState.Open ? _dbConnection.OpenAsync() : Task.CompletedTask;
    
    /// <summary>
    /// The mysql data type
    /// </summary>
    static string DataType (DynamicTableField field) => field.Type switch
    {
        DynamicTableFieldType.Bool => "TINYINT",
        DynamicTableFieldType.Smallint => "SMALLINT",
        DynamicTableFieldType.USmallint => "SMALLINT UNSIGNED",
        DynamicTableFieldType.Mediumint => "MEDIUMINT",
        DynamicTableFieldType.UMediumint => "MEDIUMINT UNSIGNED",
        DynamicTableFieldType.Int => "INT",
        DynamicTableFieldType.UInt => "INT UNSIGNED",
        DynamicTableFieldType.BigInt => "BIGINT",
        DynamicTableFieldType.UBigInt => "BIGINT UNSIGNED",
        DynamicTableFieldType.Float => "FLOAT",
        DynamicTableFieldType.Double => "DOUBLE",
        DynamicTableFieldType.Json => "JSON",
        DynamicTableFieldType.DateTime => "DATETIME",
        DynamicTableFieldType.TinyBlob => "TINYBLOB",
        DynamicTableFieldType.Blob => "BLOB",
        DynamicTableFieldType.MediumBlob => "MEDIUMBLOB",
        DynamicTableFieldType.LongBlob => "LONGBLOB",
        DynamicTableFieldType.Char => "CHAR",
        DynamicTableFieldType.VarChar => field.MaxLength.HasValue 
            ? $"VARCHAR({field.MaxLength.Value})"
            : "VARCHAR",
        DynamicTableFieldType.TinyText => "TINYTEXT",
        DynamicTableFieldType.Text => "TEXT",
        DynamicTableFieldType.MediumText => "MEDIUMTEXT",
        DynamicTableFieldType.LongText => "LONGTEXT",
        _ => throw new ArgumentOutOfRangeException()
    };


    private DbTransaction? _transaction;
    private ILogger Logger => _loggerThunk.Value;
    private readonly MySqlConnection _dbConnection;
    private readonly Lazy<ILogger> _loggerThunk;
    private string? _whereClause;

    #endregion
}
