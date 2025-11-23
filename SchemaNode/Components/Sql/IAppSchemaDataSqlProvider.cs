namespace SchemaNode.Components;

public interface IAppSchemaDataSqlProvider<T>: IAppDataProvider where T: ISqlProvider
{
}