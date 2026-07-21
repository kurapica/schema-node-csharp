using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using SchemaNode.Context;
using SchemaNode.Data;
using SchemaNode.Data.Sql;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Relation;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SchemaNode.Property.Common;
using static SchemaNode.Utility.AppConstant;
using static SchemaNode.Utility.Constant;
using ArrayType = SchemaNode.Runtime.ArrayType;
using BoolType = SchemaNode.Runtime.BoolType;
using DateType = SchemaNode.Runtime.DateType;
using DecimalType = SchemaNode.Runtime.DecimalType;
using EnumType = SchemaNode.Runtime.EnumType;
using IntType = SchemaNode.Runtime.IntType;
using RelationType = SchemaNode.Runtime.RelationType;
using RuntimeValueType = SchemaNode.Runtime.ValueType;
using StringType = SchemaNode.Runtime.StringType;
using StructType = SchemaNode.Runtime.StructType;

namespace SchemaNode.MySql;

/// <summary>
/// The implementation of IAppSchemaDataProvider for MySQL
/// </summary>
public class AppDataMySqlProvider(MySqlConnection dbConn, IServiceProvider serviceProvider, ISqlProvider sqlProvider, ISchemaContext context) : IAppDataSqlProvider<MySqlProvider>, IAsyncDisposable
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

    private readonly SchemaContext _context = context as SchemaContext ?? throw new ArgumentException("Invalid schema context");

    #endregion

    #region IAppSchemaDataProvider implementation

    /// <inheritdoc />
    public async Task<bool> EnsureDynamicTableAsync(DynamicTableSchema schema)  
    {
        string tableName = sqlProvider.QuoteTable(schema.AppField.DynamicTableName);
        await EnsureOpenConnectionAsync();

        // Check to update the data table
        bool exist = false;
        try
        {
            List<string> sb = [];
            Dictionary<string, string> nameTypes = new();
            // Gets the existed fields
            {
                await using DbCommand command = GetDbCommand();
                command.CommandText = $"DESCRIBE {tableName}";
                Logger.LogDebug(command.CommandText);
                await using DbDataReader reader = await command.ExecuteReaderAsync();
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
                foreach (DynamicTableField dyFld in schema.ValueFields)
                {
                    string dataType = DataType(dyFld);
                    if (!nameTypes.TryGetValue(dyFld.Name, out string? type))
                    {
                        sb.Add($"ALTER TABLE {tableName} ADD {sqlProvider.QuoteField(dyFld.Name)} {dataType};");
                    }
                    else if (!type.Equals(dataType, StringComparison.OrdinalIgnoreCase))
                    {
                        sb.Add(
                            $"ALTER TABLE {tableName} MODIFY COLUMN {sqlProvider.QuoteField(dyFld.Name)} {dataType};");
                    }
                }
            }

            // Check the existed indexes
            List<string> uniqueIndex = [];
            Dictionary<string, bool> names = []; // name => unique
            {
                await using DbCommand command = GetDbCommand();
                command.CommandText = $"SHOW INDEXES FROM {tableName}";
                await using DbDataReader reader = await command.ExecuteReaderAsync();

                // Check indexes
                try
                {
                    while (await reader.ReadAsync())
                    {
                        string keyName = reader.GetString("Key_name");
                        if (keyName.Equals(DYNAMIC_UNIQUE_INDEX, StringComparison.OrdinalIgnoreCase))
                        {
                            uniqueIndex.Add(reader.GetString("Column_name"));
                        }
                        else if (!keyName.Equals("PRIMARY", StringComparison.OrdinalIgnoreCase) &&
                                 !names.ContainsKey(keyName))
                        {
                            names.Add(keyName, reader.GetInt32("Non_unique") == 0);
                        }
                    }
                }
                finally
                {
                    await reader.CloseAsync();
                }
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
                    await using DbCommand updateCommand = GetDbCommand();
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
                {
                    await using DbCommand command = GetDbCommand();
                    command.CommandText = sb.ToString();
                    Logger.LogInformation(command.CommandText);
                    await command.ExecuteNonQueryAsync();
                }

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

                    await using DbCommand command = GetDbCommand();
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
        if (schema.AppField.Topology == FieldStorageTopology.AttributeBased)
        {
            // Create the attribute-value table
            try
            {
                tableName = sqlProvider.QuoteTable(schema.AppField.AttributeTableName);
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

                {
                    await using DbCommand command = GetDbCommand();
                    command.CommandText = sb.ToString();
                    Logger.LogInformation(command.CommandText);
                    await command.ExecuteNonQueryAsync();
                }
                // Create the indexes
                string scopeTargetPart = string.Join(',', schema.ScopeFields.Select(f => sqlProvider.QuoteField(f.Name)).Concat([_refAttrField]));
                sb = new StringBuilder();
                sb.Append($"ALTER TABLE {tableName} ADD INDEX {sqlProvider.QuoteIndex("IDX_TAR_FLD")}({scopeTargetPart}),");
                sb.Append($"ADD INDEX {sqlProvider.QuoteIndex("IDX_TAR_FLD_INT")}({scopeTargetPart}, {_refAttrIntField}),");
                sb.Append($"ADD INDEX {sqlProvider.QuoteIndex("IDX_TAR_FLD_STR")}({scopeTargetPart}, {_refAttrStrField}),");
                sb.Append($"ADD INDEX {sqlProvider.QuoteIndex("IDX_TAR_FLD_DAT")}({scopeTargetPart}, {_refAttrDatField});");

                {
                    await using DbCommand command = GetDbCommand();
                    command.CommandText = sb.ToString();
                    Logger.LogInformation(command.CommandText);
                    await command.ExecuteNonQueryAsync();
                }
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
    public async Task<(DataNode? result, int total)> QueryDynamicTableAsync(DynamicTableSchema schema, 
        AppSchemaDataResult type, AppSchemaDataFilter? filter = null, int skip = 0, int take = 0, bool desc = false, 
        AppSchemaDataOrder[]? orderBy = null, string? dataField = null, bool forUpdate = false)
    {
        string tableName = sqlProvider.QuoteTable(schema.AppField.DynamicTableName);
        (string wherePrefix, _) = PrepareWhere(schema, "t0");
        string querySuffix = forUpdate ? " FOR UPDATE;" : ";";
        
        await EnsureOpenConnectionAsync();
        
        // single row
        if (schema.Single)
        {
            DataNode? value = null;
            
            // Gets the data from the database
            if (schema.Fields.Last().Name.Equals(DYNAMIC_TABLE_VALUE_FIELD))
            {
                // Single value
                await using DbCommand command = GetDbCommand();
                command.CommandText = $"SELECT {sqlProvider.QuoteField(DYNAMIC_TABLE_VALUE_FIELD)} FROM {tableName} t0{wherePrefix}{TrueCond}{querySuffix}";
                Logger.LogDebug(command.CommandText);
                await using DbDataReader reader = await command.ExecuteReaderAsync();
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
                Dictionary<string, string> prefixes = new(StringComparer.OrdinalIgnoreCase)
                {
                    [MainTable] = "t0"
                };

                // Join
                Dictionary<string, string>? fieldJoins = null;
                if (!forUpdate && schema.Joins is { Length: > 0 })
                {
                    Dictionary<string, AppFieldType> joinFields = new(StringComparer.OrdinalIgnoreCase);
                    fieldJoins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var join in schema.Joins)
                    {
                        AppFieldType joinField = schema.AppField.Application.GetField(join.Field)
                                                 ?? throw new InvalidOperationException($"Join field {join.Field} not found in application {schema.AppField.Application.Name}");
                        joinFields[join.Field] = joinField;
                        prefixes[join.Field] = $"t{prefixes.Count}";
                    }

                    // field map
                    var fieldMaps = new Dictionary<string, string>();
                    foreach (DynamicTableField joinField in schema.JoinFields)
                    {
                        if (!joinFields.ContainsKey(joinField.JoinAppField!))
                            throw new InvalidOperationException($"Join field {joinField.JoinAppField} not found in application {schema.AppField.Application.Name}");
                        fieldMaps[joinField.Name] = $"{prefixes[joinField.JoinAppField!]}.{sqlProvider.QuoteField(joinField.JoinDataField!)}";
                    }

                    // join condition
                    foreach (var join in schema.Joins)
                    {
                        AppFieldType joinField = schema.AppField.Application.GetField(join.Field)!;
                        StringBuilder joinWhere = new(JoinWhere(schema, prefixes[MainTable], prefixes[join.Field]));
                        foreach (var (key, appSchemaDataFilter) in join.Matches)
                        {
                            switch (appSchemaDataFilter)
                            {
                                case AppSchemaDataFilterField filterField:
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

                // Build SELECT
                StringBuilder sb = new();
                sb.Append("SELECT ");
                bool first = false;
                foreach (var field in forUpdate ? schema.NonScopeFields : schema.QueryFields)
                {
                    if (first) sb.Append(", ");
                    first = true;

                    if (field.IsJoinField)
                    {
                        sb.Append(prefixes[field.JoinAppField!]);
                        sb.Append('.');
                        sb.Append(sqlProvider.QuoteField(field.JoinDataField!));
                        sb.Append(" AS ");
                        sb.Append(sqlProvider.QuoteField(field.Name));
                    }
                    else
                    {
                        sb.Append(prefixes[MainTable]);
                        sb.Append('.');
                        sb.Append(sqlProvider.QuoteField(field.Name));
                    }
                }

                sb.Append(" FROM ");
                sb.Append(tableName);
                sb.Append(' ');
                sb.Append(prefixes[MainTable]);

                if (fieldJoins is { Count: > 0 })
                {
                    foreach (string join in fieldJoins.Values)
                        sb.Append(join);
                }

                sb.Append(wherePrefix);
                sb.Append(TrueCond);
                sb.Append(querySuffix);

                // Get data
                await using DbCommand command = GetDbCommand();
                command.CommandText = sb.ToString();
                Logger.LogDebug(command.CommandText);
                await using DbDataReader reader = await command.ExecuteReaderAsync();
                try
                {
                    if (reader.HasRows)
                    {
                        await reader.ReadAsync();
                        value = schema.GetFieldPack(reader, queryOnly: !forUpdate);
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
                    AppFieldType joinField = schema.AppField.Application.GetField(join.Field)
                                             ?? throw new InvalidOperationException($"Join field {join.Field} not found in application {schema.AppField.Application.Name}");

                    joinFields[join.Field] = joinField;
                    prefixes[join.Field] = $"t{prefixes.Count}";
                }
                
                // field map
                fieldMaps = new Dictionary<string, string>();
                foreach (DynamicTableField joinField in schema.JoinFields)
                {
                    if (!joinFields.TryGetValue(joinField.JoinAppField!, out AppFieldType? _))
                        throw new InvalidOperationException($"Join field {joinField.JoinAppField} not found in application {schema.AppField.Application.Name}");
                    fieldMaps[joinField.Name] = $"{prefixes[joinField.JoinAppField!]}.{sqlProvider.QuoteField(joinField.JoinDataField!)}";
                }
                
                // join condition
                foreach (var join in schema.Joins)
                {
                    AppFieldType joinField = schema.AppField.Application.GetField(join.Field)!;
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
                await using DbCommand totalCommand = GetDbCommand();
                // only used to check existence
                totalCommand.CommandText = type == AppSchemaDataResult.Exist 
                    ? $"SELECT EXISTS (SELECT 1 {sb} LIMIT 1) AS exists_flag;" 
                    : $"SELECT COUNT(*) {sb};";

                Logger.LogDebug(totalCommand.CommandText);
                await using DbDataReader totalReader = await totalCommand.ExecuteReaderAsync();
                try
                {
                    if (totalReader.HasRows && await totalReader.ReadAsync())
                        total = totalReader.GetInt32(0);
                    switch (type)
                    {
                        case AppSchemaDataResult.Exist:
                            return (_context.System.Bool.From(total > 0), total);
                        case AppSchemaDataResult.Count:
                            return (_context.System.Int.From(total), total);
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
                    foreach (string join in fieldJoins.Values)
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

            ArrayNode? value = null;
            await using DbCommand command = GetDbCommand();
            command.CommandText = select.ToString();
            Logger.LogDebug(command.CommandText);
            await using DbDataReader reader = await command.ExecuteReaderAsync();
            try
            {
                if (reader.HasRows)
                {
                    while (await reader.ReadAsync())
                    {
                        DataNode? pack = type == AppSchemaDataResult.Field
                            ? schema.GetFieldPack(reader, dataField ?? "", !forUpdate)
                            : schema.GetFieldPack(reader, queryOnly: !forUpdate);
                        if (pack != null)
                        {
                            value ??= new ArrayNode(pack.Type);
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
                return (value?.ElementAtOrDefault(0) as DataNode, value is { Count: > 0 } ? 1 : 0);
            return (value, total > 0 ? total : (value?.Count ?? 0));
        }
    }

    /// <summary>
    /// Fill the attribute-based fields for the value list
    /// </summary>
    /// <param name="schema"></param>
    /// <param name="value"></param>
    /// <param name="forUpdate"></param>
    async Task FillAttributeDataAsync(DynamicTableSchema schema, ArrayNode? value, bool forUpdate = false)
    {
        (string wherePrefix, _) = PrepareWhere(schema);
        string querySuffix = forUpdate ? " FOR UPDATE;" : ";";
        
        // Load the attribute-based fields if needed
        if (value is { Count: > 0 } && 
            schema.AppField.Topology == FieldStorageTopology.AttributeBased &&
            schema.Fields.Any(p => p.HasTypeRelation))
        {
            StringBuilder select = new();
            select.Append("SELECT ");
            foreach (DynamicTableField tableField in schema.Fields.Where(p => p.Primary))
                select.Append($"{sqlProvider.QuoteField(tableField.Name)}, ");
            select.Append($"{_refAttrField}, {_refAttrIntField}, {_refAttrStrField}, {_refAttrDatField}, {_refAttrDblField}, {_refAttrTxtField}, {_refAttrJsonField} ");
            select.Append($"FROM {sqlProvider.QuoteTable(schema.AppField.AttributeTableName)} ");
            select.Append(wherePrefix);

            if (value.Count > MAX_COMBINE_CASE_COUNT)
            {
                foreach (DynamicTableField tableField in schema.Fields.Where(p => p.Primary))
                    select.Append($"{sqlProvider.QuoteField(tableField.Name)} IN ({string.Join(',', value.Cast<StructNode>().Select(v => sqlProvider.Literal(v[tableField.Name])))}) AND ");
                select.Append(TrueCond);
            }
            else
            {
                select.Append("(");
                bool hasQuery = false;
                foreach (StructNode node in value.Cast<StructNode>())
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
            
            await using var command = GetDbCommand();
            command.CommandText = select.ToString();
            Logger.LogDebug(command.CommandText);
            await using var reader = await command.ExecuteReaderAsync();
            try
            {
                if (reader.HasRows)
                {
                    while (await reader.ReadAsync())
                    {
                        int offset = 0;
                        IEnumerable<StructNode> nodes = value.OfType<StructNode>();
                        foreach (DynamicTableField tableField in schema.Fields.Where(p => p.Primary))
                        {
                            DataNode? val = tableField.FromReader(reader, offset++);
                            if (val == null || val.IsEmpty) break;
                            nodes = nodes.Where(n => val.Equals(n.GetAccessValue(tableField.Name)!));
                        }
                        StructNode[] matched = nodes.ToArray();
                        if (matched.Length != 1) continue;

                        StructNode pack = matched[0];
                        string attr = reader.GetString(offset++);
                        if (string.IsNullOrWhiteSpace(attr)) continue;

                        // For multi struct field, the attr field is in format "structField_attrField", we need to split it to get the real attr field
                        string[] paths = attr.Split('_', StringSplitOptions.RemoveEmptyEntries);
                        var attrNode = pack.GetAccessValue(paths[0]);
                        if (attrNode is StructNode structAttrNode)
                        {
                            var last = structAttrNode.GetAccessValue(string.Join(".", paths.Skip(1)));
                            if (last != null)
                            {
                                // bigint
                                if (!reader.IsDBNull(offset))
                                {
                                    last.TrySetValue(reader.GetInt64(offset));
                                }
                                // string
                                else if (!reader.IsDBNull(offset + 1))
                                {
                                    last.TrySetValue(reader.GetString(offset + 1));
                                }
                                // datetime
                                else if (!reader.IsDBNull(offset + 2))
                                {
                                    last.TrySetValue(reader.GetDateTime(offset + 2));
                                }
                                // double
                                else if (!reader.IsDBNull(offset + 3))
                                {
                                    last.TrySetValue(reader.GetDouble(offset + 3));
                                }
                                // text
                                else if (!reader.IsDBNull(offset + 4))
                                {
                                    last.TrySetValue(reader.GetString(offset + 4));
                                }
                                // json
                                else if (!reader.IsDBNull(offset + 5))
                                {
                                    object raw = reader.GetValue(offset + 5);
                                    last.TrySetValue(raw is DBNull ? null : raw switch
                                    {
                                        string s => JsonNode.Parse(s),
                                        byte[] b => JsonNode.Parse(b),
                                        _ => null
                                    });
                                }
                            }
                        }
                        else if(attrNode is AnyNode anyAttrNode)
                        {
                            JsonObject? container = anyAttrNode.GetValue<JsonObject>();
                            if (container == null)
                            {
                                container = new JsonObject();
                                anyAttrNode.TrySetValue(container);
                            }

                            for (int i = 1; i < paths.Length - 1; i++)
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
            }
            finally
            {
                await reader.CloseAsync();
            }
        }
    }
    
    /// <inheritdoc />
    public async Task<(bool result, DataNode? update, DataNode? origin)> SaveDynamicTableDataAsync(
            DynamicTableSchema schema, DataNode? value = null, 
            bool canAdd = true, bool onlyAdd = false, string[]? overrides = null)
    {
        await EnsureOpenConnectionAsync();
        
        // Prepare
        string tableName = sqlProvider.QuoteTable(schema.AppField.DynamicTableName);
        (string wherePrefix, Dictionary<string, string> scopeItems) = PrepareWhere(schema);
        
        string insertTemplate = $"INSERT INTO {tableName} ({string.Join(',', schema.AllFields.Select(f => sqlProvider.QuoteField(f.Name)))}) VALUES ({string.Join(',', schema.ScopeFields.Select(f => scopeItems[f.Name]))}{(schema.ScopeFields.Any() ? ",": "")} {{0}});";
       
        // single row
        if (schema.Single)
        {
            if (value is ArrayNode arr) value = arr.FirstOrDefault() as DataNode;
            
            // Gets the origin value
            (DataNode? origin, _) = await QueryDynamicTableAsync(schema, AppSchemaDataResult.First);

            // Delete if null
            if (value == null || value.IsEmpty)
            {
                if (origin != null)
                {
                    await using DbCommand command = GetDbCommand();
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
                        await using DbCommand command = GetDbCommand();
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

                    await using DbCommand command = GetDbCommand();
                    command.CommandText = $"UPDATE {tableName} SET {sqlProvider.QuoteField(DYNAMIC_TABLE_VALUE_FIELD)} = {sqlProvider.Literal(value)}{wherePrefix}{TrueCond};";
                    Logger.LogInformation(command.CommandText);
                    await command.ExecuteNonQueryAsync();
                }
                return (true, value, origin);
            }
            else if (value is StructNode pack)
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
                        await using DbCommand command = GetDbCommand();
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
                    foreach ((string fld, DataNode? val) in schema.GetFieldValues(pack))
                    {
                        sb.Append($"{(preCond ? "," : "")}{sqlProvider.QuoteField(fld)}={sqlProvider.Literal(val)}");
                        preCond = true;
                    }

                    // Footer
                    sb.Append($"{wherePrefix}{TrueCond};");

                    // Execute
                    await using DbCommand command = GetDbCommand();
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
            StructNode[] packs;
            switch (value)
            {
                case ArrayNode arr:
                    if (arr.Count == 0) return (false, null, null);
                    packs = arr.Cast<StructNode>().ToArray();
                    break;
                case StructNode obj:
                    packs = [obj];
                    break;
                default:
                    return (false, null, null);
            }

            // Query
            DataNode? origin = await this.QueryOriginNodesAsync(schema, packs, forUpdate: true);
            ArrayNode? oArr = origin as ArrayNode;
            if (!canAdd && (oArr == null || oArr.Count < packs.Length))
                throw new UnauthorizedAccessException();

            // record exist rows
            Dictionary<string, StructNode> existKeys = [];
            List<string> keys = [];
            if (oArr is { Count: > 0 })
            {
                foreach (StructNode obj in oArr.Cast<StructNode>())
                {
                    keys.Clear();
                    bool fullFill = true;
                    foreach ((_, DataNode? v) in schema.GetFieldValues(obj, true))
                    {
                        // Check value
                        if (v == null || v.IsEmpty)
                        {
                            fullFill = false;
                            break;
                        }
                        keys.Add(v.GetValue<string>()!);
                    }

                    if (!fullFill) return (false, null, null); // impossible
                    existKeys.Add(string.Join('|', keys), obj);
                }
            }

            // Foreach
            List<StructNode> updatedPacks = [];
            List<StructNode> originPacks = [];
            foreach (StructNode pack in packs)
            {
                // Build where condition
                bool fullFill = true;
                keys.Clear();
                sb.Clear();
                sb.Append(wherePrefix);
                foreach ((string fld, DataNode? v) in schema.GetFieldValues(pack, true))
                {
                    // Check value
                    if (v == null || v.IsEmpty)
                    {
                        fullFill = false;
                        break;
                    }
                    keys.Add(v.GetValue<string>()!);
                    sb.Append($"{sqlProvider.QuoteField(fld)} = {sqlProvider.Literal(v)} AND ");
                }
                if (!fullFill) continue;
                sb.Append(TrueCond);

                // Query the origin
                string where = sb.ToString();

                // Insert
                bool isInsert = false;
                if (!existKeys.TryGetValue(string.Join('|', keys), out StructNode? originPack))
                {
                    try
                    {
                        // Execute
                        await using DbCommand command = GetDbCommand();
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
                        if (origin is ArrayNode { Count: 1 } arr)
                            originPack = arr[0] as StructNode;
                    }

                    // Skip if no change
                    if (originPack != null && originPack.Equals(pack))
                        continue;

                    // Header
                    sb.Clear();
                    sb.Append($"UPDATE {tableName} SET ");

                    // Body
                    bool preCond = false;
                    foreach ((string fld, DataNode? v) in schema.GetFieldValues(pack, false, true))
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
                        await using DbCommand command = GetDbCommand();
                        command.CommandText = sb.ToString();
                        Logger.LogInformation(command.CommandText);
                        await command.ExecuteNonQueryAsync();
                    }

                    updatedPacks.Add(pack);
                    if (originPack != null)
                        originPacks.Add(originPack);
                }

                // Save attribute-based fields if needed
                if (schema.AppField.Topology == FieldStorageTopology.AttributeBased &&
                    (isInsert || (!onlyAdd || overrides is { Length: > 0 })))
                {
                    foreach (DynamicTableField dynamic in schema.Fields.Where(f => f.HasTypeRelation))
                    {
                        StructFieldSchema[] fields = dynamic.RelationType != null
                            ? await GetStructFieldConfigs(schema.AppField, pack, dynamic.RelationType)
                            : await GetStructFieldConfigs(pack, dynamic.StructRelation!);
                        if (fields.Length == 0) continue;

                        List<(string, DataNode v)> primaries = [];
                        foreach ((string fld, DataNode? v) in schema.GetFieldValues(pack, true))
                            primaries.Add((fld, v!));

                        await SaveAttributeBasedFieldAsync(schema.AppField.AttributeTableName, scopeItems, fields,
                            pack.GetAccessValue(dynamic.Name)?.GetValue<JsonObject>(), dynamic.Name.ToLower(), primaries);
                    }
                }
            }
            return (true, new ArrayNode(schema.ValueType, updatedPacks),  (onlyAdd && (overrides == null || overrides.Length == 0)) ? null : new ArrayNode(schema.ValueType, originPacks) );
        }
    }

    /// <inheritdoc />
    public async Task<(bool result, DataNode? origin)> DeleteDynamicTableDataAsync(DynamicTableSchema schema, AppSchemaDataFilter? filter)
    {
        await EnsureOpenConnectionAsync();
        string tableName = sqlProvider.QuoteTable(schema.AppField.DynamicTableName);        
        (string wherePrefix, _) = PrepareWhere(schema);

        // single row
        if (schema.Single)
        {
            (DataNode? origin, _) = await QueryDynamicTableAsync(schema,AppSchemaDataResult.First, forUpdate: true);
            if (origin is null) return (false, null);
            
            await using DbCommand command = GetDbCommand();
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

            (DataNode? origin, _) = await QueryDynamicTableAsync(schema, AppSchemaDataResult.List, filter, forUpdate: true);
            if (origin is not ArrayNode arr || arr.Count == 0) return (false, null);
                        
            await using DbCommand command = GetDbCommand();
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
    public async Task<(bool result, DataNode? origin)> ClearDynamicTableDataAsync(DynamicTableSchema schema)
    {
        await EnsureOpenConnectionAsync();
        (string wherePrefix, _) = PrepareWhere(schema);

        (DataNode? origin, _) = await QueryDynamicTableAsync(schema, schema.Single ? AppSchemaDataResult.First : AppSchemaDataResult.List, forUpdate: true);
        if (origin is null || origin is ArrayNode { Count:0 }) return (false, null);

        {
            await using DbCommand command = GetDbCommand();
            command.CommandText =
                $"DELETE FROM {sqlProvider.QuoteTable(schema.AppField.DynamicTableName)}{wherePrefix}{TrueCond};";
            Logger.LogInformation(command.CommandText);
            await command.ExecuteNonQueryAsync();
        }
        
        if (schema.AppField.Topology == FieldStorageTopology.AttributeBased)
        {
            await using DbCommand command = GetDbCommand();
            command.CommandText = $"DELETE FROM {sqlProvider.QuoteTable(schema.AppField.AttributeTableName)}{wherePrefix}{TrueCond};";
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
        string tableName = sqlProvider.QuoteTable(schema.AppField.DynamicTableName);
        await EnsureOpenConnectionAsync();

        {
            await using DbCommand command = GetDbCommand();
            command.CommandText = $"DROP TABLE IF EXISTS {tableName};";
            Logger.LogInformation(command.CommandText);
            await command.ExecuteNonQueryAsync();
        }
        
        if (schema.AppField.Topology == FieldStorageTopology.AttributeBased)
        {
            string attrTableName = sqlProvider.QuoteTable(schema.AppField.AttributeTableName);
            await using DbCommand attrCommand = GetDbCommand();
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

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
        GC.SuppressFinalize(this);
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
    async Task<StructFieldSchema[]> GetStructFieldConfigs(AppFieldType appField, StructNode node, RelationType relation)
    {
        if (relation.Process is not CallProcess call) throw new Exception("Only support Call relation process");
        if (call.FuncType == null) throw new Exception("The function node missing");

        string target = _context.GetContextItem<Access>()?.Target ?? string.Empty;

        // If the arguments is another field, we can query it directly, since it's designed to be used in frontend,
        // means it's value is small and easy to query, otherwise the function can be executed to gets the value directly
        object?[] args = new object[call.Args.Length];
        for (int i = 0; i < call.Args.Length; i++)
        {
            var arg = call.Args[i];
            if (!string.IsNullOrEmpty(arg.Source))
            {
                string[] path = arg.Source.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
                string fieldName = path[0];
                string? dataField = path.ElementAtOrDefault(1);
                if (fieldName.Equals(appField.Name, StringComparison.OrdinalIgnoreCase))
                {
                    args[i] = node;
                }
                else if (_relationDataCache.TryGetValue(fieldName, out DataNode? cache) && cache != null)
                {
                    args[i] = cache;
                }
                else
                {
                    var schema = appField.Application.GetField(fieldName)?.GetDynamicTableSchema(_context)
                        ?? throw new Exception($"The field {fieldName} not found in app {appField.Application.Name}");
                    (DataNode? result, int total) = await QueryDynamicTableAsync(schema,AppSchemaDataResult.List);
                    if (total > 50)
                        Logger.LogWarning($"The query result of field {fieldName} in app {appField.Application.Name} is too large, total {total}, relation function {call.Func} may not work properly");
                    
                    _relationDataCache[fieldName] = result;
                    args[i] = result;
                }
                
                if (args[i] != null && !string.IsNullOrWhiteSpace(dataField))
                    args[i] = (args[i] as StructNode)?.GetAccessValue(dataField);
            }
            else if (arg.Value != null)
            {
                args[i] = arg.Value;
            }
        }
        
        // build the unique key for cache
        string? uniqueKey = args.All(a => a is ScalarNode or EnumNode or JsonValue)
            ? $"{call.FuncType.Name}:{target}:{string.Join(":", args.Select(a => a is JsonValue jv ? jv.ToJsonString() : a?.ToString() ?? "null"))}"
            : null;

        StructFieldSchema[]? fields = null;
        if (!string.IsNullOrEmpty(uniqueKey) && _attrFields.TryGetValue(uniqueKey, out fields))
            return fields;
        
        // Execute the function to get the struct field configs
        try
        {
            JsonNode? result = await call.FuncType.CallAsync<JsonNode>(_context, args);
            // try convert
            if (result is JsonArray arr)
            {
                return arr.Deserialize<StructFieldSchema[]>() ?? [];
            }
            // try type name
            else if (result is JsonValue)
            {
                string typeName = result.ToJsonString().Trim('"');
                var type = await _context.GetNodeTypeAsync<RuntimeValueType>(typeName);
                if (type is ArrayType arrType)
                    type = arrType.Element;
                if (type is StructType structType)
                    fields = structType.GetFields().Select(GetFieldSchema).ToArray();
            }

            fields ??= [];
            
            if (!string.IsNullOrEmpty(uniqueKey))
                _attrFields[uniqueKey] = fields;
            return fields;
        }
        catch (Exception e)
        {
            Logger.LogError(e, $"Could not find unique field for {call.FuncType.Name}");
        }
        
        return [];
    }
    
    /// <summary>
    /// Gets the struct field config for dynamic type from the relation, the relation is defined in the dynamic table field
    /// </summary>
    async Task<StructFieldSchema[]> GetStructFieldConfigs(StructNode node, RelationType relation)
    {
        if (relation.Process is not CallProcess call) throw new Exception("Only support Call relation process");
        if (call.FuncType == null) throw new Exception("The function node missing");
        
        string target = _context.GetContextItem<Access>()?.Target ?? string.Empty;
        
        // If the arguments is another field, we can query it directly, since it's designed to be used in frontend,
        // means it's value is small and easy to query, otherwise the function can be executed to gets the value directly
        object?[] args = new object[call.Args.Length];
        for (int i = 0; i < call.Args.Length; i++)
        {
            var arg = call.Args[i];
            if (!string.IsNullOrEmpty(arg.Source))
            {
                args[i] = node.GetAccessValue(arg.Source);
            }
            else if (arg.Value != null)
            {
                args[i] = arg.Value;
            }
        }
        
        // build the unique key for cache
        string? uniqueKey = args.All(a => a is ScalarNode or EnumNode or JsonValue)
            ? $"{call.FuncType.Name}:{target}:{string.Join(":", args.Select(a => a is JsonValue jv ? jv.ToJsonString() : a?.ToString() ?? "null"))}"
            : null;

        StructFieldSchema[]? fields = null;
        if (!string.IsNullOrEmpty(uniqueKey) && _attrFieldsFromStruct.TryGetValue(uniqueKey, out fields))
            return fields;
        
        // Execute the function to get the struct field configs
        try
        {
            JsonNode? result = await call.FuncType.CallAsync<JsonNode>(_context, args);
            switch (result)
            {
                // try convert
                case JsonArray arr:
                    return arr.Deserialize<StructFieldSchema[]>() ?? [];
                // try type name
                case JsonValue:
                {
                    string typeName = result.ToJsonString().Trim('"');
                    var type = await _context.GetNodeTypeAsync<RuntimeValueType>(typeName);
                    if (type is ArrayType arrType)
                        type = arrType.Element;
                    if (type is StructType structType)
                    {
                        fields = structType.GetFields().Select(GetFieldSchema).ToArray();
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
            Logger.LogError(e, $"Could not find unique field for {call.FuncType.Name}");
        }
        
        return [];
    }
    
    /// <summary>
    /// Save the attribute-based field value to the attribute table, the attr field is in format "structField_attrField"
    /// </summary>
    async Task SaveAttributeBasedFieldAsync(string attrTable, Dictionary<string, string> scopeItems, StructFieldSchema[] fields, JsonObject? value, string prev, List<(string k, DataNode v)> primaries)
    {
        string[] scopeKeys = scopeItems.Keys.ToArray();
        string tableRef = sqlProvider.QuoteTable(attrTable);
        string columnList = string.Join(',', scopeKeys.Select(sqlProvider.QuoteField).Concat(primaries.Select(p => sqlProvider.QuoteField(p.k))).Concat([_refAttrField, _refAttrIntField, _refAttrStrField, _refAttrDatField, _refAttrDblField, _refAttrTxtField, _refAttrJsonField]));
        string fixedValues = string.Join(',', scopeKeys.Select(k => scopeItems[k]).Concat(primaries.Select(p => sqlProvider.Literal(p.v))));
        string sep = scopeKeys.Length > 0 || primaries.Count > 0 ? "," : "";

        foreach (StructFieldSchema field in fields.Where(f => f.GetProperty<DisplayOnly>()?.Value != true))
        {
            var type = await _context.GetNodeTypeAsync<RuntimeValueType>(field.Type);
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
                await SaveAttributeBasedFieldAsync(attrTable, scopeItems, structType.GetFields().Select(GetFieldSchema).ToArray(), r as JsonObject, attrField, primaries);
                continue;
            }

            // For one field value
            DataNode? node = r != null ? type.From(r) : null;
            if (node is { IsEmpty: false })
            {
                DataNode? intNode = null;
                DataNode? strNode = null;
                DataNode? datNode = null;
                DataNode? dblNode = null;
                DataNode? txtNode = null;
                DataNode? jsonNode = null;
                
                if (node is ScalarNode scalar)
                {
                    ScalarType scalarType = scalar.Type as ScalarType ?? throw new Exception($"The scalar type of field {field.Name} is invalid");
                    if (scalarType is BoolType or IntType)
                    {
                        intNode = node;
                    }
                    else if (scalarType is DecimalType)
                    {
                        dblNode = node;
                    }
                    else if (scalarType is StringType)
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
                    else if (scalarType is DateType)
                    {
                        datNode = node;
                    }
                }
                else if (node is EnumNode enumNode)
                {
                    EnumType enumType = (enumNode.Type as EnumType)!;
                    switch (enumType.Type)
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
                await using DbCommand command = GetDbCommand();
                command.CommandText = $"INSERT INTO {tableRef} ({columnList}) VALUES ({fixedValues}{sep} {litAttr}, {litInt}, {litStr}, {litDat}, {litDbl}, {litTxt}, {litJson}) ON DUPLICATE KEY UPDATE {_refAttrIntField} = {litInt}, {_refAttrStrField} = {litStr}, {_refAttrDatField} = {litDat}, {_refAttrDblField} = {litDbl}, {_refAttrTxtField} = {litTxt}, {_refAttrJsonField} = {litJson};";
                Logger.LogInformation(command.CommandText);
                await command.ExecuteNonQueryAsync();
            }
            else
            {
                await DeleteAttributeBasedFieldAsync(attrTable, scopeItems, field, prev, primaries);
            }
        }
    }
    
    async Task DeleteAttributeBasedFieldAsync(string attrTable, Dictionary<string, string> scopeItems, StructFieldSchema field, string prev, List<(string k, DataNode v)> primaries)
    {
        string attrField = $"{prev}_{field.Name}";
        var type = await _context.GetNodeTypeAsync<RuntimeValueType>(field.Type);
        if (type is StructType @struct)
        {
            foreach (StructFieldSchema f in @struct.GetFields().Where(f => f.DisplayOnly != true).Select(GetFieldSchema))
            {
                await DeleteAttributeBasedFieldAsync(attrTable, scopeItems, f, attrField, primaries);
            }
        }
        else
        {
            await using DbCommand command = GetDbCommand();
            command.CommandText =$"DELETE FROM {sqlProvider.QuoteTable(attrTable)} WHERE {string.Join(" AND ", scopeItems.Select(p => $"{sqlProvider.QuoteField(p.Key)} = {p.Value}").Concat([$"{_refAttrField} = {sqlProvider.Literal(attrField)}"]).Concat(primaries.Select(p => $"{sqlProvider.QuoteField(p.k)} = {sqlProvider.Literal(p.v)}")))};";
            Logger.LogInformation(command.CommandText);
            await command.ExecuteNonQueryAsync();
        }
    }

    async Task DeleteAttributeBasedFieldAsync(DynamicTableSchema schema, ArrayNode arr)
    {
        if (schema.AppField.Topology != FieldStorageTopology.AttributeBased) return;
        
        var (_, scopeItems) = PrepareWhere(schema);
        
        foreach (DynamicTableField dynamic in schema.Fields.Where(f => f.HasTypeRelation))
        {
            foreach (StructNode pack in arr.OfType<StructNode>())
            {
                StructFieldSchema[] fields = dynamic.RelationType != null 
                    ? await GetStructFieldConfigs(schema.AppField, pack, dynamic.RelationType)
                    : await GetStructFieldConfigs(pack, dynamic.StructRelation!);
                if (fields.Length == 0) continue;
                List<(string, DataNode v)> primaries = [];
                foreach ((string fld, DataNode? v) in schema.GetFieldValues(pack, true))
                    primaries.Add((fld, v!));

                foreach (StructFieldSchema field in fields)
                    await DeleteAttributeBasedFieldAsync(schema.AppField.AttributeTableName, scopeItems, field,
                        dynamic.Name.ToLower(), primaries);
            }
        }
    }

    (string where, Dictionary<string, string> scopeItems) PrepareWhere(DynamicTableSchema schema, string prefix = "")
    {
        StringBuilder sb = new(" WHERE ");
        Dictionary<string, string> items = [];
        if (!string.IsNullOrEmpty(prefix) && !prefix.EndsWith(".")) prefix += ".";
        
        // Prepare the scope items
        foreach ((string item, DataNode? value)  in schema.GetScopeItems(_context))
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
        foreach (string item in schema.GetScopeKeys(_context))
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
        DynamicTableFieldType.Char => "CHAR(1)",
        DynamicTableFieldType.VarChar => field.MaxLength.HasValue
            ? $"VARCHAR({field.MaxLength.Value})"
            : "VARCHAR(255)", // default length
        DynamicTableFieldType.TinyText => "TINYTEXT",
        DynamicTableFieldType.Text => "TEXT",
        DynamicTableFieldType.MediumText => "MEDIUMTEXT",
        DynamicTableFieldType.LongText => "LONGTEXT",
        _ => throw new ArgumentOutOfRangeException()
    };

    StructFieldSchema GetFieldSchema(StructFieldType type)
    {
        var schema = new StructFieldSchema
        {
            Name = type.Name,
            Type = type.Type!.Name,
        };
        if (type.DisplayOnly == true)
            schema.SetProperty<DisplayOnly, bool>(true);
        return schema;
    }

    private DbTransaction? _transaction;
    private ILogger Logger => _loggerThunk.Value;

    private readonly Lazy<ILogger> _loggerThunk = new (serviceProvider.GetRequiredService<ILogger<AppDataMySqlProvider>>);
    
    private readonly ConcurrentDictionary<string, DataNode?> _relationDataCache = new (StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, StructFieldSchema[]> _attrFields = new (StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, StructFieldSchema[]> _attrFieldsFromStruct = new (StringComparer.OrdinalIgnoreCase);

    #endregion
}
