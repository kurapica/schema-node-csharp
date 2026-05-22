namespace SchemaNode.Enum;

/// <summary>
/// The date format mode
/// </summary>
public enum DateFormatMode
{
    /// <summary>ISO 8601 — 2026-02-02T14:30:00Z</summary>
    Iso8601 = 0,
    /// <summary>Date only — 2026-02-02</summary>
    DateOnly = 1,
    /// <summary>Date and time — 2026-02-02 14:30:00</summary>
    DateTime = 2,
    /// <summary>Compact — 20260202 or 20260202143000</summary>
    Compact = 3,
    /// <summary>Unix timestamp (seconds) — 1706845800</summary>
    UnixSeconds = 4,
    /// <summary>Unix timestamp (milliseconds) — 1706845800123</summary>
    UnixMilliseconds = 5,
    /// <summary>.NET ticks</summary>
    Ticks = 6,
    /// <summary>RFC1123 — Mon, 02 Feb 2026 14:30:00 GMT</summary>
    Rfc1123 = 7,
    /// <summary>Slash date time — 2024/12/12 14:30:00</summary>
    SlashDateTime = 8,
}
