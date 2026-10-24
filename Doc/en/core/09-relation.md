# 09. Relation Prototype

A relation does not exist independently; it carries a set of relations by defining the `Relations` property. Prototypes such as `node.struct`, `node.array`, and `node.function` declare that they have it, in order to use the relation capability.

When we describe entities based on Meta and Property, what we get is a static snapshot of a semantic entity. When we use relations to associate entity data with the properties of other entities, what we get is a semantic space operating in real time.

Here the design specifically allows only the `data-relation->property` pattern, because circular dependencies must be avoided. If an entity's data is simply given by one or more other entities, we can express that by writing a value into the `default` property.

In summary, the design of relations themselves is prototype-agnostic, but it works based on the data access paths provided by entities. Its form is:

```json
"relations": [
  {
    "target": "max",
    "property": "lowlimit"
    "kind": "call",
    "call": {
        "args": [
        {
            "type": "system.int",
            "source": "min"
        }
        ],
        "func": "system.intrinsic.assign<system.int>",
        "mode": "call"
    }
  }
]
```

This is the relation in the test that uses the minimum value as the lower limit of the maximum value. It will be discussed later together with the relation execution process.

The relevant implementation is mainly located in:

- `Schema/RelationSchema.cs`: the relation definition body;
- `Relation/`: the relation execution process;
- `Runtime/Type/RelationType.cs`: runtime parsing and execution of relations;
- `Attribute/RelationAttribute.cs`: C# declaration syntax.
- `Relation/AnyProcesscs`: declaration of any handling.
- `Relation/CallProcesscs`: declaration of call handling.
- `Relation/AssignProcesscs`: declaration of assign handling.

## 1. Relation Definition

The relation definition body `RelationSchema` describes the target of the relation, the property to write, and the relation kind.

| Field | Meaning |
| --- | --- |
| `Target` | The target the relation acts on; can be a type name or a semantic access path |
| `Property` | The name of the property the relation writes to |
| `Kind` | The relation kind |
| `TargetType`, `TargetKind` | The type and its Schema kind derived from the target |
| `OwnerKind` | The Schema kind of the relation's owner |
| `ValueType` | The value type derived from the target and property information |

Among them, `Target`, `Property`, and `Kind` are the main declaration information of a relation, while the remaining fields are derived from the relation definition's own semantics, that is, they are not persisted.

The core of the association in the earlier example is:

```json
{
    "target": "max",
    "property": "lowlimit"
    "kind": "call",
}
```

It specifies that the target of the relation is `max`, the property to write is `lowlimit`, and the relation kind is `call`. Because it acts within a struct, its `Kind` is derived as `node.struct.field` (specified when loading properties based on the prototype), and `TargetKind` is derived as `node.int` based on the type of `max`, so that `lowlimit` can be resolved to the `system.schema.node.int.lowlimit` property, that is, the integer lower-limit property.

Note that this resolution is performed by the execution layer; the semantic declaration itself does not need to contain it. So which concrete semantic property type it is can also be determined by the execution layer.

Because it acts on a struct, accessing the `max` path in the struct naturally obtains its `max` field. The relation itself does not care whether `target` exists; it accesses the target value through the `IValueAccess` interface and applies the relation.


## 2. Three Relation Kinds

The `Kind` of a relation determines how the result is produced. Three relation kinds are currently defined:

| Kind | Effect | Typical use |
| --- | --- | --- |
| `assign` | Returns a constant value | Fixed default values, marker properties |
| `call` | Calls a function to produce a result | Dynamic default values, visibility conditions, type overrides, validation |
| `any` | Describes multiple candidate functions | If any function returns true, the result is true |

`call` is relatively common, because it uses the data of other entities as input based on the arguments of the called function, producing an association.

Now look at the example above:

```json
"call": {
    "func": "system.intrinsic.assign<system.int>",
    "mode": "call"
    "args": [
      {
        "type": "system.int",
        "source": "min"
      }
    ],
}
```

This is a typical function call. It has only one argument, the `min` access path, which in the struct represents the `min` field.

The other two kinds will not be introduced in detail, because they are easy to understand when configuring.

Note that the execution of relations is not fixed. Just like registering `struct` into `node`, the execution approach of relations can be extended without limit; see `CallProcess.cs` for reference.


## 3. C# Declaration Syntax

C# declares relations through the generic attribute `RelationAttribute<TP, TR>`:

- `TP` specifies the property type the relation writes, such as `Require`, `Display`, or `Visible`;
- `TR` specifies the relation kind, such as `Assign` or `Call`.

For example:

```csharp
[Relation<Require, Assign>(nameof(Name), true)]
```

This means setting the current object's `require` property to `true`.

The difference between assigning with `Relation` and assigning with the `Meta` attribute is that `Meta` only assigns the property directly, whereas `Relation` records the provider of the property. In particular, when configuring `EntrySource`, it is often necessary to access other fields, and only by locating the property's provider can access to other paths in the relation be completed normally.

```csharp
[Relation<Display, Call>(
    NODE_SELF,
    "example.getDisplay",
    "@name")]
```

This means calling `example.getDisplay`, reading the argument from the `name` path, and writing the result into the current object's `display` property.

The target property name is derived from the property type, and the relation kind is also determined by the metadata of the corresponding type. In this way, C# declarations and relation definitions in the Schema use the same semantic structure.

## 4. Runtime Processing

`RelationType` is the runtime representation of a relation, used by node types during loading, rather than being an independent node type.

The loading stage mainly completes three things:

1. Resolve the target property type according to the Schema kind of the relation's owner;
2. Find and load the corresponding relation execution process according to `Kind`;
3. Establish dependency references such as functions, and check whether the target path and function are valid.

During execution, `RelationType` creates the target property instance, calls the relation process to obtain the result, then writes the result into the property, and finally returns `IProperty`.

The whole process can be summarized as:

`RelationSchema → RelationType → IRelationProcess → IProperty`

A relation is only responsible for producing property results. Which property collection the result is ultimately merged into — the definition body, a field, or a usage form — is decided by the caller.

The C# backend usually only handles relations related to `IConstraintProperty` properties, for validation. However, in the App project, for fields marked with the `DisplayOnly` property, relations related to their `Default` are handled. This can often be used to track dependency associations between tables; of course, the specifics are explained at the App layer.


## 5. Summary

The relation prototype unifies semantic derivation into three core elements: **target, property, and relation kind**. The definition body describes the relation, the runtime parses and loads it, and the concrete relation process produces the result.

Working with properties, functions, and other prototypes, it enables different semantics such as default values, visibility, type overrides, validation, and candidate sources to be composed through a unified mechanism.

Therefore, a relation prototype is not yet another business object, but the general mechanism SchemaNode uses to connect existing semantics and form derived semantics.
