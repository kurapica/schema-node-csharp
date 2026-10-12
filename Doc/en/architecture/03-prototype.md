# 03. Semantic Prototypes: The Minimal Contract with the Execution Layer

In the previous chapter, we saw that a complete semantic entity is not a set of isolated fields.

It can form entities through structural composition, add semantic dimensions through Property, establish semantic associations through Relation, and then produce executable semantic effects through Function.

But if every entity had to redefine these semantics, the semantic space still could not be truly reused.

We need to define entity types first. Just as code uses `class` to define new types, the first problem we need to solve is how to define this `class` itself.

Therefore, a more fundamental concept is needed:

> **A Prototype is a semantic type definition constructed from semantic primitives, which can be reused and further extended.**

In SchemaNode, types do not exist from the very beginning.

**First there are prototypes, then types; and only with types can data use the semantics provided by those types.**

```text
                Prototype
                    │
          ┌─────────┼─────────┐
          ↓         ↓         ↓
       Property  Relation  Function
          │         │         │
          └─────────┼─────────┘
                    ↓
                   Type
                    │
          ┌─────────┼─────────┐
          ↓         ↓         ↓
       Data       Static     Dynamic
      structure   semantics  semantics
          │         │         │
          └─────────┼─────────┘
                    ↓
                   Data
```

Therefore, Property, Relation, and Function in SchemaNode are not configurations scattered across types and data, but semantic capabilities constructed and registered around prototypes.

Types provide data with default static semantics; data uses these semantics and, at runtime, produces dynamic semantics through Relations and Functions.

In traditional object-orientation, a Class is the prototype of an Object, and developers define the structure and behavior of objects through Classes.

In SchemaNode, however, the type system itself also needs to be abstracted. Therefore we introduce Prototype as the prototype of Type, forming a complete three-level structure:

```text
Prototype  —— Defines how a "type" is constructed (e.g. struct, scalar, function)
      ↓
Type       —— Concrete business semantic types constructed using prototypes (e.g. address, minMax)
      ↓
Data       —— Runtime instances carrying concrete business data and producing dynamic semantics
```

The class is not the ultimate form of the object — the prototype is the true starting point of the class. Prototype becomes the most stable, minimal contract between the semantic layer and the execution layer.

---

## 1. The Prototype as Entity Classification

The prototype is, first of all, a classification of the essence of entities.

For example, for integers, natural numbers, integers, years, months, and so on can all further form different semantic types. But no matter how much semantics is attached to these types, their data structure is essentially still an integer.

Therefore:

> **The prototype defines the Meta of a type.**

For example, the `struct` prototype prescribes that the Meta of a struct type is:

```jsonc
{
  "fields": {
    name: string,
    type: value_type,
  }[]
}
```

That is, if an entity belongs to the `struct` prototype, it has a way of describing its own structure through a field list.

The prototype not only determines how an entity is interpreted, but also determines how the Properties attached to the entity should be interpreted.

For example, a property named `uplimit`:

```text
uplimit = 10
```

For an integer type, it means the "maximum value"; for a string type, it may mean the "maximum length."

The property value alone cannot determine the complete semantics.

A property needs to declare which prototypes it serves, so that the execution layer can interpret it according to the prototype the entity belongs to.

```text
             Property
                 │
        Declares served prototypes
                 │
       ┌─────────┴─────────┐
       ↓                   ↓
      int                string
       │                   │
   uplimit=10          uplimit=10
       │                   │
   Max value          Max length
```

Therefore, it is allowed for a same-named Property to serve different Prototypes:

```text
int.uplimit
string.uplimit
```

Their names are the same, but the semantic interpretation is determined by the prototype they belong to.

Within the same Prototype, however, two same-named Properties are not allowed, because they could not form an unambiguous semantic definition.

To distinguish different prototypes across the entire entity space, each Prototype needs a unique identifier.

This identification is similar to type names in code — a unique string suffices:

```text
int
string
enum
struct
workflow
...
```

It only needs to uniquely identify its identity within the prototype space.

Therefore, what the Prototype first solves is:

