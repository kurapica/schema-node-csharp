using SchemaNode.Attribute;
using SchemaNode.Property.App;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Event;

/// <summary>
/// The schema event
/// </summary>
public abstract class SchemaEvent : BaseEvent, IEventPayload<string>;

/// <summary>
/// The node schema create event
/// </summary>
[Meta<OfNodeKind>(NODE_KIND_EVENT)]
[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.schema.create")]
[Meta<PayloadType>(typeof(AnyType))]
public class SchemaCreateEvent: SchemaEvent;

/// <summary>
/// The node schema delete event
/// </summary>
[Meta<OfNodeKind>(NODE_KIND_EVENT)]
[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.schema.delete")]
[Meta<PayloadType>(typeof(AnyType))]
public class SchemaDeleteEvent: SchemaEvent;

/// <summary>
/// The node schema change event
/// </summary>
[Meta<OfNodeKind>(NODE_KIND_EVENT)]
[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.schema.change")]
[Meta<PayloadType>(typeof(AnyType))]
public class SchemaChangeEvent : SchemaEvent;


/// <summary>
/// The application schema create event
/// </summary>
[Meta<OfNodeKind>(NODE_KIND_EVENT)]
[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.app.create")]
[Meta<PayloadType>(typeof(AppType))]
public class AppSchemaCreateEvent : SchemaEvent;

/// <summary>
/// The application schema delete event
/// </summary>
[Meta<OfNodeKind>(NODE_KIND_EVENT)]
[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.app.delete")]
[Meta<PayloadType>(typeof(AppType))]
public class AppSchemaDeleteEvent : SchemaEvent;

/// <summary>
/// THe application schema update event
/// </summary>
[Meta<OfNodeKind>(NODE_KIND_EVENT)]
[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.app.update")]
[Meta<PayloadType>(typeof(AppType))]
public class AppSchemaChangeEvent : SchemaEvent;
