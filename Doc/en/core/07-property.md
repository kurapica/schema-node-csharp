# 07. Property Prototype

The property prototype describes which additional semantics a semantic type can carry, and how these semantics are stored, merged, and consumed at runtime.

In SchemaNode, a property is both a declaration in the Schema and an `IProperty` instance at runtime. Different intents such as constraints, default values, visibility, and function calls can all be expressed through properties, and then handled by the corresponding execution logic.

The implementation related to properties is mainly concentrated in the following locations:

- `Schema/PropertySchema.cs`: the property prototype and its definition body.
- `Property/Property.cs`: `IProperty` and `Property<T>`.
- `Property/PropertyOwner.cs`: access to and merging of property collections.
- `Generator/PropertyGenerator.cs`: generating property Schema from C# property classes.
- Other files under `Property/`: concrete implementations of constraints, type references, function calls, ordering, and so on.

## 1. Prototype Declaration and Definition Body

The property prototype is declared by `PropertySchema`:

```csharp
[Meta<SchemaKind>(SCHEMA_KIND_NODE_PROPERTY, SCHEMA_KIND_ORDER_PROP)]
[Meta<NodeKind>(NODE_KIND_PROPERTY, SCHEMA_KIND_ORDER_PROP)]
[Meta<SchemaGenerator>(typeof(PropertyGenerator))]
[Meta<NodeType>(typeof(Runtime.PropertyType))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP}.schema")]
[Meta<Attach>(SCHEMA_KIND_NODE_PROPERTY)]
[Meta<Append>(typeof(Relations))]
public class PropertySchema : PropertyOwner
{
    [Meta<UpLimitString>(PRIMARY_KEY_MAX_LEN)]
    public string Property { get; set; } = string.Empty;

    [Meta<SchemaType>(typeof(ValueType))]
    public string Type { get; set; } = string.Empty;

    [Meta<SchemaType>($"{NS_SYSTEM_SCHEMA}.kind")]
    public string[] ForSchemas { get; set; } = [];
}
```

This declaration simultaneously determines the property prototype's classification, generation approach, runtime type, and definition body structure. Among them, `Property`, `Type`, and `ForSchemas` are the three fields of the definition body:

- `Property`: the name the property uses in the Schema.
- `Type`: the semantic type corresponding to the property value.
- `ForSchemas`: the Schema kinds to which the property applies.

For example, `require` is a property applicable to struct fields, with a boolean value type:

```json
{
  "namespace": "system.schema.prop.common",
  "name": "require",
  "kind": "property",
  "property": {
    "property": "require",
    "type": "system.bool",
    "forSchemas": ["node.struct.field"]
  }
}
```

The outer layer describes the property type itself, while the inner `property` is the definition body of the property prototype. The inner key name here is specified by the `Alias` of `PropertyProperty`.

The `Type` in a property definition is constrained by `PropertyType`, and can only reference types under the `node.property` classification:

```csharp
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP}.type")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_NODE_KIND,
    NODE_SELF, false, NODE_KIND_PROPERTY)]
public class PropertyType : AnyType;
```

Therefore, a property is itself a semantic type, but its definition target is not an ordinary data structure; it describes what properties other semantic types can carry.

> Note that when transmitting between execution layers, we only transmit values based on property names, such as `{ require: true }`, and do not use full paths such as `system.schema.prop.common.require`. This is because property types are tied to prototypes. When the system parses `require`, it looks up the prototype of the current entity, finds the corresponding property definition, then loads and consumes the property. If the current execution layer does not have the corresponding property, it is ignored by default.

> Different execution layers can actually interpret it using different compatible prototypes and other property types. Of course, they can also simply stay consistent, which is easier to maintain.


## 2. Property Values and Storage

Runtime properties are accessed uniformly through `IProperty`, and common implementations inherit from `Property<T>`:

