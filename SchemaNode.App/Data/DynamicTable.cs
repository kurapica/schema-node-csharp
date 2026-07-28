using System.Collections.Immutable;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Data.Common;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Core;
using SchemaNode.Relation;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using AppType = SchemaNode.Runtime.AppType;
using ArrayType = SchemaNode.Runtime.ArrayType;
using BoolType = SchemaNode.Runtime.BoolType;
using DateType = SchemaNode.Runtime.DateType;
using DecimalType = SchemaNode.Runtime.DecimalType;
using EnumType = SchemaNode.Runtime.EnumType;
using IntType = SchemaNode.Runtime.IntType;
using StringType = SchemaNode.Runtime.StringType;
using StructType = SchemaNode.Runtime.StructType;
using ValueType = SchemaNode.Runtime.ValueType;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Data;


/// <summary>
/// The dynamic table structure
/// </summary>
public class DynamicTableSchema
{
    #region Construtor
    
    internal DynamicTableSchema(AppFieldType appFieldType, SchemaContext context)
    {
        AppField  = appFieldType;
        ValueType = appFieldType.ValueType ?? throw new Exception($"The App {appFieldType.App}'s field {appFieldType.Name} has no value type");

        // no storage
        if (appFieldType is { EnableStorage: false, IsForeignView: false })
        {
            Single = true;
            Fields = [];
            return;
        }
        
        // Generate the fields
        ValueType node = ValueType;
        List<DynamicTableField> fields = [];
        List<DynamicTableJoin>? joins = null;
        DataIndex[]? indexes = null;
        bool single = true;

        // The target app type, cover the field view
        bool isView = appFieldType.IsForeignView;
        AppType targetApp = !isView ? appFieldType.Application: (appFieldType.View?.AppType ?? throw new Exception($"Foreign view app {appFieldType.View?.App} not exist"));

        // context item isolation scope
        foreach ((string item, ValueType type, bool isTarget) in targetApp.GetScopeContextItemTypes(context))
        {
            DataTypeInfo info = GetDataTypeInfo(type, null, type is StringType ? ENTITY_PRIMARY_KEY_MAX_LEN : null);

            // Add scope-target field
            fields.Add(new DynamicTableField
            {
                Name = item,
                Type = info.Type,
                MaxLength = info.MaxLength,
                ValueType = type,
                Scope = true,
                Target = isTarget
            });
        }

        // value fields
        switch (node)
        {
            case StructType structNode:
            {
                foreach (StructFieldType sField in structNode.GetFields())
                {
                    // Check if join the other field for display only
                    if (sField.DisplayOnly ?? false)
                    {
                        var relation = structNode.GetRelations(sField.Name)
                            .FirstOrDefault(r => 
                                r.ForProperty<Default>() &&
                                r.Process is CallProcess call &&
                                IsReferenceFunc(call.Func));
                        if (relation == null) continue;

                        CallProcess call = (relation.Process as CallProcess)!;

                        // app
                        string? app = call.Args.FirstOrDefault()?.Value?.ToValue<string>();
                        if (string.IsNullOrWhiteSpace(app) || !targetApp.Name.Equals(app, StringComparison.OrdinalIgnoreCase)) continue; // the same app

                        // app field
                        string? field = call.Args.ElementAtOrDefault(1)?.Value?.ToValue<string>();
                        if (string.IsNullOrWhiteSpace(field)) continue; // no app field
                        AppFieldType? appField = targetApp.GetField(field);
                        if (appField == null) continue; // app field not exist

                        // primary & struct
                        ImmutableList<string> primary = (appField.ValueType as ArrayType)?.Primary ?? [];
                        if ((appField.ValueType is ArrayType arr ? arr.Element : appField.ValueType) is not StructType structType || !structType.GetFields().Any()) continue;
                        if (primary.Count + 3 != call.Args.Length) continue; // primary fields not contains

                        // data field
                        string? dataField = call.Args.ElementAtOrDefault(2)?.Value?.ToValue<string>();
                        if (string.IsNullOrWhiteSpace(dataField) || dataField.Equals(appFieldType.Name, StringComparison.OrdinalIgnoreCase)) continue; // no data field
                        var dataFieldType = structType.GetField(dataField);
                        if (dataFieldType == null) continue; // data field not exist

                        // Check joins
                        if (joins == null || joins.All(j => !j.Field.Equals(field, StringComparison.OrdinalIgnoreCase)))
                        {
                            // collect keys
                            Dictionary<string, AppSchemaDataFilter> keyMap = new();

                            // build primary key
                            for (int i = 3; i < call.Args.Length - 1; i++)
                            {
                                keyMap[primary[i - 3]] = !string.IsNullOrEmpty(call.Args[i].Source)
                                    ? new AppSchemaDataFilterField(call.Args[i].Source!)
                                    : new AppSchemaDataFilterValue(call.Args[i].Value?.ToValue<string>() ?? string.Empty);
                            }

                            joins ??= [];
                            joins.Add(new DynamicTableJoin { Field = field, Matches = keyMap });
                        }

                        if (dataFieldType.Type is StructType @struct)
                        {
                            // As complex fields
                            foreach (StructFieldType ifield in @struct.GetFields().Where(p => p.Type != null && !(p.DisplayOnly ?? false)))
                            {
                                DataTypeInfo info = GetDataTypeInfo(ifield.Type!, ifield);
                                fields.Add(new DynamicTableField
                                {
                                    Name = $"{sField.Name}{COMPLEX_SEP}{ifield.Name}",
                                    Complex = new DataFieldComplexInfo
                                    {
                                        Main = sField.Name,
                                        Field = ifield.Name
                                    },
                                    Type = info.Type,
                                    MaxLength = info.MaxLength,
                                    ValueType = ifield.Type!,
                                    JoinAppField = field,
                                    JoinDataField = $"{dataField}{COMPLEX_SEP}{ifield.Name}"
                                });
                            }
                        }
                        else
                        {
                            // As normal field
                            DataTypeInfo info = GetDataTypeInfo(dataFieldType.Type!, dataFieldType);
                            fields.Add(new DynamicTableField
                            {
                                Name = sField.Name,
                                Type = info.Type,
                                MaxLength = info.MaxLength,
                                ValueType = dataFieldType.Type!,
                                JoinAppField = field,
                                JoinDataField = dataField
                            });
                        }
                    }

                    else if (sField.Type is StructType subStructNode) // Check if the sfield use a struct type
                    {
                        // As complex fields
                        foreach (var iField in subStructNode.GetFields().Where(p => p.Type != null && !(p.DisplayOnly ?? false)))
                        {
                            DataTypeInfo info = GetDataTypeInfo(iField.Type!, iField);
                            fields.Add(new DynamicTableField
                            {
                                Name = $"{sField.Name}{COMPLEX_SEP}{iField.Name}",
                                Complex = new DataFieldComplexInfo
                                {
                                    Main = sField.Name,
                                    Field = iField.Name
                                },
                                Type = info.Type,
                                MaxLength = info.MaxLength,
                                ValueType = iField.Type!
                            });
                        }
                    }
                    else
                    {
                        DataTypeInfo info = GetDataTypeInfo(sField.Type!, sField);
                        fields.Add(new DynamicTableField
                        {
                            Name = sField.Name,
                            Type = info.Type,
                            MaxLength = info.MaxLength,
                            ValueType = sField.Type!
                        });
                    }
                }
                break;
            }
            case ArrayType arrayNode:
            {
                node = arrayNode.Element!; // Record the base node for array
                indexes = arrayNode.GetProperty<Indexes>()?.Value;
                if (node is StructType structNode && arrayNode.Primary is { Count: > 0 })
                {
                    single = false;
                    var enableAttrTable = appFieldType.Topology == FieldStorageTopology.AttributeBased;

                    // Add primary fields
                    foreach (string n in arrayNode.Primary)
                    {
                        var sField = structNode.GetField(n)!;
                        DataTypeInfo info = GetDataTypeInfo(sField.Type!, sField);
                        fields.Add(new DynamicTableField
                        {
                            Name = sField.Name,
                            Type = info.Type,
                            Primary = true,
                            MaxLength = info.MaxLength,
                            ValueType = sField.Type!
                        });
                    }
                    
                    // Add normal fields
                    foreach (var sField in structNode.GetFields().Where(p => !arrayNode.Primary.Contains(p.Name)))
                    {
                        // Check if join the other field for display only
                        if (sField.DisplayOnly ?? false)
                        {
                            var relation = structNode.GetRelations(sField.Name).FirstOrDefault(r => 
                                r.ForProperty<Default>() &&
                                r.Process is CallProcess call &&
                                IsReferenceFunc(call.Func));
                            if (relation == null) continue;

                            CallProcess call = (relation.Process as CallProcess)!;

                            // app
                            string? app = call.Args.FirstOrDefault()?.Value?.ToValue<string>();
                            if (string.IsNullOrWhiteSpace(app) || !appFieldType.App.Equals(app, StringComparison.OrdinalIgnoreCase)) continue; // the same app

                            // app field
                            string? field = call.Args.ElementAtOrDefault(1)?.Value?.ToValue<string>();
                            if (string.IsNullOrWhiteSpace(field)) continue; // no app field
                            AppFieldType? appField = appFieldType.Application.GetField(field);
                            if (appField == null) continue; // app field not exist

                            // primary & struct
                            ImmutableList<string> primary = (appField.ValueType as ArrayType)?.Primary ?? [];
                            if ((appField.ValueType is ArrayType arr ? arr.Element : appField.ValueType) is not StructType structType || !structType.GetFields().Any()) continue;
                            if (primary.Count + 3 != call.Args.Length) continue; // primary fields not contains

                            // data field
                            string? dataField = call.Args.ElementAtOrDefault(2)?.Value?.ToValue<string>();
                            if (string.IsNullOrWhiteSpace(dataField) || dataField.Equals(appFieldType.Name, StringComparison.OrdinalIgnoreCase)) continue; // no data field
                            var dataFieldType = structType.GetField(dataField);
                            if (dataFieldType == null) continue; // data field not exist

                            // Check joins
                            if (joins == null || joins.All(j => !j.Field.Equals(field, StringComparison.OrdinalIgnoreCase)))
                            {
                                // collect keys
                                Dictionary<string, AppSchemaDataFilter> keyMap = new();

                                // build primary key
                                for (int i = 3; i < call.Args.Length - 1; i++)
                                {
                                    keyMap[primary[i - 3]] = !string.IsNullOrEmpty(call.Args[i].Source)
                                        ? new AppSchemaDataFilterField(call.Args[i].Source!)
                                        : new AppSchemaDataFilterValue(call.Args[i].Value?.ToValue<string>() ?? string.Empty);
                                }

                                joins ??= [];
                                joins.Add(new DynamicTableJoin { Field = field, Matches = keyMap });
                            }

                            if (dataFieldType.Type is StructType @struct)
                            {
                                // As complex fields
                                foreach (var ifield in @struct.GetFields().Where(p => p.Type != null && !(p.DisplayOnly ?? false)))
                                {
                                    DataTypeInfo info = GetDataTypeInfo(ifield.Type!, ifield);
                                    fields.Add(new DynamicTableField
                                    {
                                        Name = $"{sField.Name}{COMPLEX_SEP}{ifield.Name}",
                                        Complex = new DataFieldComplexInfo
                                        {
                                            Main = sField.Name,
                                            Field = ifield.Name
                                        },
                                        Type = info.Type,
                                        MaxLength = info.MaxLength,
                                        ValueType = ifield.Type!,
                                        JoinAppField = field,
                                        JoinDataField = $"{dataField}{COMPLEX_SEP}{ifield.Name}"
                                    });
                                }
                            }
                            else
                            {
                                // As normal field
                                DataTypeInfo info = GetDataTypeInfo(dataFieldType.Type!, dataFieldType);
                                fields.Add(new DynamicTableField
                                {
                                    Name = sField.Name,
                                    Type = info.Type,
                                    MaxLength = info.MaxLength,
                                    ValueType = dataFieldType.Type!,
                                    JoinAppField = field,
                                    JoinDataField = dataField
                                });
                            }
                        }

                        // Check if the s-field use a struct type
                        else  if (sField.Type is StructType type)
                        {
                            // As complex fields
                            foreach (var ifield in type.GetFields().Where(p => p.Type != null && !(p.DisplayOnly ?? false)))
                            {
                                DataTypeInfo info = GetDataTypeInfo(ifield.Type!, ifield);
                                fields.Add(new DynamicTableField
                                {
                                    Name = $"{sField.Name}{COMPLEX_SEP}{ifield.Name}",
                                    Complex = new DataFieldComplexInfo
                                    {
                                        Main = sField.Name,
                                        Field = ifield.Name
                                    },
                                    Type = info.Type,
                                    MaxLength = info.MaxLength,
                                    ValueType = ifield.Type!
                                });
                            }
                        }
                        // Check if the field is a dynamic JSON field with attribute-based topology, which need to be stored in separated attribute table
                        else if (sField.Type is ObjectType && enableAttrTable)
                        {
                            var typeRelation = appFieldType.Application.GetRelations($"{appFieldType.Name}.{sField.Name}").FirstOrDefault(r => r.ForProperty<OverrideType>());
                            var fieldRelation = structNode.GetRelations(sField.Name).FirstOrDefault(r => r.ForProperty<OverrideType>());

                            DataTypeInfo info = GetDataTypeInfo(sField.Type, sField);
                            fields.Add(new DynamicTableField
                            {
                                Name = sField.Name,
                                Type = info.Type,
                                MaxLength = info.MaxLength,
                                ValueType = sField.Type!,
                                RelationType = typeRelation,
                                StructRelation = fieldRelation
                            });
                        }
                        else
                        {
                            DataTypeInfo info = GetDataTypeInfo(sField.Type!, sField);
                            fields.Add(new DynamicTableField
                            {
                                Name = sField.Name,
                                Type = info.Type,
                                MaxLength = info.MaxLength,
                                ValueType = sField.Type!
                            });
                        }
                    }
                }
                else
                {
                    fields.Add(new DynamicTableField
                    {
                        Name = DYNAMIC_TABLE_VALUE_FIELD,
                        Type = DynamicTableFieldType.Json,
                        ValueType = node
                    });
                }
                break;
            }
            default:
            {
                DataTypeInfo info = GetDataTypeInfo(node);
                fields.Add(new DynamicTableField
                {
                    Name = DYNAMIC_TABLE_VALUE_FIELD,
                    Type = info.Type,
                    MaxLength = info.MaxLength,
                    ValueType = node
                });
                break;
            }
        }

        // Add the foreign key field if not added for query
        if (isView)
        {
            AppFieldType sourceField = appFieldType.View?.AppType?.GetField(appFieldType.View?.Field ?? "") ?? throw new Exception($"Invalid view source field: {appFieldType.View?.App}.{appFieldType.View?.Field}");
            var foreign = sourceField.Foreigns?.FirstOrDefault(f => f.App.Equals(appFieldType.App, StringComparison.OrdinalIgnoreCase)) ?? throw new Exception($"Invalid view source field: {appFieldType.View?.App}.{appFieldType.View?.Field}");
            if (!fields.Any(f => f.Name.Equals(foreign.Field, StringComparison.OrdinalIgnoreCase)))
            {
                StructType eleType = (sourceField.ValueType is ArrayType arr ? arr.Element : sourceField.ValueType) as StructType ?? throw new Exception($"The {appFieldType.Name} field can't be used as view");
                var fieldInfo = eleType.GetField(foreign.Field) ?? throw new Exception($"Invalid view source field: {appFieldType.View?.App}.{appFieldType.View?.Field}");
                DataTypeInfo info = GetDataTypeInfo(fieldInfo.Type!, fieldInfo);
                fields.Add(new DynamicTableField
                {
                    Name = fieldInfo.Name,
                    Type = info.Type,
                    MaxLength = info.MaxLength,
                    ValueType = fieldInfo.Type!
                });
            }
        }

        Single = single;
        Fields  = fields;
        Indexes = indexes;
        Pageable = appFieldType.Pageable ?? false;
        Joins = joins?.ToArray();
    }
    
