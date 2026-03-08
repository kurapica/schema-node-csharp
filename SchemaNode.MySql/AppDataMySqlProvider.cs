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

    private readonly string _refIndex = sqlProvider.QuoteIndex(DYNAMIC_UNIQUE_INDEX);
    private readonly string _refSeqNo = sqlProvider.QuoteField(DYNAMIC_TABLE_SEQNO_FIELD);
    private const string TrueCond = "1=1";
    private const string MainTable = "_main";

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
        bool exist = false;
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

            // Check the new columns since we won't touch key fields
            List<string> sb = [];
            foreach (DynamicTableField dyFld in schema.ValueFields)
            {
                string dataType = DataType(dyFld);
                if (!nameTypes.TryGetValue(dyFld.Name, out string? type))
                {
                    sb.Add($"ALTER TABLE {tableName} ADD {sqlProvider.QuoteField(dyFld.Name)} {dataType};");
                }
                else if (!type.Equals(dataType, StringComparison.OrdinalIgnoreCase))
                {
                    sb.Add($"ALTER TABLE {tableName} MODIFY COLUMN {sqlProvider.QuoteField(dyFld.Name)} {dataType};");
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
                List<string> chkUniqueIndex =schema.KeyFields.Select(field => field.Name).ToList();

                // Compares the unique indexes
                if (chkUniqueIndex.Count != uniqueIndex.Count || chkUniqueIndex.Where((p, i) => !p.Equals(uniqueIndex[i])).Any())
                {
                    // Remove the old unique index
                    if (uniqueIndex.Count > 0)
                    {
                        sb.Add($"DROP INDEX {_refIndex} ON {tableName};");
                    }

                    // Add the unique index
                    sb.Add($"ALTER TABLE {tableName} ADD UNIQUE INDEX {_refIndex}({string.Join(',', chkUniqueIndex.Select(sqlProvider.QuoteField))});");
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
                        sb.Add($"ALTER TABLE {tableName} ADD INDEX {sqlProvider.QuoteIndex(key)}({string.Join(',', schema.ScopeFields.Select(f => sqlProvider.QuoteField(f.Name)).Concat(index.Fields.Select(sqlProvider.QuoteField)))});");
                    }
                }
            }

            // Remove no use indexes
            foreach (string name in names.Keys.Where(p => !p.Equals(DYNAMIC_UNIQUE_INDEX)))
            {
                sb.Add($"DROP INDEX {sqlProvider.QuoteIndex(name)} ON {tableName};");
            }

            // Update the table
            if (sb.Count > 0)
            {
                for (int i = 0; i < sb.Count; i++)
                {
                    DbCommand updateCommand = GetDbCommand();
                    updateCommand.CommandText = sb[i];
                    Logger.LogInformation(updateCommand.CommandText);
                    await updateCommand.ExecuteNonQueryAsync();
                }
            }

            exist = true;
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

        if (!exist)
        {
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
                    sb.Append($"{sqlProvider.QuoteField(keyField.Name)} {DataType(keyField)} NOT NULL, ");

                // Generate the column lists
                foreach (DynamicTableField tableField in schema.ValueFields)
                    sb.Append($"{sqlProvider.QuoteField(tableField.Name)} {DataType(tableField)}, ");

                // Append primary key
                if (schema.Single)
                {
                    sb.Append($"PRIMARY KEY({string.Join(',', schema.KeyFields.Select(f => sqlProvider.QuoteField(f.Name)))})");
                }
                else
                {
                    // Use auto-incr seqNo as primary key
                    sb.Append($"PRIMARY KEY({_refSeqNo})");

                    // Use scope target and other primary key as unique index
                    sb.Append($", UNIQUE INDEX {_refIndex} ({string.Join(',', schema.KeyFields.Select(f => sqlProvider.QuoteField(f.Name)))})");
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
                    sb.Append($"ALTER TABLE {tableName} ");
                    bool firstIdx = true;
                    foreach (var index in schema.Indexes)
                    {
                        string key = $"IDX_{string.Join('_', index.Fields.Select(f => f.ToLower()))}";
                        if (!firstIdx) sb.Append(',');
                        sb.Append($"ADD INDEX {sqlProvider.QuoteIndex(key)}({string.Join(',', schema.ScopeFields.Select(f => sqlProvider.QuoteField(f.Name)).Concat(index.Fields.Select(sqlProvider.QuoteField)))})");
                        firstIdx = false;
                    }
                    sb.Append(';');

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

                // The key column
                foreach (DynamicTableField keyField in schema.KeyFields)
                    sb.Append($"{sqlProvider.QuoteField(keyField.Name)} {DataType(keyField)} NOT NULL, ");
                
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
                sb.Append($"PRIMARY KEY({string.Join(',', schema.KeyFields.Select(f => sqlProvider.QuoteField(f.Name)))}, {_refAttrField})");

                // End the building
                sb.Append(") engine=InnoDB;");
                
                DbCommand command = GetDbCommand();
                command.CommandText = sb.ToString();
                Logger.LogInformation(command.CommandText);
                await command.ExecuteNonQueryAsync();
                
                // Create the indexes
                string scopeTargetPart = string.Join(',', schema.ScopeFields.Select(f => sqlProvider.QuoteField(f.Name)).Concat([_refAttrField]));
                sb = new StringBuilder();
                sb.Append($"ALTER TABLE {tableName} ADD INDEX {sqlProvider.QuoteIndex("IDX_TAR_FLD")}({scopeTargetPart}),");
                sb.Append($"ADD INDEX {sqlProvider.QuoteIndex("IDX_TAR_FLD_INT")}({scopeTargetPart}, {_refAttrIntField}),");
                sb.Append($"ADD INDEX {sqlProvider.QuoteIndex("IDX_TAR_FLD_STR")}({scopeTargetPart}, {_refAttrStrField}),");
                sb.Append($"ADD INDEX {sqlProvider.QuoteIndex("IDX_TAR_FLD_DAT")}({scopeTargetPart}, {_refAttrDatField});");
                
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
    
    /// <inheritdoc />
    public async Task<(AnySchemaNode? result, int total)> QueryDynamicTableAsync(DynamicTableSchema schema, 
        AppSchemaDataResult type, AppSchemaDataFilter? filter = null, int skip = 0, int take = 0, bool desc = false, 
        AppSchemaDataOrder[]? orderBy = null, string? dataField = null, bool forUpdate = false)
    {
        string tableName = sqlProvider.QuoteTable(schema.AppFieldType.DynamicTableName);
        (string wherePrefix, _) = PrepareWhere(schema, "t0");
        string querySuffix = forUpdate ? " FOR UPDATE;" : ";";
        
        await EnsureOpenConnectionAsync();
        
        // single row
        if (schema.Single)
        {
            AnySchemaNode? value = null;
            
            // Gets the data from the database
            if (schema.Fields.Last().Name.Equals(DYNAMIC_TABLE_VALUE_FIELD))
            {
                // Single value
                DbCommand command = GetDbCommand();
                command.CommandText = $"SELECT {sqlProvider.QuoteField(DYNAMIC_TABLE_VALUE_FIELD)} FROM {tableName}{wherePrefix}{TrueCond}{querySuffix}";
                Logger.LogDebug(command.CommandText);
                DbDataReader reader = await command.ExecuteReaderAsync();
                try
                {
                    if (reader.HasRows)
                    {
                        await reader.ReadAsync();
                        value = schema.Fields.Last().FromReader(reader);
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
                sb.Append("SELECT ");
                sb.Append(string.Join(',', schema.NonScopeFields.Select(f => sqlProvider.QuoteField(f.Name))));
                sb.Append(" FROM ");
                sb.Append(tableName);
                sb.Append(wherePrefix);
                sb.Append(TrueCond);
                sb.Append(querySuffix);

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
            if (type == AppSchemaDataResult.Last) desc = !desc;
            Dictionary<string, string> prefixes = new (StringComparer.OrdinalIgnoreCase)
            {
                [MainTable] = "t0"
            };

            // Join
            Dictionary<string, string>? fieldJoins = null;
            Dictionary<string, string>? fieldMaps = null;
            if (!forUpdate && schema.Joins is { Length: > 0 })
            {
                Dictionary<string, AppFieldType> joinFields = new (StringComparer.OrdinalIgnoreCase);
                fieldJoins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                
                foreach (var join in schema.Joins)
                {
                    AppFieldType joinField = schema.AppFieldType.Application.GetField(join.Field)
                                             ?? throw new InvalidOperationException($"Join field {join.Field} not found in application {schema.AppFieldType.Application.Name}");

                    joinFields[join.Field] = joinField;
                    prefixes[join.Field] = $"t{prefixes.Count}";
                }
                
                // field map
                fieldMaps = new Dictionary<string, string>();
                foreach (DynamicTableField joinField in schema.JoinFields)
                {
                    if (!joinFields.TryGetValue(joinField.JoinAppField!, out AppFieldType? joinAppField))
                        throw new InvalidOperationException($"Join field {joinField.JoinAppField} not found in application {schema.AppFieldType.Application.Name}");
                    fieldMaps[joinField.Name] = $"{prefixes[joinField.JoinAppField!]}.{sqlProvider.QuoteField(joinField.JoinDataField!)}";
                }
                
                // join condition
                foreach (var join in schema.Joins)
                {
                    AppFieldType joinField = schema.AppFieldType.Application.GetField(join.Field)!;
                    StringBuilder joinWhere = new(JoinWhere(schema, prefixes[MainTable],prefixes[join.Field]));
                    foreach (var (key, appSchemaDataFilter) in join.Matches)
                    {
                        switch (appSchemaDataFilter)
                        {
                            case AppSchemaDataFilterField filterField:
                                // @TODO for simple now, may check in schema validation later, a waste for nothing
                                if (fieldMaps.TryGetValue(filterField.Field, out string? map))
                                    joinWhere.Append($"{prefixes[join.Field]}.{sqlProvider.QuoteField(key)} = {map} AND ");
                                else
                                    joinWhere.Append($"{prefixes[join.Field]}.{sqlProvider.QuoteField(key)} = {prefixes[MainTable]}.{sqlProvider.QuoteField(filterField.Field)} AND ");
                                break;
                            case AppSchemaDataFilterValue valueFilter:
                                joinWhere.Append($"{prefixes[join.Field]}.{sqlProvider.QuoteField(key)} = {sqlProvider.Literal(valueFilter.Value)} AND ");
                                break;
                            default:
                                throw new InvalidOperationException($"Unsupported filter type {appSchemaDataFilter.GetType().FullName} in join condition");
                        }
                    }
                    joinWhere.Append(TrueCond);
                    fieldJoins[join.Field] = $" LEFT JOIN {sqlProvider.QuoteTable(joinField.DynamicTableName)} {prefixes[join.Field]} ON {joinWhere} ";
                }
            }
            
            // Build SQL
            StringBuilder sb = new();
            sb.Append(" From ");
            sb.Append(tableName);
            sb.Append(' ');
            sb.Append(prefixes[MainTable]);

            // exp node -> sql
            string sql = filter?.ToSql(sqlProvider, schema, prefixes[MainTable], fieldMaps) ?? "";
            bool joinQuery = fieldMaps != null && fieldMaps.Values.Any(k => sql.Contains(k, StringComparison.OrdinalIgnoreCase));
            
            // join first if the filter contains join field to avoid wrong result due to filter before join
            if (joinQuery)
            {
                foreach (string join in fieldJoins!.Values)
                    sb.Append(join);
            }
            
            sb.Append(wherePrefix);
            if (!string.IsNullOrEmpty(sql))
            {
                sb.Append(sql);
                sb.Append(" AND ");
            }
            sb.Append(TrueCond);

            // Query Total
            int total = 0;
            if (type is AppSchemaDataResult.List or AppSchemaDataResult.Exist or AppSchemaDataResult.Count && !forUpdate)
            {
                DbCommand totalCommand = GetDbCommand();
                // only used to check existence
                totalCommand.CommandText = type == AppSchemaDataResult.Exist 
                    ? $"SELECT EXISTS (SELECT 1 {sb} LIMIT 1) AS exists_flag;" 
                    : $"SELECT COUNT(*) {sb};";

                Logger.LogDebug(totalCommand.CommandText);
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

                    if (total == 0)
                        return (null, 0);
                }
                finally
                {
                    await totalReader.CloseAsync();
                }
            }

            // Page info
            sb.Append(" ORDER BY ");
            bool first = false;
            foreach (var (field, d) in schema.GetOrderBys(desc, orderBy))
            {
                if (first) sb.Append(", ");
                first = true;
                sb.Append(prefixes[MainTable]);
                sb.Append('.');
                sb.Append(sqlProvider.QuoteField(field));
                if (d) sb.Append(" DESC ");
            }

            if (type is AppSchemaDataResult.First or AppSchemaDataResult.Last)
                sb.Append(" LIMIT 1");
            else if (take is > 0)
            {
                sb.Append($" LIMIT {take}");
                if (skip is > 0)
                    sb.Append($" OFFSET {skip}");
            }

            // Query Data
            StringBuilder select = new();
            select.Append("SELECT ");
            first = false;
            foreach (var field in forUpdate ? schema.NonScopeFields : schema.QueryFields)
            {
                if (first) select.Append(", ");
                first = true;

                if (field.IsJoinField)
                {
                    select.Append(prefixes[field.JoinAppField!]);
                    select.Append('.');
                    select.Append(sqlProvider.QuoteField(field.JoinDataField!));
                    select.Append(" AS ");
                    select.Append(sqlProvider.QuoteField(field.Name));
                }
                else
                {
                    select.Append(prefixes[MainTable]);
                    select.Append('.');
                    select.Append(sqlProvider.QuoteField(field.Name));
                }
            }
            
            // Already joined, no inner query
            if (joinQuery)
            {
                select.Append(sb);
            }
            else
            {
                // Build the rest
                select.Append(" FROM ");
                select.Append(tableName);
                select.Append(' ');
                select.Append(prefixes[MainTable]);
                select.Append(" JOIN (SELECT ");
                select.Append(_refSeqNo);
                select.Append(' ');
                select.Append(sb);
                select.Append(") t ON ");
                select.Append(prefixes[MainTable]);
                select.Append('.');
                select.Append(_refSeqNo);
                select.Append(" = t.");
                select.Append(_refSeqNo);
                
                // join again if not joined in filter
                if (fieldJoins is { Count: > 0 })
                {
                    foreach (string join in fieldJoins!.Values)
                        select.Append(join);
                }
                
                select.Append(" ORDER BY ");
                first = false;
                foreach (var (field, d) in schema.GetOrderBys(desc, orderBy))
                {
                    if (first) select.Append(", ");
                    first = true;
                    select.Append(prefixes[MainTable]);
                    select.Append('.');
                    select.Append(sqlProvider.QuoteField(field));
                    if (d) select.Append(" DESC ");
                }
            }

            select.Append(querySuffix);

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
                            ? schema.GetFieldPack(reader, dataField ?? "", !forUpdate)
                            : schema.GetFieldPack(reader, 0, !forUpdate);
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
            await FillAttributeDataAsync(schema, value, forUpdate);

            if (type is AppSchemaDataResult.First or AppSchemaDataResult.Last)
                return (value?.ElementAtOrDefault(0), value is { Count: > 0 } ? 1 : 0);
            return (value, total > 0 ? total : (value?.Count ?? 0));
        }
    }

    /// <summary>
    /// Fill the attribute-based fields for the value list
    /// </summary>
    /// <param name="schema"></param>
    /// <param name="value"></param>
    /// <param name="forUpdate"></param>
    async Task FillAttributeDataAsync(DynamicTableSchema schema, ArrayTypeNode? value, bool forUpdate = false)
    {
        (string wherePrefix, _) = PrepareWhere(schema);
        string querySuffix = forUpdate ? " FOR UPDATE;" : ";";
        
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
            select.Append(wherePrefix);

            if (value.Count > MAX_COMBINE_CASE_COUNT)
            {
                foreach (DynamicTableField tableField in schema.Fields.Where(p => p.Primary))
                    select.Append($"{sqlProvider.QuoteField(tableField.Name)} IN ({string.Join(',', value.Cast<StructTypeNode>().Select(v => sqlProvider.Literal(v[tableField.Name])))}) AND ");
                select.Append(TrueCond);
            }
            else
            {
                select.Append("(");
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
            select.Append(querySuffix);
            
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
            DynamicTableSchema schema, AnySchemaNode? value = null, 
            bool canAdd = true, bool onlyAdd = false, string[]? overrides = null)
    {
        await EnsureOpenConnectionAsync();
        
        // Prepare
        string tableName = sqlProvider.QuoteTable(schema.AppFieldType.DynamicTableName);
        (string wherePrefix, Dictionary<string, string> scopeItems) = PrepareWhere(schema);
        
        string insertTemplate = $"INSERT INTO {tableName} ({string.Join(',', schema.AllFields.Select(f => sqlProvider.QuoteField(f.Name)))}) VALUES ({string.Join(',', schema.ScopeFields.Select(f => scopeItems[f.Name]))}{(schema.ScopeFields.Any() ? ",": "")} {{0}});";
       
        // single row
        if (schema.Single)
        {
            if (value is ArrayTypeNode arr) value = arr.FirstOrDefault();
            
            // Gets the origin value
            (AnySchemaNode? origin, _) = await QueryDynamicTableAsync(schema, AppSchemaDataResult.First);

            // Delete if null
            if (value == null || value.IsEmpty)
            {
                if (origin != null)
                {
                    DbCommand command = GetDbCommand();
                    command.CommandText = $"DELETE FROM {tableName}{wherePrefix}{TrueCond};";
                    Logger.LogInformation(command.CommandText);
                    await command.ExecuteNonQueryAsync();
                    return (true, null, origin);
                }
                return (false, null, null);
            }
            
            if (origin != null && value.Equals(origin))
                return (false, null, null);

            // Check the value type
            if (schema.Fields.Last() is { Name: DYNAMIC_TABLE_VALUE_FIELD })
            {
                // Convert the value
                bool isInsert = false;

                // Insert the value
                if (origin == null)
                {
                    try
                    {
                        DbCommand command = GetDbCommand();
                        command.CommandText = string.Format(insertTemplate, sqlProvider.Literal(value));
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
                        (origin, _) = await QueryDynamicTableAsync(schema, AppSchemaDataResult.First);

                    DbCommand command = GetDbCommand();
                    command.CommandText = $"UPDATE {tableName} SET {sqlProvider.QuoteField(DYNAMIC_TABLE_VALUE_FIELD)} = {sqlProvider.Literal(value)}{wherePrefix}{TrueCond};";
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
                    try
                    {
                        // Execute
                        DbCommand command = GetDbCommand();
                        command.CommandText = string.Format(insertTemplate, string.Join(',', schema.GetFieldValues(pack).Select(p => sqlProvider.Literal(p.value))));
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
                        (origin, _) = await QueryDynamicTableAsync(schema, AppSchemaDataResult.First);

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
                    sb.Append($"{wherePrefix}{TrueCond};");

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
            AnySchemaNode? origin = await this.QueryOriginNodesAsync(schema, packs, forUpdate: true);
            ArrayTypeNode? oArr = origin as ArrayTypeNode;
            if (!canAdd && (oArr == null || oArr.Count < packs.Length))
                throw new UnauthorizedAccessException();

            // record exist rows
            Dictionary<string, StructTypeNode> existKeys = [];
            List<string> keys = [];
            if (oArr is { Count: > 0 })
            {
                foreach (StructTypeNode obj in oArr.Cast<StructTypeNode>())
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

            // Foreach
            List<StructTypeNode> updatedPacks = [];
            List<StructTypeNode> originPacks = [];
            foreach (StructTypeNode pack in packs)
            {
                // Build where condition
                bool fullFill = true;
                keys.Clear();
                sb.Clear();
                sb.Append(wherePrefix);
                foreach ((string fld, AnySchemaNode? v) in schema.GetFieldValues(pack, true))
                {
                    // Check value
                    if (v == null || v.IsEmpty)
                    {
                        fullFill = false;
                        break;
                    }
                    keys.Add(v.ToString());
                    sb.Append($"{sqlProvider.QuoteField(fld)} = {sqlProvider.Literal(v)} AND ");
                }
                if (!fullFill) continue;
                sb.Append(TrueCond);

                // Query the origin
                string where = sb.ToString();

                // Insert
                bool isInsert = false;
                if (!existKeys.TryGetValue(string.Join('|', keys), out StructTypeNode? originPack))
                {
                    try
                    {
                        // Execute
                        DbCommand command = GetDbCommand();
                        command.CommandText = string.Format(insertTemplate, string.Join(',', schema.GetFieldValues(pack).Select(p => sqlProvider.Literal(p.value))));
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
                        origin = await this.QueryOriginNodesAsync(schema, [pack], forUpdate: true);
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
                    if (preCond)
                    {
                        // Footer
                        sb.Append(" ");
                        sb.Append(where);

                        // Execute
                        DbCommand command = GetDbCommand();
                        command.CommandText = sb.ToString();
                        Logger.LogInformation(command.CommandText);
                        await command.ExecuteNonQueryAsync();
                    }

                    updatedPacks.Add(pack);
                    if (originPack != null)
                        originPacks.Add(originPack);
                }

                // Save attribute-based fields if needed
                if (schema.AppFieldType.Topology == FieldStorageTopology.AttributeBased &&
                    (isInsert || (!onlyAdd || overrides is { Length: > 0 })))
                {
                    SchemaContext context = serviceProvider.GetService<SchemaContext>()!;
                    foreach (DynamicTableField dynamic in schema.Fields.Where(f => f.HasTypeRelation))
                    {
                        StructFieldSchema[] fields = dynamic.RelationType != null
                            ? await GetStructFieldConfigs(schema.AppFieldType, pack, dynamic.RelationType)
                            : await GetStructFieldConfigs(pack, dynamic.StructRelation!);
                        if (fields.Length == 0) continue;

                        List<(string, AnySchemaNode v)> primaries = [];
                        foreach ((string fld, AnySchemaNode? v) in schema.GetFieldValues(pack, true))
                            primaries.Add((fld, v!));

                        await SaveAttributeBasedFieldAsync(context, schema.AppFieldType.AttributeTableName, scopeItems, fields,
                            (pack.GetField(dynamic.Name) as JsonTypeNode)?.Value as JsonObject, dynamic.Name.ToLower(), primaries);
                    }
                }
            }
            return (true, new ArrayTypeNode(schema.SchemaType, updatedPacks),  (onlyAdd && (overrides == null || overrides.Length == 0)) ? null : new ArrayTypeNode(schema.SchemaType, originPacks) );
        }
    }

    /// <inheritdoc />
    public async Task<(bool result, AnySchemaNode? origin)> DeleteDynamicTableDataAsync(DynamicTableSchema schema, AppSchemaDataFilter? filter)
    {
        await EnsureOpenConnectionAsync();
        string tableName = sqlProvider.QuoteTable(schema.AppFieldType.DynamicTableName);        
        (string wherePrefix, _) = PrepareWhere(schema);

        // single row
        if (schema.Single)
        {
            (AnySchemaNode? origin, _) = await QueryDynamicTableAsync(schema,AppSchemaDataResult.First, forUpdate: true);
            if (origin is null) return (false, null);
            
            DbCommand command = GetDbCommand();
            command.CommandText = $"DELETE FROM {tableName}{wherePrefix}{TrueCond};";
            Logger.LogInformation(command.CommandText);
            await command.ExecuteNonQueryAsync();
            
            return (true, origin);
        }
        
        // multi rows
        else
        {
            string sql = filter?.ToSql(sqlProvider, schema) ?? "";
            if (string.IsNullOrEmpty(sql)) return (false, null); // prevent full table delete

            (AnySchemaNode? origin, _) = await QueryDynamicTableAsync(schema, AppSchemaDataResult.List, filter, forUpdate: true);
            if (origin is not ArrayTypeNode arr || arr.Count == 0) return (false, null);
                        
            DbCommand command = GetDbCommand();
            command.CommandText = $"DELETE FROM {tableName}{wherePrefix}{sql};";
            Logger.LogInformation(command.CommandText);
            await command.ExecuteNonQueryAsync();
            await DeleteAttributeBasedFieldAsync(schema, arr);
            return (true, origin);
        }
    }

    /// <summary>
    /// Clear all dynamic table data
    /// </summary>
    public async Task<(bool result, AnySchemaNode? origin)> ClearDynamicTableDataAsync(DynamicTableSchema schema)
    {
        await EnsureOpenConnectionAsync();
        (string wherePrefix, _) = PrepareWhere(schema);

        (AnySchemaNode? origin, _) = await QueryDynamicTableAsync(schema, schema.Single ? AppSchemaDataResult.First : AppSchemaDataResult.List, forUpdate: true);
        if (origin is null || origin is ArrayTypeNode { Count:0 }) return (false, null);

        DbCommand command = GetDbCommand();
        command.CommandText = $"DELETE FROM {sqlProvider.QuoteTable(schema.AppFieldType.DynamicTableName)}{wherePrefix}{TrueCond};";
        Logger.LogInformation(command.CommandText);
        await command.ExecuteNonQueryAsync();
        if (schema.AppFieldType.Topology == FieldStorageTopology.AttributeBased)
        {
            command = GetDbCommand();
            command.CommandText = $"DELETE FROM {sqlProvider.QuoteTable(schema.AppFieldType.AttributeTableName)}{wherePrefix}{TrueCond};";
            Logger.LogInformation(command.CommandText);
            await command.ExecuteNonQueryAsync();
        }

        return (true, origin);
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
    async Task<StructFieldSchema[]> GetStructFieldConfigs(AppFieldType appField, StructTypeNode node, AppRelationSchema relation)
    {
        SchemaContext context = serviceProvider.GetService<SchemaContext>() ?? throw new Exception("The Schema context missing");
        if (relation.FuncNode == null) throw new Exception("The function node missing");
        
        string target = context.GetContextItem<Access>()?.Target ?? string.Empty;
        
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
                    (AnySchemaNode? result, int total) = await QueryDynamicTableAsync(schema,AppSchemaDataResult.List);
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

        StructFieldSchema[]? fields = null;
        if (!string.IsNullOrEmpty(uniqueKey) && _attrFields.TryGetValue(uniqueKey, out fields))
            return fields;
        
        // Execute the function to get the struct field configs
        try
        {
            JsonNode? result = await relation.FuncNode.CallAsync<JsonNode>(context, args, null, target);
            // try convert
            if (result is JsonArray arr)
            {
                return arr.FromJson<StructFieldSchema[]>() ?? [];
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
                    fields = structType.Fields.Select(f => new StructFieldSchema
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
    async Task<StructFieldSchema[]> GetStructFieldConfigs(StructTypeNode node, StructRelationSchema relation)
    {
        SchemaContext context = serviceProvider.GetService<SchemaContext>() ?? throw new Exception("The Schema context missing");
        if (relation.FuncNode == null) throw new Exception("The function node missing");
        
        string target = context.GetContextItem<Access>()?.Target ?? string.Empty;
        
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

        StructFieldSchema[]? fields = null;
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
                    return arr.FromJson<StructFieldSchema[]>() ?? [];
                // try type name
                case JsonValue:
                {
                    string typeName = result.ToJsonString().Trim('"');
                    AnySchemaType? type = await context.GetSchemaTypeAsync(typeName);
                    if (type is ArrayType arrType)
                        type = arrType.ElementSchemaType;
                    if (type is StructType structType)
                    {
                        fields = structType.Fields.Select(f => new StructFieldSchema
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
                _attrFieldsFromStruct[uniqueKey] = fields;
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
    async Task SaveAttributeBasedFieldAsync(SchemaContext context, string attrTable, Dictionary<string, string> scopeItems, StructFieldSchema[] fields, JsonObject? value, string prev, List<(string k, AnySchemaNode v)> primaries)
    {
        string[] scopeKeys = scopeItems.Keys.ToArray();
        string tableRef = sqlProvider.QuoteTable(attrTable);
        string columnList = string.Join(',', scopeKeys.Select(sqlProvider.QuoteField).Concat(primaries.Select(p => sqlProvider.QuoteField(p.k))).Concat([_refAttrField, _refAttrIntField, _refAttrStrField, _refAttrDatField, _refAttrDblField, _refAttrTxtField, _refAttrJsonField]));
        string fixedValues = string.Join(',', scopeKeys.Select(k => scopeItems[k]).Concat(primaries.Select(p => sqlProvider.Literal(p.v))));
        string sep = scopeKeys.Length > 0 || primaries.Count > 0 ? "," : "";

        foreach (StructFieldSchema field in fields.Where(f => f.DisplayOnly != true))
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
                await SaveAttributeBasedFieldAsync(context, attrTable, scopeItems, structType.Fields.ToArray(), r as JsonObject, attrField, primaries);
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
                
                string litAttr = sqlProvider.Literal(attrField);
                string litInt = sqlProvider.Literal(intNode);
                string litStr = sqlProvider.Literal(strNode);
                string litDat = sqlProvider.Literal(datNode);
                string litDbl = sqlProvider.Literal(dblNode);
                string litTxt = sqlProvider.Literal(txtNode);
                string litJson = sqlProvider.Literal(jsonNode);
                DbCommand command = GetDbCommand();
                command.CommandText = $"INSERT INTO {tableRef} ({columnList}) VALUES ({fixedValues}{sep} {litAttr}, {litInt}, {litStr}, {litDat}, {litDbl}, {litTxt}, {litJson}) ON DUPLICATE KEY UPDATE {_refAttrIntField} = {litInt}, {_refAttrStrField} = {litStr}, {_refAttrDatField} = {litDat}, {_refAttrDblField} = {litDbl}, {_refAttrTxtField} = {litTxt}, {_refAttrJsonField} = {litJson};";
                Logger.LogInformation(command.CommandText);
                await command.ExecuteNonQueryAsync();
            }
            else
            {
                await DeleteAttributeBasedFieldAsync(context, attrTable, scopeItems, field, prev, primaries);
            }
        }
    }
    
    async Task DeleteAttributeBasedFieldAsync(SchemaContext context, string attrTable, Dictionary<string, string> scopeItems, StructFieldSchema field, string prev, List<(string k, AnySchemaNode v)> primaries)
    {
        string attrField = $"{prev}_{field.Name}";
        AnySchemaType? type = await context.GetSchemaTypeAsync(attrField);
        if (type is StructType @struct)
        {
            foreach (StructFieldSchema f in @struct.Fields.Where(f => f.DisplayOnly != true))
            {
                await DeleteAttributeBasedFieldAsync(context, attrTable, scopeItems, f, attrField, primaries);
            }
        }
        else
        {
            DbCommand command = GetDbCommand();
            command.CommandText =$"DELETE FROM {sqlProvider.QuoteTable(attrTable)} WHERE {string.Join(" AND ", scopeItems.Select(p => $"{sqlProvider.QuoteField(p.Key)} = {p.Value}").Concat([$"{_refAttrField} = {sqlProvider.Literal(attrField)}"]).Concat(primaries.Select(p => $"{sqlProvider.QuoteField(p.k)} = {sqlProvider.Literal(p.v)}")))};";
            Logger.LogInformation(command.CommandText);
            await command.ExecuteNonQueryAsync();
        }
    }

    async Task DeleteAttributeBasedFieldAsync(DynamicTableSchema schema, ArrayTypeNode arr)
    {
        if (schema.AppFieldType.Topology != FieldStorageTopology.AttributeBased) return;
        
        SchemaContext context = serviceProvider.GetService<SchemaContext>()!;
        var (_, scopeItems) = PrepareWhere(schema);
        
        foreach (DynamicTableField dynamic in schema.Fields.Where(f => f.HasTypeRelation))
        {
            foreach (StructTypeNode pack in arr.Cast<StructTypeNode>())
            {
                var fields = dynamic.RelationType != null 
                    ? await GetStructFieldConfigs(schema.AppFieldType, pack, dynamic.RelationType)
                    : await GetStructFieldConfigs(pack, dynamic.StructRelation!);
                if (fields.Length == 0) continue;
                List<(string, AnySchemaNode v)> primaries = [];
                foreach ((string fld, AnySchemaNode? v) in schema.GetFieldValues(pack, true))
                    primaries.Add((fld, v!));

                foreach (StructFieldSchema field in fields)
                {
                    await DeleteAttributeBasedFieldAsync(context, schema.AppFieldType.AttributeTableName, scopeItems, field, dynamic.Name.ToLower(), primaries);
                }
            }
        }
    }

    (string where, Dictionary<string, string> scopeItems) PrepareWhere(DynamicTableSchema schema, string prefix = "")
    {
        StringBuilder sb = new(" WHERE ");
        Dictionary<string, string> items = [];
        if (!string.IsNullOrEmpty(prefix) && !prefix.EndsWith(".")) prefix += ".";
        
        // Prepare the scope items
        foreach ((string item, AnySchemaNode? value)  in schema.GetScopeItems(serviceProvider))
        {
            if (value == null || value.IsEmpty)
                throw new InvalidOperationException($"The scope field {item} is required for querying dynamic table data.");
            items[item] = sqlProvider.Literal(value);
            sb.Append($"{prefix}{sqlProvider.QuoteField(item)} = {items[item]} AND ");
        }
        
        var result = (sb.ToString(), items);
        return result;
    }
    
    string JoinWhere(DynamicTableSchema schema, string main, string sub)
    {
        StringBuilder sb = new(" ");
        if (!string.IsNullOrEmpty(main) && !main.EndsWith(".")) main += ".";
        if (!string.IsNullOrEmpty(sub) && !sub.EndsWith(".")) sub += ".";
        
        // Prepare the scope items
        foreach ((string item, _)  in schema.GetScopeItems(serviceProvider))
            sb.Append($"{sub}{sqlProvider.QuoteField(item)} = {main}{sqlProvider.QuoteField(item)} AND ");
        
        return sb.ToString();
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
    
    private readonly Dictionary<string, AnySchemaNode?> _relationDataCache = [];
    private readonly Dictionary<string, StructFieldSchema[]> _attrFields = [];
    private readonly Dictionary<string, StructFieldSchema[]> _attrFieldsFromStruct = [];

    #endregion
}
