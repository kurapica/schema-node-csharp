using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using SchemaNode.Components;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.MySql;

/// <summary>
/// The implementation of IAppSchemaDataProvider for MySQL
/// </summary>
public class AppDataMySqlProvider(MySqlConnection dbConn, IServiceProvider serviceProvider, ISqlProvider sqlProvider) : IAppDataSqlProvider<MySqlProvider>
{
    #region Properties and Fields

    private readonly string _refTarget = sqlProvider.QuoteField(DYNAMIC_TABLE_TARG_FIELD);
    private readonly string _refIndex = sqlProvider.QuoteIndex(DYNAMIC_UNIQUE_INDEX);
    private readonly string _refSeqNo = sqlProvider.QuoteField(DYNAMIC_TABLE_SEQNO_FIELD);

    private readonly string _refAttrField = sqlProvider.QuoteField(EAV_TABLE_FIELD);
    private readonly string _refAttrIntField = sqlProvider.QuoteField(EAV_TABLE_BIGINT_FIELD);
    private readonly string _refAttrStrField = sqlProvider.QuoteField(EAV_TABLE_STRING_FIELD);
    private readonly string _refAttrDatField = sqlProvider.QuoteField(EAV_TABLE_DATETIME_FIELD);
    private readonly string _refAttrDblField = sqlProvider.QuoteField(EAV_TABLE_DOUBLE_FIELD);
    private readonly string _refAttrTxtField = sqlProvider.QuoteField(EAV_TABLE_TEXT_FIELD);
    private readonly string _refAttrJsonField = sqlProvider.QuoteField(EAV_TABLE_JSON_FIELD);

    #endregion

    #region IAppSchemaDataProvider implementation

    /// <inheritdoc />
    public async Task<bool> EnsureDynamicTableAsync(DynamicTableSchema schema)
    {
        string tableName = sqlProvider.QuoteTable(schema.AppFieldType.DynamicTableName);
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
            foreach (DynamicTableField dyFld in schema.ValueFields)
            {
                string dataType = DataType(dyFld);
                if (!nameTypes.TryGetValue(dyFld.Name, out string? type))
                {
                    sb ??= new StringBuilder();
                    sb.Append($"ALTER TABLE {tableName} ADD {sqlProvider.QuoteField(dyFld.Name)} {dataType};");
                }
                else if (!type.Equals(dataType, StringComparison.OrdinalIgnoreCase))
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
                List<string> chkUniqueIndex =schema.KeyFields.Select(tableField => tableField.Name).ToList();

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
                        sb.Append($"ALTER TABLE {tableName} ADD INDEX {sqlProvider.QuoteIndex(key)}({string.Join(',', schema.ScopeTargetFields.Select(f => sqlProvider.QuoteField(f.Name)).Concat(index.Fields.Select(sqlProvider.QuoteField)))});");
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
            
            // Generate key columns
            foreach (DynamicTableField keyField in schema.KeyFields)
            {
                sb.Append($"{sqlProvider.QuoteField(keyField.Name)} {DataType(keyField)} NOT NULL, ");
            }

            // Generate the column lists
            foreach (DynamicTableField tableField in schema.Fields.Where(f => f.IsValueField))
            {
                // Name-Type
                sb.Append($"{sqlProvider.QuoteField(tableField.Name)} {DataType(tableField)}, ");
            }

            // Append primary key
            if (schema.Single)
            {
                sb.Append($"PRIMARY KEY({_refTarget})");
            }
            else
            {
                // Use auto-incr seqNo as primary key
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
            Logger.LogInformation(command.CommandText);
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
                Logger.LogInformation(command.CommandText);
                await command.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message);
            throw;
        }
        
        // Check if require EAV table
        if (schema.AppFieldType.Topology == FieldStorageTopology.AttributeBased)
        {
            // Create the attribute-value table
            try
            {
                tableName = sqlProvider.QuoteTable(schema.AppFieldType.AttributeTableName);
                StringBuilder sb = new();

                // Create the data table
                sb.Append($"CREATE TABLE IF NOT EXISTS {tableName} (");

                // The primary key
                sb.Append($"{_refTarget} VARCHAR({DYNAMIC_TABLE_TARG_LEN}) NOT NULL, ");
                
                // Generate the primary fields
                foreach (DynamicTableField tableField in schema.Fields.Where(f => f.Primary))
                    sb.Append($"{sqlProvider.QuoteField(tableField.Name)} {DataType(tableField)} NOT NULL, ");
                
                // The attribute field
                sb.Append($"{_refAttrField} VARCHAR({EAV_TABLE_FIELD_MAX_LENGTH}) NOT NULL, ");
                
                // The value field
                sb.Append($"{_refAttrIntField} BIGINT, ");
                sb.Append($"{_refAttrStrField} VARCHAR({ENTITY_PRIMARY_KEY_MAX_LEN}), ");
                sb.Append($"{_refAttrDatField} DATETIME, ");
                sb.Append($"{_refAttrDblField} DOUBLE, ");
                sb.Append($"{_refAttrTxtField} TEXT, ");
                sb.Append($"{_refAttrJsonField} JSON, ");
                
                // Append primary key
                // Use auto-incr seqNo as primary key
                sb.Append($"PRIMARY KEY({_refTarget}");

                // Use other primary key as unique index
                foreach (DynamicTableField tableField in schema.Fields.Where(p => p.Primary))
                    sb.Append($", {sqlProvider.QuoteField(tableField.Name)}");
                sb.Append($", {_refAttrField})");

                // End the building
                sb.Append(") engine=InnoDB;");
                
                DbCommand command = GetDbCommand();
                command.CommandText = sb.ToString();
                Logger.LogInformation(command.CommandText);
                await command.ExecuteNonQueryAsync();
                
                // Create the indexes
                sb = new StringBuilder();
                sb.Append($"ALTER TABLE {tableName} ADD INDEX {sqlProvider.QuoteIndex("IDX_TAR_FLD")}({_refTarget}, {_refAttrField}),");
                sb.Append($"ADD INDEX {sqlProvider.QuoteIndex("IDX_TAR_FLD_INT")}({_refTarget}, {_refAttrField}, {_refAttrIntField}),");
                sb.Append($"ADD INDEX {sqlProvider.QuoteIndex("IDX_TAR_FLD_STR")}({_refTarget}, {_refAttrField}, {_refAttrStrField}),");
                sb.Append($"ADD INDEX {sqlProvider.QuoteIndex("IDX_TAR_FLD_DAT")}({_refTarget}, {_refAttrField}, {_refAttrDatField});");
                
                command = GetDbCommand();
                command.CommandText = sb.ToString();
                Logger.LogInformation(command.CommandText);
                await command.ExecuteNonQueryAsync();
            }
            catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyName)
            {
                // Ignore duplicate key error
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
                throw;
            }
        }

