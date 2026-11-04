using Prism.Mvvm;
using Prism.Navigation;
using Prism.Navigation.Regions;
using SchemaNode.WPF.Man.Services;

namespace SchemaNode.WPF.Man.ViewModels;

/// <summary>
/// Type demo ViewModel (screen-scoped).
/// Each navigation activation creates its own <see cref="SchemaScreenSession"/>, attaches the editor,
/// then loads the default demo type; on leave the editor clears its bound node so the view disposes the wrapper, then releases the DataNode and context.
/// </summary>
public class TypeDemoViewModel : BindableBase, INavigationAware, IDestructible
{
    /// <summary>Navigation parameter carrying the type to demo (defaults to system.string).</summary>
    public const string TypeParameter = "type";

    private const string DefaultDemoType = "system.string";

    private readonly SchemaContextFactory _factory;
    private SchemaScreenSession? _session;

    public TypeDemoViewModel(SchemaContextFactory factory, SchemaEditorViewModel editor)
    {
        _factory = factory;
        Editor = editor;
    }

    public SchemaEditorViewModel Editor { get; }

    public bool IsNavigationTarget(NavigationContext navigationContext) => false;

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        Teardown();
        _session = _factory.CreateSession();
        Editor.Attach(_session);

        string typeName = navigationContext.Parameters.GetValue<string>(TypeParameter)
                          is { Length: > 0 } value
            ? value
            : DefaultDemoType;
        _ = Editor.ReloadAsync(typeName);
    }

    public void OnNavigatedFrom(NavigationContext navigationContext) => Teardown();

    public void Destroy() => Teardown();

    private void Teardown()
    {
        Editor.Dispose();
        _session?.Dispose();
        _session = null;
    }
}
