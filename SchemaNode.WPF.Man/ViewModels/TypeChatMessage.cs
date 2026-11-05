namespace SchemaNode.WPF.Man.ViewModels;

/// <summary>
/// One message rendered in the type definition assistant transcript. Instances are immutable; the
/// screen appends them to its observable collection.
/// </summary>
public sealed class TypeChatMessage
{
    /// <summary>Creates an assistant transcript entry.</summary>
    public TypeChatMessage(string role, string text, bool isError = false)
    {
        Role = role;
        Text = text;
        IsError = isError;
    }

    /// <summary>The transport role, <c>user</c> or <c>assistant</c>.</summary>
    public string Role { get; }

    /// <summary>The message text.</summary>
    public string Text { get; }

    /// <summary>Whether the entry presents a failure rather than a normal answer.</summary>
    public bool IsError { get; }

    /// <summary>True for user messages; used by the view for bubble styling.</summary>
    public bool IsUser => Role == "user";
}
