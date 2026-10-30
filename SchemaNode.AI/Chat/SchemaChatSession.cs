namespace SchemaNode.AI.Chat;

/// <summary>
/// Per request state shared by the chat tools. Registered as a scoped service so every HTTP
/// request gets an isolated capture slot.
/// </summary>
public sealed class SchemaChatSession
{
    private readonly List<string> _toolCalls = [];
    private readonly List<string> _toolErrors = [];

    /// <summary>The schema target of the current editor.</summary>
    public string? Target { get; set; }

    /// <summary>The last schema change proposed by the assistant.</summary>
    public SchemaChangeCandidate? Proposal { get; set; }

    /// <summary>The number of proposal tool calls in this turn.</summary>
    public int ProposalCount { get; set; }

    /// <summary>The MCP tools invoked during this turn (names only).</summary>
    public IReadOnlyList<string> ToolCalls => _toolCalls;

    /// <summary>The tool invocation errors observed during this turn.</summary>
    public IReadOnlyList<string> ToolErrors => _toolErrors;

    /// <summary>Records a tool invocation.</summary>
    public void RecordToolCall(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
            _toolCalls.Add(name);
    }

    /// <summary>Records a tool invocation failure.</summary>
    public void RecordToolError(string name, string message)
        => _toolErrors.Add($"{name}: {message}");
}
