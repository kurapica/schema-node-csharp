# SchemaNode.Core — Platform Architecture

> SchemaNode.Core is a **language-agnostic, self-describing, extensible metadata platform kernel** built on four pillars: **Meta**, **Property**, **Relation**, and **Function**.
> SchemaNode.Core is a metadata runtime rather than a database schema library. It provides a language-independent semantic model for describing types, behaviors, relations and computations.

## Table of Contents

1. [The Four-Pillar Architecture](#the-four-pillar-architecture)
2. [Meta — Schema Kind Metadata](#meta--schema-kind-metadata)
3. [Property — Composable Behavioral Annotations](#property--composable-behavioral-annotations)
4. [Relation — Dynamic Data Association](#relation--dynamic-data-association)
5. [Function — Semantic Expression Engine](#function--semantic-expression-engine)
6. [The Node Schema Family](#the-node-schema-family)

---

## The Four-Pillar Architecture

SchemaNode.Core rests on four fundamental abstractions. Each pillar addresses a distinct concern:

| Pillar | Question Answered | Implementation |
|--------|-------------------|----------------|
| **Meta** | What *is* this Schema Kind? | C# declarations of SchemaKind types and their usage |
| **Property** | How does data *behave*? | `Property<T>` declares extension attributes for SchemaKinds, describing data |
| **Relation** | How does data *relate*? | `Relation` computes Property values from associated data |
| **Function** | How does computation *work*? | Semantic functions → multi-target compilation system |

---

## Meta — Schema Kind Metadata

Using parts of the **Node Schema** declaration as an example — it is the schema declaration for data node types:

```csharp

/// <summary>
/// The schema container node, which can contain other nodes, such as scalar, struct, enum, array, etc.
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_NODE, SCHEMA_KIND_ORDER_NODE)] // Declares a new schema kind
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_NODE}.schema")]        // Registers this class as a struct type; struct is also a schema kind
[Meta<Attach>(SCHEMA_KIND_NODE)]                             // Attach is an extension property of the struct kind,
                                                             // meaning: attach the extension properties of the specified schema kind to this struct,
                                                             // making them available for frontend display and configuration
public sealed class NodeSchema: ExtensibleSchema             // ExtensibleSchema provides storage support for property extensions
{
    /// <summary>
    /// The namespace which includes the schema
    /// </summary>
    [Meta<PrimaryIndex>(0)]                                  // Primary key declaration; if a class declares primary keys, indexes, etc.,
                                                             // an array type containing this information is also generated
    [Meta<SchemaType>(typeof(NamespaceType))]                // Field SchemaType specifies the schema type when stored as a struct field;
                                                             // here it is the namespace name
    public string? Namespace { get; set; }                   // Schema types defined in the Node Schema family are managed by namespace
    
    /// <summary>
    /// The schema name
    /// </summary>
    [Meta<PrimaryIndex>(1)]                                  // Composite primary key, order determines position
    [Meta<SchemaType>(typeof(Identifier))]
    public string Name { get; set; } = null!;
    
    /// <summary>
    /// The full namespace name
    /// </summary>
    [SchemaIgnore]                                           // Ignored when generating struct type; this is for C# internal use only
    [JsonIgnore]
    public string FullName => $"{Namespace}.{Name}".Trim('.');
    
    /// <summary>
    /// The schema kind
    /// </summary>
    [Meta<SchemaType>(typeof(NodeSchemaKind))]               // Sub-schema kind, where NodeSchemaKind is defined as follows
    public string Kind { get; set; } = null!;
}

/// <summary>
/// Represents the node schema kinds
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_NODE}.kind")]         // Enum types generate an enum schema type; again specifying the namespace name
[Meta<Record>(typeof(Property.Record.NodeSchemaKind))]      // Values of this enum type are dynamically registered through declarations,
                                                            // implemented via a Record property
public enum NodeSchemaKind;

/// <summary>
/// The node schema kind record
/// </summary>
public class NodeSchemaKind : RecordProperty<string>;       // Property definitions are simple; based on Property<T> or extended types;
                                                            // some properties are for internal system use

```

The system uses `Meta<T>(...) where T : Property` declarations instead of explicit registration. This ensures that other projects can also register new schema kinds, schema types, properties, and other features through declarations, without needing to understand complex code registration syntax.

Generally, a schema kind is a type prototype. It is typically associated with a class that can be registered as a struct type. The fields of that class determine the **metadata (meta)** of the type prototype — the data that immutably defines the type's capabilities. How the type is used is determined by the system that consumes it. The SchemaNode.Core kernel only concerns itself with organizing **Meta + Property + Relation through Function**, and ensuring that configuration can be completed in the frontend configuration interface.

---

## Property — Composable Behavioral Annotations

Property is implemented through the `Property<T>` base class, providing extensions to Meta metadata and capabilities such as data validation.

```csharp
public abstract class Property<T> : IProperty
{
    public T? Value { get; private set; }
    public bool Stackable { get; }       // Can combine with same-type properties
}
```

Properties are attributes declared by each endpoint to extend the metadata of schema kinds — for example, `visible` used by forms. Properties declared by the frontend and backend can differ; each endpoint only consumes properties it can recognize.

Using **struct kind** for further explanation:

```csharp
/// <summary>
/// The struct schema
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ORDER_STRUCT)]      // Declares a new struct kind
[Meta<NodeSchemaKind>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ORDER_STRUCT)]  // Registers a new "struct" enum value for NodeSchemaKind
[Meta<ValueSchemaKind>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ORDER_STRUCT)] // Registers a new "struct" enum value for ValueSchemaKind; both are enum schema types
[Meta<NodeType>(typeof(RuntimeStructType))]                           // Declares the C# runtime type, used for managing type associations and
                                                                      // providing additional functionality, e.g., FunctionType provides CallAsync
[Meta<SchemaGenerator>(typeof(StructGenerator))]                      // Declares the generator that extracts struct schema from C# types;
                                                                      // it implements complex parsing and registration flows
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_STRUCT}.schema")]               // Registers the StructSchema C# class as the struct schema type
                                                                      // under the namespace system.schema.struct.schema
[Meta<Attach>(SCHEMA_KIND_STRUCT)]                                    // Attaches the extension properties of the struct kind to this struct schema type
[Meta<Append>(typeof(Relations))]                                     // Append adds properties that were already defined, or that didn't specify
                                                                      // which schema kinds they extend; they can be added via Append
[Relation<EntrySource>($"${nameof(UnionValids)}.{nameof(StructUnionValidation.Args)}.{nameof(CallArg.Source)}", NS_SYSTEM_SCHEMA_REFLECT_GET_SUB_ENTRIES, RELATION_OWNER, NODE_SELF)]
public sealed class StructSchema : ExtensibleSchema
{
    /// <summary>
    /// The struct fields
    /// </summary>
    public StructFieldSchema[] Fields { get; set; } = [];             // Struct metadata: defines struct field definitions
    
    /// <summary>
    /// The union validations
    /// </summary>
    public StructUnionValidation[]? UnionValids { get; set; }         // Defines cross-field validation rules
}
```

The system uses a `Generator` mechanism to extract system schemas from C# code. However, at actual runtime, the vast majority of schema types are configured through the frontend; system schemas only maintain the baseline needed for system operation.

Then, through the property system, we attach struct to Node Schema:

```csharp
/// <summary>
/// Declare struct property for node schema
/// </summary>
[Meta<Alias>("struct")]                                             // Property name; if omitted, uses class name minus "Property" suffix, lowercased — also "struct"
[Meta<ForSchema>(SCHEMA_KIND_NODE)]                                 // Declares which schema kinds this property is defined for; this is an attached property of node schema
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]                              // Declares which schema kind's generator can parse it; struct type's generator is the default;
                                                                    // others require OfSchema to scope
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.struct")]      // Registers as a property schema type, named system.schema.property.core.struct
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(NodeSchema.Kind)}", SCHEMA_KIND_STRUCT)]
public sealed class StructProperty: Property<StructSchema>;
```

Through this property, a struct attribute is attached to Node Schema. The actual effect is:

```json
{
    "namespace": "system",
    "name": "test",
    "kind": "struct",

    "struct": {
        "fields": [
            {
                "name": "x",
                "type": "system.number",
                "require": true
            }
        ]
    }
}
```

All types registered in the schema system are language-agnostic; they are typically transmitted between endpoints as JSON structures. Thus, `struct` is simply an extension property of node schema — Node schema does not need to understand it, but that does not prevent struct from being defined on top of node.

Beyond special properties like struct, the system also provides conventional properties:

```csharp
[Meta<Alias>("lowlimit")]
[Meta<ForSchema>(SCHEMA_KIND_INT)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CONSTRAINT}.{nameof(LowLimitInt)}")]
public class LowLimitInt : Property<long>, IConstraintProperty
{
    public bool? ValidateInt(SchemaContext context, IntNode node)
    {
        if (node.IsEmpty) return null;
        return node.GetValue<long>() >= Value;
    }
}


[Meta<Alias>("lowlimit")]
[Meta<ForSchema>(SCHEMA_KIND_DATE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CONSTRAINT}.{nameof(LowLimitDate)}")]
public class LowLimitDate : Property<DateTimeOffset>, IConstraintProperty
{
    public bool? ValidateDate(SchemaContext context, DateNode node)
    {
        if  (node.IsEmpty) return null;
        return node.GetValue<DateTimeOffset>() >= Value;
    }
}
```

These two classes provide the same **lowlimit** property for the `int` and `date` schema kinds respectively. At the same time, they implement the `IConstraintProperty` interface, which means they are also used for data validation — all constraints violated by a piece of data are saved in the validation result.

Note: only properties that declare `SchemaType` are registered in the schema system and can be transmitted to other endpoints (for example, for rendering in configuration interfaces).

---

## Relation — Dynamic Data Association

Relation defines how property values are dynamically computed based on other data. Two built-in types:

**Assign** — Forced value assignment:
```csharp
public class Assign : IRelationProcess
{
    public object? Value { get; set; }
    public Task<object?> ProcessAsync(...) => Task.FromResult(Value);
}
```

**Call** — Function-based computation (e.g., `lookup_manager($store_id)`):
```csharp
public class Call : IRelationProcess
{
    public string Func { get; set; }
    public CallArg[] Args { get; init; }
}
```

In the examples above, we used two Relation declarations:

```csharp
[Relation<EntrySource>($"${nameof(UnionValids)}.{nameof(StructUnionValidation.Args)}.{nameof(CallArg.Source)}", NS_SYSTEM_SCHEMA_REFLECT_GET_SUB_ENTRIES, RELATION_OWNER, NODE_SELF)]
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(NodeSchema.Kind)}", SCHEMA_KIND_STRUCT)]
```

Each Relation is associated with a Property, a Target, and execution parameters. `Relation<T>` here uses the `Call` execution style. Taking:

```csharp
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(NodeSchema.Kind)}", SCHEMA_KIND_STRUCT)]
```

as an example: it is associated with the `Visible` property. Because it is defined on a property type, its Target is the property itself — that is, **struct**. Combined with the following JSON:

```json
{
    "namespace": "system",
    "name": "test",
    "kind": "struct",
    "struct": {}
}
```

This Relation expresses: `struct.visible = kind == "struct"` — meaning the struct configuration form is only visible when the kind is "struct".

In practice, Relations are straightforward to use in the frontend configuration interface. For a type such as a struct, you can select its child fields as the target, then specify a property as the destination for the Relation's computed result. The parameters, for Call, are other fields of the struct or constants. When an associated field's value changes, the Relation is re-executed, the result is saved to the property, triggering re-rendering — this is the most basic usage.

Relations configured at the C# level are primarily for controlling linkage rules in the configuration interface, rather than hard-coding them there — which would require changes in multiple places.

---

## Function — Semantic Expression Engine

SchemaNode functions are **pure, non-Turing-complete, semantically analyzable** computations. Two types:

**Atomic functions** (registered in C#, no function body):
```csharp
// Function declarations must specify a namespace; all static methods without [SchemaIgnore] below are registered
// Function parameters and return types are resolved via reflection; generics are supported
[Meta<SchemaType>(NS_SYSTEM_LOGIC)] 
public static class SystemLogic
{
    /// <summary>
    /// system.logic.and
    /// </summary>
    public static bool and([Meta<Default>(false)] bool a, [Meta<Default>(false)] bool b) => a && b;
}
```

**Semantic functions** (expression body, configured in frontend, no variables/loops):
```json
{
    "namespace": "system.math",
    "name": "clamp",
    "kind": "function",
    "function": {
        "args": [
            {
                "name": "value",
                "type": "system.number"
            },
            {
                "name": "min",
                "type": "system.number"
            },
            {
                "name": "max",
                "type": "system.number"
            }
        ],
        "return": "system.number",
        "exps": [
            {
                "name": "cmin",
                "type": "call",
                "func": "system.math.max",
                "args": [
                    { "source": "value" },
                    { "source": "min" }
                ]
            },
            {
                "name": "result",
                "type": "call",
                "func": "system.math.min",
                "args": [
                    { "source": "cmin" },
                    { "source": "max" }
                ]
            }
        ]
    }
}
```

Semantic functions are defined using pure function-call composition, so any platform can implement them via interpreted function-call execution. There are a few special behaviors:

- Based on the return type, the system determines how to construct the result. If the return type matches the last expression's result type, that expression's result is used.
- If the return type is a struct, the system attempts to match expression names against struct field names and combine them into a struct.
- The expression `type` field indicates the call mode; the default is `call` (direct invocation), but when arguments contain arrays, modes like `map`, `reduce`, `first` can be used.

While the platform can execute semantic functions via interpretation, C# provides a more powerful compilation mode; other platforms implement according to their needs (e.g., TypeScript uses interpretation without needing multi-target compilation).

C# provides the **CompileContext** multi-target compilation system, which converts semantic function expressions into LINQ `Expression` trees and then compiles them into delegates, ensuring execution speed close to native C#. Beyond basic compilation, it also supports compiling the same function into different forms through different CompileContexts.

Taking `RowAuths` as an example: it defines rules for filtering data based on the accessing user's current role — something like `func(context, data) => data.userid == context.GetContextItem<user>().id` — retrieving the current user from context and determining data ownership.

When a user requests data, the system uses **QueryFilterCompileContext** to compile this function. Its return result is no longer a `bool` value, but rather a query tree derived from the logic processing. This query tree can be merged with other query conditions to complete the data request.

When a user submits data, the system uses the default **CompileContext** to compile the function, passing data in and returning a boolean to determine data validity. This ensures that modifying just this one function simultaneously affects both query and submission, preventing inconsistency between the two actions.

This is the **unique truth** principle: one function definition = validation + filtering + any compilation target.

These multi-target compilation contexts are easy to implement and serve as the function system's external extension point.

---

## The Node Schema Family

Based on the SchemaNode.Core kernel, different **Schema Families** can be defined. A schema family contains multiple schema kinds and the complete functional system that consumes those schema kinds. To enable this, the system provides the **Node Schema** family by default.

The **Node Schema** family defines the universal data types (`scalar`, `enum`, `struct`, `array`) and other core types (`property`, `relation`, `function`). Upper-layer applications like SchemaNode.App are all replaceable, while heterogeneous systems built on SchemaNode.Core can share data through the **Node Schema** family.


## Summary

SchemaNode.Core solely concentrates on how to construct based on `Meta`, `Property`, `Relation`, and `Function`, with the Node Schema family serving as their carrier. Based on this, we can further define other schema families to accomplish practical functions and share data based on the Node Schema.


              SchemaNode.Core

    Meta   Property   Relation   Function
         \      |      |      /
          \     |      |     /
           \    |      |    /
            ┌───────────────┐
            │   Node Schema │
            │   Data Model  │
            └───────────────┘
                    │
              Schema Runtime
                    │
      ┌─────────────┼─────────────┐
      │             │             │
  Form Engine   Workflow      App Model
      │             │             │