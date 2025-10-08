using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Node;

/// <summary>
/// The application node
/// </summary>
public class AppNode
{
    #region Properties

    /// <summary>
    /// The application name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The display name
    /// </summary>
    public LocaleString? Display { get; set; }

    /// <summary>
    /// The description
    /// </summary>
    public LocaleString? Desc { get; set; }

    /// <summary>
    /// Standalone app without app target
    /// </summary>
    public bool? Standalone { get; set; }

    /// <summary>
    /// The application field relations
    /// </summary>
    public List<AppRelationSchema>? Relations { get; set; }

    /// <summary>
    /// The sub applications
    /// </summary>
    public AppSchema[]? Apps { get; set; }

    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }

    #endregion

    #region State

    /// <summary>
    /// The application node status
    /// </summary>
    public SchemaNodeStatus Status => Fields is { Count: > 0 } && Fields.Any(p => p.Status != SchemaNodeStatus.Ready)
        ? SchemaNodeStatus.ApplicationInvalidField
        : SchemaNodeStatus.Ready;

    /// <summary>
    /// The application is used
    /// </summary>
    public bool IsUsed => Fields is { Count: > 0 } || Apps is { Length: > 0 };

    #endregion

    #region Relationship

    /// <summary>
    /// The sub application node
    /// </summary>
    public ConcurrentDictionary<string, AppNode>? SubAppList { get; set; }

    /// <summary>
    /// The application field nodes
    /// </summary>
    public List<AppFieldNode>? Fields { get; set; }

    /// <summary>
    /// The ref field
    /// </summary>
    public AppFieldNode? RefField { get; set; }

    #endregion

    #region Methods

    /// <summary>
    /// Release usages
    /// </summary>
    public void Release()
    {
        // Release the old field relationships
        Fields?.ForEach(p =>
        {
            p.TypeNode?.RemoveRef(p);
            p.FuncNode?.RemoveRef(p);
        });
        Relations?.ForEach(r =>
        {
            if (r.FieldNode != null)
                r.FunctionNode?.RemoveRef(r.FieldNode);
        });
    }

    /// <summary>
    /// Load the data
    /// </summary>
    public async Task LoadAsync(SchemaContext context, AppSchema schema, bool preLoad = false)
    {
        // Release old usages
        Release();

        // data
        Display = schema.Display;
        Desc = schema.Desc;
        Standalone = schema.Standalone;
        Apps = schema.Apps;
        Additional = schema.Additional;

        // Load the application fields
        bool useRef = false;
        bool requireDb = false;
        Fields = schema.Fields?.Select(p => (AppFieldNode)p).ToList();
        Relations = null;
        if (Fields is { Count: > 0 })
        {
            foreach (AppFieldNode field in Fields)
            {
                field.App = Name;
                field.Status = SchemaNodeStatus.Ready;

                // Valid the type
                AnySchemaNode? node = await context.GetSchemaNodeAsync(field.Type);
                if (node == null)
                    field.Status = SchemaNodeStatus.ApplicationFieldWrongType;
                else
                {
                    node.AddRef(field);
                    field.TypeNode = node;
                }

                // Valid the function
                if (!string.IsNullOrWhiteSpace(field.Func))
                {
                    node = await context.GetSchemaNodeAsync(field.Func);
                    if (node is FunctionNode funcNode)
                    {
                        field.FuncNode = funcNode;
                        funcNode.AddRef(field);
                    }
                    else
                    {
                        field.Status = SchemaNodeStatus.ApplicationFieldWrongFunc;
                    }

                    // Checks the call Arguments
                    field.FuncArgs = new List<AppFieldNodeArgument>();
                    if (field.Args is {  Length: > 0 }){
                        foreach (string arg in field.Args)
                        {
                            string[] paths = arg.Split('.',2, StringSplitOptions.RemoveEmptyEntries);
                            AppFieldNode? tar = paths.Length > 0 
                                    ? Fields.FirstOrDefault(p => p.Name.Equals(paths[0], StringComparison.OrdinalIgnoreCase))
                                    : null;
                            if (tar == null)
                                field.Status = SchemaNodeStatus.ApplicationFieldWrongFuncField;
                            else
                            {
                                // Register to observers
                                tar.Observers ??= new List<AppFieldNode>();
                                tar.Observers.Add(field);
                                field.FuncArgs.Add(new AppFieldNodeArgument
                                {
                                    AppField = tar,
                                    DataField = paths.Length > 1 ? paths[1] : null,
                                });
                            }
                        }
                    }
                }

                // Valid source
                if (!string.IsNullOrWhiteSpace(field.SourceApp) && !string.IsNullOrWhiteSpace(field.SourceField))
                {
                    AppNode? sourceNode = await context.GetAppNodeAsync(field.SourceApp);
                    if (sourceNode == null)
                    {
                        field.Status = SchemaNodeStatus.ApplicationFieldWrongRef;
                    }
                    else
                    {
                        AppFieldNode? sourceField = sourceNode.Fields?.FirstOrDefault(f => f.Name.Equals(field.SourceField, StringComparison.OrdinalIgnoreCase));
                        if (sourceField == null)
                        {
                            field.Status = SchemaNodeStatus.ApplicationFieldWrongRef;
                        }
                        else
                        {
                            useRef = true;
                            field.SourceNode = sourceField;

                            // As external
                            if (field.FuncNode != null)
                                sourceField.IsExternal = true;
                        }
                    }
                }
                else if (!(field.Frontend ?? false) && !(field.Disable ?? false))
                {
                    requireDb = true;
                }
            }

            // Check the relations
            if (schema.Relations is { Length: > 0 })
            {
                Relations = schema.Relations.Select(r => new AppRelationSchema
                {
                    AppField = r.Field.Split(".", 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty,
                    DataField = r.Field.Contains(".") ? r.Field.Split(".", 2, StringSplitOptions.RemoveEmptyEntries)[1] : string.Empty,
                    Type = r.Type,
                    Func = r.Func,
                    Args = r.Args?.Select(a => new AppArgSchema
                    {
                        AppField = a.Name?.Split(".", 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty,
                        DataField = a.Name != null && a.Name.Contains(".") ? a.Name.Split(".", 2, StringSplitOptions.RemoveEmptyEntries)[1] : string.Empty,
                        Value = a.Value,
                    }).ToArray() ?? [],
                }).ToList();

                foreach (AppRelationSchema relation in Relations)
                {
                    AppFieldNode? field = Fields?.FirstOrDefault(f => f.Name.Equals(relation.AppField, StringComparison.OrdinalIgnoreCase));
                    if (field == null) {
                        relation.Status = SchemaNodeStatus.ApplicationRelationWrongTarget;
                        continue;
                    };
                    relation.FieldNode = field;

                    if (string.IsNullOrWhiteSpace(relation.Func))
                    {
                        relation.Status = SchemaNodeStatus.ApplicationRelationWrongFunc;
                    }
                    else
                    {
                        AnySchemaNode? relationFunc = await context.GetSchemaNodeAsync(relation.Func);
                        if (relationFunc is FunctionNode funcNode)
                        {
                            funcNode.AddRef(field);
                            relation.FunctionNode = funcNode;
                        }
                        else
                        {
                            field.Status = SchemaNodeStatus.StructRelationshipWrongFunc;
                        }
                    }
                }
            }

            // Use ref
            RefField = requireDb && useRef ? new AppFieldNode
            {
                App = Name,
                Name = APP_FIELD_REF_NAME,
                Type = APP_FIELD_REFS,
                TypeNode = new ArrayNode
                {
                    Name = APP_FIELD_REFS,
                    Element = APP_FIELD_REF,
                    Primary = new string[] { APP_FIELD_REF_APP },
                    ElementNode = new StructNode
                    {
                        Name = APP_FIELD_REF,
                        Fields = new StructFieldConfig[]
                        {
                            new ()
                            {
                                Name = APP_FIELD_REF_APP,
                                Require = true,
                                Type = NS_SYSTEM_STRING,
                                UpLimit = "128",
                                TypeNode = await context.GetSchemaNodeAsync(NS_SYSTEM_STRING),
                            },
                            new ()
                            {
                                Name = APP_FIELD_REF_TARGET,
                                Type = NS_SYSTEM_STRING,
                                UpLimit = "128",
                                TypeNode = await context.GetSchemaNodeAsync(NS_SYSTEM_STRING),
                            },
                        }
                    }
                }
            } : null;
        }

        // pre-load sub applications
        else if (preLoad && Apps is { Length: > 0 })
        {
            // Load all the sub application list
            foreach (string name in Apps.Select(p => p.Name))
                await context.GetAppNodeAsync(name, preload: true);
        }
    }

    /// <summary>
    /// Gets all node schemas used by the application
    /// </summary>
    /// <returns></returns>
    public NodeSchema[] GetNodeSchemas()
    {
        if (Fields == null || Fields.Count == 0)
            return [];

        HashSet<string> types = new();
        NodeSchema root = new NodeSchema
        {
            Name = "",
            Type = SchemaType.Namespace,
            Schemas = []
        };

        Action<AnySchemaNode?> install = null!;
        install = (AnySchemaNode? node) =>
        {
            if (node == null || !types.Add(node.Name)) return;

            // install
            string[] paths = node.Name.Split('.', StringSplitOptions.RemoveEmptyEntries);
            string fullPath = string.Empty;
            NodeSchema parent = root;
            for (int i = 0; i < paths.Length - 1; i++)
            {
                string p = paths[i];
                fullPath = string.IsNullOrWhiteSpace(fullPath) ? p : $"{fullPath}.{p}";
                
                parent.Schemas ??= [];
                NodeSchema? sub = parent.Schemas.FirstOrDefault(s => s.Name.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
                if (sub == null)
                {
                    sub = new NodeSchema
                    {
                        Name = fullPath,
                        Type = SchemaType.Namespace,
                        Schemas = []
                    };
                    parent.Schemas = parent.Schemas.Append(sub).ToArray();
                }
            }
            parent.Schemas ??= [];
            parent.Schemas = parent.Schemas.Append((NodeSchema)node!).ToArray();

            // add dependencies
            foreach (AnySchemaNode n in node.GetDependNodes())
                install(n);
        };

        foreach (AppFieldNode fieldNode in Fields)
        {
            install(fieldNode.TypeNode);
            install(fieldNode.FuncNode);
        }

        if (Relations is { Count: > 0 })
        {
            foreach (AppRelationSchema relation in Relations)
            {
                install(relation.FunctionNode);
            }
        }

        return root.Schemas;
    }

    #endregion
}

/// <summary>
/// The application field node
/// </summary>
public class AppFieldNode
{
    #region Properties

    /// <summary>
    /// The application name
    /// </summary>
    public string App { get; set; } = string.Empty;

    /// <summary>
    /// The field name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The field type.
    /// </summary>
    public string Type { get; init; } = default!;

    /// <summary>
    /// The field chinese name.
    /// </summary>
    public LocaleString? Display { get; init; }

    /// <summary>
    /// The description of the field.
    /// </summary>
    public LocaleString? Desc { get; init; }

    /// <summary>
    /// 引用模型
    /// </summary>
    public string? SourceApp { get; set; }

    /// <summary>
    /// 引用字段
    /// </summary>
    public string? SourceField { get; set; }

    /// <summary>
    /// The calculate function
    /// </summary>
    public string? Func { get; set; }

    /// <summary>
    /// The input fields
    /// </summary>
    public string[]? Args { get; set; }

    /// <summary>
    /// The field is using increase update, no full data push allowed
    /// </summary>
    public bool? IncrUpdate { get; set; }

    /// <summary>
    /// The field is front-end only, no data storage
    /// </summary>
    public bool? Frontend { get; set; }

    /// <summary>
    /// The field is disabled
    /// </summary>
    public bool? Disable { get; set; }

    /// <summary>
    /// The combine rule for scalar/enum type
    /// </summary>
    public DataCombineType? Combine { get; set; }

    /// <summary>
    /// The combine rule for struct or struct-array type
    /// </summary>
    public DataCombine[]? Combines { get; set; }

    /// <summary>
    /// The additional data
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }

    /// <summary>
    /// The data is from outside application
    /// </summary>
    public bool IsExternal { get; set; }

    /// <summary>
    /// The fields that subscribe the update of this field.
    /// </summary>
    public List<AppFieldNode>? Observers { get; set; }

    #endregion

    #region States

    /// <summary>
    /// The application field node status
    /// </summary>
    public SchemaNodeStatus Status { get; set; } = SchemaNodeStatus.Ready;

    #endregion

    #region Relationship

    /// <summary>
    /// The field type node
    /// </summary>
    public AnySchemaNode? TypeNode { get; set; }

    /// <summary>
    /// The field function node
    /// </summary>
    public FunctionNode? FuncNode { get; set; }

    /// <summary>
    /// The call arguments
    /// </summary>
    public List<AppFieldNodeArgument>? FuncArgs { get; set; }

    /// <summary>
    /// The source node
    /// </summary>
    public AppFieldNode? SourceNode { get; set; }

    #endregion

    #region Conversions

    /// <summary>
    /// Gets the field node from entity
    /// </summary>
    public static implicit operator AppFieldNode(AppFieldSchema entity)
    {
        return new AppFieldNode
        {
            Name = entity.Name,
            Type = entity.Type,
            Display = entity.Display,
            Desc = entity.Desc,
            SourceApp = entity.SourceApp,
            SourceField = entity.SourceField,
            Func = entity.Func,
            Args = entity.Args,
            IncrUpdate = entity.IncrUpdate,
            Frontend = entity.Frontend,
            Disable = entity.Disable,
            Combine = entity.Combine,
            Combines = entity.Combines,
            Additional = entity.Additional,
        };
    }

    /// <summary>
    /// Convert the node to schema
    /// </summary>
    public static implicit operator AppFieldSchema(AppFieldNode entity)
    {
        return new AppFieldSchema
        {
            Name = entity.Name,
            Type = entity.Type,
            Display = entity.Display,
            Desc = entity.Desc,
            SourceApp = entity.SourceApp,
            SourceField = entity.SourceField,
            Func = entity.Func,
            Args = entity.Args,
            IncrUpdate = entity.IncrUpdate,
            Frontend = entity.Frontend,
            Disable = entity.Disable,
            Combine = entity.Combine,
            Combines = entity.Combines,
            Additional = entity.Additional,
        };
    }

    #endregion

    #region Dynamic table

    // Gets the data field dynamic table name
    public string DynamicTableName => $"{DYNAMIC_TABLE_PREFIX}_{Regex.Replace(App, @"\W+", "_")}_{Name}";

    // Generate the dynamic table schema
    public DynamicTableSchema GenDynamicTableSchema()
    {
        // Generate the fields
        AnySchemaNode node = TypeNode!;
        List<DynamicTableField> fields = new();
        DataIndex[]? indexes = null;
        bool single = true;
        DataTypeInfo info;

        if (Frontend ?? false)
        {
            return new DynamicTableSchema
            {
                Name = DynamicTableName,
                DataType = Type,
                TypeNode = node,
                Single = true,
                Fields = fields,
            };
        }

        switch (node.Type)
        {
            case SchemaType.Scalar:
            case SchemaType.Enum:
                {
                    info = GetDataTypeInfo(node);
                    fields.Add(new DynamicTableField
                    {
                        Name = DYNAMIC_TABLE_VALUE_FIELD,
                        Type = info.Type,
                        MaxLength = info.MaxLength,
                        SchemaType = node.Name
                    });
                    break;
                }
            case SchemaType.Struct:
                {
                    StructNode structNode = (StructNode)node;
                    foreach (var sfield in structNode.Fields.Where(p => !(p.DisplayOnly ?? false)))
                    {
                        if (sfield.TypeNode?.Type == SchemaType.Struct) // Check if the sfield use a struct type
                        {
                            // As complex fields
                            StructNode subStructNode = (StructNode)sfield.TypeNode;
                            foreach (var ifield in subStructNode!.Fields.Where(p => !(p.DisplayOnly ?? false)))
                            {
                                info = GetDataTypeInfo(ifield.TypeNode!, ifield);
                                fields.Add(new DynamicTableField
                                {
                                    Name = $"{sfield.Name}{COMPLEX_SEP}{ifield.Name}",
                                    Complex = new DataFieldComplexInfo
                                    {
                                        Main = sfield.Name,
                                        Field = ifield.Name
                                    },
                                    Type = info.Type,
                                    MaxLength = info.MaxLength,
                                    SchemaType = ifield.Type
                                });
                            }
                        }
                        else
                        {
                            info = GetDataTypeInfo(sfield.TypeNode!, sfield);
                            fields.Add(new DynamicTableField
                            {
                                Name = sfield.Name,
                                Type = info.Type,
                                MaxLength = info.MaxLength,
                                SchemaType = sfield.Type
                            });
                        }
                    }
                    break;
                }
            case SchemaType.Array:
                {
                    ArrayNode arrayNode = (ArrayNode)node;
                    node = arrayNode!.ElementNode!; // Record the base node for array
                    indexes = arrayNode.Indexes;
                    if (node is StructNode structNode && arrayNode.Primary is { Length: > 0 })
                    {
                        single = false;

                        // Add primary fields
                        foreach (string n in arrayNode.Primary)
                        {
                            var sfield = structNode!.Fields.First(p => p.Name == n);
                            info = GetDataTypeInfo(sfield.TypeNode, sfield);
                            fields.Add(new DynamicTableField
                            {
                                Name = sfield.Name,
                                Type = info.Type,
                                Primary = true,
                                MaxLength = info.MaxLength,
                                SchemaType = sfield.Type,
                                StructFieldNode = sfield
                            });
                        }
                        // Add normal fields
                        foreach (var sfield in structNode!.Fields.Where(p => !arrayNode.Primary.Contains(p.Name) && !(p.DisplayOnly ?? false)))
                        {
                            // Check if the sfield use a struct type
                            if (sfield.TypeNode!.Type == SchemaType.Struct)
                            {
                                // As complex fields
                                foreach (var ifield in ((StructNode)sfield.TypeNode).Fields.Where(p => !(p.DisplayOnly ?? false)))
                                {
                                    info = GetDataTypeInfo(ifield.TypeNode!, ifield);
                                    fields.Add(new DynamicTableField
                                    {
                                        Name = $"{sfield.Name}{COMPLEX_SEP}{ifield.Name}",
                                        Complex = new DataFieldComplexInfo
                                        {
                                            Main = sfield.Name,
                                            Field = ifield.Name
                                        },
                                        Type = info.Type,
                                        MaxLength = info.MaxLength,
                                        SchemaType = ifield.Type
                                    });
                                }
                            }
                            else
                            {
                                info = GetDataTypeInfo(sfield.TypeNode, sfield);
                                fields.Add(new DynamicTableField
                                {
                                    Name = sfield.Name,
                                    Type = info.Type,
                                    MaxLength = info.MaxLength,
                                    SchemaType = sfield.Type
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
                        });
                    }
                    break;
                }
            default:
                throw new ArgumentOutOfRangeException();
        }
        return new DynamicTableSchema
        {
            Name = DynamicTableName,
            DataType = Type,
            TypeNode = node,
            Single = single,
            Fields = fields,
            Indexes = indexes
        };
    }

    // Get scalar type mapping info
    static DataTypeInfo GetDataTypeInfo(AnySchemaNode node, StructFieldConfig? field = null)
    {
        if (node is ScalarNode scalar)
        {
            // Get base scalar data type info
            DataTypeInfo info = jsonDataType;
            if (scalar.IsNumber)
            {
                decimal? upLimit = !string.IsNullOrEmpty(field?.UpLimit) && decimal.TryParse(field.UpLimit, out decimal u) ? u : scalar.UpLimit;
                decimal? lowLimit = !string.IsNullOrEmpty(field?.LowLimit) && decimal.TryParse(field.LowLimit, out decimal l) ? l : scalar.LowLimit;

                // Check Number value
                if (scalar.IsInt)
                {
                    // No Limit
                    if (!upLimit.HasValue || !lowLimit.HasValue)
                    {
                        info = new DataTypeInfo
                        {
                            Type = DynamicTableFieldType.BigInt
                        };
                    }
                    // Check Range
                    else if (lowLimit >= 0)
                    {
                        // Unsigned
                        decimal maxVal = upLimit!.Value;
                        info = new DataTypeInfo
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
                        decimal maxVal = Math.Max(Math.Abs(lowLimit!.Value), Math.Abs(upLimit!.Value));
                        info = new DataTypeInfo
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
                else if (scalar.IsSingle)
                {
                    info = new DataTypeInfo
                    {
                        Type = DynamicTableFieldType.Float
                    };
                }
                else
                {
                    info = new DataTypeInfo
                    {
                        Type = DynamicTableFieldType.Double
                    };
                }
            }
            else if (scalar.IsBool)
            {
                info = new DataTypeInfo
                {
                    Type = DynamicTableFieldType.Bool
                };
            }
            else if (scalar.IsString)
            {
                decimal? upLimit = !string.IsNullOrEmpty(field?.UpLimit) && decimal.TryParse(field.UpLimit, out decimal u) ? u : scalar.UpLimit;
                info = new DataTypeInfo
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
            else if (scalar.IsDate)
            {
                info = new DataTypeInfo
                {
                    Type = DynamicTableFieldType.DateTime
                };
            }
            return info;
        }
        else if (node is EnumNode @enum)
        {
            return new DataTypeInfo
            {
                Type = @enum.ValueType switch
                {
                    EnumValueType.String => DynamicTableFieldType.VarChar,
                    EnumValueType.Int => DynamicTableFieldType.BigInt,
                    EnumValueType.Flags => DynamicTableFieldType.UInt,
                }
            };
        }

        // Use Json for all others
        return jsonDataType;
    }

    static readonly DataTypeInfo jsonDataType = new()
    {
        Type = DynamicTableFieldType.Json
    };

    #endregion
}

#region Schema Types

/// <summary>
/// The node func arguments
/// </summary>
public class AppFieldNodeArgument
{
    /// <summary>
    /// The application field
    /// </summary>
    public AppFieldNode AppField { get; init; } = default!;

    /// <summary>
    /// The data field
    /// </summary>
    public string? DataField { get; init; } = default!;
}


/// <summary>
/// The app realtion schema
/// </summary>
public class AppRelationSchema
{
    /// <summary>
    /// The application field
    /// </summary>
    public string AppField { get; init; } = default!;

    /// <summary>
    /// The data field
    /// </summary>
    public string DataField { get; init; } = default!;

    /// <summary>
    ///  The relation type
    /// </summary>
    public RelationType Type { get; init; } = RelationType.Default;

    /// <summary>
    /// The function name
    /// </summary>
    public string Func { get; init; } = default!;

    /// <summary>
    /// The function arguments
    /// </summary>
    public AppArgSchema[] Args { get; init; } = [];

    /// <summary>
    /// The field node
    /// </summary>
    [JsonIgnore]
    public AppFieldNode? FieldNode { get; set; }

    /// <summary>
    /// The function node
    /// </summary>
    [JsonIgnore]
    public FunctionNode? FunctionNode { get; set; }

    /// <summary>
    /// The relation status
    /// </summary>
    public SchemaNodeStatus Status { get; set; } = SchemaNodeStatus.Ready;
}


/// <summary>
/// The app realtion argument
/// </summary>
public class AppArgSchema
{
    /// <summary>
    /// The application field
    /// </summary>
    public string? AppField { get; init; }

    /// <summary>
    /// The data field
    /// </summary>
    public string? DataField { get; init; }

    /// <summary>
    /// The json value
    /// </summary>
    public JsonNode? Value { get; init; }
}

#endregion

#region Dynamic Data Helpers

/// <summary>
/// The dynamic table field type
/// </summary>
public enum DynamicTableFieldType
{
    /// <summary>
    /// The bool field type
    /// </summary>
    Bool,

    /// <summary>
    /// The small int field type, (-32768，32767)
    /// </summary>
    Smallint,

    /// <summary>
    /// The unsigned small int field type, (0，65535)
    /// </summary>
    USmallint,

    /// <summary>
    /// The medium int field type, 	(-8388608，8388607)
    /// </summary>
    Mediumint,

    /// <summary>
    /// The unsigned medium int field type, (0，16777215)
    /// </summary>
    UMediumint,

    /// <summary>
    /// The int field type, (-2147483648，2147483647)
    /// </summary>
    Int,

    /// <summary>
    /// The unsigned int field type, (0，4294967295)
    /// </summary>
    UInt,

    /// <summary>
    /// The big int field type, (-9,223,372,036,854,775,808，9223372036854775807)
    /// </summary>
    BigInt,

    /// <summary>
    /// The unsigned big int field type, (0，18446744073709551615)
    /// </summary>
    UBigInt,

    /// <summary>
    /// The float field type
    /// </summary>
    Float,

    /// <summary>
    /// The float field type
    /// </summary>
    Double,

    /// <summary>
    /// The date time type
    /// </summary>
    DateTime,

    /// <summary>
    /// The tiny binary string (0, 255)
    /// </summary>
    TinyBlob,

    /// <summary>
    /// The binary string (0, 65535)
    /// </summary>
    Blob,

    /// <summary>
    /// The medium blob string (0, 16777215)
    /// </summary>
    MediumBlob,

    /// <summary>
    /// The long blob string (0, 4294967295)
    /// </summary>
    LongBlob,

    /// <summary>
    /// The fix-length string (0, 255)
    /// </summary>
    Char,

    /// <summary>
    /// The variable-length string (0, 65535)
    /// </summary>
    VarChar,

    /// <summary>
    /// The short text (0, 255)
    /// </summary>
    TinyText,

    /// <summary>
    /// The text string (0, 65535)
    /// </summary>
    Text,

    /// <summary>
    /// The medium text (0, 16777215)
    /// </summary>
    MediumText,

    /// <summary>
    /// The long text (0, 4294967295)
    /// </summary>
    LongText,

    /// <summary>
    /// The json field type
    /// </summary>
    Json,
}

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
/// The dynamic table structure
/// </summary>
public class DynamicTableSchema
{
    /// <summary>
    /// The dynamic table name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The data type name
    /// </summary>
    public required string DataType { get; init; }

    /// <summary>
    /// Whether the table is single row
    /// </summary>
    public bool Single { get; init; }

    /// <summary>
    /// The dynamic table fields
    /// </summary>
    public List<DynamicTableField> Fields { get; init; } = [];

    /// <summary>
    /// The dynamic table indexes
    /// </summary>
    public DataIndex[]? Indexes { get; init; } = [];

    /// <summary>
    /// The data type node
    /// </summary>
    public required AnySchemaNode TypeNode { get; init; }

    /// <summary>
    /// Append the fields to the string builder
    /// </summary>
    public StringBuilder AppendFields(StringBuilder sb)
    {
        bool appendComma = false;
        foreach (DynamicTableField dyfld in Fields)
        {
            if (appendComma)
                sb.Append(", ");
            appendComma = true;
            sb.Append($"`{dyfld.Name}`");
        }
        return sb;
    }

    /// <summary>
    /// Gets the field values by the fields
    /// </summary>
    public IEnumerable<(string, string?, bool, bool)> GetFieldValues(JsonObject pack, bool primaryOnly = false, bool noPrimary = false)
    {
        IEnumerable<DynamicTableField> fields = Fields;
        if (primaryOnly) fields = Fields.Where(p => p.Primary);
        else if (noPrimary) fields = Fields.Where(p => !p.Primary);
        foreach (DynamicTableField field in fields)
        {
            if (field.Complex == null)
            {
                if (pack.ContainsKey(field.Name) && !pack[field.Name].IsEmpty())
                {
                    // In value list
                    if (field.Type != DynamicTableFieldType.Json && pack[field.Name] is JsonArray arr)
                    {
                        if (arr.Count > 1)
                        {
                            if (field.IsString)
                            {
                                yield return (field.Name, $"({(string.Join(",", arr.Select(v => $"\"{field.ToString(v)}\"")))})", field.IsString, true);
                            }
                            else
                            {
                                yield return (field.Name, $"({(string.Join(",", arr.Select(v => field.ToString(v))))})", field.IsString, true);
                            }
                        }
                        else
                        {
                            yield return (field.Name, field.ToString(arr[0]), field.IsString, false);
                        }
                    }
                    // Single value
                    else
                    {
                        yield return (field.Name, field.ToString(pack[field.Name]), field.IsString, false);
                    }
                }
                else
                {
                    yield return (field.Name, null, field.IsString, false);
                }
            }
            else if (pack.ContainsKey(field.Complex.Main) && pack[field.Complex.Main] is JsonObject spack && spack.ContainsKey(field.Complex.Field) && !spack[field.Complex.Field].IsEmpty())
            {
                // In value list
                if (field.Type != DynamicTableFieldType.Json && spack[field.Complex.Field] is JsonArray arr)
                {
                    if (arr.Count > 1)
                    {
                        if (field.IsString)
                        {
                            yield return (field.Name, $"({(string.Join(",", arr.Select(v => $"\"{field.ToString(v)}\"")))})", field.IsString, true);
                        }
                        else
                        {
                            yield return (field.Name, $"({(string.Join(",", arr.Select(v => field.ToString(v))))})", field.IsString, true);
                        }
                    }
                    else
                    {
                        yield return (field.Name, field.ToString(arr[0]), field.IsString, false);
                    }
                }
                // Single value
                {
                    yield return (field.Name, field.ToString(spack[field.Complex.Field]), field.IsString, false);
                }
            }
            else
            {
                yield return (field.Name, null, field.IsString, false);
            }
        }
    }

    /// <summary>
    /// Gets the field data pack from the reader
    /// </summary>
    public JsonObject GetFieldPack(DbDataReader reader, int offset = 0)
    {
        JsonObject pack = new();
        foreach (DynamicTableField field in Fields)
        {
            JsonNode? val = field.FromReader(reader, offset++);
            if (val == null) continue;
            if (field.Complex == null)
            {
                pack.Add(field.Name, val);
            }
            else
            {
                if (pack.ContainsKey(field.Complex.Main) && pack[field.Complex.Main] is JsonObject spack)
                {
                    spack.Add(field.Complex.Field, val);
                }
                else
                {
                    spack = new JsonObject()
                    {
                        { field.Complex.Field, val }
                    };
                    pack.Add(field.Complex.Main, spack);
                }
            }
        }

        return pack;
    }

    /// <summary>
    /// Generate display only fields
    /// </summary>
    public Task GenerateDisplayOnlyFields(SchemaContext context, JsonNode pack)
    {
        // Generate the display only fields
        if (TypeNode is StructNode @struct && pack is JsonObject jobj)
            return GenerateDisplayOnlyStructFields(context, @struct, jobj);
        return Task.CompletedTask;
    }

    #region Utility

    /// <summary>
    /// Whether the json token is the same
    /// </summary>

    /// <summary>
    /// Whether the json token is the same
    /// </summary>
    public bool IsSameToken(JsonNode? origin, JsonNode? value)
    {
        // Check empty
        if (origin.IsEmpty() || value.IsEmpty())
            return origin.IsEmpty() && value.IsEmpty();

        // Compare the value
        switch (origin)
        {
            case JsonValue oval when value is JsonValue val:
                return oval.Equals(val);
            case JsonObject obj when value is JsonObject vobj:
                {
                    foreach (DynamicTableField dyfld in Fields)
                    {
                        if (dyfld.Complex == null)
                        {
                            if (!IsSameToken(obj.ContainsKey(dyfld.Name) ? obj[dyfld.Name] : null, vobj.ContainsKey(dyfld.Name) ? vobj[dyfld.Name] : null))
                                return false;
                        }
                        else
                        {
                            JsonNode? left = obj.ContainsKey(dyfld.Complex.Main) ? obj[dyfld.Complex.Main] : null;
                            JsonNode? right = vobj.ContainsKey(dyfld.Complex.Main) ? vobj[dyfld.Complex.Main] : null;
                            if (left != null && right != null && left is JsonObject lobj && right is JsonObject robj)
                            {
                                if (!IsSameToken(lobj.ContainsKey(dyfld.Complex.Field) ? lobj[dyfld.Complex.Field] : null, robj.ContainsKey(dyfld.Complex.Field) ? robj[dyfld.Complex.Field] : null))
                                    return false;
                            }
                            else if (left != null || right != null)
                            {
                                return false;
                            }
                        }
                    }
                    return true;
                }
            case JsonArray oarr when value is JsonArray varr:
                {
                    if (oarr.Count != varr.Count)
                        return false;
                    return !oarr.Where((t, i) => !IsSameToken(t, varr[i])).Any();
                }
            default:
                return false;
        }
    }


    // Generate the display only fields
    static async Task GenerateDisplayOnlyStructFields(SchemaContext context, StructNode node, JsonObject pack)
    {
        if (node.Fields == null || node.Fields.Length == 0) return;
        foreach (var field in node.Fields)
        {
            if (field.DisplayOnly ?? false)
            {
                var relation = node.Relations?.FirstOrDefault(f => f.Field.Equals(field.Name, StringComparison.OrdinalIgnoreCase) && f.Type == RelationType.Default);
                if (relation == null) continue;

                JsonArray args = new();
                foreach (var arg in relation.Args)
                {
                    args.Add(!string.IsNullOrWhiteSpace(arg.Name) ? pack.GetValueByPaths(arg.Name) : arg.Value);
                }
                JsonNode? result = await context.CallFunctionAsync(relation.Func, args);
                if (!result.IsEmpty()) pack[field.Name] = result;
            }
            else if (field.TypeNode is StructNode @struct && pack.ContainsKey(field.Name) && pack[field.Name] is JsonObject spack)
            {
                await GenerateDisplayOnlyStructFields(context, @struct, spack);
            }
            else if (field.TypeNode is ArrayNode { ElementNode: StructNode arrayStruct } && pack.ContainsKey(field.Name) && pack[field.Name] is JsonArray { Count: > 0 } arrayPack)
            {
                foreach (JsonNode? token in arrayPack)
                {
                    if (token is JsonObject apack)
                        await GenerateDisplayOnlyStructFields(context, arrayStruct, apack);
                }
            }
            // Fill empty field with default value
            else if (field.TypeNode is ScalarNode scalar && !string.IsNullOrWhiteSpace(field.Default) && (!pack.ContainsKey(field.Name) || pack[field.Name].IsEmpty()))
            {
                (object? val, JsonNode? err) = await scalar.ValidateValueAsync(context, field.Default);
                if (err == null || err.IsEmpty())
                    pack[field.Name] = JsonValue.Create(val);
            }
        }
    }

    #endregion
}

