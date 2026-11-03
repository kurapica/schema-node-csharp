﻿using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Enum;
using SchemaNode.Property.String;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Struct;
using SchemaNode.Utility;
using System.Text.Json.Nodes;

namespace SchemaNode.Node;

/// <summary>
/// The scalar data node base
/// </summary>
public abstract class ScalarNode : DataNode
{
    #region Constructor

    public ScalarNode(ISchemaContext context, IValueTypeAccess type, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders)
        : base(context, type, value, parent, propertyProviders)
    {
        // entry source related properties, only work in full mode
        if (!FullMode) return;
        RecordSubscription(SubscribeSelfProperty<WhiteList, object[]>(_ => RefreshEntrySource()), this);
        RecordSubscription(SubscribeSelfProperty<BlackList, object[]>(_ => RefreshEntrySource()), this);
        RecordSubscription(SubscribeSelfProperty<Valid, FuncCall>(_ => RefreshEntrySource()), this);
        RecordSubscription(SubscribeSelfProperty<Root, string>(_ => RefreshEntrySource()), this);
        RecordSubscription(SubscribeSelfProperty<CascadeDepth, long>(_ => RefreshEntrySource()), this);
        RecordSubscription(SubscribeSelfProperty<EntrySource, FuncCall>(_ => RefreshEntrySource(), true), this);
        RecordSubscription(SubscribeSelfProperty<LeafOnly, bool>(_ => RefreshEntrySource(), true), this);
    }

    #endregion

    #region Entry Source

    private ScalarEntrySourceInfo? _entrySourceInfo;
    private bool _disposed;
    private CancellationTokenSource? _refreshEntryCts;
    private CancellationTokenSource? _delayEntryCts;

    /// <summary>
    /// Whether the data node has entry source
    /// </summary>
    public bool HasEntrySource => _entrySourceInfo?.RootEntry != null;

    /// <summary>
    /// Whether the data node use single level entry source
    /// </summary>
    public bool IsSingleLevel => _entrySourceInfo?.SingleLevel ?? false;

    /// <summary>
    /// Whether the data node use non leaf node selectable
    /// </summary>
    public bool IsNonLeafNodeSelectable => _entrySourceInfo?.AllRootPassed ?? false;

