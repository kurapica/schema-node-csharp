namespace SchemaNode.Runtime.Interface;

/// <summary>
/// The node tyep change handler
/// </summary>
public interface INodeTypeReLoadHandler
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="type"></param>
    void OnNodeTypeLoaded(NodeType[] types);
}
