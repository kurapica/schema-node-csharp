using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using SchemaNode.Components;
using SchemaNode.Node;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.MySql;

/// <summary>
/// The implementation of IAppSchemaDataProvider for MySQL
/// </summary>
public class AppDataMySqlProvider(MySqlConnection dbConn, IServiceProvider serviceProvider, ISqlProvider sqlProvider) : IAppDataSqlProvider<MySqlProvider>
{
    #region Properties and Fields

    readonly string _refTarget = sqlProvider.QuoteField(DYNAMIC_TABLE_TARG_FIELD);
    readonly string _refIndex = sqlProvider.QuoteIndex(DYNAMIC_UNIQUE_INDEX);
    readonly string _refSeqNo = sqlProvider.QuoteField(DYNAMIC_TABLE_SEQNO_FIELD);

    #endregion

    #region IAppSchemaDataProvider implementation

    /// <inheritdoc />
    public async Task<bool> EnsureDynamicTableAsync(DynamicTableSchema schema)
    {
        string tableName = sqlProvider.QuoteTable(schema.Name);
        await EnsureOpenConnectionAsync();
        
        // Check to update the data table
        try
        {
            // Gets the existed fields
            DbCommand command = GetDbCommand();
            command.CommandText = $"DESCRIBE {tableName}";
            Logger.LogDebug(command.CommandText);
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
                    sb.Append($"ALTER TABLE {tableName} ADD {sqlProvider.QuoteField(dyFld.Name)} {dataType};");
                }
                else if (!nameTypes[dyFld.Name].Equals(dataType, StringComparison.OrdinalIgnoreCase))
                {
                    sb ??= new StringBuilder();
                    sb.Append($"ALTER TABLE {tableName} MODIFY COLUMN {sqlProvider.QuoteField(dyFld.Name)} {dataType};");
                }
            }

            // Check the existed indexes
            command = GetDbCommand();
            command.CommandText = $"SHOW INDEXES FROM {tableName}";
            reader = await command.ExecuteReaderAsync();
            Dictionary<string, bool> names = []; // name => unique