    /// <summary>
    /// Gets the default display text for the current value
    /// </summary>
    public async Task<string> GetDisplayValueAsync(string? separator = null, string? valuesSeparator = null,
        CancellationToken cancellationToken = default)
    {
        if (IsEmpty) return string.Empty;
        if (!HasEntrySource) return GetValueText();

        object? raw = GetRawEntryValue();
        IEnumerable<object?> items = raw is JsonArray array ? array.Select(item => (object?)item) : [raw];
        List<string> result = [];
        foreach (object? item in items)
        {
            string text = ToKey(item) ?? string.Empty;
            EntryAccess<string>[] access = await GetEntryAccessListAsync(text, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (access.Length == 0 || access[^1].Entry == null)
            {
                result.Add(text);
                continue;
            }

            if (separator != null)
            {
                result.Add(string.Join(separator, access.Where(a => a.Entry != null).Select(a =>
                {
                    LocaleString? display = a.Entry!.GetProperty<Display>()?.Value;
                    return display != null ? display.ToString(Context) : a.Entry!.Value;
                })));
            }
            else
            {
                Entry<string> entry = access[^1].Entry!;
                LocaleString? display = entry.GetProperty<Display>()?.Value;
                result.Add(display != null ? display.ToString(Context) : text);
            }
        }
        return string.Join(valuesSeparator ?? ", ", result);
    }

    /// <summary>
    /// Query the entry access list from entry source args
    /// </summary>
    public async Task<EntryAccess<string>[]> GetEntryAccessListAsync(object? value = null, object? root = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ScalarEntrySourceInfo? info = _entrySourceInfo;
        if (!FullMode || info?.RootEntry == null) return [];

        string? key = ToKey(value);
        EntryAccess<string>[]? accessList = info.RootEntry.HasChildren == true ? info.RootEntry.GetAccessList(key) : null;

        if (accessList is not { Length: > 0 } && info.Source != null && info.Args != null)
        {
            // query access list
            accessList = await QueryEntrySourceAsync(value, cancellationToken).ConfigureAwait(false);
            if (accessList.Length > 0)
            {
                info.RootEntry.SaveAccessList(accessList);
                accessList = info.RootEntry.GetAccessList(key) ?? [];
            }
        }

        accessList ??= [];

        // check cascade level
        long cascade = info.Cascade ?? 9999;
        if (accessList.Length > 0 && cascade >= 1 && cascade <= accessList.Length)
        {
            int level = (int)cascade;
            accessList = accessList[..(level + 1)];
            accessList[level - 1].Children?.ToList().ForEach(c => c.HasChildren = false);
            if (accessList.Length > level)
            {
                accessList[level].Children = null;
                if (accessList[level].Entry != null)
                    accessList[level].Entry!.HasChildren = false;
            }
        }

        // root
        if (root != null)
        {
            string? rootKey = ToKey(root);
            int index = Array.FindIndex(accessList, a => a.Entry != null && string.Equals(ToKey(a.Entry.Value), rootKey));
            if (index >= 0)
                accessList = accessList[..index];
        }
        return accessList;
    }

    /// <summary>
    /// Gets the sub entry list of the given value
    /// </summary>
    public async Task<Entry<string>[]> GetSubEntryListAsync(object? value = null,
        CancellationToken cancellationToken = default)
    {
        EntryAccess<string>[] accessList = await GetEntryAccessListAsync(value, cancellationToken: cancellationToken).ConfigureAwait(false);
        return accessList.Length > 0 ? accessList[^1].Children ?? [] : [];
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        _disposed = true;

        // unsubscribe the entry source subscriptions registered on other nodes
        DisposeEntrySubscribes(_entrySourceInfo);
        _entrySourceInfo = null;

        // cancel the pending debounced tasks
        _refreshEntryCts?.Cancel();
        _delayEntryCts?.Cancel();

        base.Dispose(disposing);
    }

    /// <summary>
    /// Refresh the entry source with entry source & white list & black list, debounced
    /// </summary>
    private void RefreshEntrySource()
    {
        if (_disposed || !FullMode) return;

        _refreshEntryCts?.Cancel();
        CancellationTokenSource cts = new();
        _refreshEntryCts = cts;
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(10, cts.Token).ConfigureAwait(false); }
            catch (TaskCanceledException) { return; }
            if (_disposed || cts.IsCancellationRequested) return;

            try
            {
                await RefreshEntrySourceAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                // the refresh was superseded or the node was disposed
            }
            catch (Exception ex)
            {
                (Context as SchemaContext)?.LogError(ex, "Refresh entry source of {0} failed", Type.Name);
            }
        });
    }

    /// <summary>
    /// Refresh the entry source info
    /// </summary>
    private async Task RefreshEntrySourceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_disposed) return;
        if (Context is not SchemaContext ctx) return;

        ScalarEntrySourceInfo info = _entrySourceInfo ??= new();
        info.Initing = true;

        string[]? whiteList = ToKeys(GetPropertyValue<WhiteList, object[]>());
        string[]? blackList = ToKeys(GetPropertyValue<BlackList, object[]>());
        bool? leafOnly = GetProperty<LeafOnly>()?.Value;

        // entry source
        FuncCallProperty? entrySourceProp = GetProperty<EntrySource>();
        FuncCall? entrySource = entrySourceProp?.Value;
        string? root = GetProperty<Root>()?.Value;
        long? cascade = GetProperty<CascadeDepth>()?.Value;

        // entry source owner
        IValueAccess? owner = entrySourceProp?.Source;
        List<Valid> valids = [.. GetProperties<Valid>().Where(item => item.HasValue)];
        valids.Reverse(); // old first

        // access consumer to access source from ancestors
        AccessEntryConsumer? accessValueTypeConsumer = GetProperty<AccessEntryConsumer>();
        if (string.IsNullOrWhiteSpace(entrySource?.Func) &&
            (accessValueTypeConsumer?.HasValue == true || GetPropertyValue<EntrySourceConsumer, bool>() == true))
        {
            IValueAccess? parent = this;
            while (parent != null)
            {
                FuncCall? provider = parent.GetProperty<EntrySourceProvider>()?.Value;
                if (provider != null)
                {
                    entrySource = provider;
                    owner = parent;
                    break;
                }
                parent = parent.Parent;
            }
        }
        FunctionType? entryFunc = !string.IsNullOrWhiteSpace(entrySource?.Func)
            ? await ctx.GetNodeTypeAsync<FunctionType>(entrySource!.Func).ConfigureAwait(false)
            : null;

        // access value type handler
        if (accessValueTypeConsumer != null && info.ValueTypeProvider == null)
        {
            IValueAccess? parent = this;
            while (parent != null)
            {
                AccessValueTypeProvider? accessValueTypeProvider = parent.GetProperty<AccessValueTypeProvider>();
                if (accessValueTypeProvider is { HasValue: true, Value: { } providerCall })
                {
                    info.ValueTypeProvider = !string.IsNullOrWhiteSpace(providerCall.Func)
                        ? await ctx.GetNodeTypeAsync<FunctionType>(providerCall.Func).ConfigureAwait(false)
                        : null;
                    info.ValueTypeProviderArgs = providerCall.Args.Select(a => ToEntrySourceArg(accessValueTypeProvider.Source ?? parent, a)).ToArray();

                    FuncCall? consumerCall = accessValueTypeConsumer.Value;
                    info.ValueTypeConsumer = consumerCall != null && !string.IsNullOrWhiteSpace(consumerCall.Func)
                        ? await ctx.GetNodeTypeAsync<FunctionType>(consumerCall.Func).ConfigureAwait(false)
                        : null;
                    info.ValueTypeConsumerArgs = consumerCall?.Args.Select(a => ToEntrySourceArg(accessValueTypeConsumer.Source ?? parent, a)).ToArray() ?? [];
                    break;
                }
                parent = parent.Parent;
            }
        }

        if (entryFunc != null)
        {
            // refresh options if related properties changed
            if (info.Source != entryFunc ||
                info.Cascade != cascade ||
                info.Root != root ||
                info.LeafOnly != leafOnly ||
                info.Owner != owner ||
                !EqualKeys(info.WhiteList, whiteList) ||
                !EqualKeys(info.BlackList, blackList) ||
                !(info.Valids?.Count == valids.Count && info.Valids.Zip(valids).All(pair => pair.First.Prop == pair.Second)))
            {
                DisposeEntrySubscribes(info);
                info.Owner = owner;
                info.WhiteList = whiteList;
                info.BlackList = blackList;
                info.LeafOnly = leafOnly;
                info.Source = entryFunc;
                info.Cascade = cascade;
                info.Root = root;
                info.RootEntry = new Entry<string> { Value = root ?? string.Empty };
                info.Args = entrySource!.Args.Select(a => ToEntrySourceArg(owner ?? this, a)).ToArray();

                info.Valids = [];
                foreach (Valid item in valids)
                {
                    FuncCall? funcCall = item.Value;
                    FunctionType? func = funcCall != null && !string.IsNullOrWhiteSpace(funcCall.Func)
                        ? await ctx.GetNodeTypeAsync<FunctionType>(funcCall.Func).ConfigureAwait(false)
                        : null;
                    if (func != null)
                        info.Valids.Add(new ScalarEntrySourceValid
                        {
                            Prop = item,
                            Func = func,
                            Args = funcCall!.Args.Select(a => ToEntrySourceArg(item.Source ?? this, a)).ToArray()
                        });
                }

                // init options
                info.Initing = false;
                await InitEntryOptionsAsync(cancellationToken).ConfigureAwait(false);
            }
            return;
        }
        else if (info.Source != null)
        {
            info.Source = null;
            DisposeEntrySubscribes(info);
        }

        // Simple case
        if (whiteList is { Length: > 0 } && !EqualKeys(info.WhiteList, whiteList))
        {
            info.RootEntry = new Entry<string>();
            info.SingleLevel = true;
            info.WhiteList = whiteList;

            List<Entry<string>> children = [];
            foreach (string item in whiteList.Where(a => blackList?.Contains(a) != true))
            {
                Entry<string> child = new Entry<string> { Value = item, HasChildren = false };
                child.SetProperty<Display, LocaleString>(new LocaleString(item));
                children.Add(child);
            }
            info.RootEntry.SaveAccessList([new EntryAccess<string> { Children = children.ToArray() }]);

            // notify version change
            NextEntrySourceVersion();
            info.Initing = false;
        }
        else if (_entrySourceInfo != null)
        {
            _entrySourceInfo = null;

            // notify version change
            NextEntrySourceVersion();
        }
    }

    /// <summary>
    /// Init the entry options with entry source args
    /// </summary>
    private async Task InitEntryOptionsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_disposed) return;
        ScalarEntrySourceInfo? info = _entrySourceInfo;
        if (info == null || info.Initing) return;

        info.Initing = true;
        info.Validres = [];
        info.AllRootPassed = true;
        info.RootEntry = new Entry<string> { Value = info.Root ?? string.Empty };

        try
        {
            // check root && cascade
            if (info.Root != null && info.Cascade is > 0)
            {
                EntryAccess<string>[] rootAccess = await QueryEntrySourceAsync(info.Root, cancellationToken).ConfigureAwait(false);
                info.Cascade -= (Math.Max(rootAccess.Length, 1) - 1);
            }

            // white list init entry tree and as mask
            if (info.WhiteList is { Length: > 0 })
            {
                HashSet<string> passKeys = [];
                foreach (string item in info.WhiteList.Where(a => info.BlackList?.Contains(a) != true))
                {
                    EntryAccess<string>[] queryAccessList = await QueryEntrySourceAsync(item, cancellationToken).ConfigureAwait(false);
                    if (queryAccessList.Length > 0 &&
                        (info.Root == null || queryAccessList.Any(a => a.Entry != null && string.Equals(ToKey(a.Entry.Value), info.Root))))
                    {
                        info.RootEntry.SaveAccessList(queryAccessList);
                        foreach (Entry<string> entry in queryAccessList.Where(a => a.Entry != null).Select(a => a.Entry!))
                            passKeys.Add(entry.Value);
                        passKeys.Add(item);
                    }
                }

                // white list mask
                info.RootEntry.UseWhiteList(passKeys.ToArray());
            }

            // prepare entry tree and check single level
            EntryAccess<string>[] root = await GetEntryAccessListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            Entry<string>[]? children = root.Length > 0 ? root[0].Children : null;
            info.SingleLevel = children is not { Length: > 0 } || children.All(a => a.HasChildren != true);

            // load options to the value
            object? value = GetRawEntryValue();
            if (!IsEmpty && value != null)
            {
                IEnumerable<object?> items = value is JsonArray array ? array.Select(item => (object?)item) : [value];
                foreach (object? item in items)
                    await GetEntryAccessListAsync(item, cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            // notify version change
            NextEntrySourceVersion();
        }
        finally
        {
            info.Initing = false;
        }
    }

    /// <summary>
    /// Query the entry access list from entry source args
    /// </summary>
    private async Task<EntryAccess<string>[]> QueryEntrySourceAsync(object? value,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ScalarEntrySourceInfo? info = _entrySourceInfo;
        if (info?.Source == null || info.Args == null || Context is not SchemaContext ctx) return [];

        bool queryRoot = value != null && string.Equals(ToKey(value), info.Root);
        FunctionNodeArgument[] funcArgs = info.Source.Args;
        List<object?> callArgs = [];
        for (int i = 0; i < funcArgs.Length; i++)
        {
            ScalarEntrySourceArg? arg = i < info.Args.Length ? info.Args[i] : null;
            if (arg == null || arg.Source == null && arg.Value == null)
            {
                callArgs.Add(funcArgs[i].GetProperty<EntryRoot>()?.Value == true && !queryRoot ? info.Root : null);
                continue;
            }

            callArgs.Add(arg.Source != null ? arg.Source == this ? value : GetEntryValue(arg.Source) : arg.Value);
        }
        if (info.Args.Length > callArgs.Count)
        {
            foreach (ScalarEntrySourceArg arg in info.Args.Skip(callArgs.Count))
                callArgs.Add(arg.Source != null ? GetEntryValue(arg.Source) : arg.Value);
        }

        // call entry source function
        List<EntryAccess<string>> result;
        try
        {
            result = await AwaitCancellableAsync(
                info.Source.CallAsync<List<EntryAccess<string>>>(ctx, callArgs.ToArray()), cancellationToken).ConfigureAwait(false) ?? [];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ctx.LogError(ex, "Query entry source {0} failed", info.Source.Name);
            return [];
        }
        EntryAccess<string>[] accesses = result.Select(CloneEntryAccess).ToArray();

        // valid
        if (!queryRoot && (info.Valids is { Count: > 0 } || info.BlackList is { Length: > 0 } || info.ValueTypeConsumer != null))
        {
            // check black list for entry first
            if (info.BlackList is { Length: > 0 })
            {
                for (int i = 0; i < accesses.Length; i++)
                {
                    string? entryValue = accesses[i].Entry?.Value;
                    if (entryValue != null && info.BlackList.Contains(entryValue))
                    {
                        accesses = accesses[..i];
                        break;
                    }
                }
            }

            // validate the children
            for (int i = accesses.Length - 1; i >= 0; i--)
            {
                EntryAccess<string> r = accesses[i];
                if (r.Children is { Length: > 0 })
                {
                    List<Entry<string>> passed = [];
                    foreach (Entry<string> c in r.Children)
                    {
                        // black list not allow children
                        if (info.BlackList?.Contains(c.Value) == true) continue;

                        bool disable = c.GetProperty<Disable>()?.Value == true ||
                                       !await IsValidEntryValueAsync(c.Value, cancellationToken).ConfigureAwait(false);
                        if (!disable || c.HasChildren == true) // valid or has children
                        {
                            if (disable) c.SetProperty<Disable, bool>(true); // mark as disabled
                            passed.Add(c);

                            // valid & leaf only, block children
                            if (info.LeafOnly == true && !disable && (info.Valids is { Count: > 0 } || info.ValueTypeConsumer != null))
                            {
                                c.HasChildren = false;
                                c.Children = null;
                                info.RootEntry?.GetEntry(c.Value)?.DropChildren();
                            }
                        }
                    }
                    r.Children = passed.ToArray();
                }

                // reset hasChildren if no children passed
                if (r.Entry == null) continue;

                bool disabled = r.Entry.GetProperty<Disable>()?.Value == true ||
                                !await IsValidEntryValueAsync(r.Entry.Value, cancellationToken).ConfigureAwait(false);
                if (r.Entry.HasChildren == true && r.Children is not { Length: > 0 })
                {
                    r.Entry.HasChildren = false;
                    if (i > 0)
                    {
                        Entry<string>? item = accesses[i - 1].Children?.FirstOrDefault(c => c.Value.Equals(r.Entry.Value));
                        if (item != null)
                        {
                            if (disabled)
                                accesses[i - 1].Children = accesses[i - 1].Children!.Where(c => c != item).ToArray();
                            else
                                item.HasChildren = false;
                        }
                    }

                    // may need reset the options, check the loaded options
                    Entry<string>? entry = info.RootEntry?.GetEntry(r.Entry.Value);
                    if (entry != null)
                    {
                        if (disabled)
                            entry.Drop();
                        else
                            entry.DropChildren();
                    }
                }
                else if (disabled && info.AllRootPassed == true)
                {
                    info.AllRootPassed = false;
                    _ = Task.Run(NextEntrySourceVersion);
                }
                else if (info.LeafOnly == true && !disabled && (info.Valids is { Count: > 0 } || info.ValueTypeConsumer != null))
                {
                    r.Entry.HasChildren = false;
                    info.RootEntry?.GetEntry(r.Entry.Value)?.DropChildren();
                    r.Children = null;
                }
            }

            // remove no children access
            for (int i = 0; i < accesses.Length; i++)
            {
                if (accesses[i].Entry == null || accesses[i].Entry!.HasChildren == true) continue;
                accesses = accesses[..i];
                break;
            }
        }
        return accesses;
    }

    /// <summary>
    /// Await the given task but abort as soon as the token is canceled.
    /// Core's function call pipeline currently has no cancellation parameter, so this keeps
    /// the entry-query chain cancellable without changing the function API.
    /// </summary>
    private static async Task<T?> AwaitCancellableAsync<T>(Task<T?> task, CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled) return await task.ConfigureAwait(false);

        Task delay = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        Task completed = await Task.WhenAny(task, delay).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await task.ConfigureAwait(false);
    }

    /// <summary>
    /// Check the entry value is valid
    /// </summary>
    private async Task<bool> IsValidEntryValueAsync(string value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ScalarEntrySourceInfo? info = _entrySourceInfo;
        if (info == null || Context is not SchemaContext ctx) return true;
        if (info.BlackList?.Contains(value) == true) return false;

        // check cache
        if (info.Validres?.TryGetValue(value, out bool cached) == true) return cached;

        // valid
        bool isValid = true;
        if (info.Valids is { Count: > 0 })
        {
            foreach (ScalarEntrySourceValid valid in info.Valids)
            {
                bool? res = await AwaitCancellableAsync(
                    valid.Func.CallAsync<bool?>(ctx, BuildEntryArgs(valid.Args, value)), cancellationToken).ConfigureAwait(false);
                if (res != true)
                {
                    isValid = false;
                    break;
                }
            }
        }

        // validate access value type
        if (isValid && info.ValueTypeConsumer != null && info.ValueTypeProvider != null)
        {
            string? valueType = await AwaitCancellableAsync(
                info.ValueTypeProvider.CallAsync<string>(ctx, BuildEntryArgs(info.ValueTypeProviderArgs, value)),
                cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(valueType) ||
                await AwaitCancellableAsync(
                    info.ValueTypeConsumer.CallAsync<bool?>(ctx, BuildEntryArgs(info.ValueTypeConsumerArgs, valueType)),
                    cancellationToken).ConfigureAwait(false) != true)
            {
                isValid = false;
            }
        }

        // record cache
        info.Validres ??= [];
        info.Validres[value] = isValid;
        return isValid;
    }

    /// <summary>
    /// Debounce the entry options init
    /// </summary>
    private void DelayInitEntryOptions()
    {
        if (_disposed || !FullMode) return;

        _delayEntryCts?.Cancel();
        CancellationTokenSource cts = new();
        _delayEntryCts = cts;
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(50, cts.Token).ConfigureAwait(false); }
            catch (TaskCanceledException) { return; }
            if (_disposed || cts.IsCancellationRequested) return;

            try
            {
                await InitEntryOptionsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                (Context as SchemaContext)?.LogError(ex, "Init entry options of {0} failed", Type.Name);
            }
        });
    }

    /// <summary>
    /// Convert the call arg to entry source arg
    /// </summary>
    private ScalarEntrySourceArg ToEntrySourceArg(IValueAccess owner, CallArg arg)
    {
        ScalarEntrySourceArg result = new() { Value = arg.Value };
        if (!string.IsNullOrWhiteSpace(arg.Source) && !_disposed)
        {
            result.Source = owner.GetAccessValue(arg.Source, this);
            if (result.Source != null && result.Source != this)
            {
                _entrySourceInfo ??= new();
                _entrySourceInfo.Subscribes ??= [];
                _entrySourceInfo.Subscribes.Add(result.Source.Subscribe(_ => DelayInitEntryOptions()));
            }
        }
        return result;
    }

    /// <summary>
    /// Build the function call args from entry source args
    /// </summary>
    private object?[] BuildEntryArgs(ScalarEntrySourceArg[]? args, object? value)
        => args?.Select(a => a.Source == null ? a.Value : a.Source == this ? value : GetEntryValue(a.Source)).ToArray() ?? [];

    /// <summary>
    /// Dispose the entry source subscribes
    /// </summary>
    private static void DisposeEntrySubscribes(ScalarEntrySourceInfo? info)
    {
        info?.Subscribes?.ForEach(sub => sub.Dispose());
        if (info != null) info.Subscribes = null;
    }

    /// <summary>
    /// Notify the entry source version changed
    /// </summary>
    private void NextEntrySourceVersion()
    {
        if (_entrySourceInfo == null)
        {
            OnNextProperty(typeof(EntrySourceVersion), null, null);
            return;
        }
        long ver = (_entrySourceInfo.Version ?? 0) + 1;
        _entrySourceInfo.Version = ver;
        OnNextProperty(typeof(EntrySourceVersion), ver, ver - 1);
    }

    /// <summary>
    /// Gets the raw value of the node as entry value
    /// </summary>
    private object? GetRawEntryValue()
    {
        if (TryGetValue(typeof(JsonNode), out object? json) && json is JsonNode node) return node;
        return TryGetValue(typeof(object), out object? value) ? value : null;
    }

    /// <summary>
    /// Gets the entry value from the node
    /// </summary>
    private static object? GetEntryValue(IValueAccess node)
    {
        if (node.TryGetValue(typeof(JsonNode), out object? json) && json is JsonNode) return json;
        return node.TryGetValue(typeof(object), out object? value) ? value : null;
    }

    /// <summary>
    /// Gets the text for the current value
    /// </summary>
    private string GetValueText()
    {
        object? value = GetRawEntryValue();
        return value switch
        {
            null => string.Empty,
            JsonValue jsonValue => jsonValue.ToString(),
            JsonNode jsonNode => jsonNode.ToJsonString(),
            _ => value.ToString() ?? string.Empty
        };
    }

    /// <summary>
    /// Convert the value to entry key
    /// </summary>
    private static string? ToKey(object? value) => value switch
    {
        null => null,
        JsonValue jsonValue => jsonValue.ToString(),
        JsonNode jsonNode => jsonNode.ToJsonString(),
        _ => value.ToString()
    };

    /// <summary>
    /// Convert the property values to keys
    /// </summary>
    private static string[]? ToKeys(object[]? values)
        => values?.Where(v => v != null).Select(v => ToKey(v) ?? string.Empty).ToArray();

    /// <summary>
    /// Whether the two key arrays are equal
    /// </summary>
    private static bool EqualKeys(string[]? left, string[]? right)
    {
        if (left == null || right == null) return left == right;
        return left.Length == right.Length && left.Zip(right).All(pair => pair.First == pair.Second);
    }

    /// <summary>
    /// Clone the entry access to avoid mutating the source
    /// </summary>
    private static EntryAccess<string> CloneEntryAccess(EntryAccess<string> access)
        => new()
        {
            Entry = access.Entry == null ? null : CloneEntry(access.Entry),
            Children = access.Children?.Select(CloneEntry).ToArray()
        };

    private static Entry<string> CloneEntry(Entry<string> entry)
    {
        Entry<string> clone = entry.Clone();
        clone.Children = entry.Children?.Select(CloneEntry).ToArray();
        return clone;
    }

    #endregion
}

