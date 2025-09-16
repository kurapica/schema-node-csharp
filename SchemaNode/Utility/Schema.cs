using System.Text.RegularExpressions;
using SchemaNode.Enum;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Utility;

/// <summary>
/// Provide the system schema
/// </summary>
public static class Schema
{
    #region Static Methods

    /// <summary>
    /// Gets the system node schema
    /// </summary>
    public static NodeSchema? GetSystemNodeSchema(string schemaName)
    {
        schemaName = schemaName.ToLowerInvariant();
        NodeSchema[] schemas = _nodes;
        string fullPath = "";
        foreach (string path in Regex.Split(schemaName, @"\W+").SkipLast(1))
        {
            fullPath = !string.IsNullOrWhiteSpace(fullPath) ? $"{fullPath}.{path}" : path;
            NodeSchema? node = schemas.FirstOrDefault(x => x.Name == fullPath);
            if (node?.Schemas == null) return null;
            schemas = node.Schemas;
        }
        return schemas.FirstOrDefault(x => x.Name == schemaName);
    }

    /// <summary>
    /// Save the node schema as system, should only be used to save all system define functions
    /// When server init, so no lock will be used for simple
    /// </summary>
    public static void SaveSystemNodeSchema(NodeSchema schema)
    {
        schema.LoadState = SchemaLoadState.System;
        
        string schemaName = schema.Name.ToLowerInvariant();
        NodeSchema[] schemas = _nodes;
        NodeSchema? root = null;
        string fullPath = "";
        foreach (string path in Regex.Split(schemaName, @"\W+").SkipLast(1))
        {
            fullPath = !string.IsNullOrWhiteSpace(fullPath) ? $"{fullPath}.{path}" : path;
            NodeSchema? node = schemas.FirstOrDefault(x => x.Name == fullPath);
            if (node == null)
            {
                node = new NodeSchema
                {
                    Name = fullPath,
                    Type = SchemaType.Namespace,
                    LoadState = SchemaLoadState.System,
                    Schemas = []
                };

                if (root == null)
                {
                    _nodes = _nodes.Concat([node]).ToArray();
                }
                else
                {
                    root.Schemas = root.Schemas != null ? root.Schemas.Concat([node]).ToArray() : [node];
                }
            }
            else
            {
                root = node;
                root.Schemas ??= [];
                schemas = root.Schemas;
            }
        }
        
        if (schemas.Any(x => x.Name == schemaName)) return;
        if (root == null)
        {
            _nodes = _nodes.Concat([schema]).ToArray();
        }
        else
        {
            root.Schemas = root.Schemas != null ? root.Schemas.Concat([schema]).ToArray() : [schema];
        }
    }

    /// <summary>
    /// Gets the system scalar value type
    /// </summary>
    public static ScalarValueType? GetSystemScalarValueType(string schemaName)
    {
        return schemaName.ToLowerInvariant() switch
        {
            NS_SYSTEM_BOOL => ScalarValueType.Boolean,
            NS_SYSTEM_DATE => ScalarValueType.Date,
            NS_SYSTEM_NUMBER => ScalarValueType.Number,
            NS_SYSTEM_DOUBLE => ScalarValueType.Double | ScalarValueType.Number,
            NS_SYSTEM_FLOAT => ScalarValueType.Single | ScalarValueType.Number,
            NS_SYSTEM_PERCENT => ScalarValueType.Single | ScalarValueType.Number,
            NS_SYSTEM_INT => ScalarValueType.Integer | ScalarValueType.Number,
            NS_SYSTEM_FULLDATE => ScalarValueType.FullDate | ScalarValueType.Date,
            NS_SYSTEM_STRING => ScalarValueType.String,
            NS_SYSTEM_YEAR => ScalarValueType.Year | ScalarValueType.Integer | ScalarValueType.Number,
            NS_SYSTEM_YEARMONTH => ScalarValueType.YearMonth | ScalarValueType.Date,
            _ => null
        };
    }
    
    /// <summary>
    /// Try parse bool value from string
    /// </summary>
    public static bool TryParseBoolValue(string value, out bool ret)
    {
        ret = false;
        if (string.IsNullOrEmpty(value))
            return false;
        value = value.ToLower();
        switch (value)
        {
            case "true":
                ret = true;
                return true;
            case "false":
                ret = false;
                return true;
            default:
            {
                if (!int.TryParse(value, out int val) || val is < 0 or > 1)
                    return false;
                ret = val == 1;
                return true;
            }
        }
    }
    
    #endregion
    
    #region System
    
