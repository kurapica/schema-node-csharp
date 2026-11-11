using SchemaNode.Avalonia.View.NodeWrapper;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Utility;

namespace SchemaNode.Avalonia.View;

/// <summary>Where a node is being rendered.</summary>
public enum SchemaViewRole
{
    /// <summary>A form field.</summary>
    Field,

    /// <summary>A table cell (compact, no inline error block).</summary>
    Cell,

    /// <summary>A standalone row editor.</summary>
    RowEditor,
}

/// <summary>Keys of the built-in views.</summary>
public static class SchemaTemplateKeys
{
    public const string Input = "SchemaInput";
    public const string Boolean = "SchemaBoolean";
    public const string Date = "SchemaDate";
    public const string Year = "SchemaYear";
    public const string Language = "SchemaLanguage";
    public const string RangeDate = "SchemaRangeDate";
    public const string LocaleString = "SchemaLocaleString";
    public const string FlagsEnum = "SchemaFlagsEnum";
    public const string Struct = "SchemaStruct";
    public const string Array = "SchemaArray";
    public const string Table = "SchemaTable";
    public const string TypeSelector = "SchemaTypeSelector";
    public const string Fallback = "SchemaFallback";
}

/// <summary>
/// One resolved view registration: the template key plus an optional wrapper factory.
///
/// <see cref="WrapperFactory"/> is null when the registration leaves wrapper construction to the
/// built-in <see cref="NodeWrapperFactory"/>. A host or plugin can supply its own factory to render
/// a node through a wrapper the library does not know.
/// </summary>
public sealed record SchemaViewRegistration(
    string TemplateKey,
    Func<IValueAccess, DataNodeWrapper>? WrapperFactory);

/// <summary>
/// View registry, mirroring the vue view resolver:
///
///   1. explicit per-wrapper override;
///   2. type-specific registration, walking scalar <c>BaseType</c> and falling back from the
///      requested skin to <c>default</c> and from a non-field role to <c>Field</c>;
///   3. generic kind registration (array/struct/scalar dispatch lives here);
///   4. default for the node shape, ending at the read-only fallback for unknown shapes.
/// </summary>
public sealed class SchemaViewRegistry
{
    /// <summary>Default skin name.</summary>
    public const string DefaultSkin = "default";

    private static readonly Lazy<SchemaViewRegistry> DefaultRegistry = new(CreateDefault);