/// <summary>
/// The entry source call arg, resolved from a function call arg
/// </summary>
internal sealed class ScalarEntrySourceArg
{
    /// <summary>
    /// The source node of the arg
    /// </summary>
    public IValueAccess? Source { get; set; }

    /// <summary>
    /// The constant value of the arg
    /// </summary>
    public object? Value { get; init; }
}

/// <summary>
/// The valid entry source function
/// </summary>
internal sealed class ScalarEntrySourceValid
{
    /// <summary>
    /// The valid property
    /// </summary>
    public required Valid Prop { get; init; }

    /// <summary>
    /// The valid function
    /// </summary>
    public required FunctionType Func { get; init; }

    /// <summary>
    /// The function args
    /// </summary>
    public ScalarEntrySourceArg[] Args { get; init; } = [];
}

/// <summary>
/// The entry source runtime info
/// </summary>
internal sealed class ScalarEntrySourceInfo
{
    /// <summary>
    /// The entry source property owner
    /// </summary>
    public IValueAccess? Owner { get; set; }

    /// <summary>
    /// The entry source version
    /// </summary>
    public long? Version { get; set; }

    /// <summary>
    /// The entry source function
    /// </summary>
    public FunctionType? Source { get; set; }

    /// <summary>
    /// The entry source function args
    /// </summary>
    public ScalarEntrySourceArg[]? Args { get; set; }

