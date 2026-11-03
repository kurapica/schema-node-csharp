using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using Prism.Mvvm;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Property.Int;
using SchemaNode.Runtime;
using SchemaNode.WPF.Test.Type;
using SchemaNode.WPF.View;

namespace SchemaNode.WPF.Test.ViewModels;

/// <summary>One entry of the case picker.</summary>
internal sealed class SampleCase
{
    public SampleCase(string key, string title, bool tableCapable = true)
    {
        Key = key;
        Title = title;
        TableCapable = tableCapable;
    }

    public string Key { get; }

    public string Title { get; }

    public bool TableCapable { get; }

    /// <inheritdoc />
    public override string ToString() => Title;
}

/// <summary>
/// Manual harness. It renders exactly one schema case at a time so every node shape can be
/// inspected in isolation, and it prints the node's own value under the form so the write-back is
/// visible while typing.
/// </summary>
internal class MainWindowViewModel : BindableBase, IDisposable
{
    private readonly SchemaContext _context;
    private readonly SchemaViewRegistry _formRegistry;
    private readonly SchemaViewRegistry _tableRegistry;
    private SampleCase? _selectedCase;
    private IValueAccess? _node;
    private IDisposable? _nodeSubscription;
    private string? _nodeValue;
    private string? _status;
    private bool _formArrays;
    private int _loadVersion;

    public MainWindowViewModel(SchemaContext context)
    {
        _context = context;

        // Registering the element type as a "single view" is what makes an array of structs render
        // through the array/form view; the table registry instead marks the array type as table.
        _formRegistry = SchemaViewRegistry.CreateDefault();
        _formRegistry.RegisterType("test.minmax", SchemaTemplateKeys.Struct, asSingle: true);
        _tableRegistry = SchemaViewRegistry.CreateDefault();
        _tableRegistry.RegisterType("system.list<test.minmax>", SchemaTemplateKeys.Table, asSingle: true);

        Cases =
        [
            new SampleCase("string", "string"),
            new SampleCase("int", "int"),
            new SampleCase("decimal", "decimal"),
            new SampleCase("date", "date"),
            new SampleCase("bool", "bool"),
            new SampleCase("enum", "enum (single level)", false),
            new SampleCase("enumarray", "enum array (multi-select)", false),
            new SampleCase("array", "simple array"),
            new SampleCase("struct", "struct (MinMax)"),
            new SampleCase("structarray", "struct array (form / table)"),
            new SampleCase("nested", "nested struct + array"),
            new SampleCase("readonly", "read-only struct"),
            new SampleCase("required", "required string"),
            new SampleCase("limited", "limited int"),
        ];
    }

    /// <summary>Cases shown by the picker.</summary>
    public ObservableCollection<SampleCase> Cases { get; }

    /// <summary>
    /// Registry set once on the root view and inherited by nested views. The struct-array case
    /// switches registries: the form registry marks the element type as a single view, the table
    /// registry marks the array type as a table.
    /// </summary>
    public SchemaViewRegistry Registry => _formArrays ? _formRegistry : _tableRegistry;

    /// <summary>Whether arrays of structs render through the array/form view instead of the table view.</summary>
    public bool FormArrays
    {
        get => _formArrays;
        set
        {
            if (SetProperty(ref _formArrays, value))
                RaisePropertyChanged(nameof(Registry));
        }
    }

    /// <summary>Currently selected case; selecting one rebuilds the node.</summary>
    public SampleCase? SelectedCase
    {
        get => _selectedCase;
        set
        {
            if (!SetProperty(ref _selectedCase, value) || value is null || !_started) return;
            _ = LoadAsync(value);
        }
    }

    /// <summary>The root node rendered by <c>SchemaView</c>.</summary>
    public IValueAccess? Node
    {
        get => _node;
        private set => SetProperty(ref _node, value);
    }

    /// <summary>Latest node value, printed under the form so the write-back is visible.</summary>
    public string? NodeValue
    {
        get => _nodeValue;
        private set => SetProperty(ref _nodeValue, value);
    }

    /// <summary>What the harness is doing, including failures the harness itself caused.</summary>
    public string? Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    private bool _started;

    /// <summary>
    /// Called by the application only after <c>InitSchemaRuntimeAsync</c> completed, so the runtime
    /// already knows the CLR types of this assembly and the locale is already applied.
    /// </summary>
    public async Task InitializeAsync()
    {
        SampleCase first = Cases[0];
        _selectedCase = first;
        RaisePropertyChanged(nameof(SelectedCase));
        await LoadAsync(first);
        _started = true;
    }

