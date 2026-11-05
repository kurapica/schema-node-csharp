namespace SchemaNode.AI.Agents;

/// <summary>
/// Deterministic provider that replays a fixed list of responses. It performs no I/O and can be
/// used offline by unit tests and manual dry runs.
/// </summary>
public sealed class MockAgentProvider : IAgentProvider
{
    private readonly string[] _responses;
    private readonly List<AIAgentRequest> _requests = [];
    private int _calls;

    /// <summary>
    /// Creates a mock provider. When more calls are made than responses are supplied, the last
    /// response is repeated. An empty list always returns <c>{}</c>.
    /// </summary>
    public MockAgentProvider(params string[]? responses)
    {
        _responses = responses is { Length: > 0 } ? responses : ["{}"];
    }

    /// <inheritdoc />
    public string Name => "mock";

    /// <summary>
    /// The number of completions requested so far.
    /// </summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <summary>
    /// The requests received so far, in call order. Intended for assertions in tests.
    /// </summary>
    public IReadOnlyList<AIAgentRequest> Requests
    {
        get
        {
            lock (_requests) return _requests.ToArray();
        }
    }

    /// <inheritdoc />
    public Task<string> CompleteAsync(AIAgentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_requests) _requests.Add(request);

        int call = Interlocked.Increment(ref _calls);
        int index = Math.Min(call - 1, _responses.Length - 1);
        return Task.FromResult(_responses[index]);
    }
}