    /// <summary>
    /// The subscriptions registered on the source nodes
    /// </summary>
    public List<IDisposable>? Subscribes { get; set; }

    /// <summary>
    /// Whether the entry source is single level
    /// </summary>
    public bool? SingleLevel { get; set; }

    /// <summary>
    /// The cascade level
    /// </summary>
    public long? Cascade { get; set; }

    /// <summary>
    /// The root value of starting
    /// </summary>
    public string? Root { get; set; }

    /// <summary>
    /// Whether all root passed
    /// </summary>
    public bool? AllRootPassed { get; set; }

    /// <summary>
    /// The root entry
    /// </summary>
    public Entry<string>? RootEntry { get; set; }

    /// <summary>
    /// The white list
    /// </summary>
    public string[]? WhiteList { get; set; }

    /// <summary>
    /// The black list
    /// </summary>
    public string[]? BlackList { get; set; }

    /// <summary>
    /// Whether only leaf nodes can be selected
    /// </summary>
    public bool? LeafOnly { get; set; }

    /// <summary>
    /// The valid functions
    /// </summary>
    public List<ScalarEntrySourceValid>? Valids { get; set; }

    /// <summary>
    /// The access value type provider
    /// </summary>
    public FunctionType? ValueTypeProvider { get; set; }

    /// <summary>
    /// The access value type provider args
    /// </summary>
    public ScalarEntrySourceArg[]? ValueTypeProviderArgs { get; set; }