            // Check indexes
            List<string> uniqueIndex = [];
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
                        sb.Append($"DROP INDEX {_refIndex} ON {tableName};");
                    }

                    // Add the unique index
                    sb ??= new StringBuilder();
                    sb.Append($"ALTER TABLE {tableName} ADD UNIQUE INDEX {_refIndex}({string.Join(',', chkUniqueIndex.Select(sqlProvider.QuoteField))});");
                }
            }

            // Check new indexes
            if (schema.Indexes is { Length: > 0 })
            {
                foreach (var index in schema.Indexes)
                {
                    string key = $"IDX_{string.Join('_', index.Fields.Select(f => f.ToLower()))}";
                    if (!names.Remove(key))
                    {
                        sb ??= new StringBuilder();
                        sb.Append($"ALTER TABLE {tableName} ADD INDEX {sqlProvider.QuoteIndex(key)}({string.Join(',', index.Fields.Select(sqlProvider.QuoteField))});");
                    }
                }
            }

            // Remove no use indexes
            foreach (string name in names.Keys.Where(p => !p.Equals(DYNAMIC_UNIQUE_INDEX)))
            {
                sb ??= new StringBuilder();
                sb.Append($"DROP INDEX {sqlProvider.QuoteIndex(name)} ON {tableName};");
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
            sb.Append($"CREATE TABLE IF NOT EXISTS {tableName} (");

            // The primary key
            if (!schema.Single)
                sb.Append($"{_refSeqNo} BIGINT UNSIGNED AUTO_INCREMENT,");
            sb.Append($"{_refTarget} VARCHAR({DYNAMIC_TABLE_TARG_LEN}) NOT NULL, ");

            // Generate the column lists
            foreach (DynamicTableField tableField in schema.Fields)
            {
                // Name-Type
                sb.Append($"{sqlProvider.QuoteField(tableField.Name)} {DataType(tableField)}");

                // Not Null
                if (tableField.Primary) sb.Append(" NOT NULL");

                // End
                sb.Append(", ");
            }

            // Append primary key
            if (schema.Single)
                sb.Append($"PRIMARY KEY({_refTarget})");
            else
            {
                // Use auto-incr seqno as primary key
                sb.Append($"PRIMARY KEY({_refSeqNo})");

                // Use target and other primary key as unique index
                sb.Append($", UNIQUE INDEX {_refIndex} ({_refTarget}");
                foreach (DynamicTableField tableField in schema.Fields.Where(p => p.Primary))
                    sb.Append($", {sqlProvider.QuoteField(tableField.Name)}");
                sb.Append(")");
            }

            // End the building
            sb.Append(") engine=InnoDB;");
            DbCommand command = GetDbCommand();
            command.CommandText = sb.ToString();
            Logger.LogDebug(command.CommandText);
            await command.ExecuteNonQueryAsync();
            
            // Create the indexes
            if (schema.Indexes is { Length: > 0 })
            {
                sb = new StringBuilder();
                foreach (var index in schema.Indexes)
                {
                    string key = $"IDX_{string.Join('_', index.Fields.Select(f => f.ToLower()))}";
                    sb.Append($"ALTER TABLE {tableName} ADD INDEX {sqlProvider.QuoteIndex(key)}({string.Join(',', index.Fields.Select(sqlProvider.QuoteField))});");
                }
                
                command = GetDbCommand();
                command.CommandText = sb.ToString();
                Logger.LogDebug(command.CommandText);
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
    public async Task<(AnySchemaNode? result, int total)> QueryDynamicTableAsync(DynamicTableSchema schema, string target, 
        JsonNode? filter = null, int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, 
        bool forUpdate = false)
    {
        string tableName = sqlProvider.QuoteTable(schema.Name);
        await EnsureOpenConnectionAsync();        
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
                command.CommandText = $"SELECT {sqlProvider.QuoteField(DYNAMIC_TABLE_VALUE_FIELD)} FROM {tableName} WHERE {_refTarget} = {sqlProvider.Literal(target)}";
                Logger.LogDebug(command.CommandText);
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
                AppendFields(sb, schema);
                sb.Append($" FROM {tableName} WHERE {_refTarget} = {sqlProvider.Literal(target)}");
                sb.Append(forUpdate ? " FOR UPDATE;" : ";");

                // Get data
                DbCommand command = GetDbCommand();
                command.CommandText = sb.ToString();
                Logger.LogDebug(command.CommandText);
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
            sb.Append($" From {tableName} FORCE INDEX({_refIndex}) ");

            // Conditions
            sb.Append($" WHERE {_refTarget} = {sqlProvider.Literal(target)}");
            switch (filter)
            {
                // Query based on the conditions
                case JsonObject pack:
                {
                    fullFill = true;
                    foreach ((string fld, AnySchemaNode? v) in schema.GetFieldValues(pack, true))
                    {
                        if (v == null || v.IsEmpty)
                        {
                            fullFill = false;
                        }
                        else if (v is ArrayTypeNode arr)
                        {
                            sb.Append($" AND {sqlProvider.In(fld, arr.Where(a => !a.IsEmpty).Select(a => a.Value!))}");
                        }
                        else
                        {
                            sb.Append($" AND {sqlProvider.QuoteField(fld)} = {sqlProvider.Literal(v.Value)}");
                        }
                    }

                    if (!fullFill)
                    {
                        foreach ((string fld, AnySchemaNode? v) in schema.GetFieldValues(pack, noPrimary:true))
                        {
                            if (v != null && !v.IsEmpty)
                            {
                                if (v is ArrayTypeNode arr)
                                {
                                    sb.Append($" AND {sqlProvider.In(fld, arr.Where(a => !a.IsEmpty).Select(a => a.Value!))}");
                                }
                                else
                                {
                                    sb.Append($" AND {sqlProvider.QuoteField(fld)} = {sqlProvider.Literal(v.Value)}");
                                }
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
                        foreach ((string fld, AnySchemaNode? v) in schema.GetFieldValues(pack, true))
                        {
                            if (v == null || v.IsEmpty || v is ArrayTypeNode)
                            {
                                fullFill = false;
                                break;
                            }

                            if (appAnd) sb.Append(" AND ");
                            sb.Append($"{sqlProvider.QuoteField(fld)} = {sqlProvider.Literal(v.Value)}");
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
            AppendFields(select, schema, "o.");
            select.Append(" FROM ");
            select.Append(schema.Name);
            select.Append(" o JOIN (SELECT ");
            select.Append(_refSeqNo);
            select.Append(" ");
            select.Append(sb.ToString());
            select.Append(") t ON o.");
            select.Append(_refSeqNo);
            select.Append(" = t.");
            select.Append(_refSeqNo);
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
            ArrayTypeNode value = new ArrayTypeNode(schema.SchemaType);
            DbCommand command = GetDbCommand();
            command.CommandText = select.ToString();
            Logger.LogDebug(command.CommandText);
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

    public async Task<(AnySchemaNode? result, int total)> QueryDynamicTableAsync(DynamicTableSchema schema, string target, ExpNode filter, int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, bool forUpdate = false)
    {
        // single row
        if (schema.Single) return await QueryDynamicTableAsync(schema, target);

        string tableName = sqlProvider.QuoteTable(schema.Name);
        await EnsureOpenConnectionAsync();
        if (string.IsNullOrWhiteSpace(target)) return (null, -1);

        // Build sql
        bool fullFill = false;
        StringBuilder sb = new();
        sb.Append($" From {tableName} FORCE INDEX({_refIndex}) ");

        // Conditions
        sb.Append($" WHERE {_refTarget} = {sqlProvider.Literal(target)}");

        // exp node -> sql
        string sql = filter.ToSql(sqlProvider);
        if (!string.IsNullOrEmpty(sql))
        {
            sb.Append(" AND ");
            sb.Append(sql);
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
        foreach (var (field, d) in schema.GetOrderBys(desc, orderBy))
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
        AppendFields(select, schema, "o.");
        select.Append(" FROM ");
        select.Append(schema.Name);
        select.Append(" o JOIN (SELECT ");
        select.Append(_refSeqNo);
        select.Append(" ");
        select.Append(sb.ToString());
        select.Append(") t ON o.");
        select.Append(_refSeqNo);
        select.Append(" = t.");
        select.Append(_refSeqNo);
        select.Append(" ORDER BY ");
        first = false;
        foreach (var (field, d) in schema.GetOrderBys(desc, orderBy))
        {
            if (first) select.Append(", ");
            first = true;
            select.Append($"o.{field}");
            if (d) select.Append(" DESC ");
        }

        select.Append(forUpdate ? " FOR UPDATE;" : ";");
        ArrayTypeNode value = new ArrayTypeNode(schema.SchemaType);
        DbCommand command = GetDbCommand();
        command.CommandText = select.ToString();
        Logger.LogDebug(command.CommandText);
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

    /// <inheritdoc />
    public async Task<(bool result, AnySchemaNode? origin)> SaveDynamicTableDataAsync(DynamicTableSchema schema, string target, AnySchemaNode? value = null)
    {
        string tableName = sqlProvider.QuoteTable(schema.Name);
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
                    command.CommandText = $"DELETE FROM {tableName} WHERE {_refTarget} = {sqlProvider.Literal(target)}";
                    Logger.LogDebug(command.CommandText);
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
                        command.CommandText = $"INSERT INTO {tableName} ({_refTarget}, {sqlProvider.QuoteField(DYNAMIC_TABLE_VALUE_FIELD)}) VALUES ( {sqlProvider.Literal(target)}, {sqlProvider.Literal(result)} )";
                        Logger.LogDebug(command.CommandText);
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
                    command.CommandText = $"UPDATE {tableName} SET {sqlProvider.QuoteField(DYNAMIC_TABLE_VALUE_FIELD)} = {sqlProvider.Literal(result)} WHERE {_refTarget} = {sqlProvider.Literal(target)}";
                    Logger.LogDebug(command.CommandText);
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
                    sb.Append($"INSERT INTO {tableName} ({_refTarget}, ");
                    AppendFields(sb, schema);
                    sb.Append($") VALUES ( {sqlProvider.Literal(target)}");

                    // Body
                    foreach ((string _, AnySchemaNode? val) in schema.GetFieldValues(pack))
                        sb.Append($",{sqlProvider.Literal(val?.Value)}");

                    // Footer
                    sb.Append(");");
                    try
                    {
                        // Execute
                        DbCommand command = GetDbCommand();
                        command.CommandText = sb.ToString();
                        Logger.LogDebug(command.CommandText);
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
                    sb.Append($"UPDATE {tableName} SET ");

                    // Body
                    bool preCond = false;
                    foreach ((string fld, AnySchemaNode? val) in schema.GetFieldValues(pack))
                    {
                        sb.Append($"{(preCond ? "," : "")}{sqlProvider.QuoteField(fld)}={sqlProvider.Literal(val)}");
                        preCond = true;
                    }

                    // Footer
                    sb.Append($" WHERE {_refTarget} = {sqlProvider.Literal(target)}");

                    // Execute
                    DbCommand command = GetDbCommand();
                    command.CommandText = sb.ToString();
                    Logger.LogDebug(command.CommandText);
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
                        foreach ((_, AnySchemaNode? v) in schema.GetFieldValues(obj, true))
                        {
                            // Check value
                            if (v == null || v.IsEmpty)
                            {
                                fullFill = false;
                                break;
                            }
                            keys.Add(v.ToString());
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
                sb.Append($" WHERE {_refTarget} = {sqlProvider.Literal(target)}");
                foreach ((string fld, AnySchemaNode? v) in schema.GetFieldValues(pack, true))
                {
                    // Check value
                    if (v == null || v.IsEmpty)
                    {
                        fullFill = false;
                        break;
                    }
                    keys.Add(v.ToString());
                    sb.Append($" AND `{fld}` = {sqlProvider.Literal(v)}");
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
                    sb.Append($"INSERT INTO {tableName} ({_refTarget}, ");
                    AppendFields(sb, schema);
                    sb.Append($") VALUES ( {sqlProvider.Literal(target)}");

                    // Body
                    foreach ((string _, AnySchemaNode? v) in schema.GetFieldValues(pack))
                        sb.Append($",{sqlProvider.Literal(v)}");

                    // Footer
                    sb.Append(");");
                    try
                    {
                        // Execute
                        DbCommand command = GetDbCommand();
                        command.CommandText = sb.ToString();
                        Logger.LogDebug(command.CommandText);
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
                    sb.Append($"UPDATE {tableName} SET ");

                    // Body
                    bool preCond = false;
                    foreach ((string fld, AnySchemaNode? v) in schema.GetFieldValues(pack, false, true))
                    {
                        sb.Append($"{(preCond ? "," : "")}{sqlProvider.QuoteField(fld)}={sqlProvider.Literal(v)}");
                        preCond = true;
                    }

                    // Footer
                    sb.Append(" ");
                    sb.Append(where);

                    // Execute
                    DbCommand command = GetDbCommand();
                    command.CommandText = sb.ToString();
                    Logger.LogDebug(command.CommandText);
                    await command.ExecuteNonQueryAsync();
                }
            }
            return (true, origin);
        }
    }

    /// <inheritdoc />
    public async Task<(bool result, AnySchemaNode? origin)> DeleteDynamicTableDataAsync(DynamicTableSchema schema, string target, JsonNode? filter = null)
    {
        string tableName = sqlProvider.QuoteTable(schema.Name);
        await EnsureOpenConnectionAsync();
        target = !string.IsNullOrWhiteSpace(target) ? MySqlHelper.EscapeString(target) : "";

        if (string.IsNullOrWhiteSpace(target)) return (false, null);
        
        // single row
        if (schema.Single)
        {
            (AnySchemaNode? origin, _) = await QueryDynamicTableAsync(schema, target, forUpdate: true);
            if (origin is null) return (false, null);
            
            DbCommand command = GetDbCommand();
            command.CommandText = $"DELETE FROM {tableName} WHERE {_refTarget} = {sqlProvider.Literal(target)}";
            Logger.LogDebug(command.CommandText);
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
            command.CommandText = $"DELETE {_whereClause.Replace($"FORCE INDEX({_refIndex})", "")};"; // Can change to deleted flag controls
            Logger.LogDebug(command.CommandText);
            await command.ExecuteNonQueryAsync();
            
            return (true, origin);
        }

        return (false, null);
    }

    /// <inheritdoc />
    public async Task DropDynamicTableAsync(string dynamicTableName)
    {
        string tableName = sqlProvider.QuoteTable(dynamicTableName);
        await EnsureOpenConnectionAsync();
        DbCommand command = GetDbCommand();
        command.CommandText = $"DROP TABLE IF EXISTS {tableName};";
        Logger.LogDebug(command.CommandText);
        await command.ExecuteNonQueryAsync();
    }
    
    /// <inheritdoc />
    public async Task BeginTransactionAsync()
    {
        if (_transaction != null)
            throw new InvalidOperationException("There is already a transaction in progress.");

        await EnsureOpenConnectionAsync();
        _transaction = await dbConn.BeginTransactionAsync();
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

    /// <summary>
    /// Append the fields to the string builder
    /// </summary>
    void AppendFields(StringBuilder sb, DynamicTableSchema schema, string prefix = "")
    {
        bool appendComma = false;
        foreach (DynamicTableField field in schema.Fields)
        {
            if (appendComma) sb.Append(", ");
            appendComma = true;
            sb.Append($"{prefix}{sqlProvider.QuoteField(field.Name)}");
        }
    }

    // Get DbCommand
    DbCommand GetDbCommand()
    {
        DbCommand command = dbConn.CreateCommand();
        command.Transaction = _transaction;
        return command;
    }
    
    // Ensure the database connection is open
    private Task EnsureOpenConnectionAsync() => dbConn.State != ConnectionState.Open ? dbConn.OpenAsync() : Task.CompletedTask;
    
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

    private readonly Lazy<ILogger> _loggerThunk = new (serviceProvider.GetRequiredService<ILogger<AppDataMySqlProvider>>);
    private string? _whereClause;

    #endregion
}