    // System Nodes
    private static NodeSchema[] _nodes = [
        new NodeSchema
        {
            Name = NS_SYSTEM,
            Type = SchemaType.Namespace,
            LoadState = SchemaLoadState.System,
            Display = NS_SYSTEM,
            Schemas = [
                // base type
                new NodeSchema
                {
                    Name = NS_SYSTEM_ARRAY,
                    Type = SchemaType.Array,
                    LoadState = SchemaLoadState.System,
                    Display = NS_SYSTEM_ARRAY,
                    Array = new ArraySchema
                    {
                        Element = "",
                    }
                },
                new NodeSchema
                {
                    Name = NS_SYSTEM_STRUCT,
                    Type = SchemaType.Struct,
                    LoadState = SchemaLoadState.System,
                    Display = NS_SYSTEM_STRUCT,
                    Struct = new StructSchema
                    {
                        Fields = []
                    }
                },
                
                // scalar
                new NodeSchema
                {
                    Name = NS_SYSTEM_BOOL,
                    Type = SchemaType.Scalar,
                    LoadState = SchemaLoadState.System,
                    Display = NS_SYSTEM_BOOL,
                    Scalar = new ScalarSchema 
                    {
                        Error = "system.bool.error"
                    },
                },
                new NodeSchema
                {
                    Name = NS_SYSTEM_DATE,
                    Type = SchemaType.Scalar,
                    LoadState = SchemaLoadState.System,
                    Display = NS_SYSTEM_DATE,
                    Scalar = new ScalarSchema 
                    {
                        Error = "system.date.error"
                    },
                },
                new NodeSchema
                {
                    Name = NS_SYSTEM_NUMBER,
                    Type =SchemaType.Scalar,
                    LoadState = SchemaLoadState.System,
                    Display = NS_SYSTEM_NUMBER,
                    Scalar = new ScalarSchema {
                        Error = "system.number.error",
                        Regex = @"^(\\-|\\+)?\\d+(\\.\\d+)?(e\\-\\d+)?$",
                    },
                },
                new NodeSchema
                {
                    Name = NS_SYSTEM_DOUBLE,
                    Type =SchemaType.Scalar,
                    LoadState = SchemaLoadState.System,
                    Display = NS_SYSTEM_DOUBLE,
                    Scalar = new ScalarSchema 
                    {
                        Base = NS_SYSTEM_NUMBER,
                        Error = "system.double.error",
                        Regex = @"^-?\\d+\\.?\\d+$",
                    },
                },
                new NodeSchema
                {
                    Name = NS_SYSTEM_FLOAT,
                    Type =SchemaType.Scalar,
                    LoadState = SchemaLoadState.System,
                    Display = NS_SYSTEM_FLOAT,
                    Scalar = new ScalarSchema {
                        Base = NS_SYSTEM_DOUBLE,
                        Error = "system.float.error",
                        Regex = @"^\\d+(\\.\\d+)?$",
                    },
                },
                new NodeSchema
                {
                    Name = NS_SYSTEM_PERCENT,
                    Type =SchemaType.Scalar,
                    LoadState = SchemaLoadState.System,
                    Display = NS_SYSTEM_PERCENT,
                    Scalar = new ScalarSchema {
                        Base = NS_SYSTEM_FLOAT,
                        Error = "system.percent.error",
                        Regex = @"^\\d+(\\.\\d+)?$",
                        UpLimit = 100,
                        LowLimit = 0
                    },
                },
                new NodeSchema
                {
                    Name = NS_SYSTEM_FULLDATE,
                    Type =SchemaType.Scalar,
                    LoadState = SchemaLoadState.System,
                    Display = NS_SYSTEM_FULLDATE,
                    Scalar = new ScalarSchema 
                    {
                        Base = NS_SYSTEM_DATE,
                        Error = "system.fulldate.error",
                    },
                },
                new NodeSchema
                {
                    Name = NS_SYSTEM_INT,
                    Type =SchemaType.Scalar,
                    LoadState = SchemaLoadState.System,
                    Display = NS_SYSTEM_INT,
                    Scalar = new ScalarSchema {
                        Base = NS_SYSTEM_NUMBER,
                        Error = "system.int.error",
                        Regex = @"^(\\-|\\+)?\\d+$",
                    },
                },
                new NodeSchema
                {
                    Name = NS_SYSTEM_STRING,
                    Type = SchemaType.Scalar,
                    LoadState = SchemaLoadState.System,
                    Display = NS_SYSTEM_STRING,
                    Scalar = new ScalarSchema(),
                },
                new NodeSchema
                {
                    Name = NS_SYSTEM_YEAR,
                    Type =SchemaType.Scalar,
                    LoadState = SchemaLoadState.System,
                    Display = NS_SYSTEM_YEAR,
                    Scalar = new ScalarSchema 
                    {
                        Base = NS_SYSTEM_INT,
                        Unit = "system.year.unit",
                        LowLimit = 1900,
                        Regex = @"^\\d{4}$",
                    },
                },
                new NodeSchema
                {
                    Name = NS_SYSTEM_YEARMONTH,
                    Type =SchemaType.Scalar,
                    LoadState = SchemaLoadState.System,
                    Display = NS_SYSTEM_YEARMONTH,
                    Scalar = new ScalarSchema 
                    {
                        Base = NS_SYSTEM_DATE,
                    },
                },
                
                // struct
                new NodeSchema
                {
                    Name = NS_SYSTEM_RANGEDATE,
                    Type = SchemaType.Struct,
                    LoadState = SchemaLoadState.System,
                    Display = NS_SYSTEM_RANGEDATE,
                    Struct = new StructSchema {
                        Fields = [
                            new StructFieldConfig
                            {
                                Name = "start",
                                Require = true,
                                Type = NS_SYSTEM_DATE,
                                Display = "system.rangedate.start",
                            },
                            new StructFieldConfig
                            {
                                Name = "stop",
                                Require = true,
                                Type = NS_SYSTEM_DATE,
                                Display = "system.rangedate.stop",
                            }
                        ],
                    },
                },
                new NodeSchema
                {
                    Name = NS_SYSTEM_RANGEFULLDATE,
                    Type = SchemaType.Struct,
                    LoadState = SchemaLoadState.System,
                    Display = NS_SYSTEM_RANGEFULLDATE,
                    Struct = new StructSchema {
                        Fields = [
                            new StructFieldConfig
                            {
                                Name = "start",
                                Require = true,
                                Type = NS_SYSTEM_FULLDATE,
                                Display = "system.rangedate.start",
                            },
                            new StructFieldConfig
                            {
                                Name = "stop",
                                Require = true,
                                Type = NS_SYSTEM_FULLDATE,
                                Display = "system.rangedate.stop",
                            }
                        ],
                    },
                },
                new NodeSchema
                {
                    Name = NS_SYSTEM_RANGEMONTH,
                    Type = SchemaType.Struct,
                    LoadState = SchemaLoadState.System,
                    Display = NS_SYSTEM_RANGEMONTH,
                    Struct = new StructSchema
                    {
                        Fields = [
                            new StructFieldConfig
                            {
                                Name = "start",
                                Require = true,
                                Type = NS_SYSTEM_YEARMONTH,
                                Display = "system.rangemonth.start",
                            },
                            new StructFieldConfig
                            {
                                Name = "stop",
                                Require = true,
                                Type = NS_SYSTEM_YEARMONTH,
                                Display = "system.rangemonth.stop",
                            }
                        ],
                    },
                },
                new NodeSchema
                {
                    Name = NS_SYSTEM_RANGEYEAR,
                    Type = SchemaType.Struct,
                    LoadState = SchemaLoadState.System,
                    Display = NS_SYSTEM_RANGEYEAR,
                    Struct = new StructSchema
                    {
                        Fields = [
                            new StructFieldConfig
                            {
                                Name = "start",
                                Require = true,
                                Type = NS_SYSTEM_YEAR,
                                Display = "system.rangeyear.start",
                            },
                            new StructFieldConfig
                            {
                                Name = "stop",
                                Require = true,
                                Type = NS_SYSTEM_YEAR,
                                Display = "system.rangeyear.stop",
                            }
                        ],
                    },
                },
                
                // array
                new NodeSchema{
                    Name = NS_SYSTEM_STRINGS,
                    Type = SchemaType.Array,
                    LoadState = SchemaLoadState.System,
                    Display = NS_SYSTEM_STRINGS,
                    Array = new ArraySchema 
                    {
                        Element = NS_SYSTEM_STRING,
                        Primary = [],
                    },
                },
                new NodeSchema
                {
                    Name = NS_SYSTEM_NUMBERS,
                    Type = SchemaType.Array,
                    LoadState = SchemaLoadState.System,
                    Display = NS_SYSTEM_NUMBERS,
                    Array = new ArraySchema
                    {
                        Element = NS_SYSTEM_NUMBER,
                    },
                },
                new NodeSchema
                {
                    Name = NS_SYSTEM_INTS,
                    Type = SchemaType.Array,
                    LoadState = SchemaLoadState.System,
                    Display = NS_SYSTEM_INTS,
                    Array = new ArraySchema 
                    {
                        Element = NS_SYSTEM_INT
                    },
                },
            ]
        }
    ];
    
    #endregion
}