    #endregion
    
    #region Properties
    
    /// <summary>
    /// the app field type of the dynamic table
    /// </summary>
    public AppFieldType AppField { get; init; }
    
    /// <summary>
    /// The data type node
    /// </summary>
    public ValueType ValueType { get; init; }

    /// <summary>
    /// Whether the table is single row
    /// </summary>
    public bool Single { get; init; }

    /// <summary>
    /// Whether the table use increase update, no full data push allowed
    /// </summary>
    public bool Pageable { get; init; }

    /// <summary>
    /// The dynamic table fields
    /// </summary>
    public IReadOnlyList<DynamicTableField> Fields { get; init; }
    
    /// <summary>
    /// The scope target fields
    /// </summary>
    public IEnumerable<DynamicTableField> ScopeFields => Fields.Where(f => f.Scope);
    
    /// <summary>
    /// Non-scope and non-target fields, used for data query and save
    /// </summary>
    public IEnumerable<DynamicTableField> NonScopeFields => Fields.Where(f => f.Primary || f.IsValueField);
    
    /// <summary>
    /// The fields used for query, including primary fields, value fields and join fields
    /// </summary>
    public IEnumerable<DynamicTableField> QueryFields => Fields.Where(f => f.Target || f.Primary || f.IsValueField || f.IsJoinField);
    
