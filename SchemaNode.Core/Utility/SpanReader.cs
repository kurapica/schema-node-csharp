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
    public bool IsEmpty => _start >= _last || Trim().IsEmpty;
    
    /// <summary>
    /// The current read result
    /// </summary>
    public ReadOnlySpan<char> Current => _currStart >= 0 ? source.AsSpan(_currStart, _currLast - _currStart) : default;
    
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
}