```csharp
public abstract class Property<T> : IProperty
{
    public T? Value { get; private set; }

    public virtual void SetValue<TValue>(TValue value)
        => Value = value.ConvertTo<T>();

    public virtual TV? GetValue<TV>(bool matchType = false)
        => HasValue && (!matchType || Value is TV)
            ? Value.ConvertTo<TV>()
            : default(TV?);

    public virtual bool Combine(
        IProperty other,
        ISchemaRuntime? runtime = null)
    {
        if (other.GetType() != GetType()) return false;
        if (HasValue || !other.HasValue) return false;

        SetValue(other.GetValue<object>());
        return true;
    }

    public virtual bool HasValue => !SystemLogic.isempty(Value);

    public Type Type => typeof(T);
}
```

This base class provides uniform carrying, type conversion, and default merging behavior for property values. Specific properties can override these operations to satisfy their own merging rules.

Property names are also determined by metadata: `Alias` is preferred; if it is not declared, the `Property` suffix is removed from the property class name and the result is converted to camelCase. For example, `LowLimitProperty` corresponds to `lowLimit`.

### Property Collections

The definition body types of all prototypes must inherit from `PropertyOwner`. For example, `NodeSchema`, `IntSchema`, and so on; only then can properties be stored normally, because we cannot actually dynamically define properties in C#.

`PropertyOwner` provides unified collection operations for objects that can carry properties. Property values are stored in `Extensions`:

```csharp
[SchemaIgnore]
[JsonExtensionData]
public JsonObject? Extensions { get; internal set; }
```

This approach serializes extension properties directly into the JSON of the owning object, without having to modify the object structure for every property.

Whether a property can be stacked is determined by `Stackable`. Ordinary properties usually store a single value; stackable properties can store multiple values, for example, a field can declare multiple `valid` validations:

```json
{
  "require": true,
  "valid": [
    ["system.logic.notempty"],
    ["example.checkAge", "@age", 18]
  ]
}
```

When reading, `GetProperty` returns the single value of the specified property; for stackable properties, it returns only the first value. `GetProperties` is used to obtain all values, suitable for scenarios where multiple constraints need to be executed.

The main operations of a property collection are as follows:

| Operation | Purpose |
| --- | --- |
| `SetProperty` | Writes a property value, handling stackable properties |
| `GetProperty<T>` | Gets the specified property |
| `GetProperties<T>` | Gets all values of the specified property |
| `CombineProperties` | Merges the properties of another object according to property rules |

When merging, stackable properties append non-duplicate values; non-stackable properties call the property's own `Combine`, and the concrete implementation decides how to handle existing values. After specifying a runtime and Schema kind, merging also refers to the property set supported by that kind.

This means the storage and merging mechanism of properties does not depend on the business meaning of each concrete property.


## 3. Implementation Approaches for Properties

The value type and behavior of a property are determined by the concrete property class. Several common implementation approaches can be seen in the `Property/` directory.

### 3.1 Constraint Properties

`IConstraintProperty` provides a unified validation entry for properties, and dispatches to the corresponding validation logic according to the target node type.

For example:

- `Require` checks whether a node is empty.
- `Valid` executes custom validation through function calls.
- `MinSize` and `MaxSize` check array length.
- `Primary` and `Indexes` provide related constraints for primary keys and indexes in arrays.
- `StructValue` and `ArrayValue` aggregate the validation results of structs and arrays.

Validation results use a three-state representation:

| Return value | Meaning |
| --- | --- |
| `true` | Validation passed |
| `false` | Validation failed |
| `null` | The current validation does not apply to this node |

Here it is necessary to distinguish between semantic declaration and execution logic: properties express the constraints to be satisfied, while the concrete node and validation implementation are responsible for consuming these constraints.

### 3.2 Loading and Type References

Some properties need to resolve references or access runtime context during the type loading stage. Such properties implement `ILoadableProperty`:

```csharp
public interface ILoadableProperty
{
    Task LoadAsync(
        SchemaContext context,
        Runtime.ValueType? ownerType = null);
}
```

Type reference properties expose the referenced semantic type through `ITypeRefProperty`, allowing the runtime to recognize loading dependencies. This is used when a type is requested, or for an application and the like, so that all non-system-level semantic types can be sent in one package to avoid repeated requests.