    /// <summary>
    /// The join fields
    /// </summary>
    public IEnumerable<DynamicTableField> JoinFields => Fields.Where(f => f.IsJoinField);

    /// <summary>
    /// Gets the key fields
    /// </summary>
    public IEnumerable<DynamicTableField> KeyFields => Fields.Where(f => f.IsKeyField);
    
    /// <summary>
    /// Gets the value fields
    /// </summary>
    public IEnumerable<DynamicTableField> ValueFields => Fields.Where(f => f.IsValueField);

    /// <summary>
    /// Gets the fields without type relation, used for data query and save
    /// </summary>
    public IEnumerable<DynamicTableField> AllFields => Fields.Where(f => f.IsKeyField || f.IsValueField);
    
    /// <summary>
    /// The dynamic table indexes
    /// </summary>
    public DataIndex[]? Indexes { get; init; }
    
    /// <summary>
    /// The dynamic table joins
    /// </summary>
    public DynamicTableJoin[]? Joins { get; init; } 

    #endregion

    #region Methods
    
    public IEnumerable<(string field, DataNode? value)> GetFieldValues(StructNode pack, bool primaryOnly = false, bool noPrimary = false)
    {
        IEnumerable<DynamicTableField> fields = NonScopeFields;
        if (primaryOnly) fields = fields.Where(p => p.Primary);
        else if (noPrimary) fields = fields.Where(p => !p.Primary);
        foreach (DynamicTableField field in fields)
        {
            if (field.Complex == null)
            {
                var fieldNode = pack.GetAccessValue(field.Name);
                if (fieldNode is { IsEmpty: false })
                {
                    yield return (field.Name, fieldNode as DataNode);
                }
                else
                {
                    yield return (field.Name, null);
                }
            }
            else
            {
                var complex = pack.GetAccessValue(field.Complex.Main);
                if (complex is StructNode sPack && sPack.GetAccessValue(field.Complex.Field) is { IsEmpty: false } part)
                {
                    yield return (field.Name, part as DataNode);
                }
                else
                {
                    yield return (field.Name, null);
                }
            }
        }
    }