> **What basic category an entity belongs to, and how this category should be interpreted.**

---

## 2. Prototypes Associate Semantic Properties

Once we know what prototype an entity belongs to, we still need to describe what semantics this prototype can have.

A Prototype can declare multiple Properties.

Only Properties already declared by the prototype can be used in the types it derives.

Therefore:

```text
Prototype
   │
   ├── Property A
   ├── Property B
   ├── Property C
   └── ...
          ↓
        Type
          ↓
        Data
```

Property thus becomes a semantic dimension an entity can possess.

This also means that Property is not a globally, infinitely open key-value table.

Same-named Properties serving different Prototypes are allowed, but within the same Prototype uniqueness must be maintained.

A Property can also serve multiple Prototypes simultaneously.

For example, the `Relations` property describes the association definitions stored on an entity; it does not belong to one specific data structure, and can serve multiple prototypes.

```text
Relations
   │
   ├── struct
   ├── array
   ├── function
   └── ...
```

For `function`, it can even be used to describe what semantic associations function arguments have when being used.

Therefore, from the perspective of "mounting properties," if a definition needs more composable semantic descriptions in addition to its own data structure, then that definition itself can be abstracted as a prototype.

For example, the Meta of `struct` is:

```json
{
  "fields": []
}
```

And the semantic description of one field can be:

```jsonc
{
  // Meta
  "name": "age",
  "type": "int",

  // Property
  "display": "Age",
  "lowlimit": 0,
  "uplimit": 200,
  "unit": "years"
}
```

Here, `field` is no longer just a name and a type.

On the basis of the `int` type, it carries additional Properties as its own semantic description.

Therefore, a struct field itself can also serve as a semantic mounting point:

```text
struct
  │
  └── field
       │
       ├── Meta
       └── Property
```

Thus we can define the `struct.field` prototype.

Function arguments are similar:

```text
function
  │
  └── arg
       │
       ├── Meta
       └── Property
```

Hence the `function.arg` prototype can be defined.

This approach is not for adding levels of abstraction, but because these structures themselves already have independent semantic dimensions and need to be able to mount Properties.

---

## 3. Prototypes Do Not Define the Relation Mechanism

The Prototype is responsible for defining the basic category of an entity and which semantic properties that category can mount.

It does not need to define a separate Relation mechanism for each Prototype.

If a type needs to establish associations, it only needs to have a Property such as `Relations`.

```text
Prototype
    │
    └── Relations
           │
           ↓
       Relation
           │
           ↓
      Entities in the semantic space
```

Therefore:

> **Relation is a behavior of the semantic space, not an internal mechanism of any specific Prototype.**

The prototype only needs to define:

> Entities of this type support the `Relations` property.

What entities these associations connect to, and how they produce effects, belongs to the semantic space itself.

This also explains why the same Relation mechanism can span entities of different types.

For example:

```text
struct
   │
   └── Relations
          ↓
       Relation
          ↓
function
   │
   └── Relations
          ↓
       Relation
```

What prototypes provide is a unified semantic carrying capability, rather than reinventing an association mechanism for each kind of entity.

---

## 4. Function, Relation, and Property Are Also Semantic Entities

In the preceding discussion, Property, Relation, and Function were treated as the basic primitives for constructing semantics.

But they themselves are not indescribable black boxes either.

Functions, associations, and properties are themselves semantic entities, and can have their own properties and types.

### Function

Functions can declare their own execution constraints.

For example:

* ServerOnly: only allowed to execute on the server;
* NoCache: clients should not cache this function's data;

Function arguments have a semantic structure similar to struct fields, and can likewise carry multiple descriptions:

* Require: the argument must be provided;
* Default: the argument has a default value;
* Variadic: the number of arguments can vary;
* Display: the display semantics of the function or argument.

Therefore, a Function is not:

function name + a piece of code

but an entity that can be fully described by the semantic system:

```text
Function
 ├── Meta
 ├── Property
 └── Arguments
       ├── Meta
       └── Property
```

This elevates a function's invocation constraints from implicit checks inside the function implementation to definitions that the semantic system can directly understand and process.

### Relation

