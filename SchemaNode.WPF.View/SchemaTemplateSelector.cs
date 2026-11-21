using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SchemaNode.WPF.View.NodeWrapper;

namespace SchemaNode.WPF.View;

/// <summary>
/// Turns the registry decision into a <see cref="DataTemplate"/>.
///
/// Lookup order gives the host the last word: resources reachable from the container (window /
/// user control / application) are tried first, then the built-in template dictionary packed in
/// this library. A host can therefore override any key, or register a brand new key, without
/// modifying the library.
/// </summary>
public sealed class SchemaTemplateSelector : DataTemplateSelector
{
    private static readonly Lazy<ResourceDictionary?> BuiltInTemplates = new(() =>
    {
        try
        {
            return new ResourceDictionary
            {
                Source = new Uri(
                    "/SchemaNode.WPF.View;component/Templates/SchemaTemplates.xaml",
                    UriKind.Relative)
            };
        }
        catch (Exception)
        {
            return null;
        }
    });

    /// <inheritdoc />
    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not DataNodeWrapper wrapper) return null;

        SchemaViewRole role = wrapper.Role;
        string skin = SchemaViewRegistry.DefaultSkin;

        // The nearest SchemaView is the innermost dispatch point. Role and skin are already
        // propagated through the wrapper tree, so only an explicit override on that view applies.
        DependencyObject? current = container;
        for (int i = 0; i < 16 && current is not null; i++)
        {
            if (current is SchemaView view)
            {
                if (view.Role != SchemaViewRole.Field) role = view.Role;
                if (view.Skin != SchemaViewRegistry.DefaultSkin) skin = view.Skin;
                break;
            }
            current = VisualTreeHelper.GetParent(current);
        }

        string key = wrapper.TemplateKeyOverride
                     ?? wrapper.Registry.Resolve(wrapper.Node, role, skin).TemplateKey;

        if (container is FrameworkElement element)
        {
            object? hostResource = element.TryFindResource(key);
            if (hostResource is DataTemplate hostTemplate) return hostTemplate;
        }

        ResourceDictionary? defaults = BuiltInTemplates.Value;
        if (defaults?[key] is DataTemplate defaultTemplate) return defaultTemplate;

        object? fallback = defaults?[SchemaTemplateKeys.Input];
        return fallback as DataTemplate;
    }
}
