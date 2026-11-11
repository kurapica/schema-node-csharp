namespace SchemaNode.Avalonia.Man.Navigation;

/// <summary>
/// Single-region navigation contract. The WPF Man layer used Prism regions to host one content
/// view; this port keeps the same call shape (`RequestNavigate(region, viewName, callback, parameters)`)
/// while a single <see cref="RegionManager"/> owns the current view.
/// </summary>
public interface IRegionManager
{
    /// <summary>
    /// Creates the target view model and view for <paramref name="viewName"/>, runs
    /// <see cref="INavigationAware"/> on both sides and publishes the new view.
    /// </summary>
    void RequestNavigate(
        string regionName,
        string viewName,
        Action<INavigationResult>? navigationCallback = null,
        NavigationParameters? navigationParameters = null);
}