The execution approach of an association is likewise not fixed.

SchemaNode Core provides several basic Relation execution types by default, for example:

* assign
    > Directly assigns the source value to the target property

* call
    > Executes the association through a Function

* any
   > Calls multiple Functions;
   > the association is satisfied if any one returns true

These are only the basic semantics provided by the system.

The execution layer or other semantic dialects can still extend the execution types of Relation according to their own needs.

Since these extensions are themselves accomplished through semantic definitions, they do not need to become special features in the semantic editor. As long as the semantics is defined, the semantic editor can present it according to the definition.

### Property

Property itself can also have Properties that describe its own behavior.

For example:

* Static 
   > Indicates that this Property's value is statically defined and must not be modified by a Relation at runtime.
   > When configuring a Relation, Properties of the target entity that are Static will not appear as selectable options.

Here an important recursion forms:

```text
Property
   │
   └── Property
          │
          └── Describes the semantic behavior of Property itself
```

Property is no longer just a simple key-value pair, but a semantic entity that can be described and processed by the semantic system itself.

Property also has explicit value types.

For example, a Display Property requires a string, a Static Property is a boolean, and the values of some Properties can themselves be structured semantics.

Among them, the function call is rather special.

A function call can itself act as a semantic value, becoming an important source of dynamic capability for semantic entities. Later chapters will show, through concrete semantic types, how it participates in Relations, Properties, and other semantic definitions.

Therefore, Function, Relation, and Property are not special mechanisms outside the semantic system.

They themselves also live in the same semantic space:

```text
      Semantic Entity
             │
 ┌───────────┼─────────┐
 ↓           ↓         ↓
Property  Relation   Function
 │           │         │
 └───────────┼─────────┘
             ↓
    Can be further described
```

The concrete forms of actual type definitions will be expanded in the later chapters on semantic type implementation.

---

## 5. From Prototype to Type

The Prototype itself is not the entity directly used by business data. It only defines the structure a semantic type has and the properties allowed to be mounted.

On this basis, concrete Types can be further defined.

```text
Prototype
    ↓
Type definition
    ↓
Type
    ↓
Data
```

For example, `int` can serve as the type base corresponding to a prototype.

On this basis, more specific types can continue to be defined:

```text
int
 │
 ├── natural
 ├───── lowlimit = 0
 |
 ├── year
 ├───── lowlimit = 1900
 |
 ├── month
 ├───── lowlimit = 1
 ├───── uplimit = 12
 └── ...
```

These types still keep the integer data structure, but can continue to add their own semantic Properties and Relations.

Therefore, the type system is not a pre-fixed list.

> **New types can continue to be constructed based on existing Prototypes.**

This is also the real significance of the Prototype's existence:

It separates "how to construct a type" from "the type itself," giving the type system the capability of continuous extension. The kernel allows third parties to define new prototypes and related properties without modifying the kernel itself.

---

## 6. Types and Data

Only with a Type does data have a semantic definition to attach to.

Types provide data with default structure and static semantics:

```text
Type
 │
 ├── Meta
 ├── Property
 ├── Relation
 └── Function
        │
        ↓
      Data
```

For example, a type can define:

```text
display = "Age"
lowlimit = 0
uplimit = 200
unit = "years"
```

After data uses this type, it does not need to redefine these semantics.

Therefore:

> **Types provide default semantics; data uses the type's semantics.**

At runtime, data can also produce dynamic semantics through Relations and Functions.

For example:

```text
Data A
   │
 Relation
   ↓
Data B.Property
```

Thus the two can be distinguished as:

```text
Type
 └── Defines static semantics

Data
 ├── Uses the static semantics provided by the type
 └── Produces dynamic semantics at runtime
```

This is also an important difference between "types" in SchemaNode and ordinary data-structure templates.

A type does not merely prescribe what the data should look like; it simultaneously prescribes what semantic capabilities the data can have.

---

## 7. The Prototype Is the Minimal Contract of the Execution Layer

Semantic types themselves do not need to be known in advance by the concrete execution layer.

What the execution layer really needs to know is:

> **What Prototype this type belongs to.**

