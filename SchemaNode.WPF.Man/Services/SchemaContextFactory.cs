using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using SchemaNode.AI.Chat;
using SchemaNode.Api.Client;
using SchemaNode.Context;
using SchemaNode.Http;
using SchemaNode.WPF.View.Localization;

namespace SchemaNode.WPF.Man.Services;

/// <summary>
/// A Schema session exclusively owned by a single WPF screen.
///
/// Hard lifecycle constraints:
///   1. After creating <see cref="SchemaContext"/>, set <c>FullMode = true</c> <b>immediately</b>; only afterwards may types be loaded and DataNodes created;
///   2. Teardown happens in strict reverse order: the screen clears its bound nodes so the views dispose their wrappers,
///      then calls <see cref="ReleaseNode"/> / this class fallback-disposes DataNodes, and finally disposes the SchemaContext and its scope;
///   3. This class is responsible only for DataNodes, the context, and the scope; it never touches WPF view objects.
/// </summary>
public sealed class SchemaScreenSession : IDisposable
{
    private readonly IServiceScope _scope;
    private readonly List<IDisposable> _nodes = [];
    private readonly Action<SchemaContext> _unregister;
    private bool _disposed;

    internal SchemaScreenSession(
        IServiceScope scope,
        SchemaContext context,
        ISchemaApiProvider api,
        Action<SchemaContext> unregister,
        IAIChat? chat,
        ISchemaFeatures features)
    {
        _scope = scope;
        _unregister = unregister;
        Context = context;
        Api = api;
        Chat = chat;
        Features = features;
    }

    /// <summary>Schema context exclusively owned by this screen (FullMode already enabled)</summary>
    public SchemaContext Context { get; }

    /// <summary>Data-access implementation for this screen (local mode runs on its own non-FullMode context)</summary>
    public ISchemaApiProvider Api { get; }

    /// <summary>
    /// The schema assistant resolved from the same scope as <see cref="Context"/>, so the local
    /// implementation sees exactly this screen's definitions. Null when the host did not register
    /// an assistant.
    /// </summary>
    public IAIChat? Chat { get; }

    /// <summary>
    /// The enabled server features, answered in process from the same registry that HTTP hosts
    /// publish through the protocol response. The WPF shell never fetches the protocol.
    /// </summary>
    public ISchemaFeatures Features { get; }

    /// <summary>
    /// Registers a DataNode created by this screen so it can be fallback-disposed in reverse order when the screen closes.
    /// </summary>
    public void TrackNode(IDisposable node)
    {
        if (_disposed || node is null) return;
        _nodes.Add(node);
    }

    /// <summary>
    /// Explicitly disposes a DataNode at the screen level.
    /// Precondition: the corresponding Wrapper tree has already been disposed (unsubscribed), so no UI callbacks are triggered anymore.
    /// </summary>
    public void ReleaseNode(IDisposable node)
    {
        if (node is null) return;
        _nodes.Remove(node);
        node.Dispose();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Remove from the live registry before disposing: a language switch must never touch a dead context.
        _unregister(Context);

        // Fallback: if the screen did not call ReleaseNode for each node, dispose DataNodes in reverse creation order.
        for (int i = _nodes.Count - 1; i >= 0; i--)
            _nodes[i].Dispose();
        _nodes.Clear();

        // After all DataNodes are disposed, finally dispose the Context and its scope.
        Context.Dispose();
        _scope.Dispose();
    }
}

/// <summary>
/// Screen-level SchemaContext factory.
///
/// The runtime (<c>ISchemaRuntime</c>) is a process-wide shared singleton; each screen only adds one
/// <see cref="IServiceScope"/> and one scoped <see cref="SchemaContext"/>,
/// and does not call <c>InitSchemaRuntimeAsync</c> again. This both isolates FullMode / Access / ContextItems /
/// DataNode subscriptions and avoids paying the runtime initialization cost once per screen.
///
/// In local mode each screen gets its own <see cref="LocalSchemaApiProvider"/> running on that
/// screen's scoped <see cref="SchemaContext"/>; remote mode is stateless and can share a single
/// <see cref="RemoteSchemaApiProvider"/>.
/// </summary>
public sealed class SchemaContextFactory
{
    private readonly IServiceProvider _runtimeServices;
    private readonly ISchemaApiProvider? _sharedRemote;

    // Live screen contexts. A language switch must reach every screen that still exists.
    private readonly HashSet<SchemaContext> _liveContexts = [];
    private readonly object _liveLock = new();
    private string _locale = LocaleManager.ZH_CN;

    public SchemaContextFactory(IServiceProvider runtimeServices, ISchemaApiProvider? sharedRemote = null)
    {
        _runtimeServices = runtimeServices ?? throw new ArgumentNullException(nameof(runtimeServices));
        _sharedRemote = sharedRemote;
    }

    /// <summary>Locale applied to newly created screen contexts (the persisted setting)</summary>
    public string CurrentLocale
    {
        get
        {
            lock (_liveLock) return _locale;
        }
    }

    /// <summary>
    /// Creates a screen session: scoped context → FullMode=true immediately → screen-level API provider.
    /// The order must not be reversed: FullMode must be set before loading types or creating DataNodes.
    /// The context is registered as live and its <see cref="SchemaContext.Locale"/> is initialized from
    /// the persisted language, not from transient UI state.
    /// </summary>
    public SchemaScreenSession CreateSession()
    {
        IServiceScope scope = _runtimeServices.CreateScope();
        SchemaContext context = scope.ServiceProvider.GetRequiredService<SchemaContext>();

        // Enable full mode immediately after creation (DataNode.FullMode reads Context.FullMode directly)
        context.FullMode = true;

        string locale;
        lock (_liveLock)
        {
            locale = _locale;
            _liveContexts.Add(context);
        }
        context.Locale = locale;

        ISchemaApiProvider api = _sharedRemote ?? new LocalSchemaApiProvider(context);

        // Resolve from the screen scope, never from the root provider: LocalAIChat must use the
        // scoped SchemaContext and must be disposed with the session.
        IAIChat? chat = scope.ServiceProvider.GetService<IAIChat>();
        ISchemaFeatures features = scope.ServiceProvider.GetService<ISchemaFeatures>() ?? new SchemaFeatures();
        return new SchemaScreenSession(scope, context, api, Unregister, chat, features);
    }

    /// <summary>
    /// Applies the locale to every live screen context and remembers it for future sessions.
    /// Core reads <see cref="SchemaContext.Locale"/> when it produces new text, so any error or
    /// validation message generated afterwards uses the new language.
    /// </summary>
    public void ApplyLocale(string? locale)
    {
        string normalized = LocaleManager.Normalize(locale);
        SchemaContext[] contexts;
        lock (_liveLock)
        {
            _locale = normalized;
            contexts = [.. _liveContexts];
        }

        foreach (SchemaContext context in contexts)
            context.Locale = normalized;
    }

    private void Unregister(SchemaContext context)
    {
        lock (_liveLock) _liveContexts.Remove(context);
    }
}