    public IEnumerable<DynamicTableField> GetDynamicTableFields(string fieldName)
    {
        foreach (DynamicTableField field in Fields)
        {
            if (field.Complex == null)
            {
                if (field.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    yield return field;
                }
            }
            else
            {
                if (field.Complex.Main.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    yield return field;
                }
            }
        }
    }

    /// <summary>
    /// Gets the scope context items for the dynamic table, used for data partition and target selection
    /// </summary>
    public IEnumerable<string> GetScopeKeys(ISchemaContext context)
    {
        if (context is not SchemaContext schemaContext) yield break;
        foreach (var (item, _, _) in AppField.Application.GetScopeContextItemTypes(schemaContext))
            yield return item;
    }

    /// <summary>
    /// Gets the scope context items for the dynamic table, used for data partition and target selection
    /// </summary>
    public IEnumerable<(string item, DataNode? value)> GetScopeItems(ISchemaContext context)
    {
        if (context is not SchemaContext schemaContext) yield break;
        bool isview = AppField.IsForeignView;
        foreach (var (item, value, isTarget) in AppField.Application.GetScopeContextItems(schemaContext))
        {
            if (isview && isTarget)
            {
                // change the view target to the foreign field
                var sourceField = AppField.View?.AppType?.GetField(AppField.View?.Field ?? "") ?? throw new Exception($"Invalid view source field: {AppField.View?.App}.{AppField.View?.Field}");
                var foreign = sourceField.Foreigns?.FirstOrDefault(f => f.App.Equals(AppField.App, StringComparison.OrdinalIgnoreCase)) ?? throw new Exception($"Invalid view source field: {AppField.View?.App}.{AppField.View?.Field}");
                yield return (foreign.Field, value);
            }
            else
                yield return (item, value);
        }
    }
    