### 3.3 Function Call Properties

`FuncCallProperty` implements `ITypeRefProperty` at the same time, used to carry function calls and their arguments, and to provide reference information for the function type.

At the same time, to accommodate the limitations of C#'s Attribute system, it provides a simplified usage form. For example:

```C#
[Meta<EntrySourceProvider>($"{NS_SYSTEM_SCHEMA_REFLECT_FUNC}.getaccessentries", $"@{nameof(Args)}", $"@{nameof(Exps)}.{ARRAY_PREVIOUS}", NODE_SELF)]
```

This property is used to configure a cascading data source, that is, a function call. Its function is `$"{NS_SYSTEM_SCHEMA_REFLECT_FUNC}.getaccessentries"`, and the rest are arguments. If an argument starts with @, it represents an access path; otherwise it represents a literal value. After configuration it looks like:

```json
{
    "func": "system.schema.reflect.func.getaccessentries",
    "args": [
        { "source": "args" },
        { "source": "exps.$prev" },
        { "source": "$self" }
    ]
}
```

The actual meaning here is: when defining the expression of a function, all entries accessible when configuring the arguments of this expression, including all function arguments and all expressions before this expression. `$self` means the current input value, so cascading selection can be formed, for example selecting deep access paths such as `input.data.age`.


### 3.4 Recording and Ordering Properties

`OrderProperty<T>` provides ordering information for property values; `RecordProperty<T>` records property declarations at assignment time.

Such mechanisms are used to collect information declared in C#, such as classifications, enum values, and primary keys, for generators to read uniformly. They mainly serve the declaration and generation stages, and differ from ordinary runtime properties in carrying values.


## 4. Meta-Properties of Properties

Properties can themselves carry properties. These meta-properties determine how properties are named, which Schema kinds they apply to, and how they stack, merge, and inherit.

| Meta-property | Purpose |
| --- | --- |
| `ForSchema` | Declares the Schema kinds to which the property applies |
| `Append` | Declares the properties supported by a prototype |
| `Alias` | Specifies the name the property uses in the Schema |
| `Static` | Marks that the property value cannot be modified by the relation system |
| `Stackable` | Allows the same property to appear multiple times on a node |
| `NoInherit` | Prevents the property from being inherited along the access chain |
| `PropertyValueType` | Specifies the semantic type of the property value |

Among them, `Append` and `ForSchema` describe the applicability relationship between properties and prototypes from two directions:

- `Append` is declared on a prototype, describing which properties the prototype supports.
- `ForSchema` is declared on a property, describing which Schema kinds the property applies to.

For example:

```csharp
[Meta<ForSchema>(SCHEMA_KIND_NODE_STRUCT_FIELD)]
public class Unpack : Property<bool>;
```

This indicates that `Unpack` applies to struct fields. At runtime, properties applicable to a specific Schema kind can be filtered based on this.

In a property collection, `Stackable` determines whether multiple values are allowed to coexist; for non-stackable properties, the property's own `Combine` decides how to merge. `NoInherit` controls whether a property can be inherited along the property access chain, while `Static` marks property values that cannot be modified by the relation system.

These meta-properties make the property mechanism itself extensible. When adding a new property, its name, applicable scope, and merging behavior can be declared through metadata, without having to modify the collection management logic separately for each property.


## 5. Summary

The property prototype organizes additional semantics into several cooperating parts:

- The **definition body** describes the property name, value type, and applicable kinds.
- **`IProperty` and `Property<T>`** provide unified carrying and basic operations for property values.
- **Meta-properties** describe the property's own naming, applicable scope, stacking, merging, and inheritance rules.
- **`PropertyOwner`** manages the access, serialization, and merging of property collections.
- **Runtime and generators** are responsible for loading property definitions and generating Schema from C# declarations, respectively.

The property mechanism organizes the declaration of additional semantics separately from concrete execution logic. New properties can reuse the existing storage, serialization, and collection management mechanisms, and then express new semantics through their own types, metadata, and corresponding execution logic, without modifying the entire property system.
