using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using SchemaNode.Enum;
using SchemaNode.Schema;
using SchemaNode.Utility;

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
            if (res?.schemaType == SchemaType.Namespace)
            {
                NodeSchema schema = await LoadSchemaFile(Path.Combine(res.Value.file, "__ns.json")) ?? new NodeSchema
                {
                    Name = name,
                    Type = SchemaType.Namespace,
                    Display = name
                };
                
                // load sub schemas
                List<NodeSchema> nodes = [];
                foreach (string f in Directory.GetFiles(res.Value.file, "*.json", SearchOption.TopDirectoryOnly))
                {
                    string[] path = f.Split(".").Where(s => !string.IsNullOrEmpty(s)).ToArray();
                    if (path.Length == 3)
                    {
                        SchemaType? type = path[1] switch
                        {
                            "scalar" => SchemaType.Scalar,
                            "enum" => SchemaType.Enum,
                            "struct" => SchemaType.Struct,
                            "array" => SchemaType.Array,
                            "func" => SchemaType.Function,
                            _ => null
                        };
                        if (type == null) continue;
                        nodes.Add(new NodeSchema
                        {
                            Name = $"{name}.{path[0]}",
                            Type = type.Value,
                            Display = path[0]
                        });
                    }
                }
                schema.Schemas = nodes.ToArray();
                schemas.Add(schema);
            }
            else
            {
                NodeSchema? schema = await LoadSchemaFile(res!.Value.file);
                if (schema != null) schemas.Add(schema);
            }
        }
        return schemas.ToArray();
    }

    /// <inheritdoc />
    public async Task<AppSchema?> LoadAppSchemaAsync(string app)
    {
        throw new NotImplementedException();
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
        string root = Path.Combine(AppContext.BaseDirectory, Folder);

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
                SchemaType.Function => "func",
                _ => throw new ArgumentOutOfRangeException()
            };
            await WriteSchemaFile(Path.Combine(folder, $"{paths.Last()}.{type}.json"), schema);
        }
        
    }

    /// <inheritdoc />
    public async Task<bool> DeleteSchemaAsync(string schema)
    {
        (string file, SchemaType schemaType)? res = CheckSchemaFile(schema);
        if (string.IsNullOrEmpty(res?.file)) return false;
        if (res?.schemaType == SchemaType.Namespace)
        {
            Directory.Delete(res.Value.file, true);
        }
        else
        {
            File.Delete(res!.Value.file);
        }
    }

    /// <inheritdoc />
    public async Task<bool> SaveEnumSubListAsync(string schema, string? value, EnumValueInfo[] values, bool? append)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public async Task<bool> DeleteEnumSubListAsync(string schema, string value)
    {
        throw new NotImplementedException();
    }
    
    public SchemaLoadState? DefaultLoadState { get; } = SchemaLoadState.Server;

    /// <summary>
    /// The root folder
    /// </summary>
    public string Folder { get; set; } = "Schema";

    #region Utility

    (string file, SchemaType schemaType)? CheckSchemaFile(string name)
    {
        string root = Path.Combine(AppContext.BaseDirectory, Folder);
        string[] paths = name.ToLowerInvariant().Split(".").Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        string folder = Path.Combine(paths.Prepend(root).ToArray());
        
        // namespace check first
        if (Directory.Exists(folder))
            return (folder, SchemaType.Namespace);
        
        // sub types
        folder = Path.Combine(paths.SkipLast(1).Append(root).ToArray());
        if (Directory.Exists(folder))
        {
            string? find = Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(s => s.StartsWith($"{name}.", StringComparison.OrdinalIgnoreCase));
            if (find is not null)
            {
                string type = find[(name.Length + 1)..^5];
                SchemaType? schemaType = type.ToLower() switch
                {
                    "ns" => SchemaType.Namespace,
                    "scalar" => SchemaType.Scalar,
                    "enum" => SchemaType.Enum,
                    "struct" => SchemaType.Struct,
                    "array" => SchemaType.Array,
                    "func" => SchemaType.Function,
                    _ => null
                };
                if (schemaType is null) return null;
                return (Path.Combine(folder, find), schemaType.Value);
            }
        }

        return null;
    }

    async Task<NodeSchema?> LoadSchemaFile(string name)
    {
        try
        {
            string readJson = await File.ReadAllTextAsync(name);
            return readJson.FromJson<NodeSchema>();
        }
        catch
        {
            return null;
        }
    }

    async Task WriteSchemaFile(string name, NodeSchema schema)
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