    /// <summary>
    /// The access value type consumer
    /// </summary>
    public FunctionType? ValueTypeConsumer { get; set; }

    /// <summary>
    /// The access value type consumer args
    /// </summary>
    public ScalarEntrySourceArg[]? ValueTypeConsumerArgs { get; set; }

    /// <summary>
    /// The valid result cache
    /// </summary>
    public Dictionary<string, bool>? Validres { get; set; }

    /// <summary>
    /// Whether the entry options are initializing
    /// </summary>
    public bool Initing { get; set; }
}

/// <summary>
/// The value type scalar data node
/// </summary>
public abstract class ScalarNode<T>(ISchemaContext context, IValueTypeAccess type, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders)
    : ScalarNode(context, type, value, parent, propertyProviders) where T : struct
{
    protected T? Value;

    /// <summary>
    /// The confirmed value
    /// </summary>
    protected T? Original;

    /// <inheritdoc/>
    public override bool IsEmpty => Value is null;

    /// <inheritdoc/>
    public override bool TrySetValue<T1>(T1? value) where T1 : default
    {
        if (!value.TryConvertTo<T>(out var result)) return false;
        if (EqualityComparer<T?>.Default.Equals(result, Value)) return true;
        Value = result;
        OnNext(Value);
        return true;
    }

    /// <inheritdoc/>
    public override bool TryGetValue(Type type, out object? value)
    {
        if (type.TryConvert(Value, out var result))
        {
            value = result;
            return true;
        }
        value = null;
        return false;
    }

    /// <inheritdoc/>
    public override bool TryGetValue<T1>(out T1? value) where T1 : default
    {
        if (Value.TryConvertTo<T1>(out var result))
        {
            value = result;
            return true;
        }
        value = default(T1?);
        return false;
    }

    /// <inheritdoc/>
    public override bool TryGetOriginal(Type type, out object? value)
    {
        if (type.TryConvert(Original, out var result))
        {
            value = result;
            return true;
        }
        value = null;
        return false;
    }

    /// <inheritdoc/>
    public override bool Changed => !(Value is null && Original is null) && !EqualityComparer<T?>.Default.Equals(Value, Original);

    /// <inheritdoc/>
    public override void Confirm() => Original = Value;

    /// <inheritdoc/>
    public override void Reset() => TrySetValue<object>(Original);

    /// <inheritdoc/>
    public override void ClearValue()
    {
        if (Value is null) return;
        Value = default(T?);
        OnNext(Value);
    }

    /// <summary>
    /// Copies the confirmed original value, used by clone
    /// </summary>
    protected void CopyOriginal(T? original) => Original = original;

    /// <inheritdoc/>
    public override bool Equals(IValueAccess? other) => other is ScalarNode<T> scalarNode && Equals(Value, scalarNode.Value) ||
                                                        other is EnumNode enumNode && enumNode.TryGetValue(out T? val) && Equals(Value, val);
}

