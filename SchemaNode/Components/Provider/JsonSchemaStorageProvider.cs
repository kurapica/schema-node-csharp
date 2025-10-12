using SchemaNode.Enum;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SchemaNode.Components.Provider;

/// <summary>
/// The default json schema storage provider, which save schema in json files
/// </summary>
public class JsonSchemaStorageProvider: ISchemaStorageProvider
{
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
                        Name = name,
                        Type = SchemaType.Namespace,
                        Display = name
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
            }
        }
        return schemas.ToArray();
    }

    /// <inheritdoc />
    public async Task<AppSchema?> LoadAppSchemaAsync(string app)
    {
        await Task.Yield();
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
        return schema;
    }

    /// <inheritdoc />
    public Task<EnumValueInfo[]> LoadEnumSubListAsync(string schemaName, string? value, bool? fullList = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<EnumValueAccess[]> LoadEnumAccessListAsync(string schemaName, string value, bool? noSubList = null, bool? withSubList = null)
    {
        throw new NotImplementedException();
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

    /// <inheritdoc />
    public async Task<bool> SaveEnumSubListAsync(EnumType @enum, string? value, EnumValueInfo[] values, bool? append)
    {
        @enum.SaveEnumSubListAsync(value, values); // do it directly for simple @TODO 
        await SaveSchemaAsync(@enum.ToNodeSchema(99));
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteEnumSubListAsync(EnumType @enum, string value)
    {
        @enum.DeleteEnumSubListAsync(value); // do it directly for simple @TODO 
        await SaveSchemaAsync(@enum.ToNodeSchema(99));
        return true;
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
            Standalone = app.Standalone,
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

    public SchemaLoadState? DefaultLoadState { get; } = SchemaLoadState.Server;

    /// <summary>
    /// The root folder for type
    /// </summary>
    public string SchemaFolder { get; set; } = "Schema";

    /// <summary>
    /// The root folder for app
    /// </summary>
    public string AppFolder { get; set; } = "App";

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