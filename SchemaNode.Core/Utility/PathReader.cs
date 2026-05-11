namespace SchemaNode.Utility;

/// <summary>
/// The path reader for 'person.name'
/// </summary>
public ref struct PathReader(ReadOnlySpan<char> path)
{
    private ReadOnlySpan<char> _remaining;
    
    /// <summary>
    /// The path is empty
    /// </summary>
    public bool IsEmpty => _remaining.IsEmpty;

    public bool TryRead(out ReadOnlySpan<char> segment)
    {
        if (_remaining.IsEmpty)
        {
            segment = default;
            return false;
        }

        var index = _remaining.IndexOf('.');

        if (index < 0)
        {
            segment = _remaining;
            _remaining = default;
            return true;
        }

        segment = _remaining[..index];
        _remaining = _remaining[(index + 1)..];

        return true;
    }
    
    public static PathReader Create(ReadOnlySpan<char> path) => new(path);
}