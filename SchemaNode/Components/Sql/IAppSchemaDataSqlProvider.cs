namespace SchemaNode.Components;

public interface IAppSchemaDataSqlProvider<T>: IAppSchemaDataProvider where T: ISqlProvider
{
}