    /// <summary>
    /// Gets the primary token from the data
    /// </summary>
    public string? GetPrimaryKey(StructNode pack)
    {
        List<string> keys = [];
        foreach ((string _, DataNode? node) in GetFieldValues(pack, true))
        {
            if (node == null || node.IsEmpty) return null;
            keys.Add(node.GetValue<string>()!);
        }
        return string.Join(":", keys);
    }

    /// <summary>
    /// Gets the primary token from the key nodes
    /// </summary>
    public string? GetPrimaryKey(params DataNode?[] keyNodes)
    {
        string[] keys = new string[keyNodes.Length];
        for (int i = 0; i < keyNodes.Length; i++)
        {
            if (keyNodes[i] == null || keyNodes[i] is { IsEmpty: true }) return null;
            keys[i] = keyNodes[i]!.GetValue<string>()!;
        }
        return string.Join(":", keys);
    }

    /// <summary>
    /// Gets the field data pack from the reader
    /// </summary>
    public DataNode GetFieldPack(DbDataReader reader, int offset = 0, bool queryOnly = false)
    {
        StructNode result = new StructNode((StructType)(ValueType is ArrayType arr ? arr.Element : ValueType)!);
        foreach (DynamicTableField field in queryOnly ? QueryFields : NonScopeFields)
        {
            DataNode? val = field.FromReader(reader, offset++);

            if (field.Target)
            {
                // set the map
                if (AppField.IsForeignView)
                {
                    string? map = AppField.View?.Map;
                    if (!string.IsNullOrWhiteSpace(map))
                        result[map] = val;
                }
                continue;
            }

            if (val == null)
            {
                if (field is { IsJoinField: true, ValueType: DecimalType or IntType})
                    val = field.ValueType.From(0);
                else
                    continue;
            }
            if (field.Complex == null)
            {
                result[field.Name] = val;
            }
            else
            {
                var main = result.GetAccessValue(field.Complex.Main);
                if (main == null)
                {
                    main = new StructNode((StructType)((StructType)ValueType).GetField(field.Complex.Main)!.Type!);
                    result[field.Complex.Main] = main;
                }
                (main as StructNode)![field.Complex.Field] = val;
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the field data pack from the reader by field name
    /// </summary>
    public DataNode? GetFieldPack(DbDataReader reader, string fieldName, bool queryOnly = false)
    {
        int offset = 0;
        StructNode? complexResult = null;
        foreach (DynamicTableField field in queryOnly ? QueryFields : NonScopeFields)
        {
            if (field.Complex == null && field.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
            {
                return field.FromReader(reader, offset);
            }
            else if (field.Complex != null && field.Complex.Field.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
            {
                complexResult ??= new StructNode((StructType)((StructType)ValueType).GetField(field.Complex.Main)!.Type!);
                DataNode? val = field.FromReader(reader, offset);
                if (val != null)
                    complexResult[field.Complex.Field] = val;
            }

            offset++;
        }
        return complexResult;
    }

    /// <summary>
    /// Gets the order by fields
    /// </summary>
    public IEnumerable<(string field, bool desc)> GetOrderBys(bool desc = false, AppSchemaDataOrder[]? orderBy = null)
    {
        if (orderBy is { Length: > 0 })
        {
            bool has = false;
            foreach (DynamicTableField field in Fields.Where(f => f.Primary))
            {
                AppSchemaDataOrder? order = orderBy.FirstOrDefault(o => o.Field.Equals(field.Name, StringComparison.OrdinalIgnoreCase));
                if (order == null) continue;

                has = true;
                yield return (field.Name, order.Desc);
            }
            if (has) yield break;
        }
        yield return (DYNAMIC_TABLE_SEQNO_FIELD, desc);
    }

    /// <summary>
    /// Generate display only fields
    /// </summary>
    public Task GenerateDisplayOnlyFields(SchemaContext context, DataNode? pack)
    {
        // Generate the display only fields
        return ValueType is StructType @struct 
            ? GenerateDisplayOnlyFields(context, @struct, pack)
            : ValueType is ArrayType { Element: StructType structEle }
                ? GenerateDisplayOnlyFields(context, structEle, pack)
                : Task.CompletedTask;
    }

    #endregion
    
    #region Utility

    internal static bool IsReferenceFunc(string func) => $"{NS_SYSTEM_DATA}.app.{nameof(SystemAppData.getfield)}".Equals(func, StringComparison.OrdinalIgnoreCase);

    // Generate the display only fields
    private static async Task GenerateDisplayOnlyFields(SchemaContext context, StructType type, IValueAccess? node, bool joinHandled = false)
    {
        switch (node)
        {
            case ArrayNode array:
            {
                // batch process for join functions
                if (type.GetRelations().Any())
                {
                    foreach (var relation in (type.GetRelations().Where(r =>
                                 r.ForProperty<Default>() &&
                                 r.Process is CallProcess call &&
                                 IsReferenceFunc(call.Func) &&
                                 type.GetField(r.Target) != null && 
                                 (type.GetField(r.Target)?.DisplayOnly ?? false))))
                    {
                        // call
                        CallProcess call = (relation.Process as CallProcess)!;
                        
                        // app
                        string? app = call.Args.FirstOrDefault()?.Value?.ToValue<string>();
                        if (string.IsNullOrWhiteSpace(app)) continue; // no app
                        AppType? appType = await context.GetAppTypeAsync(app);
                        if (appType == null) continue; // app not exist

                        // app field
                        string? field = call.Args.ElementAtOrDefault(1)?.Value?.ToValue<string>();
                        if (string.IsNullOrWhiteSpace(field)) continue; // no app field
                        AppFieldType? appField = appType.GetField(field);
                        if (appField == null) continue; // app field not exist

                        // primary & struct
                        ImmutableList<string> primary = (appField.ValueType as ArrayType)?.Primary ?? [];
                        if ((appField.ValueType is ArrayType arr
                                ? arr.Element
                                : appField.ValueType) is not StructType structType || !structType.GetFields().Any()) continue;
                        if (primary.Count + 3 != call.Args.Length) continue; // primary fields not contains

                        // data field
                        string? dataField = call.Args.ElementAtOrDefault(2)?.Value?.ToValue<string>();
                        if (string.IsNullOrWhiteSpace(dataField)) continue; // no data field
                        var dataFieldType = structType.GetField(dataField);
                        if (dataFieldType == null) continue; // data field not exist

                        // target
                        string? target = context.GetContextItem<Access>()?.Target;
                        if (string.IsNullOrWhiteSpace(target)) continue; // no app target

                        // collect keys
                        Dictionary<string, List<DataNode>> keyMap = new();
                        AppSchemaDataFilter? filter = null;
                        if (primary.Count > 0)
                        {
                            foreach (DataNode row in array)
                            {
                                if (row is not StructNode pack || 
                                    pack.GetAccessValue(relation.Target) is null ||
                                    !pack.GetAccessValue(relation.Target)!.IsEmpty) continue;

                                // build primary key
                                List<string> keys = [];
                                AppSchemaDataFilter? rowFilter = null;
                                for (int i = 3; i < call.Args.Length; i++)
                                {
                                    string? key = !string.IsNullOrEmpty(call.Args[i].Source)
                                        ? pack.GetAccessValue(call.Args[i].Source!)?.ToString()
                                        : call.Args[i].Value?.ToValue<string>();
                                    
                                    if (string.IsNullOrEmpty(key))
                                    {
                                        keys.Clear();
                                        break;
                                    }
                                    var argFilter = new AppSchemaDataFilterBinary(LogicType.Equal,
                                        new AppSchemaDataFilterField(primary[i - 3]), new AppSchemaDataFilterValue(key));
                                    rowFilter = rowFilter == null ? argFilter : new AppSchemaDataFilterBinary(LogicType.AndAlso, rowFilter, argFilter);
                                    keys.Add(key);
                                }
                                
                                if (keys.Count == 0 || rowFilter == null) continue; // no valid primary key
                                string pkey = string.Join(":", keys);

                                // add to map
                                if (!keyMap.ContainsKey(pkey))
                                {
                                    keyMap[pkey] = [];
                                    filter = filter == null ? rowFilter : new AppSchemaDataFilterBinary(LogicType.OrElse, filter, rowFilter);
                                }

                                keyMap[pkey].Add(pack);
                            }
                        }
                        
                        if (filter == null) continue; // no valid data to query

                        // query the dynamic data
                        (DataNode? value, _) = await context.GetAppFieldDataAsync(appField, AppSchemaDataResult.List, filter);

                        // set the display only field value
                        switch (primary.Count)
                        {
                            case > 0 when value is ArrayNode resultArray:
                            {
                                foreach (DataNode resultRow in resultArray)
                                {
                                    if (resultRow is not StructNode resultStruct) continue;

                                    // build primary key
                                    List<string> keys = [];
                                    foreach (string path in primary)
                                    {
                                        var n = resultStruct.GetAccessValue(path);
                                        if (n == null || n.IsEmpty)
                                        {
                                            keys.Clear();
                                            break;
                                        }

                                        keys.Add(n.GetValue<string>()!);
                                    }

                                    if (keys.Count == 0) continue; // no valid primary key
                                    string pkey = string.Join(":", keys);

                                    // get data node
                                    var dataNode = resultStruct.GetAccessValue(dataField);
                                    if (dataNode == null || dataNode.IsEmpty) continue;

                                    // set value
                                    if (!keyMap.TryGetValue(pkey, out List<DataNode>? packs)) continue;
                                    foreach (DataNode row in packs)
                                    {
                                        if (row is not StructNode pack) continue;
                                        var fld = pack.GetAccessValue(relation.Target);
                                        if (fld is not { IsEmpty: true }) continue;

                                        // set value
                                        fld.TrySetValue(dataNode);
                                    }
                                }

                                break;
                            }
                            case 0 when value is StructNode resultStruct:
                            {
                                // single key
                                var dataNode = resultStruct.GetAccessValue(dataField);
                                if (dataNode == null || dataNode.IsEmpty) continue;

                                foreach (DataNode row in array)
                                {
                                    if (row is not StructNode pack) continue;
                                    var fld = pack.GetAccessValue(relation.Target);
                                    if (fld is not { IsEmpty: true }) continue;

                                    // set value
                                    fld.TrySetValue(dataNode);
                                }

                                break;
                            }
                        }
                    }
                }
                
                // generate for each row
                foreach (DataNode row in array)
                    await GenerateDisplayOnlyFields(context, type, row, true);
                break;
            }
            case StructNode pack:
            {
                foreach (var field in type.GetFields())
                {
                    // Gets the field node
                    var fld = pack.GetAccessValue(field.Name);
                    if (fld == null) continue; // impossible
                    
                    if (field.DisplayOnly ?? false)
                    {
                        if (!fld.IsEmpty) continue; // already set value
                
                        // default for display only
                        var relation = type.GetRelations(field.Name).FirstOrDefault(r => r.ForProperty<Default>());
                        if (relation == null) continue;
                        
                        // handled by array node
                        if (joinHandled && relation.Process is CallProcess call && IsReferenceFunc(call.Func)) continue; 

                        // call function to get value
                        try
                        {
                            if (await relation.ProcessAsync(context, pack, fld) is Default { HasValue: true } result)
                                fld.TrySetValue(result.Value);
                        }
                        catch
                        {
                            // ignore errors
                        }
                    }
                    else switch (field.Type)
                    {
                        case StructType @struct:
                            await GenerateDisplayOnlyFields(context, @struct, fld);
                            break;
                        case ArrayType { Element: StructType arrayStruct }:
                            await GenerateDisplayOnlyFields(context, arrayStruct, fld);
                            break;
                        // Fill empty field with default value
                        default:
                            if (fld.IsEmpty && field.Default is { IsEmpty: false })
                                fld.TrySetValue(field.Default);
                            break;
                    }
                }
                
                break;
            }
        }
    }

    // Get scalar type mapping info
    static DataTypeInfo GetDataTypeInfo(ValueType node, StructFieldType? field = null, decimal? upLimit = null, decimal? lowLimit = null)
    {
        switch (node)
        {
            case IntType intType:
            {
                upLimit ??= field?.GetProperty<UpLimitInt>()?.Value ?? intType.GetProperty<UpLimitInt>()?.Value;
                lowLimit ??= field?.GetProperty<LowLimitInt>()?.Value ?? intType.GetProperty<LowLimitInt>()?.Value;
                
                // No Limit
                if (!upLimit.HasValue || !lowLimit.HasValue)
                {
                    return new DataTypeInfo
                    {
                        Type = DynamicTableFieldType.BigInt
                    };
                }
                // Check Range
                else if (lowLimit >= 0)
                {
                    // Unsigned
                    decimal maxVal = upLimit.Value;
                    return new DataTypeInfo
                    {
                        Type = maxVal switch
                        {
                            <= 0xffff => DynamicTableFieldType.USmallint,
                            <= 0xffffff => DynamicTableFieldType.UMediumint,
                            <= 0xffffffff => DynamicTableFieldType.UInt,
                            _ => DynamicTableFieldType.UBigInt
                        }
                    };
                }
                else
                {
                    // Signed
                    decimal maxVal = Math.Max(Math.Abs(lowLimit.Value), Math.Abs(upLimit.Value));
                    return new DataTypeInfo
                    {
                        Type = maxVal switch
                        {
                            <= 0x7fff => DynamicTableFieldType.Smallint,
                            <= 0x7fffff => DynamicTableFieldType.Mediumint,
                            <= 0x7fffffff => DynamicTableFieldType.Int,
                            _ => DynamicTableFieldType.BigInt
                        }
                    };
                }
            }
            case DecimalType:
            {
                return new DataTypeInfo
                {
                    Type = DynamicTableFieldType.Double
                };
            }
            case StringType stringType:
            {
                upLimit ??= field?.GetProperty<UpLimitString>()?.Value ?? stringType.GetProperty<UpLimitString>()?.Value;
                
                if (upLimit == 1)
                {
                    // char
                    return new DataTypeInfo
                    {
                        Type = DynamicTableFieldType.Char,
                        MaxLength = null
                    };
                }
                else
                {
                    return new DataTypeInfo
                    {
                        Type = upLimit.HasValue ? upLimit.Value switch
                        {
                            <= 0xffff => DynamicTableFieldType.VarChar,
                            <= 0xffffff => DynamicTableFieldType.MediumText,
                            _ => DynamicTableFieldType.LongText
                        } : DynamicTableFieldType.LongText,
                        MaxLength = upLimit < 0xffff ? (int)upLimit.Value : null
                    };
                }
            }
            case BoolType:
            {
                return new DataTypeInfo
                {
                    Type = DynamicTableFieldType.Bool
                };
            }
            case DateType:
            {
                return new DataTypeInfo
                {
                    Type = DynamicTableFieldType.DateTime
                };
            }
            case EnumType enumType:
            {
                return  new DataTypeInfo
                {
                    Type = enumType.Type switch
                    {
                        EnumValueType.String => DynamicTableFieldType.VarChar,
                        EnumValueType.Int => DynamicTableFieldType.BigInt,
                        EnumValueType.Flags => DynamicTableFieldType.UInt,
                        _ => throw new ArgumentOutOfRangeException()
                    },
                    MaxLength = enumType.Type == EnumValueType.String ? ENTITY_PRIMARY_KEY_MAX_LEN : null
                };
            }
            default:
            {
                return JsonDataType;
            }
        }
    }

    static readonly DataTypeInfo JsonDataType = new()
    {
        Type = DynamicTableFieldType.Json
    };
    #endregion
}


#region Dynamic Data Helpers

/// <summary>
/// The operation to modify the dynamic table values
/// </summary>
public enum TransactionChangeOperation
{
    Create,
    Modify,
    Delete,
    DropAll
}

/// <summary>
/// The field data change info
/// </summary>
internal record FieldDataChangeData(TransactionChangeOperation Operation, DataNode? Value, DataNode? Origin);

// The transaction change data
internal class TransactionChangeData
{
    /// <summary>
    /// The change operations
    /// </summary>
    public Dictionary<AppFieldType, List<FieldDataChangeData>> Changes { get; } = new();
}

/// <summary>
/// The push levels
/// </summary>
internal class FieldDataPushLevel
{
    /// <summary>
    /// The fields to be updated
    /// </summary>
    public List<AppFieldType> Fields { get; } = new();

    /// <summary>
    /// The next level to be updated
    /// </summary>
    public FieldDataPushLevel? Next { get; set; }
}

/// <summary>
/// The push argument
/// </summary>
internal struct FieldDataPushArg
{
    /// <summary>
    /// The value
    /// </summary>
    public DataNode? Value { get; set; }

    /// <summary>
    /// The origin value
    /// </summary>
    public DataNode? Origin { get; set; }

    /// <summary>
    /// Whether is array data
    /// </summary>
    public bool IsArray => Type is ArrayType;

    /// <summary>
    /// The value type
    /// </summary>
    public ValueType Type { get; set; }

    /// <summary>
    /// Whether is full data
    /// </summary>
    public bool IsFull { get; set; }

    /// <summary>
    /// Whether the data is changed
    /// </summary>
    public bool Changed { get; set; }
}

/// <summary>
/// The data type map info
/// </summary>
class DataTypeInfo
{
    /// <summary>
    /// The dynamic field type
    /// </summary>
    public DynamicTableFieldType Type { get; init; }

    /// <summary>
    /// The max length of the string type
    /// </summary>
    public int? MaxLength { get; init; }
}

/// <summary>
/// The dynamic table join info
/// </summary>
public class DynamicTableJoin
{
    /// <summary>
    /// The join field
    /// </summary>
    public string Field { get; set; } = null!;

    /// <summary>
    /// The join data field
    /// </summary>
    public Dictionary<string, AppSchemaDataFilter> Matches { get; set; } = null!;
}

/// <summary>
/// The complex field info
/// </summary>
public class DataFieldComplexInfo
{
    /// <summary>
    /// The complex main name
    /// </summary>
    public required string Main { get; init; }

    /// <summary>
    /// The complex field name
    /// </summary>
    public required string Field { get; init; }
}


#endregion

public static class DynamicTableExtension
{
    /// <summary>
    /// Gets the dynamic table schema of the app field type
    /// </summary>
    public static DynamicTableSchema GetDynamicTableSchema(this AppFieldType appFieldType, SchemaContext ctx)
    {
        if (appFieldType.GetItem<DynamicTableSchema>() is { } schema) return schema;
        schema = new DynamicTableSchema(appFieldType, ctx);
        appFieldType.SetItem(schema);
        return schema;
    }
}