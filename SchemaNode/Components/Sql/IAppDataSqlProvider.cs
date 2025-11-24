namespace SchemaNode.Components;

/// <summary>
/// The application data provider for SQL-based databases.
/// </summary>
/// <typeparam name="T">The sql provider</typeparam>
public interface IAppDataSqlProvider<T>: IAppDataProvider where T: ISqlProvider
{
}