/// <summary>
/// The dynamic table field info
/// </summary>
public class DynamicTableField
{
    /// <summary>
    /// The field name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The dynamic field type
    /// </summary>
    public DynamicTableFieldType Type { get; init; }

    /// <summary>
    /// The complex field info
    /// </summary>
    public DataFieldComplexInfo? Complex { get; init; }

    /// <summary>
    /// Whether the field is primary
    /// </summary>
    public bool Primary { get; init; }

    /// <summary>
    /// The max length of the string type
    /// </summary>
    public int? MaxLength { get; init; }

    /// <summary>
    /// The data dict type
    /// </summary>
    public string SchemaType { get; init; } = default!;

    /// <summary>
    /// The struct field node of primary field
    /// </summary>
    public StructFieldConfig? StructFieldNode { get; init; }

    /// <summary>
    /// The mysql data type
    /// </summary>
    public string DataType => Type switch
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
        DynamicTableFieldType.VarChar => $"VARCHAR({MaxLength!.Value})",
        DynamicTableFieldType.TinyText => "TINYTEXT",
        DynamicTableFieldType.Text => "TEXT",
        DynamicTableFieldType.MediumText => "MEDIUMTEXT",
        DynamicTableFieldType.LongText => "LONGTEXT",
        _ => throw new ArgumentOutOfRangeException()
    };

    /// <summary>
    /// Whether the type is string data
    /// </summary>
    public bool IsString => Type switch
    {
        DynamicTableFieldType.Bool => false,
        DynamicTableFieldType.Smallint => false,
        DynamicTableFieldType.USmallint => false,
        DynamicTableFieldType.Mediumint => false,
        DynamicTableFieldType.UMediumint => false,
        DynamicTableFieldType.Int => false,
        DynamicTableFieldType.UInt => false,
        DynamicTableFieldType.BigInt => false,
        DynamicTableFieldType.UBigInt => false,
        DynamicTableFieldType.Float => false,
        DynamicTableFieldType.Double => false,
        DynamicTableFieldType.DateTime => true,
        _ => true
    };

    /// <summary>
    /// Get JToken from reader
    /// </summary>
    public JsonNode? FromReader(DbDataReader reader, int col = 0)
    {
        if (reader.IsDBNull(col)) return null;
        return Type switch
        {
            DynamicTableFieldType.Bool => reader.GetByte(col) == 1,
            DynamicTableFieldType.Smallint => reader.GetInt16(col),
            DynamicTableFieldType.USmallint => reader.GetInt32(col),
            DynamicTableFieldType.Mediumint => reader.GetInt32(col),
            DynamicTableFieldType.UMediumint => reader.GetInt32(col),
            DynamicTableFieldType.Int => reader.GetInt32(col),
            DynamicTableFieldType.UInt => reader.GetInt64(col),
            DynamicTableFieldType.BigInt => reader.GetInt64(col),
            DynamicTableFieldType.UBigInt => reader.GetInt64(col),
            DynamicTableFieldType.Float => reader.GetFloat(col),
            DynamicTableFieldType.Double => reader.GetDouble(col),
            DynamicTableFieldType.DateTime => reader.GetDateTime(col),
            DynamicTableFieldType.Json => JsonNode.Parse(reader.GetString(col)),
            _ => JsonValue.Create(reader.GetString(col))
        };
    }

    /// <summary>
    /// Gets the string of the JToken value
    /// </summary>
    public string? ToString(JsonNode? value)
    {
        if (value.IsEmpty()) return null;
        return Type switch
        {
            DynamicTableFieldType.Bool => value.GetValue<bool>() ? "1" : "0",
            DynamicTableFieldType.Smallint => value.GetValue<short>().ToString(),
            DynamicTableFieldType.USmallint => value.GetValue<ushort>().ToString(),
            DynamicTableFieldType.Mediumint => value.GetValue<int>().ToString(),
            DynamicTableFieldType.UMediumint => value.GetValue<uint>().ToString(),
            DynamicTableFieldType.Int => value.GetValue<int>().ToString(),
            DynamicTableFieldType.UInt => value.GetValue<uint>().ToString(),
            DynamicTableFieldType.BigInt => value.GetValue<long>().ToString(),
            DynamicTableFieldType.UBigInt => value.GetValue<ulong>().ToString(),
            DynamicTableFieldType.Float => value.GetValue<float>().ToString(CultureInfo.InvariantCulture),
            DynamicTableFieldType.Double => value.GetValue<double>().ToString(CultureInfo.InvariantCulture),
            DynamicTableFieldType.DateTime => value.GetValue<DateTime>().ToString("yyyy-MM-dd HH:mm:ss"),
            DynamicTableFieldType.Json => value.ToString(),
            _ => value!.GetValue<string>()
        };
    }
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

