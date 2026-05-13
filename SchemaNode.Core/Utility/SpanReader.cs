using System.Runtime.InteropServices.Marshalling;

namespace SchemaNode.Utility;

/// <summary>
/// The path reader for 'person.name'
/// </summary>
public class SpanReader(string source)
{
    private int _start = 0;
    private int _last = source.Length;

    private int _currStart = -1;
    private int _currLast = -1;

    private ReadOnlySpan<char> Trim()
    {
        ReadOnlySpan<char> span = source.AsSpan(_start, _last - _start);
        while (!span.IsEmpty && char.IsWhiteSpace(span[0]))
        {
            _start++;
            span = span[1..];
        }
        while (!span.IsEmpty && char.IsWhiteSpace(span[^1]))
        {
            _last--;
            span = span[..^1];
        }
        return span;
    }
    
    /// <summary>
    /// Whether there are no more to be read
    /// </summary>
    public bool IsEnd => _start >= _last || Trim().IsEmpty;
    
    /// <summary>
    /// The current read result
    /// </summary>
    public ReadOnlySpan<char> Current => _currStart >= 0 ? source.AsSpan(_currStart, _currLast - _currStart) : default;
    
    /// <summary>
    /// The full read result
    /// </summary>
    public ReadOnlySpan<char> Matched => _start > 0 ? source.AsSpan(0, _start) : default;
    
    /// <summary>
    /// The previous read result, which is the same as Matched if the current read is successful, otherwise the same as Current if the current read is failed
    /// </summary>
    public ReadOnlySpan<char> Previous => source.AsSpan(0, _currStart >= 0 ? _currStart : 0);
    
    /// <summary>
    /// Try read the next segment of the path, separated by the specified character.
    /// </summary>
    public bool Next(char sep)
    {
        var span = Trim();
        if (span.IsEmpty)
        {
            _currStart = -1;
            _currLast = -1;
            return false;
        }
        
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] != sep) continue;
            _currStart = _start;
            _currLast = _start + i;
            _start += i + 1;
            return true;
        }
        _currStart = _start;
        _currLast = _last;
        _start = _last;
        return true;
    }
    
    /// <summary>
    /// Try read the next path, like 'person.age'
    /// </summary>
    public bool NextPath() => Next('.');

    /// <summary>
    /// Try read generic parameter
    /// </summary>
    public bool NextGenericParam()
    {
        var span = Trim();
        if (!span.IsEmpty && span[0] == '<')
        {
            _start++;
            span = span[1..];

            if (!span.IsEmpty && span[^1] == '>')
            {
                _last--;
                span = span[..^1];
            }
        }

        if (span.IsEmpty)
        {
            _currStart = -1;
            _currLast = -1;
            return false;
        }

        int depth = 0;
        for (int i = 0; i < span.Length; i++)
        {
            switch (span[i])
            {
                case ',' when depth == 0:
                    _currStart = _start;
                    _currLast = i;
                    _start += i + 1;
                    return true;
                case '<':
                    depth++;
                    break;
                case '>':
                    depth--;
                    break;
            }
        }
        _currStart = _start;
        _currLast = _last;
        _start = _last;
        return true;
    }
    
    /// <summary>
    /// Try read the next namespace part
    /// </summary>
    public bool NextNamespace()
    {
        var span = Trim();
        if (span.IsEmpty)
        {
            _currStart = -1;
            _currLast = -1;
            return false;
        }

        for (int i = 0; i < span.Length; i++)
        {
            switch (span[i])
            {
                case '.':
                    _currStart = _start;
                    _currLast = _start + i;
                    _start += i + 1;
                    return true;
                case '<' when i > 0: // must be used for generic type parameters, e.g. List<int>
                    _currStart = _start;
                    _currLast = _start + i;
                    _start += i;
                    return true;
            }
        }
        _currStart = _start;
        _currLast = _last;
        _start = _last;
        return true;
    }

    /// <summary>
    /// Generate Span reader based on string
    /// </summary>
    public static implicit operator SpanReader(string source) => new(source);

    /// <summary>
    /// Generate Span reader based on span
    /// </summary>
    public static implicit operator SpanReader(ReadOnlySpan<char> source) => new(source.ToString());
}