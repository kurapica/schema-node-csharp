using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SchemaNode.Components.Provider;
using static SchemaNode.Utility.Constant;
using SchemaNode.Runtime;

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
                AnySchemeType? node = await context.GetSchemaNodeAsync(field.Type);
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
                    if (node is FunctionType funcNode)
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
                
                if (field.EnableDynamicTable)
                    requireDb = true;
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
                    Args = r.Args.Select(a => new AppArgSchema
                    {
                        AppField = a.Name?.Split(".", 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty,
                        DataField = a.Name != null && a.Name.Contains(".") ? a.Name.Split(".", 2, StringSplitOptions.RemoveEmptyEntries)[1] : string.Empty,
                        Value = a.Value,
                    }).ToArray(),
                }).ToList();

                foreach (AppRelationSchema relation in Relations)
                {
                    AppFieldNode? field = Fields?.FirstOrDefault(f => f.Name.Equals(relation.AppField, StringComparison.OrdinalIgnoreCase));
                    if (field == null) {
                        relation.Status = SchemaNodeStatus.ApplicationRelationWrongTarget;
                        continue;
                    }
                    relation.FieldNode = field;

                    if (string.IsNullOrWhiteSpace(relation.Func))
                    {
                        relation.Status = SchemaNodeStatus.ApplicationRelationWrongFunc;
                    }
                    else
                    {
                        AnySchemeType? relationFunc = await context.GetSchemaNodeAsync(relation.Func);
                        if (relationFunc is FunctionType funcNode)
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
                TypeNode = new ArrayType
                {
                    Name = APP_FIELD_REFS,
                    Element = APP_FIELD_REF,
                    Primary = [APP_FIELD_REF_APP],
                    ElementNode = new StructType
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
    /// Gets the app field by name
    /// </summary>
    public AppFieldNode? GetField(string name)
    {
        return Fields?.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Gets all node schemas used by the application
    /// </summary>
    /// <returns></returns>
    public NodeSchema[] GetNodeSchemas(NodeSchema? root = null)
    {
        if (Fields == null || Fields.Count == 0)
            return [];

        HashSet<string> types = new();
        root ??= new NodeSchema
        {
            Name = "",
            Type = SchemaType.Namespace,
            Schemas = []
        };

        Action<AnySchemeType?> install = null!;
        install = (node) =>
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
                parent = sub;
            }
            parent.Schemas ??= [];
            parent.Schemas = parent.Schemas.Append((NodeSchema)node!).ToArray();

            // add dependencies
            foreach (AnySchemeType n in node.GetDependNodes())
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

        return root.Schemas!;
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
    /// The seqno
    /// </summary>
    public int Seqno { get; set; } = 0;

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
    /// The source application
    /// </summary>
    public string? SourceApp { get; set; }

    /// <summary>
    /// The source field
    /// </summary>
    public string? SourceField { get; set; }

    /// <summary>
    /// Track the push data to the source field, so toggle the source target, will also re-push the data
    /// </summary>
    public bool? TrackPush { get; set; }
    
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

    /// <summary>
    /// Enable dynamic table
    /// </summary>
    public bool EnableDynamicTable => !(Frontend ?? false) && !(Disable ?? false) && (SourceNode == null || TrackPush == true && FuncNode != null);

   
    /// <summary>
    /// The data is queryable
    /// </summary>
    public bool IsQueryable => !(Frontend ?? false) && !(Disable ?? false) && (SourceNode == null || FuncNode == null);

    #endregion

    #region Relationship

    /// <summary>
    /// The field type node
    /// </summary>
    public AnySchemeType? TypeNode { get; set; }

    /// <summary>
    /// The field function node
    /// </summary>
    public FunctionType? FuncNode { get; set; }

    /// <summary>
    /// The call arguments
    /// </summary>
    public List<AppFieldNodeArgument>? FuncArgs { get; set; }

    /// <summary>
    /// The source node
    /// </summary>
    public AppFieldNode? SourceNode { get; set; }
    
    /// <summary>
    /// The dynamic table schema
    /// </summary>
    public DynamicTableSchema? Schema { get; set; }

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
            Seqno = entity.Seqno,
            Display = entity.Display,
            Desc = entity.Desc,
            SourceApp = entity.SourceApp,
            SourceField = entity.SourceField,
            TrackPush = entity.TrackPush,
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
            Seqno = entity.Seqno,
            Display = entity.Display,
            Desc = entity.Desc,
            SourceApp = entity.SourceApp,
            SourceField = entity.SourceField,
            TrackPush = entity.TrackPush,
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
        AnySchemeType node = TypeNode!;
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
                        SchemaType = node
                    });
                    break;
                }
            case SchemaType.Struct:
                {
                    StructType structNode = (StructType)node;
                    foreach (var sField in structNode.Fields.Where(p => !(p.DisplayOnly ?? false)))
                    {
                        if (sField.TypeNode?.Type == SchemaType.Struct) // Check if the sfield use a struct type
                        {
                            // As complex fields
                            StructType subStructNode = (StructType)sField.TypeNode;
                            foreach (var iField in subStructNode.Fields.Where(p => !(p.DisplayOnly ?? false)))
                            {
                                info = GetDataTypeInfo(iField.TypeNode!, iField);
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
                                    SchemaType = iField.TypeNode!
                                });
                            }
                        }
                        else
                        {
                            info = GetDataTypeInfo(sField.TypeNode!, sField);
                            fields.Add(new DynamicTableField
                            {
                                Name = sField.Name,
                                Type = info.Type,
                                MaxLength = info.MaxLength,
                                SchemaType = sField.TypeNode!
                            });
                        }
                    }
                    break;
                }
            case SchemaType.Array:
                {
                    ArrayType arrayNode = (ArrayType)node;
                    node = arrayNode.ElementNode!; // Record the base node for array
                    indexes = arrayNode.Indexes;
                    if (node is StructType structNode && arrayNode.Primary is { Length: > 0 })
                    {
                        single = false;

                        // Add primary fields
                        foreach (string n in arrayNode.Primary)
                        {
                            var sField = structNode.Fields.First(p => p.Name == n);
                            info = GetDataTypeInfo(sField.TypeNode!, sField);
                            fields.Add(new DynamicTableField
                            {
                                Name = sField.Name,
                                Type = info.Type,
                                Primary = true,
                                MaxLength = info.MaxLength,
                                SchemaType = sField.TypeNode!,
                                StructFieldNode = sField,
                            });
                        }
                        // Add normal fields
                        foreach (var sField in structNode.Fields.Where(p => !arrayNode.Primary.Contains(p.Name) && !(p.DisplayOnly ?? false)))
                        {
                            // Check if the sfield use a struct type
                            if (sField.TypeNode!.Type == SchemaType.Struct)
                            {
                                // As complex fields
                                foreach (var ifield in ((StructType)sField.TypeNode).Fields.Where(p => !(p.DisplayOnly ?? false)))
                                {
                                    info = GetDataTypeInfo(ifield.TypeNode!, ifield);
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
                                        SchemaType = ifield.TypeNode!
                                    });
                                }
                            }
                            else
                            {
                                info = GetDataTypeInfo(sField.TypeNode, sField);
                                fields.Add(new DynamicTableField
                                {
                                    Name = sField.Name,
                                    Type = info.Type,
                                    MaxLength = info.MaxLength,
                                    SchemaType = sField.TypeNode!
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
                            SchemaType = node
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
            Indexes = indexes,
            IncrUpdate = IncrUpdate ?? false,
        };
    }

    // Get scalar type mapping info
    static DataTypeInfo GetDataTypeInfo(AnySchemeType node, StructFieldConfig? field = null)
    {
        if (node is ScalarType scalar)
        {
            // Get base scalar data type info
            DataTypeInfo info = JsonDataType;
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
                        decimal maxVal = upLimit.Value;
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
                        decimal maxVal = Math.Max(Math.Abs(lowLimit.Value), Math.Abs(upLimit.Value));
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
        else if (node is EnumType @enum)
        {
            return new DataTypeInfo
            {
                Type = @enum.ValueType switch
                {
                    EnumValueType.String => DynamicTableFieldType.VarChar,
                    EnumValueType.Int => DynamicTableFieldType.BigInt,
                    EnumValueType.Flags => DynamicTableFieldType.UInt,
                    _ => throw new ArgumentOutOfRangeException()
                }
            };
        }

        // Use Json for all others
        return JsonDataType;
    }

    static readonly DataTypeInfo JsonDataType = new()
    {
        Type = DynamicTableFieldType.Json
    };

    /// <summary>
    /// Validate field by DynamicTableSchema and return nullable
    /// </summary>
    public async Task<(bool isEmpty, AnySchemaNode? result, JsonNode? error)> ValidateDataAsync(SchemaContext context, JsonNode? token)
    {
        if (token.IsEmpty()) return (true, null, null);
        
        (AnySchemaNode? value, JsonNode? error) = await TypeNode!.ValidateValueAsync(context, token!);
        return (false, value, error);
    }

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
    public required AppFieldNode AppField { get; init; }

    /// <summary>
    /// The data field
    /// </summary>
    public string? DataField { get; init; }
}


/// <summary>
/// The app relation schema
/// </summary>
public class AppRelationSchema
{
    /// <summary>
    /// The application field
    /// </summary>
    public required string AppField { get; init; }

    /// <summary>
    /// The data field
    /// </summary>
    public string DataField { get; init; } = string.Empty;

    /// <summary>
    ///  The relation type
    /// </summary>
    public RelationType Type { get; init; } = RelationType.Default;

    /// <summary>
    /// The function name
    /// </summary>
    public required string Func { get; init; }

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
    public FunctionType? FunctionNode { get; set; }

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
    /// The small int field type, (-32768��32767)
    /// </summary>
    Smallint,

    /// <summary>
    /// The unsigned small int field type, (0��65535)
    /// </summary>
    USmallint,

    /// <summary>
    /// The medium int field type, 	(-8388608��8388607)
    /// </summary>
    Mediumint,

    /// <summary>
    /// The unsigned medium int field type, (0��16777215)
    /// </summary>
    UMediumint,

    /// <summary>
    /// The int field type, (-2147483648��2147483647)
    /// </summary>
    Int,

    /// <summary>
    /// The unsigned int field type, (0��4294967295)
    /// </summary>
    UInt,

    /// <summary>
    /// The big int field type, (-9,223,372,036,854,775,808��9223372036854775807)
    /// </summary>
    BigInt,

    /// <summary>
    /// The unsigned big int field type, (0��18446744073709551615)
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
    /// Whether the table use increase update, no full data push allowed
    /// </summary>
    public bool IncrUpdate { get; init; }

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
    public required AnySchemeType TypeNode { get; init; }

    /// <summary>
    /// Append the fields to the string builder
    /// </summary>
    public StringBuilder AppendFields(StringBuilder sb, string prefix="")
    {
        bool appendComma = false;
        foreach (DynamicTableField dyfld in Fields)
        {
            if (appendComma)
                sb.Append(", ");
            appendComma = true;
            sb.Append($"{prefix}`{dyfld.Name}`");
        }
        return sb;
    }

    /// <summary>
    /// Gets the field values by the fields
    /// </summary>
    public IEnumerable<(string field, string? value, bool isString, bool isList)> GetFieldValues(JsonObject pack, bool primaryOnly = false, bool noPrimary = false)
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
                                yield return (field.Name, $"({string.Join(",", arr.Select(v => $"\"{field.ToString(v)}\""))})", true, true);
                            }
                            else
                            {
                                yield return (field.Name, $"({string.Join(",", arr.Select(v => field.ToString(v)))})", false, true);
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
            else if (pack.ContainsKey(field.Complex.Main) && pack[field.Complex.Main] is JsonObject sPack && sPack.ContainsKey(field.Complex.Field) && !sPack[field.Complex.Field].IsEmpty())
            {
                // In value list
                if (field.Type != DynamicTableFieldType.Json && sPack[field.Complex.Field] is JsonArray arr)
                {
                    if (arr.Count > 1)
                    {
                        if (field.IsString)
                        {
                            yield return (field.Name, $"({string.Join(",", arr.Select(v => $"\"{field.ToString(v)}\""))})", field.IsString, true);
                        }
                        else
                        {
                            yield return (field.Name, $"({string.Join(",", arr.Select(v => field.ToString(v)))})", field.IsString, true);
                        }
                    }
                    else
                    {
                        yield return (field.Name, field.ToString(arr[0]), field.IsString, false);
                    }
                }
                // Single value
                {
                    yield return (field.Name, field.ToString(sPack[field.Complex.Field]), field.IsString, false);
                }
            }
            else
            {
                yield return (field.Name, null, field.IsString, false);
            }
        }
    }

    public IEnumerable<(string field, string? value, bool isString, bool isList)> GetFieldValues(StructNode pack, bool primaryOnly = false, bool noPrimary = false)
    {
        IEnumerable<DynamicTableField> fields = Fields;
        if (primaryOnly) fields = Fields.Where(p => p.Primary);
        else if (noPrimary) fields = Fields.Where(p => !p.Primary);
        foreach (DynamicTableField field in fields)
        {
            if (field.Complex == null)
            {
                AnySchemaNode? fieldNode = pack.GetField(field.Name);
                if (fieldNode != null && !fieldNode.IsEmpty)
                {
                    // In value list
                    if (field.Type != DynamicTableFieldType.Json && fieldNode is ArrayNode arr)
                    {
                        if (arr.Count > 1)
                        {
                            if (field.IsString)
                            {
                                yield return (field.Name, $"({string.Join(",", arr.Select(v => $"\"{field.ToString(v)}\""))})", true, true);
                            }
                            else
                            {
                                yield return (field.Name, $"({string.Join(",", arr.Select(v => field.ToString(v)))})", false, true);
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
                        yield return (field.Name, field.ToString(fieldNode), field.IsString, false);
                    }
                }
                else
                {
                    yield return (field.Name, null, field.IsString, false);
                }
            }
            else
            {
                AnySchemaNode? complex = pack.GetField(field.Complex.Main);
                if (complex is StructNode sPack && sPack.GetField(field.Complex.Field) is { IsEmpty: false } part)
                {
                    // In value list
                    if (field.Type != DynamicTableFieldType.Json && part is ArrayNode arr)
                    {
                        if (arr.Count > 1)
                        {
                            if (field.IsString)
                            {
                                yield return (field.Name, $"({string.Join(",", arr.Select(v => $"\"{field.ToString(v)}\""))})", field.IsString, true);
                            }
                            else
                            {
                                yield return (field.Name, $"({string.Join(",", arr.Select(v => field.ToString(v)))})", field.IsString, true);
                            }
                        }
                        else
                        {
                            yield return (field.Name, field.ToString(arr[0]), field.IsString, false);
                        }
                    }
                    // Single value
                    {
                        yield return (field.Name, field.ToString(part), field.IsString, false);
                    }
                }
                else
                {
                    yield return (field.Name, null, field.IsString, false);
                }
            }
        }
    }

    /// <summary>
    /// Gets the field data pack from the reader
    /// </summary>
    public AnySchemaNode? GetFieldPack(DbDataReader reader, int offset = 0)
    {
        // single value
        if (Fields.Count == 1 && Fields[0].SchemaType == TypeNode)
        {
            return Fields[0].FromReader(reader, offset);
        }

        StructNode result = new StructNode((StructType)(TypeNode is ArrayType arr ? arr.ElementNode : TypeNode)!);
        foreach (DynamicTableField field in Fields)
        {
            AnySchemaNode? val = field.FromReader(reader, offset++);
            if (val == null) continue;
            if (field.Complex == null)
            {
                result.SetField(field.Name, val);
            }
            else
            {
                AnySchemaNode? main = result.GetField(field.Complex.Main);
                if (main == null)
                {
                    main = new StructNode((StructType)((StructType)TypeNode).Fields.First(f => f.Name == field.Complex.Main).TypeNode!);
                    result.SetField(field.Complex.Main, main);
                }
                (main as StructNode)![field.Complex.Field] = val;
            }
        }

        return result;
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
    public async Task GenerateDisplayOnlyFields(SchemaContext context, AnySchemaNode? pack)
    {
        // Generate the display only fields
        if (TypeNode is StructType @struct)
        {
            if (pack is StructNode obj)
                await GenerateDisplayOnlyStructFields(context, @struct, obj);
            else if (pack is ArrayNode arr)
            {
                foreach (AnySchemaNode? item in arr)
                {
                    if (item is StructNode aObj)
                        await GenerateDisplayOnlyStructFields(context, @struct, aObj);
                }
            }
        }
    }

    #region Utility

    // Generate the display only fields
    static async Task GenerateDisplayOnlyStructFields(SchemaContext context, StructType node, StructNode pack)
    {
        if (node.Fields.Length == 0) return;
        foreach (var field in node.Fields)
        {
            if (field.DisplayOnly ?? false)
            {
                var relation = node.Relations?.FirstOrDefault(f => f.Field.Equals(field.Name, StringComparison.OrdinalIgnoreCase) && f.Type == RelationType.Default);
                if (relation == null) continue;

                JsonArray args = new();
                foreach (var arg in relation.Args)
                {
                    args.Add(!string.IsNullOrWhiteSpace(arg.Name) ? pack.GetValueByPaths(arg.Name)?.ToJson() : arg.Value);
                }
                JsonNode? result = await context.CallFunctionAsync(relation.Func, args);
                if (!result.IsEmpty()) pack[field.Name] = result;
            }
            else if (field.TypeNode is StructType @struct && pack.GetField(field.Name) is StructNode spack)
            {
                await GenerateDisplayOnlyStructFields(context, @struct, spack);
            }
            else if (field.TypeNode is ArrayType { ElementNode: StructType arrayStruct } && pack.GetField(field.Name) is ArrayNode { Count: > 0 } arrayPack)
            {
                foreach (var token in arrayPack)
                {
                    if (token is StructNode apack)
                        await GenerateDisplayOnlyStructFields(context, arrayStruct, apack);
                }
            }
            // Fill empty field with default value
            else if (field.TypeNode is ScalarType scalar && !string.IsNullOrWhiteSpace(field.Default) && (pack.GetField(field.Name)?.IsEmpty ?? false))
            {
                (AnySchemaNode? val, JsonNode? err) = await scalar.ValidateValueAsync(context, field.Default);
                if (err == null || err.IsEmpty())
                    pack[field.Name] = val;
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
    public required AnySchemeType SchemaType { get; init; }

    /// <summary>
    /// The struct field node of primary field
    /// </summary>
    public StructFieldConfig? StructFieldNode { get; init; }

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
    public AnySchemaNode? FromReader(DbDataReader reader, int col = 0)
    {
        if (reader.IsDBNull(col)) return null;
        object? data;
        if (Type == DynamicTableFieldType.Json)
        {
            data = JsonNode.Parse(reader.GetString(col));
        }
        else
        {
            data = (Type switch
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
                _ => reader.GetString(col)
            });
        }

        return SchemaType.CreateNode(data);
    }

    /// <summary>
    /// Gets the string of the JToken value
    /// </summary>
    public string? ToString(AnySchemaNode? value)
    {
        if (value?._value == null) return null;

        return Type switch
        {
            DynamicTableFieldType.Bool => value.ToValue<bool>() ? "1" : "0",
            DynamicTableFieldType.DateTime => value.ToValue<DateTime>().ToString("yyyy-MM-dd HH:mm:ss"),
            _ => value._value.ToString()
        };
    }

    /// <summary>
    /// Gets the string of the JToken value
    /// </summary>
    public string? ToString(object? value)
    {
        if (value == null) return null;

        return Type switch
        {
            DynamicTableFieldType.Bool => Convert.ToBoolean(value) ? "1" : "0",
            DynamicTableFieldType.DateTime => Convert.ToDateTime(value).ToString("yyyy-MM-dd HH:mm:ss"),
            _ => value.ToString()
        };
    }

    /// <summary>
    /// Gets the string of the JToken value
    /// </summary>
    public string? ToString(JsonNode? value)
    {
        if (value == null && value.IsEmpty()) return null;
        JsonNode v = value!;
        return Type switch
        {
            DynamicTableFieldType.Bool => v.GetValue<bool>() ? "1" : "0",
            DynamicTableFieldType.DateTime => v.GetValue<DateTime>().ToString("yyyy-MM-dd HH:mm:ss"),
            _ => v.ToString()
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
public record FieldDataChangeData(TransactionChangeOperation Operation, AnySchemaNode? Value, AnySchemaNode? Origin);

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
    public AnySchemaNode? Value { get; set; }

    /// <summary>
    /// The origin value
    /// </summary>
    public AnySchemaNode? Origin { get; set; }

    /// <summary>
    /// Whether is array data
    /// </summary>
    public bool IsArray => Type is ArrayType;

    /// <summary>
    /// The value type
    /// </summary>
    public AnySchemeType Type { get; set; }

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