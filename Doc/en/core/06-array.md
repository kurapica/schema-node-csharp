# 06. Array Prototype

The array prototype describes an ordered collection of elements of the same type. Its core definition is the element type, but it also supports primary keys, indexes, size constraints, and relative access paths, so that an array can represent either an ordinary list or a data collection with primary keys and indexes.

The implementation related to arrays is concentrated in the following locations:

- `Schema/ArraySchema.cs`: array prototype, definition body, and usage form;
- `Runtime/Type/ArrayType.cs`: runtime implementation of the array type;
- `Node/ArrayNode.cs`: array value node;
- `Property/Array/`: properties such as `ArrayValue`, `Primary`, `Indexes`, `MinSize`, and `MaxSize`;
- `Generator/StructGenerator.cs`: generating companion arrays for structs with primary keys or indexes.


## 1. Prototype Declaration

```csharp
[Meta<SchemaKind>(SCHEMA_KIND_NODE_ARRAY, SCHEMA_KIND_ORDER_ARRAY)]
[Meta<NodeKind>(NODE_KIND_ARRAY, SCHEMA_KIND_ORDER_ARRAY)]
[Meta<NodeValueKind>(NODE_KIND_ARRAY, SCHEMA_KIND_ORDER_ARRAY)]
[Meta<NodeType>(typeof(RuntimeArrayType))]
[Meta<SchemaUsage>(typeof(ArrayUsage))]
[Meta<Append>(typeof(Generics), typeof(Relations))]
[Meta<ArrayValue>]
public sealed class ArrayKind;
```

The declaration of the array prototype is similar to enums and structs: it specifies the prototype identifier, node classification, runtime type, and usage form, and declares support for generics, relations, and the array validation property.

Unlike enums and structs, the array prototype has no independent `SchemaGenerator`. Conversion from C# types to Schema is handled by existing generation logic such as `StructGenerator`; among them, structs with primary keys or indexes can also generate corresponding companion arrays.


## 2. Array Definition Body

The array definition body `ArraySchema` has only one required field: `Element`, used to reference the semantic type of the array element.

```csharp
[Meta<SchemaKind>(SCHEMA_KIND_NODE_ARRAY_DEFINE, SCHEMA_KIND_ORDER_ARRAY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ARRAY}.schema")]
[Meta<Attach>(SCHEMA_KIND_NODE_ARRAY_DEFINE)]
public sealed class ArraySchema : PropertyOwner
{
    [Meta<SchemaType>(typeof(ElementType))]
    public required string Element { get; set; }
}
```

Besides the element type, an array can also describe the primary key fields and indexes on the element struct through `Primary` and `Indexes`.

- `Primary` stores the list of primary key field names.
- `Indexes` stores index definitions; each index contains a name, a field list, and a uniqueness flag.

These properties are not fixed fields of the array definition body, but are provided through semantic properties associated with the array prototype. They are visible only when the element type is a struct, and the candidate sources for fields are determined by the indexable fields of the struct type.

For example:

```json
{
  "namespace": "example",
  "name": "persons",
  "kind": "array",
  "array": {
    "element": "example.person",
    "primary": ["id"],
    "indexes": [
      {
        "name": "byAge",
        "fields": ["age"],
        "isUnique": false
      }
    ]
  }
}
```

Here, `example.person` is the element type, `primary` specifies the primary key fields, and `indexes` declares indexes. If the element is a scalar or enum, the primary key and index configuration does not apply.

For details such as field selection for primary keys and indexes, and duplicate-field constraints, see the `Primary`, `DataIndex`, and their associated `Relation` declarations directly.

## 3. Array Usage Form

The array usage form `ArrayUsage` does not redeclare the element type, but adds constraints for a specific usage scenario on top of an existing array definition.

The most common are `MinSize` and `MaxSize`, which limit the minimum and maximum number of elements in the array respectively:

```csharp
[Meta<ForSchema>(SCHEMA_KIND_NODE_ARRAY, SCHEMA_KIND_ARRAY_USAGE)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_ARRAY}.{nameof(MinSize)}")]
[Meta<LowLimitInt>(0L)]
[Relation<UpLimitInt, Call>(
    nameof(MinSize),
    $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}",
    $"@{nameof(MaxSize)}")]
public class MinSize : Property<long>, IConstraintProperty;

[Meta<ForSchema>(SCHEMA_KIND_NODE_ARRAY, SCHEMA_KIND_ARRAY_USAGE)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_ARRAY}.{nameof(MaxSize)}")]
[Meta<LowLimitInt>(0L)]
public class MaxSize : Property<long>, IConstraintProperty;
```