        return true;
    }
    
    // <inheritdoc />
    public async Task<(AnySchemaNode? result, int total)> QueryDynamicTableAsync(DynamicTableSchema schema, string target, 
        AppSchemaDataResult type, AppSchemaDataFilter? filter = null, int skip = 0, int take = 0, bool desc = false, 
        AppSchemaDataOrder[]? orderBy = null, string? dataField = null, bool forUpdate = false)
    {
        string tableName = sqlProvider.QuoteTable(schema.AppFieldType.DynamicTableName);
        
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
        else
        {
            await EnsureOpenConnectionAsync();
            if (string.IsNullOrWhiteSpace(target)) return (null, -1);

            if (type == AppSchemaDataResult.Last) desc = !desc;

            // Build SQL
            StringBuilder sb = new();
            sb.Append($" From {tableName} ");

            // Conditions
            sb.Append($" WHERE {_refTarget} = {sqlProvider.Literal(target)}");

            // exp node -> sql
            string sql = filter?.ToSql(sqlProvider, schema) ?? "";
            if (!string.IsNullOrEmpty(sql))
            {
                sb.Append(" AND ");
                sb.Append(sql);
            }

            // Query Total
            int total = 0;
            if (type is AppSchemaDataResult.List or AppSchemaDataResult.Exist or AppSchemaDataResult.Count &&
                !forUpdate)
            {
                DbCommand totalCommand = GetDbCommand();
                // only used to check existence
                totalCommand.CommandText = type == AppSchemaDataResult.Exist 
                    ? $"SELECT EXISTS (SELECT 1 {sb} LIMIT 1) AS exists_flag;" 
                    : $"SELECT COUNT(*) {sb};";

                Logger.LogInformation(totalCommand.CommandText);
                DbDataReader totalReader = await totalCommand.ExecuteReaderAsync();
                try
                {
                    if (totalReader.HasRows && await totalReader.ReadAsync())
                        total = totalReader.GetInt32(0);
                    switch (type)
                    {
                        case AppSchemaDataResult.Exist:
                            return (SchemaContext.SystemBool.CreateNode(total > 0), total);
                        case AppSchemaDataResult.Count:
                            return (SchemaContext.SystemInt.CreateNode(total), total);
                    }
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

            if (type is AppSchemaDataResult.First or AppSchemaDataResult.Last)
                sb.Append(" LIMIT 1");
            else if (take is > 0)
                sb.Append($" LIMIT {take}");
            if (skip is > 0)
                sb.Append($" OFFSET {skip}");

            // Query Data
            StringBuilder select = new();
            select.Append("SELECT ");
            AppendFields(select, schema, "o.");
            select.Append(" FROM ");
            select.Append(tableName);
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

            ArrayTypeNode? value = null;
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
                        AnySchemaNode? pack = type == AppSchemaDataResult.Field
                            ? schema.GetFieldPack(reader, dataField ?? "")
                            : schema.GetFieldPack(reader);
                        if (pack != null)
                        {
                            value ??= new ArrayTypeNode(pack.SchemaType);
                            value.Add(pack);
                        }
                    }
                }
            }
            finally
            {
                await reader.CloseAsync();
            }

            // Load the attribute-based fields if needed
            await FillAttributeDataAsync(schema, target, value, forUpdate);

            if (type is AppSchemaDataResult.First or AppSchemaDataResult.Last)
                return (value?.ElementAtOrDefault(0), value is { Count: > 0 } ? 1 : 0);
            return (value, total > 0 ? total : (value?.Count ?? 0));
        }
    }

    /// <summary>
    /// Fill the attribute-based fields for the value list
    /// </summary>
    /// <param name="schema"></param>
    /// <param name="target"></param>
    /// <param name="value"></param>
    /// <param name="forUpdate"></param>
    async Task FillAttributeDataAsync(DynamicTableSchema schema, string target, ArrayTypeNode? value, bool forUpdate = false)
    {
        // Load the attribute-based fields if needed
        if (value is { Count: > 0 } && 
            schema.AppFieldType.Topology == FieldStorageTopology.AttributeBased &&
            schema.Fields.Any(p => p.HasTypeRelation))
        {
            StringBuilder select = new();
            select.Append("SELECT ");
            foreach (DynamicTableField tableField in schema.Fields.Where(p => p.Primary))
                select.Append($"{sqlProvider.QuoteField(tableField.Name)}, ");
            select.Append($"{_refAttrField}, {_refAttrIntField}, {_refAttrStrField}, {_refAttrDatField}, {_refAttrDblField}, {_refAttrTxtField}, {_refAttrJsonField} ");
            select.Append($"FROM {sqlProvider.QuoteTable(schema.AppFieldType.AttributeTableName)} ");
            select.Append($"WHERE {_refTarget} = {sqlProvider.Literal(target)} ");

            if (value.Count > MAX_COMBINE_CASE_COUNT)
            {
                foreach (DynamicTableField tableField in schema.Fields.Where(p => p.Primary))
                    select.Append($"AND {sqlProvider.QuoteField(tableField.Name)} IN ({string.Join(',', value.Cast<StructTypeNode>().Select(v => sqlProvider.Literal(v[tableField.Name])))} ) ");
            }
            else
            {
                select.Append("AND (");
                bool hasQuery = false;
                foreach (StructTypeNode node in value.Cast<StructTypeNode>())
                {
                    select.Append(hasQuery ? "OR (" : "(");
                    bool first = false;
                    hasQuery = true;
                    foreach (DynamicTableField tableField in schema.Fields.Where(p => p.Primary))
                    {
                        if (first) select.Append(" AND ");
                        first = true;
                        select.Append($"{sqlProvider.QuoteField(tableField.Name)} = {sqlProvider.Literal(node[tableField.Name])}");
                    }
                    select.Append(")");
                }
                select.Append(")");
            }
            
            select.Append(forUpdate ? " FOR UPDATE;" : ";");
            var command = GetDbCommand();
            command.CommandText = select.ToString();
            Logger.LogDebug(command.CommandText);
            var reader = await command.ExecuteReaderAsync();
            try
            {
                if (reader.HasRows)
                {
                    while (await reader.ReadAsync())
                    {
                        int offset = 0;
                        IEnumerable<StructTypeNode> nodes = value.Cast<StructTypeNode>();
                        foreach (DynamicTableField tableField in schema.Fields.Where(p => p.Primary))
                        {
                            AnySchemaNode? val = tableField.FromReader(reader, offset++);
                            if (val == null || val.IsEmpty) break;
                            nodes = nodes.Where(n => val.Equals(n.GetField(tableField.Name)!));
                        }
                        StructTypeNode[] matched = nodes.ToArray();
                        if (matched.Length != 1) continue;

                        StructTypeNode pack = matched[0];
                        string attr = reader.GetString(offset++);
                        if (string.IsNullOrWhiteSpace(attr)) continue;

                        // For multi struct field, the attr field is in format "structField_attrField", we need to split it to get the real attr field
                        string[] paths = attr.Split('_', StringSplitOptions.RemoveEmptyEntries);
                        JsonTypeNode jsonNode = (pack.GetField(paths[0]) as JsonTypeNode)!;
                        jsonNode.Value ??= new JsonObject();
                        JsonObject container = (jsonNode.Value as JsonObject)!;
                        for(int i = 1; i < paths.Length - 1; i++)
                        {
                            if (!container.TryGetPropertyValue(paths[i], out JsonNode? next) || next is not JsonObject)
                            {
                                next = new JsonObject();
                                container[paths[i]] = next;
                            }
                            container = (JsonObject)next;
                        }
                        attr = paths[^1];
                        
                        // bigint
                        if (!reader.IsDBNull(offset))
                        {
                            container[attr] = reader.GetInt64(offset);
                        }
                        // string
                        else if (!reader.IsDBNull(offset + 1))
                        {
                            container[attr] = reader.GetString(offset + 1);
                        }
                        // datetime
                        else if (!reader.IsDBNull(offset + 2))
                        {
                            container[attr] = reader.GetDateTime(offset + 2);
                        }
                        // double
                        else if (!reader.IsDBNull(offset + 3))
                        {
                            container[attr] = reader.GetDouble(offset + 3);
                        }
                        // text
                        else if (!reader.IsDBNull(offset + 4))
                        {
                            container[attr] = reader.GetString(offset + 4);
                        }
                        // json
                        else if (!reader.IsDBNull(offset + 5))
                        {
                            object raw = reader.GetValue(offset + 5);
                            container[attr] = raw is DBNull ? null : raw switch
                            {
                                string s => JsonNode.Parse(s),
                                byte[] b => JsonNode.Parse(b),
                                _ => null
                            };
                        }
                    }
                }
            }
            finally
            {
                await reader.CloseAsync();
            }
        }
    }
    
    /// <inheritdoc />
    public async Task<(bool result, AnySchemaNode? update, AnySchemaNode? origin)> SaveDynamicTableDataAsync(
            DynamicTableSchema schema, string target, AnySchemaNode? value = null, 
            bool canAdd = true, bool onlyAdd = false, string[]? overrides = null)
    {
        string tableName = sqlProvider.QuoteTable(schema.AppFieldType.DynamicTableName);
        await EnsureOpenConnectionAsync();
        target = !string.IsNullOrWhiteSpace(target) ? MySqlHelper.EscapeString(target) : "";
        if (string.IsNullOrWhiteSpace(target)) return (false, null, null);
        
        // single row
        if (schema.Single)
        {
            // Gets the origin value
            (AnySchemaNode? origin, _) = await QueryDynamicTableAsync(schema, target, AppSchemaDataResult.First);

            // Delete if null
            if (value == null)
            {
                if (origin != null)
                {
                    DbCommand command = GetDbCommand();
                    command.CommandText = $"DELETE FROM {tableName} WHERE {_refTarget} = {sqlProvider.Literal(target)}";
                    Logger.LogInformation(command.CommandText);
                    await command.ExecuteNonQueryAsync();
                    return (true, null, origin);
                }
                return (false, null, null);
            }
            
            if (value is ArrayTypeNode arr)
            {
                if (arr is [not null])
                    value = arr.First();
                else
                    return (false, null, null);
            }
            if (origin != null && value.Equals(origin))
                return (false, null, null);

            // Check the value type
            if (schema.Fields is [{ Name: DYNAMIC_TABLE_VALUE_FIELD }])
            {
                // Convert the value
                string? result = schema.Fields[0].ToString(value);
                bool isInsert = false;

                // Insert the value
                if (origin == null)
                {
                    if (result == null) return (false, null, null);
                    try
                    {
                        DbCommand command = GetDbCommand();
                        command.CommandText = $"INSERT INTO {tableName} ({_refTarget}, {sqlProvider.QuoteField(DYNAMIC_TABLE_VALUE_FIELD)}) VALUES ( {sqlProvider.Literal(target)}, {sqlProvider.Literal(result)} )";
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
                    // Gets the origin value again
                    if (origin == null)
                        (origin, _) = await QueryDynamicTableAsync(schema, target, AppSchemaDataResult.First);

                    DbCommand command = GetDbCommand();
                    command.CommandText = $"UPDATE {tableName} SET {sqlProvider.QuoteField(DYNAMIC_TABLE_VALUE_FIELD)} = {sqlProvider.Literal(result)} WHERE {_refTarget} = {sqlProvider.Literal(target)}";
                    Logger.LogInformation(command.CommandText);
                    await command.ExecuteNonQueryAsync();
                }
                return (true, value, origin);
            }
            else if (value is StructTypeNode pack)
            {
                // Build the SQL
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
                    // Gets the origin value again
                    if (origin == null)
                        (origin, _) = await QueryDynamicTableAsync(schema, target, AppSchemaDataResult.First);

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
                    Logger.LogInformation(command.CommandText);
                    await command.ExecuteNonQueryAsync();
                }
                return (true, value, origin);
            }
            else
            {
                return (false, null, null);
            }
        }
        
        // multi rows
        else
        {
            StringBuilder sb = new();

            // Prepare the data
            StructTypeNode[] packs;
            switch (value)
            {
                case ArrayTypeNode arr:
                    if (arr.Count == 0) return (false, null, null);
                    packs = arr.Cast<StructTypeNode>().ToArray();
                    break;
                case StructTypeNode obj:
                    packs = [obj];
                    break;
                default:
                    return (false, null, null);
            }

            // Query
            AnySchemaNode? origin = await this.QueryOriginNodesAsync(schema, target, packs, forUpdate: true);
            ArrayTypeNode? oArr = origin as ArrayTypeNode;
            if (!canAdd && (oArr == null || oArr.Count < packs.Length))
                throw new UnauthorizedAccessException();

            // record exist rows
            Dictionary<string, StructTypeNode> existKeys = [];
            List<string> keys = [];
            if (oArr is { Count: > 0 })
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

                        if (!fullFill) return (false, null, null); // impossible
                        existKeys.Add(string.Join('|', keys), obj);
                    }
                }
            }

            // Foreach
            List<StructTypeNode> updatedPacks = [];
            List<StructTypeNode> originPacks = [];
            foreach (StructTypeNode pack in packs)
            {
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
                if (!existKeys.TryGetValue(string.Join('|', keys), out StructTypeNode? originPack))
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
                        Logger.LogInformation(command.CommandText);
                        await command.ExecuteNonQueryAsync();
                        isInsert = true;

                        updatedPacks.Add(pack);
                    }
                    catch (MySqlException ex)
                    {
                        if (ex.ErrorCode != MySqlErrorCode.DuplicateKeyEntry)
                            throw;
                    }
                }

                if (!isInsert && (!onlyAdd || overrides is { Length: > 0 }))
                {
                    // query again
                    if (originPack == null)
                    {
                        origin = await this.QueryOriginNodesAsync(schema, target, [pack], forUpdate: true);
                        if (origin is ArrayTypeNode { Count: 1 } arr)
                            originPack = arr[0] as StructTypeNode;
                    }

                    // Skip if no change
                    if (originPack != null && originPack.Equals(pack))
                        continue;

                    // Header
                    sb.Clear();
                    sb.Append($"UPDATE {tableName} SET ");

                    // Body
                    bool preCond = false;
                    foreach ((string fld, AnySchemaNode? v) in schema.GetFieldValues(pack, false, true))
                    {
                        // Check override
                        if (overrides is { Length: > 0 } && !overrides.Contains(fld, StringComparer.OrdinalIgnoreCase))
                            continue;

                        sb.Append($"{(preCond ? "," : "")}{sqlProvider.QuoteField(fld)}={sqlProvider.Literal(v)}");
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

                    updatedPacks.Add(pack);
                    if (originPack != null)
                        originPacks.Add(originPack);
                }

                // Save attribute-based fields if needed
                if (schema.AppFieldType.Topology == FieldStorageTopology.AttributeBased)
                {
                    SchemaContext context = serviceProvider.GetService<SchemaContext>()!;
                    foreach (DynamicTableField dynamic in schema.Fields.Where(f => f.HasTypeRelation))
                    {
                        var fields = dynamic.RelationType != null
                            ? await GetStructFieldConfigs(schema.AppFieldType, target, pack, dynamic.RelationType)
                            : await GetStructFieldConfigs(target, pack, dynamic.StructRelation!);
                        if (fields.Length == 0) continue;

                        List<(string, AnySchemaNode v)> primaries = [];
                        foreach ((string fld, AnySchemaNode? v) in schema.GetFieldValues(pack, true))
                            primaries.Add((fld, v!));

                        await SaveAttributeBasedFieldAsync(context, schema.AppFieldType.AttributeTableName, target, fields,
                            (pack.GetField(dynamic.Name) as JsonTypeNode)?.Value as JsonObject, dynamic.Name.ToLower(), primaries);
                    }
                }
            }
            return (true, new ArrayTypeNode(schema.SchemaType, updatedPacks),  (onlyAdd && (overrides == null || overrides.Length == 0)) ? null : new ArrayTypeNode(schema.SchemaType, originPacks) );
        }
    }

    /// <inheritdoc />
    public async Task<(bool result, AnySchemaNode? origin)> DeleteDynamicTableDataAsync(DynamicTableSchema schema, string target, AppSchemaDataFilter filter)
    {
        string tableName = sqlProvider.QuoteTable(schema.AppFieldType.DynamicTableName);
        await EnsureOpenConnectionAsync();
        target = !string.IsNullOrWhiteSpace(target) ? MySqlHelper.EscapeString(target) : "";

        if (string.IsNullOrWhiteSpace(target)) return (false, null);
        
        // single row
        if (schema.Single)
        {
            (AnySchemaNode? origin, _) = await QueryDynamicTableAsync(schema, target, AppSchemaDataResult.First, forUpdate: true);
            if (origin is null) return (false, null);
            
            DbCommand command = GetDbCommand();
            command.CommandText = $"DELETE FROM {tableName} WHERE {_refTarget} = {sqlProvider.Literal(target)}";
            Logger.LogInformation(command.CommandText);
            await command.ExecuteNonQueryAsync();
            
            return (true, origin);
        }
        
        // multi rows
        else
        {
            _whereClause = null;
            (AnySchemaNode? origin, _) = await QueryDynamicTableAsync(schema, target, AppSchemaDataResult.List, filter, forUpdate: true);
            if (origin is not ArrayTypeNode arr || arr.Count == 0 || _whereClause == null) return (false, null);
            
            DbCommand command = GetDbCommand();
            command.CommandText = $"DELETE {_whereClause.Replace($"FORCE INDEX({_refIndex})", "")};"; // Can change to deleted flag controls
            Logger.LogInformation(command.CommandText);
            await command.ExecuteNonQueryAsync();

            await DeleteAttributeBasedFieldAsync(schema, target, arr);
            return (true, origin);
        }
    }

    /// <summary>
    /// Clear all dynamic table data
    /// </summary>
    public async Task<(bool result, AnySchemaNode? origin)> ClearDynamicTableDataAsync(DynamicTableSchema schema, string target)
    {
        string tableName = sqlProvider.QuoteTable(schema.AppFieldType.DynamicTableName);
        await EnsureOpenConnectionAsync();
        target = !string.IsNullOrWhiteSpace(target) ? MySqlHelper.EscapeString(target) : "";

        if (string.IsNullOrWhiteSpace(target)) return (false, null);
        
        // single row
        if (schema.Single)
        {
            (AnySchemaNode? origin, _) = await QueryDynamicTableAsync(schema, target, AppSchemaDataResult.First, forUpdate: true);
            if (origin is null) return (false, null);
            
            DbCommand command = GetDbCommand();
            command.CommandText = $"DELETE FROM {tableName} WHERE {_refTarget} = {sqlProvider.Literal(target)}";
            Logger.LogInformation(command.CommandText);
            await command.ExecuteNonQueryAsync();
            
            return (true, origin);
        }
        
        // multi rows
        else
        {
            _whereClause = null;
            (AnySchemaNode? origin, _) = await QueryDynamicTableAsync(schema, target, AppSchemaDataResult.List, forUpdate: true);
            if (origin is not ArrayTypeNode arr || arr.Count == 0 || _whereClause == null) return (false, null);
            
            DbCommand command = GetDbCommand();
            command.CommandText = $"DELETE {_whereClause.Replace($"FORCE INDEX({_refIndex})", "")};"; // Can change to deleted flag controls
            Logger.LogInformation(command.CommandText);
            await command.ExecuteNonQueryAsync();
            
            await DeleteAttributeBasedFieldAsync(schema, target, arr);
            return (true, origin);
        }
    }

    /// <inheritdoc />
    public async Task DropDynamicTableAsync(DynamicTableSchema schema)
    {
        await Task.Yield();
        
        #if DEBUG
        string tableName = sqlProvider.QuoteTable(schema.AppFieldType.DynamicTableName);
        await EnsureOpenConnectionAsync();
        
        DbCommand command = GetDbCommand();
        command.CommandText = $"DROP TABLE IF EXISTS {tableName};";
        Logger.LogInformation(command.CommandText);
        await command.ExecuteNonQueryAsync();
        
        if (schema.AppFieldType.Topology == FieldStorageTopology.AttributeBased)
        {
            string attrTableName = sqlProvider.QuoteTable(schema.AppFieldType.AttributeTableName);
            DbCommand attrCommand = GetDbCommand();
            attrCommand.CommandText = $"DROP TABLE IF EXISTS {attrTableName};";
            Logger.LogInformation(attrCommand.CommandText);
            await attrCommand.ExecuteNonQueryAsync();
        }
        #endif
        
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
        foreach (DynamicTableField field in schema.Fields.Where(f => f.IsValueField))
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
    private async Task EnsureOpenConnectionAsync()
    {
        try
        {
            if (dbConn.State != ConnectionState.Open)
                await dbConn.OpenAsync();
        }
        catch (Exception)
        {
            // ignore
        }
    }

    /// <summary>
    /// Gets the struct field config for dynamic type from the relation
    /// </summary>
    async Task<StructFieldConfig[]> GetStructFieldConfigs(AppFieldType appField, string target, StructTypeNode node, AppRelationSchema relation)
    {
        SchemaContext context = serviceProvider.GetService<SchemaContext>() ?? throw new Exception("The Schema context missing");
        if (relation.FuncNode == null) throw new Exception("The function node missing");
        
        // If the arguments is another field, we can query it directly, since it's designed to be used in frontend,
        // means it's value is small and easy to query, otherwise the function can be executed to gets the value directly
        object?[] args = new object[relation.Args.Length];
        for (int i = 0; i < relation.Args.Length; i++)
        {
            var arg = relation.Args[i];
            if (!string.IsNullOrEmpty(arg.AppField))
            {
                if (arg.AppField.Equals(appField.Name, StringComparison.OrdinalIgnoreCase))
                {
                    args[i] = node;
                }
                else if (_relationDataCache.TryGetValue(arg.AppField.ToLower(), out AnySchemaNode? cache) && cache != null)
                {
                    args[i] = cache;
                }
                else
                {
                    var schema = appField.Application.GetField(arg.AppField)?.Schema;
                    if (schema == null)
                        throw new Exception($"The field {arg.AppField} not found in app {appField.Application.Name}");
                    (AnySchemaNode? result, int total) = await QueryDynamicTableAsync(schema, target, AppSchemaDataResult.List);
                    if (total > 50)
                        Logger.LogWarning($"The query result of field {arg.AppField} in app {appField.Application.Name} is too large, total {total}, relation function {relation.Func} may not work properly");
                    
                    _relationDataCache[arg.AppField.ToLower()] = result;
                    args[i] = result;
                }
                
                if (args[i] != null && !string.IsNullOrWhiteSpace(arg.DataField))
                    args[i] = (args[i] as StructTypeNode)?.GetValueByPaths(arg.DataField);
            }
            else if (arg.Value != null)
            {
                args[i] = arg.Value;
            }
        }
        
        // build the unique key for cache
        string? uniqueKey = args.All(a => a is ScalarTypeNode or EnumTypeNode or JsonValue)
            ? $"{relation.FuncNode.Name}:{target}:{string.Join(":", args.Select(a => a is JsonValue jv ? jv.ToJsonString() : a?.ToString() ?? "null"))}"
            : null;

        StructFieldConfig[]? fields = null;
        if (!string.IsNullOrEmpty(uniqueKey) && _attrFields.TryGetValue(uniqueKey, out fields))
            return fields;
        
        // Execute the function to get the struct field configs
        try
        {
            JsonNode? result = await relation.FuncNode.CallAsync<JsonNode>(context, args, null, target);
            // try convert
            if (result is JsonArray arr)
            {
                return arr.FromJson<StructFieldConfig[]>() ?? [];
            }
            // try type name
            else if (result is JsonValue)
            {
                string typeName = result.ToJsonString().Trim('"');
                AnySchemaType? type = await context.GetSchemaTypeAsync(typeName);
                if (type is ArrayType arrType)
                    type = arrType.ElementSchemaType;
                if (type is StructType structType)
                {
                    fields = structType.Fields.Select(f => new StructFieldConfig
                    {
                        Name = f.Name,
                        Type = f.Type
                    }).ToArray();
                }
            }

            fields ??= [];
            
            if (!string.IsNullOrEmpty(uniqueKey))
                _attrFields[uniqueKey] = fields;
            return fields;
        }
        catch (Exception e)
        {
            Logger.LogError(e, $"Could not find unique field for {relation.FuncNode.Name}");
        }
        
        return [];
    }
    
    /// <summary>
    /// Gets the struct field config for dynamic type from the relation, the relation is defined in the dynamic table field
    /// </summary>
    async Task<StructFieldConfig[]> GetStructFieldConfigs(string target, StructTypeNode node, StructFieldRelation relation)
    {
        SchemaContext context = serviceProvider.GetService<SchemaContext>() ?? throw new Exception("The Schema context missing");
        if (relation.FuncNode == null) throw new Exception("The function node missing");
        
        // If the arguments is another field, we can query it directly, since it's designed to be used in frontend,
        // means it's value is small and easy to query, otherwise the function can be executed to gets the value directly
        object?[] args = new object[relation.Args.Length];
        for (int i = 0; i < relation.Args.Length; i++)
        {
            var arg = relation.Args[i];
            if (!string.IsNullOrEmpty(arg.Name))
            {
                args[i] = node.GetValueByPaths(arg.Name);
            }
            else if (arg.Value != null)
            {
                args[i] = arg.Value;
            }
        }
        
        // build the unique key for cache
        string? uniqueKey = args.All(a => a is ScalarTypeNode or EnumTypeNode or JsonValue)
            ? $"{relation.FuncNode.Name}:{target}:{string.Join(":", args.Select(a => a is JsonValue jv ? jv.ToJsonString() : a?.ToString() ?? "null"))}"
            : null;

        StructFieldConfig[]? fields = null;
        if (!string.IsNullOrEmpty(uniqueKey) && _attrFieldsFromStruct.TryGetValue(uniqueKey, out fields))
            return fields;
        
        // Execute the function to get the struct field configs
        try
        {
            JsonNode? result = await relation.FuncNode.CallAsync<JsonNode>(context, args, null, target);
            switch (result)
            {
                // try convert
                case JsonArray arr:
                    return arr.FromJson<StructFieldConfig[]>() ?? [];
                // try type name
                case JsonValue:
                {
                    string typeName = result.ToJsonString().Trim('"');
                    AnySchemaType? type = await context.GetSchemaTypeAsync(typeName);
                    if (type is ArrayType arrType)
                        type = arrType.ElementSchemaType;
                    if (type is StructType structType)
                    {
                        fields = structType.Fields.Select(f => new StructFieldConfig
                        {
                            Name = f.Name,
                            Type = f.Type
                        }).ToArray();
                    }

                    break;
                }
            }

            fields ??= [];
            
            if (!string.IsNullOrEmpty(uniqueKey))
                _attrFields[uniqueKey] = fields;
            return fields;
        }
        catch (Exception e)
        {
            Logger.LogError(e, $"Could not find unique field for {relation.FuncNode.Name}");
        }
        
        return [];
    }
    
    /// <summary>
    /// Save the attribute-based field value to the attribute table, the attr field is in format "structField_attrField"
    /// </summary>
    async Task SaveAttributeBasedFieldAsync(SchemaContext context, string attrTable, string target, StructFieldConfig[] fields, JsonObject? value, string prev, List<(string k, AnySchemaNode v)> primaries)
    {
        string insertTemplate = $"INSERT INTO {sqlProvider.QuoteTable(attrTable)} ({_refTarget}, {string.Join(',', primaries.Select(p => sqlProvider.QuoteField(p.k)))}, {_refAttrField}, {_refAttrIntField}, {_refAttrStrField}, {_refAttrDatField}, {_refAttrDblField}, {_refAttrTxtField}, {_refAttrJsonField}) VALUES ({sqlProvider.Literal(target)}, {string.Join(',', primaries.Select(p => sqlProvider.Literal(p.v)))}, {{0}}, {{1}}, {{2}}, {{3}}, {{4}}, {{5}}, {{6}}) ON DUPLICATE KEY UPDATE {_refAttrIntField} = {{1}}, {_refAttrStrField} = {{2}}, {_refAttrDatField} = {{3}}, {_refAttrDblField} = {{4}}, {_refAttrTxtField} = {{5}}, {_refAttrJsonField} = {{6}};";

        foreach (StructFieldConfig field in fields.Where(f => f.DisplayOnly != true))
        {
            AnySchemaType? type = await context.GetSchemaTypeAsync(field.Type);
            if (type == null)
            {
                Logger.LogWarning($"The attribute field {field.Name} type not found, will be ignored");
                continue;
            }
            
            string attrField = $"{prev}_{field.Name}";
            JsonNode? r = null;
            if (value != null)
            {
                foreach (var (key, jsonNode) in value)
                {
                    if (!key.Equals(field.Name, StringComparison.InvariantCultureIgnoreCase)) continue;
                    r = jsonNode;
                    break;
                }
            }

            // For struct type, we need to save the nested fields separately, since the attribute table is designed to be flat, means the field name is in format "structField_attrField"
            if (type is StructType structType)
            {
                await SaveAttributeBasedFieldAsync(context, attrTable, target, structType.Fields.ToArray(), r as JsonObject, attrField, primaries);
                continue;
            }

            // For one field value
            AnySchemaNode? node = r != null && !r.IsEmpty() ? type.CreateNode(r) : null;
            if (node is { IsEmpty: false })
            {
                AnySchemaNode? intNode = null;
                AnySchemaNode? strNode = null;
                AnySchemaNode? datNode = null;
                AnySchemaNode? dblNode = null;
                AnySchemaNode? txtNode = null;
                AnySchemaNode? jsonNode = null;
                
                if (node is ScalarTypeNode scalar)
                {
                    ScalarType scalarType = scalar.SchemaType as ScalarType ?? throw new Exception($"The scalar type of field {field.Name} is invalid");
                    if (scalarType.IsBool || scalarType.IsInt)
                    {
                        intNode = node;
                    }
                    else if (scalarType.IsNumber)
                    {
                        dblNode = node;
                    }
                    else if (scalarType.IsString)
                    {
                        if (scalarType.IsIndexable)
                        {
                            strNode = node;
                        }
                        else
                        {
                            txtNode = node;
                        }
                    }
                    else if (scalarType.IsDate)
                    {
                        datNode = node;
                    }
                }
                else if (node is EnumTypeNode enumNode)
                {
                    EnumType enumType = (enumNode.SchemaType as EnumType)!;
                    switch (enumType.ValueType)
                    {
                        case EnumValueType.Int:
                        case EnumValueType.Flags:
                            intNode = enumNode;
                            break;
                        case EnumValueType.String:
                        default:
                            strNode = enumNode;
                            break;
                    }
                }
                else
                {
                    jsonNode = node;
                }
                
                DbCommand command = GetDbCommand();
                command.CommandText = string.Format(insertTemplate, sqlProvider.Literal(attrField), sqlProvider.Literal(intNode), sqlProvider.Literal(strNode), sqlProvider.Literal(datNode), sqlProvider.Literal(dblNode), sqlProvider.Literal(txtNode), sqlProvider.Literal(jsonNode));
                Logger.LogInformation(command.CommandText);
                await command.ExecuteNonQueryAsync();
            }
            else
            {
                await DeleteAttributeBasedFieldAsync(context, attrTable, target, field, prev, primaries);
            }
        }
    }
    
    async Task DeleteAttributeBasedFieldAsync(SchemaContext context, string attrTable, string target, StructFieldConfig field, string prev, List<(string k, AnySchemaNode v)> primaries)
    {
        string attrField = $"{prev}_{field.Name}";
        AnySchemaType? type = await context.GetSchemaTypeAsync(attrField);
        if (type is StructType @struct)
        {
            foreach (StructFieldConfig f in @struct.Fields.Where(f => f.DisplayOnly != true))
            {
                await DeleteAttributeBasedFieldAsync(context, attrTable, target, f, attrField, primaries);
            }
        }
        else
        {
            DbCommand command = GetDbCommand();
            command.CommandText =$"DELETE FROM {sqlProvider.QuoteTable(attrTable)} WHERE {_refTarget} = {sqlProvider.Literal(target)} AND {_refAttrField} = {sqlProvider.Literal(attrField)} AND {string.Join(" AND ", primaries.Select(p => $"{sqlProvider.QuoteField(p.k)} = {sqlProvider.Literal(p.v)}"))};";
            Logger.LogInformation(command.CommandText);
            await command.ExecuteNonQueryAsync();
        }
    }

    async Task DeleteAttributeBasedFieldAsync(DynamicTableSchema schema, string target, ArrayTypeNode arr)
    {
        if (schema.AppFieldType.Topology != FieldStorageTopology.AttributeBased) return;
        
        SchemaContext context = serviceProvider.GetService<SchemaContext>()!;
        
        foreach (DynamicTableField dynamic in schema.Fields.Where(f => f.HasTypeRelation))
        {
            foreach (StructTypeNode pack in arr.Cast<StructTypeNode>())
            {
                var fields = dynamic.RelationType != null 
                    ? await GetStructFieldConfigs(schema.AppFieldType, target, pack, dynamic.RelationType)
                    : await GetStructFieldConfigs(target, pack, dynamic.StructRelation!);
                if (fields.Length == 0) continue;
                List<(string, AnySchemaNode v)> primaries = [];
                foreach ((string fld, AnySchemaNode? v) in schema.GetFieldValues(pack, true))
                    primaries.Add((fld, v!));

                foreach (StructFieldConfig field in fields)
                {
                    await DeleteAttributeBasedFieldAsync(context, schema.AppFieldType.AttributeTableName, target, field, dynamic.Name.ToLower(), primaries);
                }
            }
        }
    }
    
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
            : "VARCHAR(255)", // default length
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

    private readonly Dictionary<string, AnySchemaNode?> _relationDataCache = [];
    private readonly Dictionary<string, StructFieldConfig[]> _attrFields = [];
    private readonly Dictionary<string, StructFieldConfig[]> _attrFieldsFromStruct = [];

    #endregion
}
