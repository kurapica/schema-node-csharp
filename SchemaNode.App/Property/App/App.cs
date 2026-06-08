namespace SchemaNode.Property.App;

/// <summary>
/// The app property node, which represents a string value that corresponds to an app in the system.
/// Used to mark a class to be an app field of the given application name, the class should be marked with [Meta&lt;SchemaType&gt;]
/// </summary>
public class App : Property<string>;