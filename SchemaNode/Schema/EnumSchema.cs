using System.Text.Json.Serialization;
using SchemaNode.Enum;

namespace SchemaNode.Schema;

/// <summary>
/// The enum type schema
/// </summary>
public class EnumSchema
{
    /// <summary>
    /// The enum value type
    /// </summary>
    public EnumValueType Type { get; set; }

    /// <summary>
    /// The cascades of the enum value
    /// </summary>
    public string[]? Cascade { get; set; }

    /// <summary>
    /// The enum values
    /// </summary>
    public EnumValueInfo[] Values { get; set; } = [];
}

/// <summary>
/// The enum value info
/// </summary>
public class EnumValueInfo
{
    /// <summary>
    /// The value
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// The name of the enum value
    /// </summary>
    public LocaleString Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether the enum value is disabled
    /// </summary>
    public bool? Disable  { get; set; }

    /// <summary>
    /// Whether the enum value has sub enum values
    /// </summary>
    public bool? HasSubList { get; set; }
    
    /// <summary>
    /// The sub enum values
    /// </summary>
    public EnumValueInfo[]? SubList { get; set; }

    /// <summary>
    /// Whether the enum value is fully loaded
    /// </summary>
    [JsonIgnore]
    public bool IsFullyLoaded { get; set; } = false;

    /// <summary>
    /// Refresh status
    /// </summary>
    public bool CheckFullyLoadedStatus(int level = 999)
    {
        if (IsFullyLoaded || level == 0) return true;
        
        // If loaded from static resources
        if (SubList is not null && SubList.Length > 0) HasSubList = true;

        if (HasSubList ?? false)
        {
            if (SubList is not null && SubList.Length > 0 && 
                SubList.All(x => x.CheckFullyLoadedStatus(level - 1)))
            {
                IsFullyLoaded = SubList.All(x => x.IsFullyLoaded);
                return true;
            }
        }
        else
        {
            IsFullyLoaded = true;
        }

        return IsFullyLoaded;
    }

    /// <summary>
    /// Combine the access list
    /// </summary>
    /// <param name="accesses"></param>
    public void CombineAccessList(EnumValueAccess[] accesses)
    {
        if (accesses.Length == 0) return;
        EnumValueAccess current = accesses[0];

        if (current.SubList is not null && current.SubList.Length > 0)
        {
            if (SubList is null || SubList.Length != current.SubList.Length)
            {
                // replace with new
                if (SubList is not null && SubList.Length > 0) {
                    foreach (EnumValueInfo v in current.SubList)
                    {
                        EnumValueInfo? match = SubList!.FirstOrDefault(x => x.Value.Equals(v.Value, StringComparison.OrdinalIgnoreCase));
                        if (match is not null) v.SubList = match.SubList;
                    }
                }

                SubList = current.SubList;
            }

            if (accesses.Length > 1)
            {
                EnumValueInfo? match = SubList!.FirstOrDefault(x => x.Value.Equals(current.Value, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                {
                    match.CombineAccessList(accesses.Skip(1).ToArray());
                }
            }
        }
    }
    
    /// <summary>
    /// Gets the already existed sub enum node
    /// </summary>
    public EnumValueInfo[]? GetEnumAccesses(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(Value)) return [this];
        if (Value.Equals(value, StringComparison.OrdinalIgnoreCase)) return [this];
        if (SubList is not null && SubList.Length > 0)
        {
            foreach (EnumValueInfo info in SubList)
            {
                EnumValueInfo[]? subInfo = info.GetEnumAccesses(value);
                if (subInfo?.Length > 0) return subInfo.Prepend(this).ToArray();
            }
        }
        return null;
    }

    /// <summary>
    /// Clones the enum value with limit level
    /// </summary>
    /// <param name="limitLevel"></param>
    /// <returns></returns>
    public EnumValueInfo Clone(int limitLevel = 0)
    {
        return new EnumValueInfo
        {
            Value = Value,
            Name = Name,
            Disable = Disable,
            HasSubList = HasSubList,
            SubList = (HasSubList ?? false) && SubList is { Length: > 0 } && limitLevel > 0 
                ? SubList.Select(e => e.Clone(limitLevel - 1)).ToArray()
                : null
        };
    }
}

/// <summary>
/// The enum value access info
/// </summary>
public class EnumValueAccess
{
    /// <summary>
    /// The cascade name
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// The enum value of the cascade
    /// </summary>
    public string Value { get; set; } = string.Empty;
    
    /// <summary>
    /// The sublist of the enum value
    /// </summary>
    public EnumValueInfo[]? SubList { get; set; }
}