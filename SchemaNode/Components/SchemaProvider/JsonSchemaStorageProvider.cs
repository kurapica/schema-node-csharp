using SchemaNode.Enum;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

/// <summary>
/// The default json schema storage provider, which save schema in json files
/// </summary>
public class JsonSchemaStorageProvider: ISchemaStorageProvider
{
    #region Schema

    /// <inheritdoc />
    public async Task<NodeSchema[]> LoadSchemaAsync(string[] names)
    {
        List<NodeSchema> schemas = new();
        foreach (string name in names)
        {
            (string file, SchemaType schemaType)? res = CheckSchemaFile(name);
            if (string.IsNullOrEmpty(res?.file)) continue;
            if (res.Value.schemaType == SchemaType.Namespace)
            {
                NodeSchema schema = await LoadSchemaFile<NodeSchema>(Path.Combine(res.Value.file, "__ns.json")) ?? new NodeSchema
                {
                    Name = name,
                    Type = SchemaType.Namespace,
                    Display = name
                };
                
                // load sub schemas
                List<NodeSchema> nodes = [];
                foreach (string d in Directory.GetDirectories(res.Value.file, "*", SearchOption.TopDirectoryOnly))
                {
                    nodes.Add(await LoadSchemaFile<NodeSchema>(Path.Combine(d, "__ns.json")) ?? new NodeSchema
                    {
                        Name = string.IsNullOrEmpty(name) ? Path.GetFileName(d).ToLower() : $"{name}.{Path.GetFileName(d).ToLower()}",
                        Type = SchemaType.Namespace,
                        Display = string.IsNullOrEmpty(name) ? Path.GetFileName(d).ToLower() : $"{name}.{Path.GetFileName(d).ToLower()}"
                    });

                    if (Directory.GetFiles(d, "*", SearchOption.TopDirectoryOnly).Length > 1
                        || Directory.GetDirectories(d).Length > 0)
                        nodes.Last().HasSchemas = true;
                }
                foreach (string f in Directory.GetFiles(res.Value.file, "*.json", SearchOption.TopDirectoryOnly))
                {
                    string[] path = Path.GetFileName(f).Split(".").Where(s => !string.IsNullOrEmpty(s)).ToArray();
                    if (path.Length == 3)
                    {
                        SchemaType? type = path[1] switch
                        {
                            "scalar" => SchemaType.Scalar,
                            "enum" => SchemaType.Enum,
                            "struct" => SchemaType.Struct,
                            "array" => SchemaType.Array,
                            "func" => SchemaType.Func,
                            "policy" => SchemaType.Policy,
                            _ => null
                        };
                        if (type == null) continue;
                        NodeSchema? subNs = await LoadSchemaFile<NodeSchema>(f);
                        if (subNs != null) nodes.Add(subNs);
                    }
                }
                schema.Schemas = nodes.ToArray();
                schemas.Add(schema);
            }
            else
            {
                NodeSchema? schema = await LoadSchemaFile<NodeSchema>(res.Value.file);
                if (schema != null) schemas.Add(schema);
                
                // check enum sub list
                if (schema is { Type: SchemaType.Enum, Enum.Cascade.Length: > 0 })
                {
                    string root = Path.Combine(AppContext.BaseDirectory, EnumFolder);
                    foreach (string s in name.Split('.', StringSplitOptions.RemoveEmptyEntries))
                        root = Path.Combine(root, s.ToLower());
                    foreach (var item in schema.Enum.Values)
                    {
                        item.HasSubList = File.Exists(Path.Combine(root, $"{item.Value}.json"));
                    }
                }
            }
        }
        return schemas.ToArray();
    }

