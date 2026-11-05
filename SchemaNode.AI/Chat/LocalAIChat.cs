using System.Runtime.CompilerServices;

namespace SchemaNode.AI.Chat;

/// <summary>
/// The in-process <see cref="IAIChat"/> implementation. It delegates to
/// <see cref="SchemaChatService"/> so prompt assembly, tool execution and validation are shared
/// with the HTTP endpoints instead of being duplicated by hosts.
/// </summary>
public sealed class LocalAIChat : IAIChat
{
    private const string Owner = "in-process";

    private readonly SchemaChatService _service;

    /// <summary>Creates the local implementation.</summary>
    public LocalAIChat(SchemaChatService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AIChatUpdate> ChatAsync(
        AIChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ChatSchemaRequest wire = AIChatMapper.ToWire(request);
        IAsyncEnumerable<SchemaChatUpdate> updates = request.Stream
            ? _service.ExecuteStreamAsync(wire, Owner, cancellationToken)
            : _service.ExecuteAsync(wire, Owner, cancellationToken);

        await foreach (SchemaChatUpdate update in updates.ConfigureAwait(false))
        {
            switch (update)
            {
                case SchemaChatStarted started:
                    yield return new AIChatStarted(started.ConversationId);
                    break;
                case SchemaChatTextDelta delta:
                    yield return new AIChatDelta(delta.Text);
                    break;
                case SchemaChatCompleted completed:
                    yield return new AIChatCompleted(AIChatMapper.ToResult(completed.Response));
                    break;
                case SchemaChatFailed failed:
                    yield return new AIChatFailed(failed.Code, failed.Message);
                    break;
            }
        }
    }
}
