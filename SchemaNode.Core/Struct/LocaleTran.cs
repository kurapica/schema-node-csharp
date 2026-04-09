using SchemaNode.Attribute;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Struct;

/// <summary>
/// The locale translate
/// </summary>
[Schema(NS_SYSTEM_LOCALE_TRAN)]
public sealed class LocaleTran
{
    /// <summary>
    /// default constructor
    /// </summary>
    public LocaleTran() { }

    /// <summary>
    /// The locale translate
    /// </summary>
    public LocaleTran(string lang, string? tran)
    {
        Lang = lang;
        Tran = tran;
    }

    /// <summary>
    /// The language
    /// </summary>
    [Schema(NS_SYSTEM_LANGUAGE)]
    [MaxLength(8)]
    [Index]
    public string Lang { get; set; } = string.Empty;

    /// <summary>
    /// The translation
    /// </summary>
    public string? Tran { get; set; }

    /// <summary>
    /// Convert tuple to locale translate
    /// </summary>
    public static implicit operator LocaleTran((string lang, string tran) tuple)
    {
        return new LocaleTran(tuple.lang, tuple.tran);
    }
}