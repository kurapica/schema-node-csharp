namespace SchemaNode.Example;

/// <summary>
/// Marks the category of a specific API.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class MicroserviceApiCategoryAttribute : System.Attribute
{
    #region Constructors

    /// <summary>
    /// The microservice api category attribtue
    /// </summary>
    public MicroserviceApiCategoryAttribute(params string[] categories) => Category = string.Join('.', categories);

    #endregion

    #region Category

    /// <summary>
    /// The api category
    /// </summary>
    public string Category { get; set; }

    #endregion
}