Because the Prototype determines the two most fundamental questions:

```text
Prototype
    │
    ├── Meta
    │      ↓
    │   How to load and interpret the structure
    │
    └── Property
           ↓
       How to consume the semantics
```

For example, for the `struct` prototype, an execution layer can define a corresponding runtime type:

```text
struct
  ↓
StructType
```

The execution layer obtains the field declarations through `fields` in the type declaration.

And the fields themselves can belong to:

```text
struct.field
      ↓
StructFieldType
```

Thus a field can be loaded as a runtime object, and the Properties declared on the field further determine its execution behavior.

For example, in the frontend execution layer, this field can be displayed as an age input box with minimum and maximum limits, followed by a "years" unit suffix:

```text
field
 ├── type = int
 ├── lowlimit = 0
 ├── uplimit = 200
 ├── unit = 'years'
 └── display = 'Age'
```


The execution layer does not need to know what a specific business type is; it only needs to know:

```text
This is a struct type
This is a struct.field
This field has the lowlimit Property
```

Then it can consume all semantic types that belong to the Prototypes it supports.

```text
             Semantic Types
          ┌───────┼────────┐
          ↓       ↓        ↓
        Type A  Type B   Type C
          │       │        │
          └───────┼────────┘
                  ↓
              Prototype
                  ↓
          Execution Layer
```

Therefore, the Prototype becomes the **minimal and stable contract between the semantic layer and the execution layer**.

The semantic layer can keep extending new types based on Prototypes, while the execution layer only needs to implement the Prototypes it supports in order to consume this entire class of semantic types.

---

## 8. The Meta Declared by a Prototype Is Itself Structured Semantics

Whether it is a data type or a functional type like Function, their Meta is itself structured data.

For example, a struct type can describe its basic structure through:

```json
{
  "fields": []
}
```

But fields is not just an ordinary data array.

Each field within it has its own Meta, Property, and other semantic definitions, so it is actually a complete definition structure.

This means:

The Meta of a prototype is itself a kind of semantic type.

Usually, such structures can be described using the struct prototype.

Thus:

```text
Prototype
   │
   └── Meta
        │
        ↓
      struct
        │
        ├── fields
        ├── Property
        └── Relation
```

Here an important recursive relationship arises:

```text
Use Prototype to define Type
       ↓
The Meta of the Type is itself structured data
       ↓
This structured data can also be described by a Type
```

Therefore, SchemaNode's semantic editor does not need a completely special editing mechanism for "defining prototypes."

Prototype definitions are themselves structured semantics.

For the editor, there is no essential difference between them and filling in an ordinary structured entity:

```text
Prototype definition
    ↓
Structured data
    ↓
Edited according to the corresponding Type
```

This is also one of the foundations for SchemaNode's gradual realization of bootstrapping:

The language that describes types can itself become a described type.

---

## 9. From Prototype to Core Types

What the Prototype solves is:

> **How types are constructed.**

Which Prototypes and Types have already been provided by SchemaNode Core is another question.

Core provides a set of public Node types on top of the prototype mechanism, organizing them through namespaces and type registration.

These concrete types include different data structures and semantic carrying approaches.

The next chapter moves into this layer, introducing:

* The family of Node types provided by Core;
* How types are registered into namespaces;
* Concrete types such as `scalar`, `enum`, and `struct`;
* The complete definition structure of a concrete type;
* And how these types are ultimately used by data.

Therefore, this chapter leaves behind the **construction mechanism of the type system**, and the next chapter begins to introduce **the concrete type system SchemaNode already provides**.

```text
Prototype
    ↓
The capability to define types
    ↓
Core Type System
    ↓
Concrete Type
    ↓
Data
```

**Prototype makes types semantic definitions that can continue to be constructed and extended, rather than a pre-fixed collection of names.**


## Conclusion

The Prototype solves the problem of "how semantic types are defined and reused," but it does not prescribe a specific language implementation.

The next question to solve is: if we truly want to construct such an executable semantic language, which Prototypes, Properties, Relations, and Functions should become the public semantic foundation? SchemaNode provides one concrete implementation for this.