/// <summary>
/// The data type map info
/// </summary>
public class DataTypeInfo
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
/// The field data change info
/// </summary>
public record FieldDataChangeData(TransactionChangeOperation Operation, JsonNode Value, JsonNode Origin);

// The transaction change data
public class TransactionChangeData
{
    /// <summary>
    /// The change operations
    /// </summary>
    public Dictionary<AppFieldNode, List<FieldDataChangeData>> Changes { get; } = new();
}

/// <summary>
/// The push levels
/// </summary>
public class FieldDataPushLevel
{
    /// <summary>
    /// The fields to be updated
    /// </summary>
    public List<AppFieldNode> Fields { get; } = new();

    /// <summary>
    /// The next level to be updated
    /// </summary>
    public FieldDataPushLevel? Next { get; set; }
}

/// <summary>
/// The push argument
/// </summary>
public struct FieldDataPushArg
{
    /// <summary>
    /// The value
    /// </summary>
    public JsonNode Value { get; set; }

    /// <summary>
    /// The origin value
    /// </summary>
    public JsonNode Origin { get; set; }

    /// <summary>
    /// Whether is array data
    /// </summary>
    public bool IsArray => Type is ArrayNode;

    /// <summary>
    /// The value type
    /// </summary>
    public AnySchemaNode Type { get; set; }

    /// <summary>
    /// Whether is full data
    /// </summary>
    public bool IsFull { get; set; }

    /// <summary>
    /// Whether the data is changed
    /// </summary>
    public bool Changed { get; set; }
}

#endregion