/// <summary>
/// The reference type scalar data node
/// </summary>
public abstract class RefScalarNode<T>(ISchemaContext context, IValueTypeAccess type, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders)
    : ScalarNode(context, type, value, parent, propertyProviders) where T : class
{
    protected T? Value;

    /// <summary>
    /// The confirmed value
    /// </summary>
    protected T? Original;

    /// <inheritdoc/>
    public override bool IsEmpty => Value is null;

    /// <inheritdoc/>
    public override bool TrySetValue<T1>(T1? value) where T1 : default
    {
        if (!value.TryConvertTo<T>(out var result)) return false;
        if (Equals(result, Value)) return true;
        Value = result;
        OnNext(Value);
        return true;
    }

    /// <inheritdoc/>
    public override bool TryGetValue(Type type, out object? value)
    {
        if (type.TryConvert(Value, out var result))
        {
            value = result;
            return true;
        }
        value = null;
        return false;
    }

    /// <inheritdoc/>
    public override bool TryGetValue<T1>(out T1? value) where T1 : default
    {
        if (Value.TryConvertTo<T1>(out var result))
        {
            value = result;
            return true;
        }
        value = default(T1?);
        return false;
    }

    /// <inheritdoc/>
    public override bool TryGetOriginal(Type type, out object? value)
    {
        if (type.TryConvert(Original, out var result))
        {
            value = result;
            return true;
        }
        value = null;
        return false;
    }

    /// <inheritdoc/>
    public override bool Changed => !(Value is null && Original is null) && !Equals(Value, Original);

    /// <inheritdoc/>
    public override void Confirm() => Original = Value;

    /// <inheritdoc/>
    public override void Reset() => TrySetValue<object>(Original);

    /// <inheritdoc/>
    public override void ClearValue()
    {
        if (Value is null) return;
        Value = default(T?);
        OnNext(Value);
    }

    /// <summary>
    /// Copies the confirmed original value, used by clone
    /// </summary>
    protected void CopyOriginal(T? original) => Original = original;

    /// <inheritdoc/>
    public override bool Equals(IValueAccess? other) => other is ScalarNode scalarNode && Equals(Value, scalarNode.GetValue<T>()) ||
                                                        other is EnumNode enumNode && enumNode.TryGetValue(out T? val) && Equals(Value, val);
}

