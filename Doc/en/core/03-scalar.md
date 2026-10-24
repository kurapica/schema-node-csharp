# 03. Scalar Prototypes

The `Schema/Scalar/` directory defines the prototypes related to scalar types, including `node.bool` (boolean), `node.int` (integer), `node.decimal` (decimal), `node.string` (string), and `node.date` (date).

These prototypes are used to define the semantic structure of basic data types.

Their definition structure is the same as `IntSchema` introduced in the previous chapter, and all support specifying a base type through the `base` field. For example, the base type of `system.year` is `system.int`. In this way, semantic types can reuse the semantic properties of their base types along the inheritance chain.

The previous chapter already explained how the `int` classification is mounted onto the `node` prototype, so this chapter will not repeat that process. Instead, it will further introduce the declaration of public scalar semantic types, and how they build type constraints through semantic properties.

## Declaration of System Semantic Types

The public scalar semantic types defined by the system can be found in the `Scalar` directory. Below, take the integer type `Int` as an example:

```csharp
/// <summary>
/// Represents the int scalar value type (root of the int type family)
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_INT)]
[Meta<ClrEquivalent>(typeof(int))]
[Meta<ClrEquivalent>(typeof(short))]
[Meta<ClrEquivalent>(typeof(sbyte))]
[Meta<ClrEquivalent>(typeof(byte))]
[Meta<ClrEquivalent>(typeof(ushort))]
[Meta<ClrEquivalent>(typeof(uint))]
[Meta<ClrEquivalent>(typeof(ulong))]
[Meta<ClrEquivalent>(typeof(Int64))]
[Meta<ClrEquivalent>(typeof(UInt16))]
[Meta<OfNodeKind>(NODE_KIND_INT)]
public class Int : Number, IScalarType<long>;
```

Here, `OfNodeKind` declares `Int` as a semantic type under the `node.int` classification. The multiple `ClrEquivalent` metadata declare the C# types corresponding to `system.int`. When semantic types are registered through reflection, these CLR types are mapped to `system.int`, so that multiple integer types in the host language can be expressed using a unified semantic type. The `ClrEquivalent` attribute is internal to the C# execution layer and is not exposed externally.

`IScalarType<long>` indicates that the scalar processing type corresponding to `Int` is C#'s `long`. When processing data of this semantic type, the execution layer can perform the corresponding type conversion and processing based on this convention.

In this way, a clear mapping is established between type declarations in the semantic layer and type processing in the execution layer. When declaring types and functions in C#, developers do not need to repeatedly describe the correspondence between these types in every piece of code.

## String Type and Namespace Name Type

Among the scalar prototypes, the declaration of the string type is relatively simple:

```csharp
[Meta<SchemaType>(NS_SYSTEM_STRING)]
[Meta<OfNodeKind>(NODE_KIND_STRING)]
public class String : IScalarType<string>;
```

It declares `system.string` as a public semantic type under the `node.string` classification, and specifies its corresponding CLR scalar type as `string`.

Strings are used not only to represent text, but also frequently to reference other semantic types. For example, the `base` field in `IntSchema` is used to specify the base type of an integer type:

```csharp
[Meta<SchemaType>(typeof(IntType))]
public override string? Base { get; set; }
```

Although this field is declared as `string?` in C#, semantically it uses `IntType`, not the ordinary string type. This is because what the field needs to express is not arbitrary text, but a reference to a specific semantic type.

Looking further at `IntType` and its base classes, we can see the following inheritance chain:

```csharp
// Int type
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_INT}.type")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_NODE_KIND, NODE_SELF, false, NODE_KIND_INT)]
public class IntType : ValueType;

// Value type
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_NODE}.valuetype")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_VALUE_KIND, NODE_SELF)]
public class ValueType : AnyType;

// Any type
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_NODE}.type")]
[Meta<UpLimitString>(PRIMARY_KEY_MAX_LEN)]
[Meta<EntrySource>($"{NS_SYSTEM_SCHEMA_REFLECT_TYPE}.{nameof(Function.Reflect.Type.gettypeentries)}", NODE_SELF)]
public class AnyType : String;
```

This inheritance chain shows how type definitions are extended layer by layer through semantic properties.

`AnyType` represents any semantic type. It provides a data source for cascading selection through `EntrySource`, enabling the definition domain to select semantic types from the system, while inheriting the basic semantics of `String`.

`ValueType` adds a `Valid` constraint on top of `AnyType` to verify whether the selected semantic type belongs to the `node.value` classification. It inherits the selection capability of `AnyType`, without needing to redeclare it.

`IntType` further adds a `Valid` constraint, narrowing the selectable range to the `node.int` classification.

It can thus be seen that the definition of a semantic type does not have to concentrate all properties and constraints in a single type, but can combine them layer by layer through inheritance: the base type provides general capabilities, and derived types add more specific constraints.

**Scalar semantic types establish a unified convention from type declaration to execution processing precisely through the combination of prototypes, type mapping, inheritance, and semantic properties.**

However, types such as `AnyType` are definition-domain semantic types, not public semantic types. The explanation here is a guide to scalar semantic types.

Because scalar types are very simple, they will not be expanded further here.
