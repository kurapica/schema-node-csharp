using System.Collections.Concurrent;
using SchemaNode.Schema;
using SchemaNode.Schema.Provider;

namespace SchemaNode.RefactorTest.Base;

public class InMemorySchemaStorageProvider : IAppSchemaStorageProvider
{
    private static readonly ConcurrentDictionary<string, AppSchema> _apps = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, List<AppFieldSchema>> _fields = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, AppWorkflowSchema> _workflows = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, NodeSchema> _schemas = new(StringComparer.OrdinalIgnoreCase);

    public static void Reset()
    {
        _apps.Clear();
        _fields.Clear();
        _workflows.Clear();
        _schemas.Clear();
    }

    public Task<AppSchema?> LoadAppSchemaAsync(string name)
    {
        if (!_apps.TryGetValue(name, out var app)) return Task.FromResult<AppSchema?>(null);
        if (_fields.TryGetValue(name, out var fieldList))
            app.Fields = fieldList.ToArray();
        return Task.FromResult(app)!;
    }

    public Task<NodeSchema[]> LoadSchemaAsync(string[] names) =>
        Task.FromResult(names.Select(n => _schemas.TryGetValue(n, out var s) ? s : null).Where(s => s != null).Cast<NodeSchema>().ToArray());

    public Task<EnumValueSchema[]> LoadEnumSubListAsync(string schemaName, string? value) =>
        Task.FromResult(Array.Empty<EnumValueSchema>());

    public Task<EnumValueAccess[]> LoadEnumAccessListAsync(string schemaName, string value, bool? noSubList = null, bool? withSubList = null) =>
        Task.FromResult(Array.Empty<EnumValueAccess>());

    public Task<bool> SaveSchemaAsync(NodeSchema schema) { _schemas[schema.FullName] = schema; return Task.FromResult(true); }
    public Task<bool> DeleteSchemaAsync(string schema) { _schemas.TryRemove(schema, out _); return Task.FromResult(true); }

    public Task<EnumValueSchema[]> SaveEnumSubListAsync(string name, string? value, EnumValueSchema[] values, bool? append) =>
        Task.FromResult(values);

    public Task<bool> SaveAppSchemaAsync(AppSchema app) { _apps[app.FullName] = app; return Task.FromResult(true); }
    public Task<bool> DeleteAppSchemaAsync(string app) { _apps.TryRemove(app, out _); _fields.TryRemove(app, out _); return Task.FromResult(true); }

    public Task<bool> SaveAppFieldSchemaAsync(string app, AppFieldSchema field)
    {
        var list = _fields.GetOrAdd(app, _ => []);
        list.RemoveAll(f => f.Name.Equals(field.Name, StringComparison.OrdinalIgnoreCase));
        list.Add(field);
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAppFieldSchemaAsync(string app, string field)
    {
        if (_fields.TryGetValue(app, out var list)) list.RemoveAll(f => f.Name.Equals(field, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(true);
    }

    public Task<bool> SwapAppFieldSchemaAsync(string app, string field1, string field2) => Task.FromResult(true);

    public Task<bool> SaveAppWorkflowSchemaAsync(string app, AppWorkflowSchema workflow)
    {
        _workflows[workflow.Name] = workflow;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAppWorkflowSchemaAsync(string app, string workflow) { _workflows.TryRemove(workflow, out _); return Task.FromResult(true); }
}
