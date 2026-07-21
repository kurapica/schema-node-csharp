using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using SchemaNode.Context;
using SchemaNode.Data;
using SchemaNode.Data.Sql;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Relation;
using SchemaNode.Runtime;
using SchemaNode.Schema;
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

namespace SchemaNode.PostgreSQL;

/// <summary>
/// The implementation of IAppSchemaDataProvider for PostgreSQL
/// </summary>
public class AppDataPostgreSqlProvider(NpgsqlConnection dbConn, IServiceProvider serviceProvider, ISqlProvider sqlProvider, ISchemaContext context) : IAppDataSqlProvider<PostgreSqlProvider>, IAsyncDisposable
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
        string tableNameRaw = schema.AppField.DynamicTableName.Replace("'", "''");
        // PostgreSQL indexes are schema-level objects, not table-scoped like MySQL.
        // Prefix every index name with the table name to prevent conflicts across tables.
        string uniqueIndexName = $"{tableNameRaw}_{DYNAMIC_UNIQUE_INDEX}";
        string refIndex = sqlProvider.QuoteIndex(uniqueIndexName);
        await EnsureOpenConnectionAsync();

        bool exist = false;
        try
        {
            // Check columns via information_schema (no exception thrown if table is missing)
            Dictionary<string, string> nameTypes = new();
            {
                await using DbCommand command = GetDbCommand();
                command.CommandText =
                    $"SELECT column_name, data_type, character_maximum_length FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = '{tableNameRaw}' ORDER BY ordinal_position";
                Logger.LogDebug(command.CommandText);
                await using DbDataReader reader = await command.ExecuteReaderAsync();
                try
                {
                    while (await reader.ReadAsync())
                    {
                        string colName = reader.GetString(0);
                        string pgType = reader.GetString(1);
                        int? maxLen = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
                        nameTypes.Add(colName, NormalizePgType(pgType, maxLen));
                    }
                }
                finally
                {
                    await reader.CloseAsync();
                }
            }

            if (nameTypes.Count > 0)
            {
                // Check the new columns since we won't touch key fields
                List<string> sb = [];
                foreach (DynamicTableField dyFld in schema.ValueFields)
                {
                    string dataType = DataType(dyFld);
                    if (!nameTypes.TryGetValue(dyFld.Name, out string? type))
                    {
                        sb.Add($"ALTER TABLE {tableName} ADD COLUMN {sqlProvider.QuoteField(dyFld.Name)} {dataType};");
                    }
                    else if (!type.Equals(dataType, StringComparison.OrdinalIgnoreCase))
                    {
                        sb.Add($"ALTER TABLE {tableName} ALTER COLUMN {sqlProvider.QuoteField(dyFld.Name)} TYPE {dataType};");
                    }
                }

                // Check the existing indexes via pg_catalog
                Dictionary<string, bool> names = []; // name => unique
                {
                    await using DbCommand command = GetDbCommand();
                    command.CommandText = $"""
                                           SELECT i.relname, a.attname, ix.indisunique, ix.indisprimary
                                           FROM pg_class t
                                           JOIN pg_index ix ON t.oid = ix.indrelid
                                           JOIN pg_class i ON i.oid = ix.indexrelid
                                           CROSS JOIN LATERAL unnest(ix.indkey::int2[]) WITH ORDINALITY AS k(attnum, ord)
                                           JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum AND k.attnum > 0
                                           WHERE t.relname = '{tableNameRaw}' AND t.relkind = 'r'
                                             AND t.relnamespace = (SELECT oid FROM pg_namespace WHERE nspname = current_schema())
                                           ORDER BY i.relname, k.ord
                                           """;
                    Logger.LogDebug(command.CommandText);
                    await using DbDataReader reader = await command.ExecuteReaderAsync();
                    List<string> uniqueIndex = [];
                    try
                    {
                        while (await reader.ReadAsync())
                        {
                            string keyName = reader.GetString(0);
                            bool isPrimary = reader.GetBoolean(3);
                            if (keyName.Equals(uniqueIndexName, StringComparison.OrdinalIgnoreCase))
                            {
                                uniqueIndex.Add(reader.GetString(1));
                            }
                            else if (!isPrimary && !names.ContainsKey(keyName))
                            {
                                names.Add(keyName, reader.GetBoolean(2));
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
                        List<string> chkUniqueIndex = schema.KeyFields.Select(field => field.Name).ToList();

                        if (chkUniqueIndex.Count != uniqueIndex.Count ||
                            chkUniqueIndex.Where((p, i) => !p.Equals(uniqueIndex[i])).Any())
                        {
                            if (uniqueIndex.Count > 0)
                                sb.Add($"DROP INDEX IF EXISTS {refIndex};");

                            sb.Add(
                                $"CREATE UNIQUE INDEX {refIndex} ON {tableName}({string.Join(',', chkUniqueIndex.Select(sqlProvider.QuoteField))});");
                        }
                    }

                    // Check new indexes
                    if (schema.Indexes is { Length: > 0 })
                    {
                        foreach (var index in schema.Indexes)
                        {
                            string key =
                                $"{tableNameRaw}_IDX_{string.Join('_', index.Fields.Select(f => f.ToLower()))}";
                            if (!names.Remove(key))
                            {
                                sb.Add(
                                    $"CREATE INDEX {sqlProvider.QuoteIndex(key)} ON {tableName}({string.Join(',', schema.ScopeFields.Select(f => sqlProvider.QuoteField(f.Name)).Concat(index.Fields.Select(sqlProvider.QuoteField)))});");
                            }
                        }
                    }

                    // Remove unused indexes
                    foreach (string name in names.Keys.Where(p =>
                                 !p.Equals(uniqueIndexName, StringComparison.OrdinalIgnoreCase)))
                    {
                        sb.Add($"DROP INDEX IF EXISTS {sqlProvider.QuoteIndex(name)};");
                    }

                    // Execute pending DDL
                    if (sb.Count > 0)
                    {
                        foreach (var t in sb)
                        {
                            await using DbCommand updateCommand = GetDbCommand();
                            updateCommand.CommandText = t;
                            Logger.LogInformation(updateCommand.CommandText);
                            await updateCommand.ExecuteNonQueryAsync();
                        }
                    }

                    exist = true;
                }
            }
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

                sb.Append($"CREATE TABLE IF NOT EXISTS {tableName} (");

                // BIGSERIAL auto-increment primary key for multi-row tables
                if (!schema.Single)
                    sb.Append($"{_refSeqNo} BIGSERIAL,");

                // Generate key columns
                foreach (DynamicTableField keyField in schema.KeyFields)
                    sb.Append($"{sqlProvider.QuoteField(keyField.Name)} {DataType(keyField)} NOT NULL, ");

                // Generate value columns
                foreach (DynamicTableField tableField in schema.ValueFields)
                    sb.Append($"{sqlProvider.QuoteField(tableField.Name)} {DataType(tableField)}, ");

                // Append primary key
                if (schema.Single)
                {
                    sb.Append($"PRIMARY KEY({string.Join(',', schema.KeyFields.Select(f => sqlProvider.QuoteField(f.Name)))})");
                }
                else
                {
                    sb.Append($"PRIMARY KEY({_refSeqNo})");
                }

                sb.Append(");");
                {
                    await using DbCommand command = GetDbCommand();
                    command.CommandText = sb.ToString();
                    Logger.LogInformation(command.CommandText);
                    await command.ExecuteNonQueryAsync();
                }

                // Create the unique index separately (PostgreSQL does not support inline UNIQUE INDEX in CREATE TABLE)
                if (!schema.Single)
                {
                    await using DbCommand command = GetDbCommand();
                    command.CommandText = $"CREATE UNIQUE INDEX {refIndex} ON {tableName}({string.Join(',', schema.KeyFields.Select(f => sqlProvider.QuoteField(f.Name)))});";  
                    Logger.LogInformation(command.CommandText);
                    await command.ExecuteNonQueryAsync();
                }

                // Create additional indexes
                if (schema.Indexes is { Length: > 0 })
                {
                    foreach (var index in schema.Indexes)
                    {
                        string key = $"{tableNameRaw}_IDX_{string.Join('_', index.Fields.Select(f => f.ToLower()))}";
                        await using DbCommand command = GetDbCommand();
                        command.CommandText = $"CREATE INDEX {sqlProvider.QuoteIndex(key)} ON {tableName}({string.Join(',', schema.ScopeFields.Select(f => sqlProvider.QuoteField(f.Name)).Concat(index.Fields.Select(sqlProvider.QuoteField)))});";  
                        Logger.LogInformation(command.CommandText);
                        await command.ExecuteNonQueryAsync();
                    }
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
            try
            {
                tableName = sqlProvider.QuoteTable(schema.AppField.AttributeTableName);
                string attrTableNameRaw = schema.AppField.AttributeTableName.Replace("'", "''");
                StringBuilder sb = new();

                sb.Append($"CREATE TABLE IF NOT EXISTS {tableName} (");

                foreach (DynamicTableField keyField in schema.KeyFields)
                    sb.Append($"{sqlProvider.QuoteField(keyField.Name)} {DataType(keyField)} NOT NULL, ");

                sb.Append($"{_refAttrField} VARCHAR({EAV_TABLE_FIELD_MAX_LENGTH}) NOT NULL, ");
                sb.Append($"{_refAttrIntField} BIGINT, ");
                sb.Append($"{_refAttrStrField} VARCHAR({ENTITY_PRIMARY_KEY_MAX_LEN}), ");
                sb.Append($"{_refAttrDatField} TIMESTAMP, ");
                sb.Append($"{_refAttrDblField} DOUBLE PRECISION, ");
                sb.Append($"{_refAttrTxtField} TEXT, ");
                sb.Append($"{_refAttrJsonField} JSONB, ");
                sb.Append($"PRIMARY KEY({string.Join(',', schema.KeyFields.Select(f => sqlProvider.QuoteField(f.Name)))}, {_refAttrField})");
                sb.Append(");");

                {
                    await using DbCommand command = GetDbCommand();
                    command.CommandText = sb.ToString();
                    Logger.LogInformation(command.CommandText);
                    await command.ExecuteNonQueryAsync();
                }

                // Create indexes with IF NOT EXISTS to allow idempotent re-runs
                string scopeTargetPart = string.Join(',', schema.ScopeFields.Select(f => sqlProvider.QuoteField(f.Name)).Concat([_refAttrField]));

                {
                    await using DbCommand command = GetDbCommand();
                    command.CommandText =
                        $"CREATE INDEX IF NOT EXISTS {sqlProvider.QuoteIndex($"{attrTableNameRaw}_IDX_TAR_FLD")} ON {tableName}({scopeTargetPart});";
                    Logger.LogInformation(command.CommandText);
                    await command.ExecuteNonQueryAsync();
                }

                {
                    await using DbCommand command = GetDbCommand();
                    command.CommandText =
                        $"CREATE INDEX IF NOT EXISTS {sqlProvider.QuoteIndex($"{attrTableNameRaw}_IDX_TAR_FLD_INT")} ON {tableName}({scopeTargetPart}, {_refAttrIntField});";
                    Logger.LogInformation(command.CommandText);
                    await command.ExecuteNonQueryAsync();
                }

                {
                    await using DbCommand command = GetDbCommand();
                    command.CommandText =
                        $"CREATE INDEX IF NOT EXISTS {sqlProvider.QuoteIndex($"{attrTableNameRaw}_IDX_TAR_FLD_STR")} ON {tableName}({scopeTargetPart}, {_refAttrStrField});";
                    Logger.LogInformation(command.CommandText);
                    await command.ExecuteNonQueryAsync();
                }

                {
                    await using DbCommand command = GetDbCommand();
                    command.CommandText =
                        $"CREATE INDEX IF NOT EXISTS {sqlProvider.QuoteIndex($"{attrTableNameRaw}_IDX_TAR_FLD_DAT")} ON {tableName}({scopeTargetPart}, {_refAttrDatField});";
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

            if (schema.Fields.Last().Name.Equals(DYNAMIC_TABLE_VALUE_FIELD))
            {
                DbCommand command = GetDbCommand();
                command.CommandText = $"SELECT {sqlProvider.QuoteField(DYNAMIC_TABLE_VALUE_FIELD)} FROM {tableName} t0{wherePrefix}{TrueCond}{querySuffix}";
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
                Dictionary<string, string> prefixes = new(StringComparer.OrdinalIgnoreCase)
                {
                    [MainTable] = "t0"
                };

                // Join
                Dictionary<string, string>? fieldJoins = null;
                Dictionary<string, string>? fieldMaps = null;
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
                    fieldMaps = new Dictionary<string, string>();
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
                DbCommand command = GetDbCommand();
                command.CommandText = sb.ToString();
                Logger.LogDebug(command.CommandText);
                DbDataReader reader = await command.ExecuteReaderAsync();
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
            Dictionary<string, string> prefixes = new(StringComparer.OrdinalIgnoreCase)
            {
                [MainTable] = "t0"
            };

            // Join
            Dictionary<string, string>? fieldJoins = null;
            Dictionary<string, string>? fieldMaps = null;
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
                fieldMaps = new Dictionary<string, string>();
                foreach (DynamicTableField joinField in schema.JoinFields)
                {
                    if (!joinFields.TryGetValue(joinField.JoinAppField!, out AppFieldType? joinAppField))
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

            // Build SQL
            StringBuilder sb = new();
            sb.Append(" From ");
            sb.Append(tableName);
            sb.Append(' ');
            sb.Append(prefixes[MainTable]);

            string sql = filter?.ToSql(sqlProvider, schema, prefixes[MainTable], fieldMaps) ?? "";
            bool joinQuery = fieldMaps != null && fieldMaps.Values.Any(k => sql.Contains(k, StringComparison.OrdinalIgnoreCase));

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
                // PostgreSQL EXISTS returns boolean; use CASE WHEN to get an integer
                totalCommand.CommandText = type == AppSchemaDataResult.Exist
                    ? $"SELECT CASE WHEN EXISTS (SELECT 1 {sb} LIMIT 1) THEN 1 ELSE 0 END;"
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

            ArrayNode? value = null;
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

            await FillAttributeDataAsync(schema, value, forUpdate);

            if (type is AppSchemaDataResult.First or AppSchemaDataResult.Last)
                return (value?.ElementAtOrDefault(0) as DataNode, value is { Count: > 0 } ? 1 : 0);
            return (value, total > 0 ? total : (value?.Count ?? 0));
        }
    }

    /// <summary>
    /// Fill the attribute-based fields for the value list
    /// </summary>
    async Task FillAttributeDataAsync(DynamicTableSchema schema, ArrayNode? value, bool forUpdate = false)
    {
        (string wherePrefix, _) = PrepareWhere(schema);
        string querySuffix = forUpdate ? " FOR UPDATE;" : ";";

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
                foreach (StructNode node in value.OfType<StructNode>())
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
                        IEnumerable<StructNode> nodes = value.Cast<StructNode>();
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

        string tableName = sqlProvider.QuoteTable(schema.AppField.DynamicTableName);
        (string wherePrefix, Dictionary<string, string> scopeItems) = PrepareWhere(schema);

        string insertTemplate = $"INSERT INTO {tableName} ({string.Join(',', schema.AllFields.Select(f => sqlProvider.QuoteField(f.Name)))}) VALUES ({string.Join(',', schema.ScopeFields.Select(f => scopeItems[f.Name]))}{(schema.ScopeFields.Any() ? "," : "")} {{0}});";

        // single row
        if (schema.Single)
        {
            if (value is ArrayNode arr) value = arr.FirstOrDefault() as DataNode;

            (DataNode? origin, _) = await QueryDynamicTableAsync(schema, AppSchemaDataResult.First);

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

            if (schema.Fields.Last() is { Name: DYNAMIC_TABLE_VALUE_FIELD })
            {
                bool isInsert = false;

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
                    catch (PostgresException ex)
                    {
                        if (ex.SqlState != "23505") // unique_violation
                            throw;
                    }
                }

                if (!isInsert)
                {
                    if (origin == null)
                        (origin, _) = await QueryDynamicTableAsync(schema, AppSchemaDataResult.First);

                    DbCommand command = GetDbCommand();
                    command.CommandText = $"UPDATE {tableName} SET {sqlProvider.QuoteField(DYNAMIC_TABLE_VALUE_FIELD)} = {sqlProvider.Literal(value)}{wherePrefix}{TrueCond};";
                    Logger.LogInformation(command.CommandText);
                    await command.ExecuteNonQueryAsync();
                }
                return (true, value, origin);
            }
            else if (value is StructNode pack)
            {
                StringBuilder sb = new();
                bool isInsert = false;

                if (origin == null)
                {
                    try
                    {
                        DbCommand command = GetDbCommand();
                        command.CommandText = string.Format(insertTemplate, string.Join(',', schema.GetFieldValues(pack).Select(p => sqlProvider.Literal(p.value))));
                        Logger.LogInformation(command.CommandText);
                        await command.ExecuteNonQueryAsync();
                        isInsert = true;
                    }
                    catch (PostgresException ex)
                    {
                        if (ex.SqlState != "23505") // unique_violation
                            throw;
                    }
                }

                if (!isInsert)
                {
                    if (origin == null)
                        (origin, _) = await QueryDynamicTableAsync(schema, AppSchemaDataResult.First);

                    sb.Clear();
                    sb.Append($"UPDATE {tableName} SET ");

                    bool preCond = false;
                    foreach ((string fld, DataNode? val) in schema.GetFieldValues(pack))
                    {
                        sb.Append($"{(preCond ? "," : "")}{sqlProvider.QuoteField(fld)}={sqlProvider.Literal(val)}");
                        preCond = true;
                    }

                    sb.Append($"{wherePrefix}{TrueCond};");

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

            DataNode? origin = await this.QueryOriginNodesAsync(schema, packs, forUpdate: true);
            ArrayNode? oArr = origin as ArrayNode;
            if (!canAdd && (oArr == null || oArr.Count < packs.Length))
                throw new UnauthorizedAccessException();

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
                        if (v == null || v.IsEmpty)
                        {
                            fullFill = false;
                            break;
                        }
                        keys.Add(v.ToString()!);
                    }

                    if (!fullFill) return (false, null, null);
                    existKeys.Add(string.Join('|', keys), obj);
                }
            }

            List<StructNode> updatedPacks = [];
            List<StructNode> originPacks = [];
            foreach (StructNode pack in packs)
            {
                bool fullFill = true;
                keys.Clear();
                sb.Clear();
                sb.Append(wherePrefix);
                foreach ((string fld, DataNode? v) in schema.GetFieldValues(pack, true))
                {
                    if (v == null || v.IsEmpty)
                    {
                        fullFill = false;
                        break;
                    }
                    keys.Add(v.ToString()!);
                    sb.Append($"{sqlProvider.QuoteField(fld)} = {sqlProvider.Literal(v)} AND ");
                }
                if (!fullFill) continue;
                sb.Append(TrueCond);

                string where = sb.ToString();

                bool isInsert = false;
                if (!existKeys.TryGetValue(string.Join('|', keys), out StructNode? originPack))
                {
                    try
                    {
                        DbCommand command = GetDbCommand();
                        command.CommandText = string.Format(insertTemplate, string.Join(',', schema.GetFieldValues(pack).Select(p => sqlProvider.Literal(p.value))));
                        Logger.LogInformation(command.CommandText);
                        await command.ExecuteNonQueryAsync();
                        isInsert = true;

                        updatedPacks.Add(pack);
                    }
                    catch (PostgresException ex)
                    {
                        if (ex.SqlState != "23505") // unique_violation
                            throw;
                    }
                }

                if (!isInsert && (!onlyAdd || overrides is { Length: > 0 }))
                {
                    if (originPack == null)
                    {
                        origin = await this.QueryOriginNodesAsync(schema, [pack], forUpdate: true);
                        if (origin is ArrayNode { Count: 1 } arr)
                            originPack = arr[0] as StructNode;
                    }

                    if (originPack != null && originPack.Equals(pack))
                        continue;

                    sb.Clear();
                    sb.Append($"UPDATE {tableName} SET ");

                    bool preCond = false;
                    foreach ((string fld, DataNode? v) in schema.GetFieldValues(pack, false, true))
                    {
                        if (overrides is { Length: > 0 } && !overrides.Contains(fld, StringComparer.OrdinalIgnoreCase))
                            continue;

                        sb.Append($"{(preCond ? "," : "")}{sqlProvider.QuoteField(fld)}={sqlProvider.Literal(v)}");
                        preCond = true;
                    }
                    if (preCond)
                    {

                        sb.Append(" ");
                        sb.Append(where);

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
                if (schema.AppField.Topology == FieldStorageTopology.AttributeBased &&
                    (isInsert || (!onlyAdd || overrides is { Length: > 0 })))
                {
                    SchemaContext context = serviceProvider.GetService<SchemaContext>()!;
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
            return (true, new ArrayNode(schema.ValueType, updatedPacks), (onlyAdd && (overrides == null || overrides.Length == 0)) ? null : new ArrayNode(schema.ValueType, originPacks));
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
            (DataNode? origin, _) = await QueryDynamicTableAsync(schema, AppSchemaDataResult.First, forUpdate: true);
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

            (DataNode? origin, _) = await QueryDynamicTableAsync(schema, AppSchemaDataResult.List, filter, forUpdate: true);
            if (origin is not ArrayNode arr || arr.Count == 0) return (false, null);

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
    public async Task<(bool result, DataNode? origin)> ClearDynamicTableDataAsync(DynamicTableSchema schema)
    {
        await EnsureOpenConnectionAsync();
        (string wherePrefix, _) = PrepareWhere(schema);

        (DataNode? origin, _) = await QueryDynamicTableAsync(schema, schema.Single ? AppSchemaDataResult.First : AppSchemaDataResult.List, forUpdate: true);
        if (origin is null || origin is ArrayNode { Count: 0 }) return (false, null);

        DbCommand command = GetDbCommand();
        command.CommandText = $"DELETE FROM {sqlProvider.QuoteTable(schema.AppField.DynamicTableName)}{wherePrefix}{TrueCond};";
        Logger.LogInformation(command.CommandText);
        await command.ExecuteNonQueryAsync();
        if (schema.AppField.Topology == FieldStorageTopology.AttributeBased)
        {
            command = GetDbCommand();
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

        DbCommand command = GetDbCommand();
        command.CommandText = $"DROP TABLE IF EXISTS {tableName};";
        Logger.LogInformation(command.CommandText);
        await command.ExecuteNonQueryAsync();

        if (schema.AppField.Topology == FieldStorageTopology.AttributeBased)
        {
            string attrTableName = sqlProvider.QuoteTable(schema.AppField.AttributeTableName);
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

    DbCommand GetDbCommand()
    {
        DbCommand command = dbConn.CreateCommand();
        command.Transaction = _transaction;
        return command;
    }

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
        
        // Conflict target = all primary key columns of the EAV table
        string conflictCols = string.Join(", ", scopeKeys.Select(sqlProvider.QuoteField).Concat(primaries.Select(p => sqlProvider.QuoteField(p.k))).Concat([_refAttrField]));
        
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
                // PostgreSQL: ON CONFLICT ... DO UPDATE SET using EXCLUDED pseudo-table
                command.CommandText = $"INSERT INTO {tableRef} ({columnList}) VALUES ({fixedValues}{sep} {litAttr}, {litInt}, {litStr}, {litDat}, {litDbl}, {litTxt}, {litJson}) ON CONFLICT ({conflictCols}) DO UPDATE SET {_refAttrIntField} = EXCLUDED.{_refAttrIntField}, {_refAttrStrField} = EXCLUDED.{_refAttrStrField}, {_refAttrDatField} = EXCLUDED.{_refAttrDatField}, {_refAttrDblField} = EXCLUDED.{_refAttrDblField}, {_refAttrTxtField} = EXCLUDED.{_refAttrTxtField}, {_refAttrJsonField} = EXCLUDED.{_refAttrJsonField};";
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
            command.CommandText = $"DELETE FROM {sqlProvider.QuoteTable(attrTable)} WHERE {string.Join(" AND ", scopeItems.Select(p => $"{sqlProvider.QuoteField(p.Key)} = {p.Value}").Concat([$"{_refAttrField} = {sqlProvider.Literal(attrField)}"]).Concat(primaries.Select(p => $"{sqlProvider.QuoteField(p.k)} = {sqlProvider.Literal(p.v)}")))};";
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

        foreach ((string item, DataNode? value) in schema.GetScopeItems(_context))
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

        foreach (string item in schema.GetScopeKeys(_context))
            sb.Append($"{sub}{sqlProvider.QuoteField(item)} = {main}{sqlProvider.QuoteField(item)} AND ");

        return sb.ToString();
    }

    /// <summary>
    /// Maps a <see cref="DynamicTableFieldType"/> to the corresponding PostgreSQL column type.
    /// </summary>
    static string DataType(DynamicTableField field) => field.Type switch
    {
        DynamicTableFieldType.Bool      => "SMALLINT",  // Use SMALLINT (0/1) to contains MySQL TINYINT; base library reads bool fields as byte/short
        DynamicTableFieldType.Smallint  => "SMALLINT",
        DynamicTableFieldType.USmallint => "INTEGER",        // no UNSIGNED in PostgreSQL
        DynamicTableFieldType.Mediumint => "INTEGER",
        DynamicTableFieldType.UMediumint => "INTEGER",
        DynamicTableFieldType.Int       => "INTEGER",
        DynamicTableFieldType.UInt      => "BIGINT",         // preserve unsigned 32-bit range
        DynamicTableFieldType.BigInt    => "BIGINT",
        DynamicTableFieldType.UBigInt   => "BIGINT",
        DynamicTableFieldType.Float     => "REAL",
        DynamicTableFieldType.Double    => "DOUBLE PRECISION",
        DynamicTableFieldType.Json      => "JSONB",
        DynamicTableFieldType.DateTime  => "TIMESTAMP",
        DynamicTableFieldType.TinyBlob  => "BYTEA",
        DynamicTableFieldType.Blob      => "BYTEA",
        DynamicTableFieldType.MediumBlob => "BYTEA",
        DynamicTableFieldType.LongBlob  => "BYTEA",
        DynamicTableFieldType.Char      => "CHAR(1)",
        DynamicTableFieldType.VarChar   => field.MaxLength.HasValue
            ? $"VARCHAR({field.MaxLength.Value})"
            : "VARCHAR(255)",
        DynamicTableFieldType.TinyText  => "TEXT",
        DynamicTableFieldType.Text      => "TEXT",
        DynamicTableFieldType.MediumText => "TEXT",
        DynamicTableFieldType.LongText  => "TEXT",
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
    
    /// <summary>
    /// Normalises the type string returned by <c>information_schema.columns</c> to contains
    /// what <see cref="DataType"/> produces so the two can be compared.
    /// </summary>
    private static string NormalizePgType(string dataType, int? maxLength) =>
        dataType.ToLowerInvariant() switch
        {
            "character varying"                          => maxLength.HasValue ? $"VARCHAR({maxLength})" : "TEXT",
            "character"                                  => maxLength.HasValue ? $"CHAR({maxLength})"    : "CHAR(1)",
            "timestamp without time zone" or "timestamp" => "TIMESTAMP",
            "timestamp with time zone"                   => "TIMESTAMPTZ",
            var t                                        => t.ToUpperInvariant()
        };

    private DbTransaction? _transaction;
    private ILogger Logger => _loggerThunk.Value;

    private readonly Lazy<ILogger> _loggerThunk = new(serviceProvider.GetRequiredService<ILogger<AppDataPostgreSqlProvider>>);

    private readonly Dictionary<string, DataNode?> _relationDataCache = [];
    private readonly Dictionary<string, StructFieldSchema[]> _attrFields = [];
    private readonly Dictionary<string, StructFieldSchema[]> _attrFieldsFromStruct = [];

    #endregion
}
