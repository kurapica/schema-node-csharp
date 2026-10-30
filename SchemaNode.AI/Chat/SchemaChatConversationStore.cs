using Microsoft.Extensions.AI;

namespace SchemaNode.AI.Chat;

/// <summary>
/// A bounded in-memory conversation store for the schema assistant.
/// <para>
/// Only the user and assistant text is kept. Tool calls, raw MCP output and schema candidates are
/// intentionally not stored, so a later turn only sees what the user saw.
/// </para>
/// </summary>
public sealed class SchemaChatConversationStore
{
    private sealed class Conversation
    {
        public List<ChatMessage> Messages { get; } = [];
        public DateTimeOffset UpdatedAt { get; set; }
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Conversation> _conversations = new(StringComparer.Ordinal);
    private readonly SchemaChatOptions _options;

    /// <summary>Creates the store.</summary>
    public SchemaChatConversationStore(SchemaChatOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Validates a client supplied conversation identifier. Unknown or malformed identifiers are
    /// replaced by a new conversation instead of failing the request.
    /// </summary>
    public static bool IsValidId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length is < 8 or > 64) return false;
        return id.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');
    }

    /// <summary>Creates a new conversation identifier.</summary>
    public static string NewId() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets the stored messages for a conversation owned by <paramref name="owner"/>.
    /// </summary>
    public IReadOnlyList<ChatMessage> Get(string owner, string conversationId)
    {
        string key = Key(owner, conversationId);
        lock (_gate)
        {
            Cleanup();
            return _conversations.TryGetValue(key, out var conversation)
                ? conversation.Messages.ToArray()
                : [];
        }
    }

    /// <summary>Appends a message to the conversation, creating it when needed.</summary>
    public void Append(string owner, string conversationId, ChatMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Text)) return;

        string key = Key(owner, conversationId);
        lock (_gate)
        {
            Cleanup();

            if (!_conversations.TryGetValue(key, out var conversation))
            {
                conversation = new Conversation();
                _conversations[key] = conversation;
            }

            conversation.Messages.Add(message);
            conversation.UpdatedAt = DateTimeOffset.UtcNow;

            int overflow = conversation.Messages.Count - Math.Max(2, _options.MaxHistoryMessages);
            if (overflow > 0)
                conversation.Messages.RemoveRange(0, overflow);
        }
    }

    /// <summary>Removes a conversation.</summary>
    public void Clear(string owner, string conversationId)
    {
        lock (_gate)
            _conversations.Remove(Key(owner, conversationId));
    }

    private void Cleanup()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (_options.ConversationTimeoutMinutes > 0)
        {
            TimeSpan ttl = TimeSpan.FromMinutes(_options.ConversationTimeoutMinutes);
            foreach (string key in _conversations
                         .Where(p => now - p.Value.UpdatedAt > ttl)
                         .Select(p => p.Key)
                         .ToArray())
            {
                _conversations.Remove(key);
            }
        }

        int max = Math.Max(1, _options.MaxConversations);
        if (_conversations.Count <= max) return;

        foreach (string key in _conversations
                     .OrderBy(p => p.Value.UpdatedAt)
                     .Take(_conversations.Count - max)
                     .Select(p => p.Key)
                     .ToArray())
        {
            _conversations.Remove(key);
        }
    }

    private static string Key(string owner, string conversationId)
        => $"{owner}\n{conversationId}";
}
