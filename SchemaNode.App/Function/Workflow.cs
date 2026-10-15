using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Workflow;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Struct;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Function;


[Meta<SchemaType>(NS_SYSTEM_SCHEMA_REFLECT_WORKFLOW)]
public static class SystemReflectWorkflow
{
    /// <summary>
    /// Checks the given workflow is of the given kind
    /// </summary>
    public static async Task<bool> iskind(SchemaContext context,
        [Meta<SchemaType>(typeof(Schema.WorkflowType))] string? workflow,
        [Meta<SchemaType>(typeof(WorkflowKind))] string kind)
    {
        var workflowType = !string.IsNullOrWhiteSpace(workflow) ? await context.GetNodeTypeAsync<Runtime.WorkflowType>(workflow) : null;
        return kind.Equals(workflowType?.WorkflowKind, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The workflow type has arguments
    /// </summary>
    public static async Task<bool> hasargs(SchemaContext context,
        [Meta<SchemaType>(typeof(Schema.WorkflowType))] string workflow)
    {
        var workflowType = !string.IsNullOrWhiteSpace(workflow) ? await context.GetNodeTypeAsync<Runtime.WorkflowType>(workflow) : null;
        return workflowType?.Args?.Length > 0;
    }

    /// <summary>
    /// The workflow type has state
    /// </summary>
    public static async Task<bool> hasstate(SchemaContext context,
        [Meta<SchemaType>(typeof(Schema.WorkflowType))] string workflow)
    {
        var workflowType = !string.IsNullOrWhiteSpace(workflow) ? await context.GetNodeTypeAsync<Runtime.WorkflowType>(workflow) : null;
        return workflowType?.State != null;
    }

    /// <summary>
    /// The workflow type is forkable
    /// </summary>
    public static async Task<bool> isforkable(SchemaContext context,
        [Meta<SchemaType>(typeof(WorkflowKind))] string workflow)
    {
        var workflowType = !string.IsNullOrWhiteSpace(workflow) ? await context.GetNodeTypeAsync<Runtime.WorkflowType>(workflow) : null;
        return workflowType?.GetProperty<Forkable>()?.GetValue<bool>() ?? false;
    }

    /// <summary>
    /// Gets the workflow payload type
    /// </summary>
    public static async Task<string?> getpayloadtype(SchemaContext context,
        [Meta<SchemaType>(typeof(Schema.WorkflowType))] string workflow)
    {
        var workflowType = !string.IsNullOrWhiteSpace(workflow) ? await context.GetNodeTypeAsync<Runtime.WorkflowType>(workflow) : null;
        return workflowType?.Payload?.Name;
    }

    /// <summary>
    /// Gets the workflow state type
    /// </summary>
    public static async Task<string?> getstatetype(SchemaContext context,
        [Meta<SchemaType>(typeof(Schema.WorkflowType))] string workflow)
    {
        var workflowType = !string.IsNullOrWhiteSpace(workflow) ? await context.GetNodeTypeAsync<Runtime.WorkflowType>(workflow) : null;
        return workflowType?.State?.Name;
    }

    /// <summary>
    /// Gets the arguments of the function schema
    /// </summary>
    public static async Task<List<EntryAccess<string>>> getaccessentries(SchemaContext context,
        AppWorkflowNodeSchema[]? nodes,
        string[]? previous = null,
        string? path = null,
        [Meta<EntryRoot>(true)] string? root = null)
    {
        if (!string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(path) && !path.Equals(root, StringComparison.OrdinalIgnoreCase) && !path.StartsWith($"{root}.", StringComparison.OrdinalIgnoreCase))
            return []; // not access-able
        path ??= root;

        List<Entry<string>> first = [];
        IValueTypeAccess? valueType = null;
        Entry<string>? curr = null;

        // expressions
        HashSet<string>? prevNodes;
        if (nodes is { Length: > 0 })
        {
            if (previous is {  Length: > 0})
            {
                prevNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                addPrevious(previous);
                nodes = nodes.Where(p => prevNodes.Contains(p.Name)).ToArray();
            }

            foreach (var node in nodes)
            {
                if (string.IsNullOrWhiteSpace(node.Name) || string.IsNullOrWhiteSpace(node.Type)) continue;
                var workflowType = await context.GetNodeTypeAsync<Runtime.WorkflowType>(node.Type);
                if (workflowType?.Payload == null) continue;

                var entry = new Entry<string> { Value = node.Name, HasChildren = workflowType.Payload is not Runtime.ArrayType && workflowType.Payload.HasAccessEntries };
                entry.SetProperty<Display, LocaleString>(node.GetProperty<Display>()?.Value ?? node.Name);
                first.Add(entry);

                if (curr == null && !string.IsNullOrWhiteSpace(path) &&
                    (path.Equals(node.Name, StringComparison.OrdinalIgnoreCase) ||
                     path.StartsWith($"{node.Name}.", StringComparison.OrdinalIgnoreCase)))
                {
                    valueType = workflowType.Payload;
                    curr = entry;
                }
            }
        }

        // build the access entries
        List<EntryAccess<string>> result = [new() { Children = first.ToArray() }];

        while (valueType != null)
        {
            var accessEntry = new EntryAccess<string>();
            Entry<string>[] accesses = valueType is not Runtime.ArrayType ? valueType.GetAccessEntries().ToArray() : [];
            if (curr != null)
            {
                accessEntry.Entry = new Entry<string> { Value = curr.Value, HasChildren = accesses.Length > 0 };
                accessEntry.Entry.SetProperty<Display, LocaleString>(curr.GetProperty<Display>()?.Value ?? curr.Value);
            }
            accessEntry.Children = accesses;

            // check next part
            IValueTypeAccess? next = null;
            Entry<string>? nextCurr = null;
            foreach (var a in accesses)
            {
                string n = a.Value;
                var nvtype = valueType.GetAccessValueType(n);
                if (curr != null) a.Value = $"{curr.Value}.{n}";
                if (nvtype is Runtime.ArrayType) a.HasChildren = false;
                if (!string.IsNullOrWhiteSpace(path) && (path.Equals(a.Value, StringComparison.OrdinalIgnoreCase) ||
                                                         path.StartsWith($"{a.Value}.", StringComparison.OrdinalIgnoreCase)))
                {
                    next = nvtype;
                    nextCurr = a;
                }
            }
            result.Add(accessEntry);
            valueType = next;
            curr = nextCurr;
        }

        // cut
        if (!string.IsNullOrWhiteSpace(root))
            result = result.SkipWhile(r => (r.Entry?.Value.Length ?? 0) < root.Length).ToList();
        return result;

        void addPrevious(string[]? prevs)
        {
            if (prevs == null || prevs.Length == 0) return;
            foreach(var n in nodes)
            {
                if (prevNodes.Contains(n.Name)) continue;
                if (prevs.Any(p => p.Equals(n.Name, StringComparison.CurrentCulture)))
                {
                    prevNodes!.Add(n.Name);
                    addPrevious(n.Previous);
                }
            }
        }
    }

    /// <summary>
    /// Gets the access value type
    /// </summary>
    public static async Task<string?> getaccessvaluetype(SchemaContext context, AppWorkflowNodeSchema[]? nodes, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        string[] paths = path.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        var type = nodes?.FirstOrDefault(f => f.Name.Equals(paths[0], StringComparison.OrdinalIgnoreCase))?.Type;
        var workflowType = !string.IsNullOrWhiteSpace(type) ? await context.GetNodeTypeAsync<Runtime.WorkflowType>(type) : null;
        return paths.Length > 1 ? workflowType?.Payload?.GetAccessValueType(paths[1])?.Name : workflowType?.Payload?.Name;
    }
}