    private async Task LoadAsync(SampleCase sampleCase)
    {
        int version = ++_loadVersion;
        Status = $"loading {sampleCase.Title}…";
        IValueAccess? next = null;
        string? failure = null;
        try
        {
            next = await CreateAsync(sampleCase.Key);
        }
        catch (Exception ex)
        {
            failure = ex.Message;
        }

        if (version != _loadVersion)
        {
            if (next is IDisposable stale) stale.Dispose();
            return;
        }

        ReleaseNode();
        Node = next;
        if (next is null)
        {
            Status = failure is null
                ? $"no node for {sampleCase.Title}"
                : $"{sampleCase.Title} failed: {failure}";
            return;
        }

        try
        {
            _nodeSubscription = next.Subscribe(items => NodeValue = Format(items.Item2), immediate: true);
            Status = $"{sampleCase.Title} · {next.Type?.Name}";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    /// <summary>Builds each case from real Core nodes: CLR value types, enums, arrays and structs.</summary>
    private async Task<IValueAccess?> CreateAsync(string key)
    {
        switch (key)
        {
            case "string":
                return await _context.GetSchemaNodeAsync(new StringCase());
            case "int":
                return await _context.GetSchemaNodeAsync(new IntCase());
            case "decimal":
                return await _context.GetSchemaNodeAsync(new DecimalCase());
            case "date":
                return await _context.GetSchemaNodeAsync(new DateCase());
            case "bool":
                return await _context.GetSchemaNodeAsync(new BoolCase());
            case "enum":
                return await _context.GetSchemaNodeAsync(new EnumCase());
            case "enumarray":
            {
                IValueTypeAccess? element = await _context.GetNodeTypeAsync<EnumType>("test.mode");
                return await CreateArrayAsync(element, new object?[] { "alpha", "gamma" }, "test.mode[]");
            }
            case "array":
                return await _context.GetSchemaNodeAsync(new ArrayCase());
            case "struct":
                return await _context.GetSchemaNodeAsync(new MinMax { Min = 2, Max = 3 });
            case "structarray":
            {
                IValueTypeAccess? element = await _context.GetNodeTypeAsync<SchemaNode.Runtime.ValueType>("test.minmax");
                return await CreateArrayAsync(
                    element,
                    new object?[] { new MinMax { Min = 1, Max = 2 }, new MinMax { Min = 3, Max = 4 } },
                    "test.minmax[]");
            }
            case "nested":
            {
                // A struct whose fields are themselves a struct and an array of structs cannot be
                // built from one raw CLR object (Core's object path only converts scalar lists), so
                // the empty node is created from its type and the fields are filled by access.
                IValueTypeAccess? type = await _context.GetNodeTypeAsync<SchemaNode.Runtime.ValueType>("test.nested");
                if (type is null)
                    throw new InvalidOperationException("schema type 'test.nested' is not registered");

                IValueAccess? node = type.Create(_context);
                if (node is null)
                    throw new InvalidOperationException("'test.nested' created no node");

                node.GetAccessValue("inner")?.TrySetValue(new MinMax { Min = 5, Max = 6 });

                if (node.GetAccessValue("lines") is ArrayNode rows)
                {
                    IValueTypeAccess? element = await _context.GetNodeTypeAsync<SchemaNode.Runtime.ValueType>("test.minmax");
                    IValueAccess? row = element?.Create(_context, new MinMax { Min = 7, Max = 8 });
                    if (row is not null) rows.Add(row);
                }
                return node;
            }
            case "readonly":
            {
                IValueAccess? node = await _context.GetSchemaNodeAsync(new MinMax { Min = 2, Max = 3 });
                node?.SetPropertyValue<ReadOnly, bool>(true, null, null);
                return node;
            }
            case "required":
            {
                IValueAccess? node = await _context.GetSchemaNodeAsync(new RequiredCase());
                node?.GetAccessValue("text")?.SetPropertyValue<Require, bool>(true, null, null);
                return node;
            }
            case "limited":
            {
                IValueAccess? node = await _context.GetSchemaNodeAsync(new IntCase());
                node?.GetAccessValue("count")?.SetPropertyValue<LowLimitInt, long>(10L, null, null);
                return node;
            }
            default:
                return null;
        }
    }

    /// <summary>
    /// Builds an array node from its element type. A plain CLR array value cannot be handed to
    /// <c>GetSchemaNodeAsync</c> when the element is a struct or an enum: that path only knows the
    /// built-in scalar list types, so the array type is resolved from the element type instead and
    /// the value is converted element by element.
    /// </summary>
    private async Task<IValueAccess?> CreateArrayAsync(IValueTypeAccess? element, object?[] items, string typeName)
    {
        if (element is null)
            throw new InvalidOperationException($"element type of '{typeName}' is not registered");

        ArrayType? arrayType = await _context.GetArrayNodeTypeAsync(element);
        if (arrayType is null)
            throw new InvalidOperationException($"'{typeName}' has no array type");

        IValueAccess? node = arrayType.Create(_context);
        if (node is null)
            throw new InvalidOperationException($"'{typeName}' could not create an array node");

        // Enum arrays hold a JsonArray of keys; plain arrays accept one converted element per item.
        if (node is EnumArrayNode enumArray)
        {
            JsonArray keys = new(items.Select(item => JsonValue.Create(item)).ToArray());
            enumArray.TrySetValue(keys);
        }
        else if (node is ArrayNode array)
        {
            foreach (object? item in items)
            {
                IValueAccess? elementNode = element.Create(_context, item);
                if (elementNode is null)
                    throw new InvalidOperationException($"'{typeName}' could not convert an element");
                array.Add(elementNode);
            }
        }
        else
        {
            throw new InvalidOperationException($"'{typeName}' created no collection node");
        }

        return node;
    }

    private void ReleaseNode()
    {
        _nodeSubscription?.Dispose();
        _nodeSubscription = null;

        IValueAccess? node = _node;
        Node = null;
        if (node is IDisposable disposable) disposable.Dispose();
        NodeValue = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ReleaseNode();
    }

    private static string Format(object? value)
        => value is null ? "(empty)" : value.ToString() ?? "(empty)";
}
