using SchemaNode.Attribute;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Event;

/// <summary>
/// The schema event
/// </summary>
public abstract class SchemaEvent: Event;

/// <summary>
/// The node schema create event
/// </summary>
[Meta<OfSchema>(SCHEMA_KIND_EVENT)]
[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.schema.create")]
public class SchemaCreateEvent: SchemaEvent, IEventPayload<string>;

/// <summary>
/// The node schema delete event
/// </summary>
[Meta<OfSchema>(SCHEMA_KIND_EVENT)]
[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.schema.delete")]
public class SchemaDeleteEvent: SchemaEvent, IEventPayload<string>;

/// <summary>
/// The node schema change event
/// </summary>
[Meta<OfSchema>(SCHEMA_KIND_EVENT)]
[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.schema.change")]
public class SchemaChangeEvent : SchemaEvent, IEventPayload<string>;


/// <summary>
/// The application schema create event
/// </summary>
[Meta<OfSchema>(SCHEMA_KIND_EVENT)]
[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.app.create")]
public class AppSchemaCreateEvent : SchemaEvent, IEventPayload<string>;

/// <summary>
/// The application schema delete event
/// </summary>
[Meta<OfSchema>(SCHEMA_KIND_EVENT)]
[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.app.delete")]
public class AppSchemaDeleteEvent : SchemaEvent, IEventPayload<string>;

/// <summary>
/// THe application schema update event
/// </summary>
[Meta<OfSchema>(SCHEMA_KIND_EVENT)]
[Meta<SchemaType>($"{NS_SYSTEM_EVENT}.app.update")]
public class AppSchemaChangeEvent : SchemaEvent, IEventPayload<string>;
