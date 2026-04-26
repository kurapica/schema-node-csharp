using System.Collections.Concurrent;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Schema;
using SchemaNode.Struct;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory enum schema representation
/// </summary>
public sealed class EnumType : AnySchemaType
{
    private const int MAX_SUBLIST_LEVEL = 3;

    #region Data

    /// <summary>
    /// The enum value type
    /// </summary>
    public EnumValueType ValueType { get; internal set; } = EnumValueType.String;

    /// <summary>
    /// The cascade list
    /// </summary>
    public LocaleString[]? Cascade { get; internal set; }

    #endregion

    #region Status

    /// <summary>
    /// The max flags value
    /// </summary>
    long MaxFlags { get; set; }

    /// <summary>
    /// The root for all enum values
    /// </summary>
    EnumValueSchema Root { get; set; } = new();

    /// <summary>
    /// The enum value cache
    /// </summary>
    readonly ConcurrentDictionary<string, EnumValueSchema> _valueMaps = new(StringComparer.OrdinalIgnoreCase);

    #endregion

    #region Loading

    /// <inheritdoc />
    public override Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        EnumSchema? @enum = schema.GetProperty<EnumProperty>()?.Value;

        // Data
        _valueMaps.Clear();
        ValueType = @enum?.Type ?? EnumValueType.String;
        Cascade = @enum?.Cascade;
        Root = new EnumValueSchema
        {
            SubList = @enum?.Values
        };
        UpdateLoadState(Root, reset: true);
        UpdateMaxFlags();

        // Status
        if (@enum == null) Error = "no_definition";
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the enum value info by value
    /// </summary>
    public EnumValueSchema? GetEnumValueInfo(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return _valueMaps.TryGetValue(value, out var node) ? node : null;
    }

    /// <summary>
    /// Gets the root values
    /// </summary>
    public EnumValueSchema[] GetRootValues() => Root.SubList ?? [];

    /// <inheritdoc />
    public override void ReleaseType()
    {
        _valueMaps.Clear();
        Root = new EnumValueSchema();
        base.ReleaseType();
    }

    #endregion

    #region Utility

    void UpdateLoadState(EnumValueSchema node, int level = 999, EnumValueSchema? parent = null, bool reset = false)
    {
        if (node.IsFullyLoaded && !reset || level == 0) return;
        node.IsFullyLoaded = false;
        if (!string.IsNullOrWhiteSpace(node.Value))
            _valueMaps[node.Value] = node;

        if (parent != null)
        {
            node.Parent = parent;
            node.Level = parent.Level + 1;
        }

        if (node.SubList is { Length: > 0 }) node.HasSubList = true;

        if (node.HasSubList ?? false)
        {
            if (node.SubList is { Length: > 0 })
            {
                foreach (var item in node.SubList)
                    UpdateLoadState(item, level - 1, node, reset);
                node.IsFullyLoaded = node.SubList.All(x => x.IsFullyLoaded);
                return;
            }
        }
        else
        {
            node.IsFullyLoaded = true;
        }
    }

    void UpdateMaxFlags()
    {
        if (ValueType != EnumValueType.Flags || Root.SubList is not { Length: > 0 }) return;
        long max = 0;
        foreach (EnumValueSchema info in Root.SubList)
        {
            if (long.TryParse(info.Value, out long val))
                max = Math.Max(max, val);
        }
        MaxFlags = max * 2;
    }

    #endregion
}