    /// <inheritdoc />
    public Task<JsonNode?> CallFunctionAsync(string schemaName, JsonArray args, string[]? generic = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public async Task<bool> SaveSchemaAsync(NodeSchema schema)
    {
        string[] paths = schema.Name.ToLowerInvariant().Split(".").Where(s => !string.IsNullOrEmpty(s)).ToArray();
        string root = Path.Combine(AppContext.BaseDirectory, SchemaFolder);

        if (schema.Type == SchemaType.Namespace)
        {
            string folder = Path.Combine(paths.Prepend(root).ToArray());
            Directory.CreateDirectory(folder);
            await WriteSchemaFile(Path.Combine(folder, "__ns.json"), new NodeSchema
            {
                Name = schema.Name,
                Type = SchemaType.Namespace,
                Display = schema.Display,
            });
        }
        else
        {
            string folder = Path.Combine(paths.SkipLast(1).Prepend(root).ToArray());
            Directory.CreateDirectory(folder);
            string type = schema.Type switch
            {
                SchemaType.Scalar => "scalar",
                SchemaType.Enum => "enum",
                SchemaType.Struct => "struct",
                SchemaType.Array => "array",
                SchemaType.Func => "func",
                SchemaType.Policy => "policy",
                _ => throw new ArgumentOutOfRangeException()
            };
            await WriteSchemaFile(Path.Combine(folder, $"{paths.Last()}.{type}.json"), schema);
        }
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteSchemaAsync(string schema)
    {
        await Task.Yield();
        (string file, SchemaType schemaType)? res = CheckSchemaFile(schema);
        if (string.IsNullOrEmpty(res?.file)) return false;
        if (res.Value.schemaType == SchemaType.Namespace)
        {
            File.Delete(Path.Combine(res.Value.file, "__ns.json"));
            Directory.Delete(res.Value.file);
        }
        else
        {
            File.Delete(res.Value.file);
        }

        return true;
    }

    #endregion

    #region Enum List
    
    /// <inheritdoc />
    public async Task<EnumValueInfo[]> LoadEnumSubListAsync(string schemaName, string? value, bool? fullList = null)
    {
        // combine path
        string root = Path.Combine(AppContext.BaseDirectory, EnumFolder);
        foreach (string s in schemaName.Split('.', StringSplitOptions.RemoveEmptyEntries))
            root = Path.Combine(root, s.ToLower());
        if (!Directory.Exists(root)) return [];
        
        // load enum list
        string path = Path.Combine(root, $"{(string.IsNullOrEmpty(value) ? "__root" : value)}.json");
        if (!File.Exists(path)) return [];
        
        EnumValueInfo[] list = await LoadSchemaFile<EnumValueInfo[]>(path) ?? [];
        
        // sub list check
        foreach (EnumValueInfo item in list)
        {
            if (!File.Exists(Path.Combine(root, $"{item.Value}.json"))) continue;
            item.HasSubList = true;
            item.SubList = (fullList ?? false) ? await LoadEnumSubListAsync(schemaName, item.Value, true) : null;
        }
        return list;
    }

    /// <inheritdoc />
    public async Task<EnumValueAccess[]> LoadEnumAccessListAsync(string schemaName, string value, bool? noSubList = null, bool? withSubList = null)
    {
        NodeSchema[] nodeSchemas = await LoadSchemaAsync([schemaName]);
        if (nodeSchemas.Length == 0 || nodeSchemas[0].Type != SchemaType.Enum) return [];
        NodeSchema enumSchema = nodeSchemas[0];
        
        // root value check
        EnumValueInfo? existed = enumSchema.Enum?.Values.FirstOrDefault(v => v.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
        if (existed != null)
        {
            if ((withSubList == true) && enumSchema.Enum?.Cascade is { Length: > 1} )
            {
                return
                [
                    new EnumValueAccess
                    {
                        Name = enumSchema.Enum.Cascade[0],
                        Value = value,
                        SubList = enumSchema.Enum!.Values
                    },
                    new EnumValueAccess
                    {
                        Name = enumSchema.Enum.Cascade[1],
                        Value = "",
                        SubList = await LoadEnumSubListAsync(schemaName, value)
                    }
                ];
            }
            return
            [
                new EnumValueAccess
                {
                    Name = enumSchema.Enum?.Cascade is { Length: > 0 } ? enumSchema.Enum.Cascade[0] : null,
                    Value = value,
                    SubList = enumSchema.Enum!.Values
                }
            ];
        }
        
        // no sub list
        if (enumSchema.Enum?.Cascade is null || enumSchema.Enum.Cascade.Length <= 1) return [];
        
        // combine path
        string root = Path.Combine(AppContext.BaseDirectory, EnumFolder);
        foreach (string s in schemaName.Split('.', StringSplitOptions.RemoveEmptyEntries))
            root = Path.Combine(root, s.ToLower());
        
        if (!Directory.Exists(root)) return [];
        
        // load enum list
        string path = Path.Combine(root, "__access.json");
        if (!File.Exists(path)) return [];
        
        Dictionary<string, string> map = await LoadSchemaFile<Dictionary<string, string>>(path) ?? [];
        if (!map.ContainsKey(value)) return [];

        List<EnumValueAccess> accesses = [];
        if (withSubList == true && File.Exists(Path.Combine(root, $"{value}.json")))
            accesses.Add(new EnumValueAccess
            {
                Value = "",
                SubList = await LoadEnumSubListAsync(schemaName, value)
            });
        
        while (map.TryGetValue(value, out string? parent))
        {
            accesses.Insert(0, new EnumValueAccess
            {
                Value = value,
                SubList = !(noSubList ?? false) && File.Exists(Path.Combine(root, $"{parent}.json"))
                    ? await LoadEnumSubListAsync(schemaName, parent, noSubList == true) : null
            });
            value = parent;
        }
        accesses.Insert(0, new EnumValueAccess
        {
            Value = value,
            SubList = enumSchema.Enum!.Values
        });
        for(int i = 0; i < accesses.Count; i++)
        {
            accesses[i].Name = enumSchema.Enum.Cascade.Length > i ? enumSchema.Enum.Cascade[i] : null;
        }
        return accesses.ToArray();
    }

    /// <inheritdoc />
    public async Task<EnumValueInfo[]> SaveEnumSubListAsync(EnumType @enum, string? value, EnumValueInfo[] values, bool? append)
    {
        if (string.IsNullOrEmpty(value)) return values;
        
        // combine path
        string root = Path.Combine(AppContext.BaseDirectory, EnumFolder);
        foreach (string s in @enum.Name.Split('.', StringSplitOptions.RemoveEmptyEntries))
            root = Path.Combine(root, s.ToLower());
        Directory.CreateDirectory(root);
        
        // save sub list
        string path = Path.Combine(root, $"{(string.IsNullOrEmpty(value) ? "__root" : value)}.json");
        EnumValueInfo[] existed = await LoadSchemaFile<EnumValueInfo[]>(path) ?? [];
        if (append ?? false)
        {
            values = existed.Concat(values.Where(v => existed.All(e => !e.Value.Equals(v.Value, StringComparison.OrdinalIgnoreCase))).ToArray()).ToArray();
        }
        else
        {
            if (existed.Where(item => values.All(v => !v.Value.Equals(item.Value, StringComparison.OrdinalIgnoreCase))).Any(item => Directory.Exists(Path.Combine(root, $"{item.Value}.json"))))
            {
                throw new Exception(TYPE_ENUM_VALUE_HAS_SUBLIST);
            }
        }

        if (values.Length == 0 && !string.IsNullOrEmpty(value))
        {
            File.Delete(path);
        }
        else
        {
            await WriteSchemaFile(path, values.Select(v => v.Clone()).ToArray());
        }

        // save access map
        if (!string.IsNullOrEmpty(value))
        {
            string accessPath = Path.Combine(root, "__access.json");
            Dictionary<string, string> accessMap = await LoadSchemaFile<Dictionary<string, string>>(accessPath) ?? [];
            foreach (EnumValueInfo v in values)
            {
                accessMap[v.Value] = value;
            }
            await WriteSchemaFile(accessPath, accessMap);
        }
        
        return values;
    }

    #endregion
    
    #region App Schema
    
    /// <inheritdoc />
    public async Task<AppSchema?> LoadAppSchemaAsync(string app)
    {
        app = app.ToLowerInvariant();

        string[] paths = app.Split(".").Where(s => !string.IsNullOrEmpty(s)).ToArray();
        string root = Path.Combine(AppContext.BaseDirectory, AppFolder);
        string folder = Path.Combine(paths.Prepend(root).ToArray());
        if (!Directory.Exists(folder)) return null;

        AppSchema schema = await LoadSchemaFile<AppSchema>(Path.Combine(folder, "__app.json")) ?? new AppSchema
        {
            Name = app,
        };

        // load apps
        string[] dirs = Directory.GetDirectories(folder);
        if (dirs.Length > 0)
        {
            schema.Apps = new AppSchema[dirs.Length];

            for (int i = 0; i < dirs.Length; i++)
            {
                string d = dirs[i];
                schema.Apps[i] = await LoadSchemaFile<AppSchema>(Path.Combine(d, "__app.json")) ?? new AppSchema
                {
                    Name = string.IsNullOrWhiteSpace(app) ? Path.GetFileName(d) : $"{app}.{Path.GetFileName(d)}"
                };
                if (Directory
                    .GetFiles(d, "*.json", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileNameWithoutExtension).Any(p => Regex.IsMatch(p!, @"^\d{3}\..+$")))
                {
                    schema.Apps[i].HasFields = true;
                }
                else if (Directory.GetDirectories(d).Length > 0)
                {
                    schema.Apps[i].HasApps = true;
                }
            }
        }

        // load fields
        var fields = Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(p => Regex.IsMatch(p!, @"^\d{3}\..+$"))
            .Select(p =>
            {
                string[] path = p!.Split('.', StringSplitOptions.RemoveEmptyEntries);
                return (int.Parse(path[0]), path[1]);
            }).OrderBy(v => v.Item1).ToArray();
        if (fields.Length > 0)
        {
            schema.Fields = new AppFieldSchema[fields.Length];
            for(int i = 0; i < fields.Length; i++)
            {
                schema.Fields[i] = await LoadSchemaFile<AppFieldSchema>(Path.Combine(folder, $"{fields[i].Item1.ToString("D3")}.{fields[i].Item2}.json")) 
                    ?? new AppFieldSchema
                    {
                        Name = fields[i].Item2,
                    };
            }   
        }
        
        // load workflows
        var workflows = Directory.GetFiles(folder, "*.workflow", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(p => Regex.IsMatch(p!, @"^\d{3}\..+$"))
            .Select(p =>
            {
                string[] path = p!.Split('.', StringSplitOptions.RemoveEmptyEntries);
                return (int.Parse(path[0]), path[1]);
            }).OrderBy(v => v.Item1).ToArray();
        if (workflows.Length > 0)
        {
            schema.Workflows = new AppWorkflowSchema[workflows.Length];
            for(int i = 0; i < workflows.Length; i++)
            {
                schema.Workflows[i] = await LoadSchemaFile<AppWorkflowSchema>(Path.Combine(folder, $"{workflows[i].Item1.ToString("D3")}.{workflows[i].Item2}.workflow")) 
                    ?? new AppWorkflowSchema
                    {
                        Name = workflows[i].Item2,
                    };
            }   
        }
        return schema;
    }

    /// <inheritdoc />
    public async Task<bool> SaveAppSchemaAsync(AppSchema app)
    {
        string[] paths = app.Name.ToLowerInvariant().Split(".").Where(s => !string.IsNullOrEmpty(s)).ToArray();
        string root = Path.Combine(AppContext.BaseDirectory, AppFolder);
        string folder = Path.Combine(paths.Prepend(root).ToArray());
        Directory.CreateDirectory(folder);

        await WriteSchemaFile(Path.Combine(folder, "__app.json"), new AppSchema
        {
            Name = app.Name,
            Display = app.Display,
            Desc = app.Desc,
            Relations = app.Relations,
            Additional = app.Additional
        });
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAppSchemaAsync(string app)
    {
        await Task.Yield();

        string[] paths = app.ToLowerInvariant().Split(".").Where(s => !string.IsNullOrEmpty(s)).ToArray();
        string root = Path.Combine(AppContext.BaseDirectory, AppFolder);
        string folder = Path.Combine(paths.Prepend(root).ToArray());
        if (!Directory.Exists(folder)) return false;

        if (Directory.GetDirectories(folder).Length > 0 ||
            Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly).Length > 1)
            return false; // not empty

        File.Delete(Path.Combine(folder, "__app.json"));
        Directory.Delete(folder);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> SaveAppFieldSchemaAsync(string app, AppFieldSchema field)
    {
        string[] paths = app.ToLowerInvariant().Split(".").Where(s => !string.IsNullOrEmpty(s)).ToArray();
        string root = Path.Combine(AppContext.BaseDirectory, AppFolder);
        string folder = Path.Combine(paths.Prepend(root).ToArray());
        Directory.CreateDirectory(folder);

        int maxOrder = 0;
        foreach (var file in Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(p => Regex.IsMatch(p!, @"^\d{3}\..+$" ))
            .Select(p => {
                string[] path = p!.Split('.', StringSplitOptions.RemoveEmptyEntries);
                return (int.Parse(path[0]), path[1]);
            }).OrderBy(v => v.Item1))
        {
            if (file.Item2.Equals(field.Name, StringComparison.OrdinalIgnoreCase)) break;
            maxOrder = file.Item1;
        }

        // save
        string fileName = $"{(maxOrder + 1):D3}.{field.Name}.json";

        await WriteSchemaFile(Path.Combine(folder, fileName), field);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAppFieldSchemaAsync(string app, string field)
    {
        await Task.Yield();

        string[] paths = app.ToLowerInvariant().Split(".").Where(s => !string.IsNullOrEmpty(s)).ToArray();
        string root = Path.Combine(AppContext.BaseDirectory, AppFolder);
        string folder = Path.Combine(paths.Prepend(root).ToArray());
        if (!Directory.Exists(folder)) return false;

        int maxOrder = 1;
        foreach (var file in Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(p => Regex.IsMatch(p!, @"^\d{3}\..+$"))
            .Select(p => {
                string[] path = p!.Split('.', StringSplitOptions.RemoveEmptyEntries);
                return (int.Parse(path[0]), path[1]);
            }).OrderBy(v => v.Item1))
        {
            if (file.Item2.Equals(field, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(Path.Combine(folder, $"{file.Item1:D3}.{file.Item2}.json"));
                continue;
            }
            else if(file.Item1 != maxOrder)
            {
                // re-order
                File.Move(Path.Combine(folder, $"{file.Item1:D3}.{file.Item2}.json"),
                    Path.Combine(folder, $"{maxOrder:D3}.{file.Item2}.json"));
            }
            maxOrder += 1;
        }
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> SwapAppFieldSchemaAsync(string app, string field1, string field2)
    {
        await Task.Yield();

        string[] paths = app.ToLowerInvariant().Split(".").Where(s => !string.IsNullOrEmpty(s)).ToArray();
        string root = Path.Combine(AppContext.BaseDirectory, AppFolder);
        string folder = Path.Combine(paths.Prepend(root).ToArray());
        if (!Directory.Exists(folder)) return false;

        (int, string)? order1 = null;
        (int, string)? order2 = null;
        foreach (var file in Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(p => Regex.IsMatch(p!, @"^\d{3}\..+$"))
            .Select(p => {
                string[] path = p!.Split('.', StringSplitOptions.RemoveEmptyEntries);
                return (int.Parse(path[0]), path[1]);
            }).OrderBy(v => v.Item1))
        {
            if (file.Item2.Equals(field1, StringComparison.OrdinalIgnoreCase))
                order1 = file;
            else if (file.Item2.Equals(field2, StringComparison.OrdinalIgnoreCase))
                order2 = file;
        }
        if (order1 is null || order2 is null) return false;
        File.Move(Path.Combine(folder, $"{order1.Value.Item1:D3}.{order1.Value.Item2}.json"), Path.Combine(folder, $"__temp__.json"));
        File.Move(Path.Combine(folder, $"{order2.Value.Item1:D3}.{order2.Value.Item2}.json"), Path.Combine(folder, $"{order1.Value.Item1:D3}.{order2.Value.Item2}.json"));
        File.Move(Path.Combine(folder, $"__temp__.json"), Path.Combine(folder, $"{order2.Value.Item1:D3}.{order1.Value.Item2}.json"));
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> SaveAppWorkflowSchemaAsync(string app, AppWorkflowSchema workflow)
    {
        string[] paths = app.ToLowerInvariant().Split(".").Where(s => !string.IsNullOrEmpty(s)).ToArray();
        string root = Path.Combine(AppContext.BaseDirectory, AppFolder);
        string folder = Path.Combine(paths.Prepend(root).ToArray());
        Directory.CreateDirectory(folder);

        int maxOrder = 0;
        foreach (var file in Directory.GetFiles(folder, "*.workflow", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(p => Regex.IsMatch(p!, @"^\d{3}\..+$" ))
            .Select(p => {
                string[] path = p!.Split('.', StringSplitOptions.RemoveEmptyEntries);
                return (int.Parse(path[0]), path[1]);
            }).OrderBy(v => v.Item1))
        {
            if (file.Item2.Equals(workflow.Name, StringComparison.OrdinalIgnoreCase)) break;
            maxOrder = file.Item1;
        }

        // save
        string fileName = $"{(maxOrder + 1):D3}.{workflow.Name}.workflow";

        await WriteSchemaFile(Path.Combine(folder, fileName), workflow);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAppWorkflowSchemaAsync(string app, string workflow)
    {
        await Task.Yield();

        string[] paths = app.ToLowerInvariant().Split(".").Where(s => !string.IsNullOrEmpty(s)).ToArray();
        string root = Path.Combine(AppContext.BaseDirectory, AppFolder);
        string folder = Path.Combine(paths.Prepend(root).ToArray());
        if (!Directory.Exists(folder)) return false;

        int maxOrder = 1;
        foreach (var file in Directory.GetFiles(folder, "*.workflow", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(p => Regex.IsMatch(p!, @"^\d{3}\..+$"))
            .Select(p => {
                string[] path = p!.Split('.', StringSplitOptions.RemoveEmptyEntries);
                return (int.Parse(path[0]), path[1]);
            }).OrderBy(v => v.Item1))
        {
            if (file.Item2.Equals(workflow, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(Path.Combine(folder, $"{file.Item1:D3}.{file.Item2}.json"));
                continue;
            }
            else if(file.Item1 != maxOrder)
            {
                // re-order
                File.Move(Path.Combine(folder, $"{file.Item1:D3}.{file.Item2}.json"),
                    Path.Combine(folder, $"{maxOrder:D3}.{file.Item2}.json"));
            }
            maxOrder += 1;
        }
        return true;
    }
    
    #endregion
    
    #region Property
    
    public SchemaLoadState? DefaultLoadState { get; } = SchemaLoadState.Server;

    /// <summary>
    /// The root folder for type
    /// </summary>
    public string SchemaFolder { get; set; } = "Schema";

    /// <summary>
    /// The root folder for app
    /// </summary>
    public string AppFolder { get; set; } = "App";
    
    /// <summary>
    /// The enum sub list folder
    /// </summary>
    public string EnumFolder { get; set; } = "Enum";

    #endregion
    
    #region Utility

    (string file, SchemaType schemaType)? CheckSchemaFile(string name)
    {
        string root = Path.Combine(AppContext.BaseDirectory, SchemaFolder);
        string[] paths = name.ToLowerInvariant().Split(".").Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        string folder = Path.Combine(paths.Prepend(root).ToArray());
        
        // namespace check first
        if (Directory.Exists(folder))
            return (folder, SchemaType.Namespace);
        
        // sub types
        folder = Path.Combine(paths.SkipLast(1).Prepend(root).ToArray());
        if (Directory.Exists(folder))
        {
            string? find = Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly).Select(Path.GetFileName)
                .FirstOrDefault(s => s is not null && s.StartsWith($"{paths.Last()}.", StringComparison.OrdinalIgnoreCase));
            if (find is not null)
            {
                string type = find[(paths.Last().Length + 1)..^5];
                SchemaType? schemaType = type.ToLower() switch
                {
                    "ns" => SchemaType.Namespace,
                    "scalar" => SchemaType.Scalar,
                    "enum" => SchemaType.Enum,
                    "struct" => SchemaType.Struct,
                    "array" => SchemaType.Array,
                    "func" => SchemaType.Func,
                    "policy" => SchemaType.Policy,
                    _ => null
                };
                if (schemaType is null) return null;
                return (Path.Combine(folder, find), schemaType.Value);
            }
        }

        return null;
    }

    async Task<T?> LoadSchemaFile<T>(string name)
    {
        try
        {
            if (!File.Exists(name)) return default;
            string readJson = await File.ReadAllTextAsync(name);
            return readJson.FromJson<T>();
        }
        catch
        {
            return default;
        }
    }

    async Task WriteSchemaFile<T>(string name, T schema)
    {
        try
        {
            await File.WriteAllTextAsync(name, schema.ToJson());
        }
        catch
        {
            // pass
        }
    }
    
    #endregion
}