namespace SchemaNode.Enum;

/// <summary>
/// The date format mode
/// </summary>
public enum DateFormatMode
{
    /// <summary>
    /// ISO 8601
    /// 2026-02-02T14:30:00Z / 2026-02-02T14:30:00+08:00
    /// </summary>
    Iso8601 = 0,

    /// <summary>
    /// 2026-02-02
    /// </summary>
    DateOnly = 1,

    /// <summary>
    /// 2026-02-02 14:30:00
    /// </summary>
    DateTime = 2,

    /// <summary>
    /// 20260202 或 20260202143000
    /// </summary>
    Compact = 3,

    /// <summary>
    /// Unix Timestamp(sec)
    /// 1706845800
    /// </summary>
    UnixSeconds = 4,

    /// <summary>
    /// Unix Timestamp(nano)
    /// 1706845800123
    /// </summary>
    UnixMilliseconds = 5,

    /// <summary>
    /// .NET ticks
    /// </summary>
    Ticks = 6,

    /// <summary>
    /// RFC1123
    /// Mon, 02 Feb 2026 14:30:00 GMT
    /// </summary>
    Rfc1123 = 7,
    
    /// <summary>
    /// Slash Date Time
    /// 2024/12/12 14:30:00
    /// </summary>
    SlashDateTime = 8,
}