/// <summary>
/// Object data node
/// </summary>
public class AnyNode(ISchemaContext context, IValueTypeAccess type, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders)
    : RefScalarNode<object>(context, type, value, parent, propertyProviders)
{
    public override IValueAccess Clone()
    {
        var node = new AnyNode(Context, Type, Value, null, PropertyProviders.ToArray());
        node.CopyOriginal(Original);
        return node;
    }
}

/// <summary>
///  Bool data node
/// </summary>
public class BoolNode(ISchemaContext context, IValueTypeAccess type, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders)
    : ScalarNode<bool>(context, type, value, parent, propertyProviders)
{
    public override IValueAccess Clone()
    {
        var node = new BoolNode(Context, Type, Value, null, PropertyProviders.ToArray());
        node.CopyOriginal(Original);
        return node;
    }
}

/// <summary>
///  String data node
/// </summary>
public class StringNode(ISchemaContext context, IValueTypeAccess type, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders)
    : RefScalarNode<string>(context, type, value, parent, propertyProviders)
{
    public override IValueAccess Clone()
    {
        var node = new StringNode(Context, Type, Value, null, PropertyProviders.ToArray());
        node.CopyOriginal(Original);
        return node;
    }
}

/// <summary>
///  Numeric data node
/// </summary>
public class DecimalNode(ISchemaContext context, IValueTypeAccess type, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders)
    : ScalarNode<decimal>(context, type, value, parent, propertyProviders)
{
    public override IValueAccess Clone()
    {
        var node = new DecimalNode(Context, Type, Value, null, PropertyProviders.ToArray());
        node.CopyOriginal(Original);
        return node;
    }
}

/// <summary>
/// Int data node
/// </summary>
public class IntNode(ISchemaContext context, IValueTypeAccess type, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders)
    : ScalarNode<long>(context, type, value, parent, propertyProviders)
{
    public override IValueAccess Clone()
    {
        var node = new IntNode(Context, Type, Value, null, PropertyProviders.ToArray());
        node.CopyOriginal(Original);
        return node;
    }
}

/// <summary>
/// DateTime data node
/// </summary>
public class DateNode(ISchemaContext context, IValueTypeAccess type, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders)
    : ScalarNode<DateTimeOffset>(context, type, value, parent, propertyProviders)
{
    public override IValueAccess Clone()
    {
        var node = new DateNode(Context, Type, Value, null, PropertyProviders.ToArray());
        node.CopyOriginal(Original);
        return node;
    }
}