    private static readonly HashSet<string> SimpleElementKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        Constant.SCHEMA_KIND_NODE_BOOL,
        Constant.SCHEMA_KIND_NODE_INT,
        Constant.SCHEMA_KIND_NODE_DECIMAL,
        Constant.SCHEMA_KIND_NODE_STRING,
        Constant.SCHEMA_KIND_NODE_DATE,
        Constant.SCHEMA_KIND_NODE_ENUM,
    };

    private readonly Dictionary<string, KindRegistration> _kinds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<TypeKey, TypeRegistration> _types = new();
    private readonly Dictionary<TypeKey, TypeLookup> _typeViews = new();
    private readonly HashSet<string> _singleViews = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SchemaTableOptions> _tableOptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Func<DataNode, SchemaViewRole, string, SchemaViewRegistration?>> _nodeResolvers = [];

    /// <summary>A shared registry with the built-in registrations.</summary>
    public static SchemaViewRegistry Default => DefaultRegistry.Value;

    /// <summary>Creates a registry with the built-in kind and type mappings.</summary>
    public static SchemaViewRegistry CreateDefault()
    {
        SchemaViewRegistry registry = new();

        registry.RegisterKind(Constant.SCHEMA_KIND_NODE_BOOL, SchemaTemplateKeys.Boolean);
        registry.RegisterKind(Constant.SCHEMA_KIND_NODE_INT, SchemaTemplateKeys.Input);
        registry.RegisterKind(Constant.SCHEMA_KIND_NODE_DECIMAL, SchemaTemplateKeys.Input);
        registry.RegisterKind(Constant.SCHEMA_KIND_NODE_STRING, SchemaTemplateKeys.Input);
        // Struct nodes report the generic object kind, so this mapping must point at the struct view.
        registry.RegisterKind(Constant.SCHEMA_KIND_NODE_OBJECT, SchemaTemplateKeys.Struct);
        registry.RegisterKind(Constant.SCHEMA_KIND_NODE_ENUM, SchemaTemplateKeys.Input);
        registry.RegisterKind(Constant.SCHEMA_KIND_NODE_DATE, SchemaTemplateKeys.Date);
        registry.RegisterKind(Constant.SCHEMA_KIND_NODE_STRUCT, SchemaTemplateKeys.Struct);
        registry.RegisterKind(Constant.SCHEMA_KIND_NODE_ARRAY, null, registry.ResolveArray);

        // Type path nodes (namespace / type / value type / array element / function type, and the
        // schema kind subclasses of system.schema.node.type) use the namespace.vue counterpart: the
        // plain path cascade plus generic argument editors. Registering the base type covers every
        // subclass through the BaseType walk.
        string typePath = $"{Constant.NS_SYSTEM_SCHEMA_NODE}.type";
        foreach (SchemaViewRole role in new[] { SchemaViewRole.Field, SchemaViewRole.Cell, SchemaViewRole.RowEditor })
        {
            registry.RegisterType(typePath, SchemaTemplateKeys.TypeSelector, role, DefaultSkin,
                wrapperFactory: node => new TypeSelectorWrapper((DataNode)node, registry, null, role));
        }

        // Date-semantic scalar and range structs. The type names are the Core ones, so every host
        // that uses the default registry gets the year / range / single-language editors; a host
        // that wants a different presentation overrides the same type in its own registry.
        registry.RegisterType(Constant.NS_SYSTEM_YEAR, SchemaTemplateKeys.Year,
            wrapperFactory: node => new YearWrapper((DataNode)node, registry, null, SchemaViewRole.Field));

        registry.RegisterType(Constant.NS_SYSTEM_LANGUAGE, SchemaTemplateKeys.Language,
            wrapperFactory: node => new LanguageWrapper((DataNode)node, registry, null, SchemaViewRole.Field));

        foreach (string rangeType in new[]
                 {
                     Constant.NS_SYSTEM_RANGE_YEAR,
                     Constant.NS_SYSTEM_RANGE_MONTH,
                     Constant.NS_SYSTEM_RANGE_DATE,
                     Constant.NS_SYSTEM_RANGE_FULL_DATE,
                 })
        {
            registry.RegisterType(rangeType, SchemaTemplateKeys.RangeDate,
                wrapperFactory: node => new RangeDateWrapper((DataNode)node, registry, null, SchemaViewRole.Field));
        }

        registry.RegisterType(Constant.NS_SYSTEM_LOCALE_STRING, SchemaTemplateKeys.LocaleString,
            wrapperFactory: node => new LocaleStringWrapper((DataNode)node, registry, null, SchemaViewRole.Field));

        // Flags enums share the enum kind; the flag characteristic is part of the node, not the
        // type name. The node resolver keeps this dispatch in the registry so no generic wrapper
        // has to test "is this a flag enum".
        registry.RegisterNodeResolver((node, role, skin) =>
            node is EnumNode { Type: EnumType { Type: EnumValueType.Flags } }
                ? new SchemaViewRegistration(
                    SchemaTemplateKeys.FlagsEnum,
                    value => new FlagsEnumWrapper((DataNode)value, registry, null, role))
                : null);

        return registry;
    }

    /// <summary>
    /// Registers a generic kind view. A resolver can decide per node; when it returns null the
    /// static <paramref name="templateKey"/> (if any) is used. <paramref name="wrapperFactory"/>
    /// replaces the built-in wrapper for nodes of this kind.
    /// </summary>
    public void RegisterKind(
        string kind,
        string? templateKey,
        Func<IValueAccess, string?>? resolve = null,
        Func<IValueAccess, DataNodeWrapper>? wrapperFactory = null)
    {
        _kinds[kind] = new KindRegistration(templateKey, resolve, wrapperFactory);
        _typeViews.Clear();
    }

    /// <summary>
    /// Registers (or overrides) a type-specific view. Registering the same type twice replaces the
    /// previous entry, which is how a host or a third-party plugin overrides a built-in view.
    /// </summary>
    public void RegisterType(
        string typeName,
        string templateKey,
        SchemaViewRole role = SchemaViewRole.Field,
        string skin = DefaultSkin,
        bool asSingle = false,
        Func<IValueAccess, DataNodeWrapper>? wrapperFactory = null)
    {
        string normalizedSkin = NormalizeSkin(skin);
        TypeKey key = new(typeName.ToLowerInvariant(), normalizedSkin, role);
        _types[key] = new TypeRegistration(templateKey, wrapperFactory);
        if (asSingle)
            _singleViews.Add(SingleViewKey(typeName, normalizedSkin));
        _typeViews.Clear();
    }

    /// <summary>
    /// Marks an array type as a single "container" view and maps it to the table template.
    /// </summary>
    public void RegisterTable(
        string arrayTypeName,
        string skin = DefaultSkin,
        Func<IValueAccess, DataNodeWrapper>? wrapperFactory = null)
        => RegisterType(arrayTypeName, SchemaTemplateKeys.Table, SchemaViewRole.Field, skin, asSingle: true, wrapperFactory);

    /// <summary>
    /// Marks an array type as a table and carries the registration-time table options (column
    /// whitelist, read-only projection and row-editor appearance).
    /// </summary>
    public void RegisterTable(
        string arrayTypeName,
        SchemaTableOptions options,
        string skin = DefaultSkin,
        Func<IValueAccess, DataNodeWrapper>? wrapperFactory = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        _tableOptions[arrayTypeName.ToLowerInvariant()] = options;
        RegisterTable(arrayTypeName, skin, wrapperFactory);
    }

    /// <summary>Options registered for an array type, or null when the type keeps the plain defaults.</summary>
    public SchemaTableOptions? GetTableOptions(IValueTypeAccess type)
        => _tableOptions.GetValueOrDefault(type.Name.ToLowerInvariant());

    /// <summary>
    /// Resolves the template key and optional wrapper factory for a node. The node is not wrapped
    /// by this method; the caller materializes a wrapper only when it actually renders the node.
    /// </summary>
    public SchemaViewRegistration Resolve(IValueAccess node, SchemaViewRole role, string skin)
    {
        if (node is not DataNode dataNode)
            return new SchemaViewRegistration(SchemaTemplateKeys.Fallback, null);

        SchemaViewRegistration? nodeSpecific =
            ResolveNodeRegistration(dataNode, role, NormalizeSkin(skin));
        if (nodeSpecific is not null) return nodeSpecific;

        TypeRegistration? type = FindTypeRegistration(dataNode.Type, role, NormalizeSkin(skin));
        if (type is not null)
            return new SchemaViewRegistration(type.TemplateKey, type.WrapperFactory);

        KindRegistration? kind = _kinds.GetValueOrDefault(dataNode.Type.Kind);
        if (kind is not null)
        {
            string? resolved = kind.Resolve?.Invoke(dataNode);
            string? key = resolved ?? kind.TemplateKey;
            if (key is not null)
                return new SchemaViewRegistration(key, kind.WrapperFactory);
        }

        return new SchemaViewRegistration(DefaultTemplateKey(dataNode), null);
    }

    /// <summary>
    /// Built-in view for a node shape. Unknown shapes degrade to the read-only fallback so the rest
    /// of the form stays usable instead of rendering blank.
    /// </summary>
    public string DefaultTemplateKey(IValueAccess node)
    {
        if (node is EnumArrayNode) return SchemaTemplateKeys.Input;
        if (node is ArrayNode array)
            return ShouldUseTable(array) ? SchemaTemplateKeys.Table : SchemaTemplateKeys.Array;
        if (node is IEnumerable<IValueAccess>) return SchemaTemplateKeys.Array;
        if (node is StructNode) return SchemaTemplateKeys.Struct;
        if (node is DateNode) return SchemaTemplateKeys.Date;
        if (node is BoolNode) return SchemaTemplateKeys.Boolean;
        if (node is ScalarNode) return SchemaTemplateKeys.Input;
        return SchemaTemplateKeys.Fallback;
    }

    /// <summary>
    /// Returns the custom wrapper factory declared by the winning registration, or null when the
    /// built-in shape factory should be used.
    /// </summary>
    public Func<IValueAccess, DataNodeWrapper>? GetWrapperFactory(
        IValueAccess node,
        SchemaViewRole role,
        string skin)
    {
        if (node is not DataNode dataNode) return null;

        string normalizedSkin = NormalizeSkin(skin);
        SchemaViewRegistration? nodeSpecific =
            ResolveNodeRegistration(dataNode, role, normalizedSkin);
        if (nodeSpecific is not null) return nodeSpecific.WrapperFactory;

        TypeRegistration? type = FindTypeRegistration(dataNode.Type, role, normalizedSkin);
        if (type is not null) return type.WrapperFactory;

        return _kinds.GetValueOrDefault(dataNode.Type.Kind)?.WrapperFactory;
    }

    /// <summary>
    /// Looks up a type-specific view: own type first, then scalar base types, role fallback and
    /// skin fallback. Returns null when nothing is registered.
    /// </summary>
    public string? GetTypeView(IValueTypeAccess type, SchemaViewRole role, string skin)
        => FindTypeRegistration(type, role, NormalizeSkin(skin))?.TemplateKey;

    /// <summary>
    /// Mirrors vue's <c>useSingleView</c>: an explicit single-view registration wins; structs are
    /// single only when they are registered and contain no nested struct/array field; arrays of
    /// simple scalar kinds are single.
    /// </summary>
    public bool UseSingleView(IValueTypeAccess type, string skin = DefaultSkin)
    {
        string normalizedSkin = NormalizeSkin(skin);
        if (_singleViews.Contains(SingleViewKey(type.Name, normalizedSkin))) return true;

        if (type is StructType structType)
        {
            bool registered = _types.Keys.Any(k =>
                string.Equals(k.TypeName, type.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(k.Skin, normalizedSkin, StringComparison.OrdinalIgnoreCase));
            if (!registered) return false;
            return !structType.GetFields().Any(f => f.Type is StructType or ArrayType);
        }

        if (type is ArrayType arrayType)
            return arrayType.Element is not null && SimpleElementKinds.Contains(arrayType.Element.Kind);

        return true;
    }

    /// <summary>
    /// Optional host hook used by the table view instead of the built-in modal row editor.
    /// The handler returns <see langword="true"/> when the user confirms; a confirmed add-row then
    /// inserts the node passed in the request. It is asynchronous because Avalonia dialogs complete
    /// through the dispatcher rather than blocking the calling thread.
    /// </summary>
    public Func<TableRowEditorRequest, Task<bool>>? RowEditorFactory { get; set; }

    /// <summary>
    /// Optional host hook consulted before type and kind registrations. It lets a host pick a
    /// node-specific view that cannot be expressed by type name.
    /// </summary>
    public Func<IValueAccess, SchemaViewRole, string, SchemaViewRegistration?>? NodeRegistrationResolver { get; set; }

    /// <summary>
    /// Registers a built-in node-specific resolver. Unlike <see cref="NodeRegistrationResolver"/>,
    /// which a host owns, these are part of the registry itself and can express a view choice that
    /// depends on a node characteristic rather than on the type name (for example flag enums).
    /// </summary>
    public void RegisterNodeResolver(
        Func<DataNode, SchemaViewRole, string, SchemaViewRegistration?> resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _nodeResolvers.Add(resolver);
    }

    private SchemaViewRegistration? ResolveNodeRegistration(
        DataNode node,
        SchemaViewRole role,
        string normalizedSkin)
    {
        if (NodeRegistrationResolver?.Invoke(node, role, normalizedSkin) is { } hostSpecific)
            return hostSpecific;

        foreach (Func<DataNode, SchemaViewRole, string, SchemaViewRegistration?> resolver in _nodeResolvers)
        {
            if (resolver(node, role, normalizedSkin) is { } registration)
                return registration;
        }

        return null;
    }

    /// <summary>
    /// Whether an array is rendered as a table. Explicit registration wins; otherwise a struct
    /// element that is not a single view becomes a table.
    /// </summary>
    public bool ShouldUseTable(IValueTypeAccess arrayType)
    {
        if (arrayType is not ArrayType array) return false;

        string? typeView = GetTypeView(array, SchemaViewRole.Field, DefaultSkin);
        if (string.Equals(typeView, SchemaTemplateKeys.Table, StringComparison.Ordinal)) return true;

        return array.Element is StructType element && !UseSingleView(element);
    }

    /// <summary>Whether an array is rendered as a table.</summary>
    public bool ShouldUseTable(ArrayNode array) => ShouldUseTable(array.Type);

    private TypeRegistration? FindTypeRegistration(IValueTypeAccess type, SchemaViewRole role, string skin)
    {
        TypeKey cacheKey = new(type.Name.ToLowerInvariant(), skin, role);
        if (_typeViews.TryGetValue(cacheKey, out TypeLookup cached))
            return cached.Registration;

        TypeRegistration? found = null;
        IValueTypeAccess? current = type;
        while (current is not null && found is null)
        {
            found = FindRegisteredType(current.Name, role, skin);
            current = current is ScalarType scalar ? scalar.BaseType : null;
        }

        _typeViews[cacheKey] = new TypeLookup(found);
        return found;
    }

    private TypeRegistration? FindRegisteredType(string typeName, SchemaViewRole role, string skin)
    {
        string normalizedName = typeName.ToLowerInvariant();

        TypeRegistration? exact = Lookup(normalizedName, skin, role);
        if (exact is not null) return exact;

        if (role != SchemaViewRole.Field)
        {
            TypeRegistration? field = Lookup(normalizedName, skin, SchemaViewRole.Field);
            if (field is not null) return field;
        }

        if (skin != DefaultSkin)
        {
            TypeRegistration? defaultSkin = Lookup(normalizedName, DefaultSkin, role);
            if (defaultSkin is not null) return defaultSkin;

            if (role != SchemaViewRole.Field)
                return Lookup(normalizedName, DefaultSkin, SchemaViewRole.Field);
        }

        return null;
    }

    private TypeRegistration? Lookup(string normalizedName, string skin, SchemaViewRole role)
        => _types.GetValueOrDefault(new TypeKey(normalizedName, skin, role));

    private string? ResolveArray(IValueAccess node)
    {
        if (node is not ArrayNode array) return null;
        return ShouldUseTable(array)
            ? SchemaTemplateKeys.Table
            : SchemaTemplateKeys.Array;
    }

    private static string NormalizeSkin(string? skin)
        => string.IsNullOrWhiteSpace(skin) ? DefaultSkin : skin.Trim().ToLowerInvariant();

    private static string SingleViewKey(string typeName, string skin)
        => $"{typeName.ToLowerInvariant()}-{skin}";

    private sealed record KindRegistration(
        string? TemplateKey,
        Func<IValueAccess, string?>? Resolve,
        Func<IValueAccess, DataNodeWrapper>? WrapperFactory);

    private sealed record TypeRegistration(
        string TemplateKey,
        Func<IValueAccess, DataNodeWrapper>? WrapperFactory);

    private readonly record struct TypeLookup(TypeRegistration? Registration);

    private readonly record struct TypeKey(string TypeName, string Skin, SchemaViewRole Role);
}