Both properties are non-negative integers, and a relation constrains `MinSize` not to exceed `MaxSize`. Their validation target is the whole array, not a single element.

For example:

```json
{
  "kind": "array.usage",
  "array": {
    "minSize": 1,
    "maxSize": 100
  }
}
```

In addition, when an array appears as a property of `node`, `ArrayProperty` can derive the default name and display text from the element type. Therefore, when automatically generating a companion array, this information does not need to be configured again.


## 4. Runtime and Access Paths

`ArrayType` is responsible for loading the element type, handling array-related properties, and providing capabilities such as type compatibility, access paths, and primary key operations. The array itself does not need to know the concrete structure of its elements in advance; it only needs to be able to load the element type and create the corresponding nodes.

Arrays provide two special access paths:

| Path | Meaning |
|---|---|
| `$prev` | The part of the array before the current element |
| `$ele` | The current element |

`$ele` enters the current element; if the element type is a struct, its fields can be accessed further. `$prev` represents the part of the array before the current element, which can be used for relation computation that depends on preceding elements.

For example, in an enum definition, `Values.$prev` can be used to compute the default value of an entry, and a primary key field list can also use `Primary.$prev` to avoid selecting duplicates.

These paths provide the access structure and target value type through the array type, and the actual values are obtained by the array node. The relation mechanism therefore does not need built-in array-specific business rules.

## 5. Array Node and Validation

`ArrayNode` is the runtime value node of an array. Internally it maintains a list of elements and provides operations such as index access, assignment, iteration, comparison, cloning, and primary key filtering.

When assigning, it can accept existing value nodes, or create element nodes one by one from JSON or CLR collections. Array elements must conform to the declared element type; the indexer supports reading, replacing, and appending at the end, but does not allow skipping intermediate positions.

Array validation is organized by `ArrayValue`:

```csharp
public async Task<bool?> ValidateArrayAsync(
    SchemaContext context,
    ArrayNode node)
{
    ArrayType type = (node.Type as ArrayType)!;
    if (type.Element == null) return null;

    foreach (IConstraintProperty prop in type.Constraints)
    {
        foreach (IValueAccess element in node)
        {
            bool? result = await prop.ValidateAsync(context, element);
            if (result.HasValue)
                element.RecordConstraint(prop, result.Value);
        }
    }

    await type.ValidateWithRelationsAsync(context, node);
    return null;
}
```

Validation is divided into two parts:

- Applying the constraint properties on the array type to each element;
- Executing the relation validation declared by the array type.

`MinSize` and `MaxSize`, by contrast, directly check the number of elements in the array. Thus, array-level constraints, element constraints, and relation validation can be declared separately, and then organized and executed by the corresponding runtime logic.


## 6. Generating Companion Arrays from Structs

When `StructGenerator` processes a struct with a primary key or indexes, it can simultaneously generate a companion array whose element type is that struct.

For example, define a struct with a primary key:

```csharp
public class Person
{
    [Meta<PrimaryIndex>]
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
```

In addition to the `person` struct type, the corresponding array type can also be generated:

```json
{
  "namespace": "example",
  "name": "persons",
  "kind": "array",
  "array": {
    "element": "example.person",
    "primary": ["id"]
  }
}
```

The element type of the companion array points to the original struct, and the primary key and indexes come from relevant metadata on the struct's fields or type. The array name and display text can be generated automatically based on the element type.

This also explains why `PrimaryIndex` on a struct field is associated with the array prototype: it describes the primary key role of the field in a collection scenario, while the array is responsible for carrying the corresponding collection configuration and operation capabilities.


## 7. Summary

The array prototype builds an ordered collection around an element type, and extends it for different purposes through semantic properties:

- The **definition body** describes the element type;
- **Array properties** describe primary keys and indexes;
- The **usage form** provides scenario constraints such as minimum and maximum size;
- The **runtime** handles element access, value processing, validation, and primary key operations.

`$prev` and `$ele` provide access paths in the array context for relation computation, allowing relations to reference the current element, preceding elements, and their subfields.

An array does not need to understand the concrete business semantics of its elements. As long as the element type can be loaded and created, an array can contain scalars, enums, structs, or other arrays, thereby composing different semantic types through a unified mechanism.
