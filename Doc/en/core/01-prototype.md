# 01. Semantic Prototypes and the Definition Domain

Before starting this series, it is recommended to first read the [architecture chapters](../architecture/01-why.md).

This chapter starts from practice, introducing semantic entities, semantic types, and semantic prototypes in turn, and then further explains the relationship between the definition domain and the public semantic space, laying the foundation for the next chapter's introduction of SchemaNode.Core's `node` prototype family.



## Semantic Entity

In the architecture documentation, we define a semantic entity as: **the whole formed by structured data together with its related semantic properties.**

A semantic entity can be passed between different execution layers, but the implementation form in each execution layer may differ. When exchanging across execution layers, it is possible to pass only the structured data, and then each execution layer restores and processes the complete semantics by combining the corresponding semantic type and the semantic properties it supports.

For example, a semantic entity representing age can take the following form in a frontend execution layer:

```json
{
    "value": 55,
    "properties": {
        "display": "Age",
        "unit": "years",
        "type": "number",
        "lowlimit": 0,
        "uplimit": 100,
        "expectation_life": 80
    }
}
```

Here, **the structured data carries the value, while the semantic definition gives the value its interpretable meaning.**

The semantic properties here have different sources.

`display`, `unit`, `type`, `lowlimit`, and `uplimit` describe the common characteristics of the semantic type "age". For entities of the same semantic type, these properties usually share the same definition, so they can be provided by the semantic type; we call them **static properties**.

`expectation_life` (life expectancy), by contrast, is different. It cannot be determined merely from the type of the age field; it may also depend on contextual information such as gender, living environment, and lifestyle habits. Such properties need to be computed by relating other semantic information, so we call them **dynamic properties**.

Note that static and dynamic describe the source of a property and how it is determined, not a fixed classification of the property itself.

Between execution layers, only the data `55` is passed; the remaining properties are restored based on context, that is, the semantic type.

---

## Semantic Type

A semantic type is an abstraction of semantic entities of the same kind. It not only describes the data structure of the entity, but also defines the static semantic properties applicable to that type, as well as the relations needed to compute dynamic properties.

In terms of composition, a semantic type needs to provide three aspects of capability:

- **Structure (Meta)**: describes the data structure of the entity and the types of its constituent parts.
- **Static Properties (Property)**: describe the common semantic characteristics that the entity or its constituent parts possess.
- **Relation**: describes the dependency relations between pieces of semantic information, and how to compute or derive the corresponding semantic properties based on those relations.

Together these three aspects form the foundation of a semantic type.

Consider a concrete example: age is a constituent part of a person, and life expectancy can only be determined by combining information such as the person's gender. Therefore, the relation definition for life expectancy does not necessarily belong to the type of age itself; it can be defined in the semantic type of the person, with age specified as the target of the related property.

In other words, **a semantic property can be attached to a local entity, while the relation that determines that property can be defined in a larger entity.** A relation can thereby make use of the contextual information provided by an upper-level entity.

For different execution layers to exchange and interpret semantic types, we need a unified format for declaring semantic types.

For example, the semantic type of a person can be described with the following declaration:

```jsonc
{
    "type": "struct",
    "display": "Person",
    "fields": [
        {
            "name": "name",
            "type": "string"
        },
        {
            "name": "gender",
            "type": "string",
            "whitelist": [
                "Male",
                "Female"
            ]
        },
        {
            "name": "age",
            "type": "number",
            "display": "Age",
            "unit": "years",
            "lowlimit": 0,
            "uplimit": 100
        }
    ],
    "relations": [
        {
            // Provides the life expectancy property for age; in this example it is computed only from gender
            "target": "age",
            "property": "expectation_life",
            "calc": "getexpectlifefromgender",
            "args": [
                {
                    "source": "gender"
                }
            ]
        }
    ]
}
```

Here, `struct` declares the structure of this semantic type, the field declarations provide the type and properties of each field, and `relations` describes how to associate the life expectancy property with age.

The specific property names, argument formats, and relation declaration rules depend on the semantic definition system being used. The example above illustrates the basic composition of a semantic type, not a complete declaration specification.

As long as an execution layer can recognize and interpret this declaration format and supports the semantic capabilities involved, it can process the corresponding entities according to the same semantic definition, without having to write dedicated processing logic for each concrete semantic type.

This raises the next question: how does an execution layer recognize the structure of a semantic type, determine which semantic properties it supports, and parse and consume these declarations in the corresponding way?

This is precisely the problem that semantic prototypes need to solve.

---

## Semantic Prototype

If an execution layer were required to implement processing logic separately for every semantic type, then the more semantic types there are, the more dedicated code the execution layer would need to maintain.

Especially in cross-system interaction, an execution layer frequently encounters semantic types it did not foresee when it was developed. If every new type required modifying the execution layer, the separation of the semantic layer and the execution layer would lose its meaning.

Therefore, we need to abstract semantic types further, and, according to their structure and semantic characteristics, reduce them to a finite number of recognizable categories.

For example, types such as `enum`, `struct`, and `scalar` can describe enumerations, structured data, and scalar data respectively. An execution layer does not need to know every concrete type in advance; it only needs to understand the semantic prototype each type belongs to, and the semantic capabilities that the corresponding prototype supports.

We call this layer of abstraction a **semantic prototype (Prototype)**.

The number of semantic prototypes is far smaller than the number of concrete semantic types. Taking SchemaNode.Core as an example, there are currently 32 prototype definitions in total; excluding prototypes such as `node.string.usage` that are used to attach to or extend other definitions, there are about 17 core prototypes.

For an execution layer, this is a relatively limited set of contracts that it can implement and maintain independently. This means that even if a business system later expands to thousands of semantic types, the complexity of the underlying logic the execution layer must maintain remains on the order of a few dozen prototypes.

The core role of a semantic prototype is to connect semantic declarations with the execution layer's processing logic:

1. A semantic type declares its own structure and semantic characteristics based on a prototype.
2. The execution layer recognizes the prototype and parses the semantic type according to the convention.
3. The execution layer interprets and consumes the corresponding semantic entity according to the capabilities it possesses.

Therefore, a semantic prototype is not a collection of concrete business logic, but the basis on which the execution layer understands semantic types. If we assign essential complexity to the semantic layer and accidental complexity to the execution layer, then the prototype is the minimal coupling between the two.

For example, given a semantic property `visible`, if the execution layer cannot understand and process it, then it will inevitably be ignored, because it is hard for us to introduce a new concept to describe that `visible` means visibility. Therefore, a property that can be executed by the execution layer must be one it can recognize based on the prototype.

Note that **the prototype name itself is not the key to the cross-execution-layer contract.** A name is merely one way for an execution layer to recognize a prototype, and different execution layers may use different internal names or implementation forms. What truly needs to remain compatible is the structure and semantic conventions the prototype describes, not that all execution layers adopt the same prototype names or code implementations.

The `node` prototype family provided by SchemaNode.Core could, in theory, be replaced, as long as it covers all public semantic prototypes, without affecting the parsing and execution of existing semantic types. So the design avoids the risk of such a god execution layer existing, even though it is the current core implementation.

A semantic prototype itself also needs to be defined. It must describe both the type structures it supports and the semantic properties these types can use together with their constraints. At the same time, a semantic property can declare the semantic prototypes it applies to, so that a clear constraint relationship is formed between types and properties.

There is another important characteristic here: **the definition of a semantic prototype can itself be expressed using structured semantic types.**

This means we do not need to design a completely independent definition system for semantic prototypes, nor hard-code a special metalanguage in the code. A prototype can be described by the semantic types it supports, and those semantic types can in turn be constructed based on prototypes, thus forming a bootstrapping structure.

The next chapter will specifically introduce how SchemaNode.Core implements this mechanism through the `node` prototype family.

---

## The Definition Domain

The definition domain concerns a special problem space: **how semantic types themselves should be defined.**

The semantic types introduced earlier are mainly used to describe business entities, whereas the definition domain is responsible for describing the declaration rules of semantic types, including which prototypes can be used, which properties can be attached, and how the corresponding structures and relations are organized.

**The definition domain concerns the declaration and construction of semantics, not the concrete execution of business semantics.**

This can be observed when trying to define a semantic type in [SchemaNode Man](https://kurapica.github.io/schema-node-man/#/app): the editing interface presents the corresponding fields, options, show/hide rules, and other constraints according to the currently selected semantic prototype.

These interface behaviors are not independent forms written in advance for every semantic type; they are jointly determined by the semantic prototype and its related properties. In essence it merely consumes the `node` prototype, while the remaining prototypes are implemented as extension properties of `node`.

This is similar to writing a program in an IDE. The programming language determines which structures can be declared, which syntax can be used, and how hints are provided when errors occur; the definition domain determines how a semantic type can be declared, and whether the declaration conforms to the corresponding semantic rules.

The difference between the two is that the object the definition domain deals with is the declaration of a semantic type, not the business data the semantic type describes.

Therefore, the definition domain is usually handled by an execution layer capable of generating semantic type declarations. For example, semantic types can be generated through a dedicated editor, a configuration tool, or even program code. They need not adopt the same implementation approach; as long as the semantic declarations they ultimately produce satisfy the same compatibility conventions, they can be consumed by other execution layers.

Note also that **the types used in the definition domain to describe semantic types are not necessarily the same set of types as those used when interacting with business data.**

The former is used to construct and constrain semantic declarations; the latter is used to describe and process business entities. The two can share a common semantic foundation, but they bear different responsibilities.

---

## The Public Semantic Space

The public semantic space is a concept that takes shape gradually. The problem it aims to solve is: when different execution layers need to exchange structured data and their semantic definitions, how can a commonly followed mechanism be provided so that each execution layer can understand and consume the semantics declared by the others?

In this process, two kinds of entities of different natures need to be distinguished.

One kind is universal data-type entities, such as order details or workflow execution states. These entities are mainly used to express business data and its semantics, and their structure and semantic definitions can be understood independently of a specific execution environment. Their Meta, Property, and the semantic relations associated with them can become a foundation commonly followed by different execution layers.

The other kind is functional entities within a dialect, such as orders, workflows, and applications themselves. These entities not only carry data, but are also associated with a specific business domain, organizational scope, or execution mechanism. Functions such as the transitions of an order state, the way a workflow executes, and the data storage mechanism of an application usually need to be interpreted and executed by an execution layer in combination with the corresponding Meta, Property, and other configurations. The semantics of these entities often depends on domain-specific conventions or execution capabilities, and cannot obtain a complete, universal interpretation from generic metadata alone.

The difference between the two kinds of entities is not whether they contain Meta and Property, but whether their semantics can be commonly understood by different execution layers independently of a specific dialect and execution mechanism. The public semantic space mainly concerns the former kind of entity and its semantic definition; the latter kind is defined more by dialects, with the corresponding execution layers providing concrete capabilities.

This does not mean the two kinds of entities must be isolated from each other. A dialect can perfectly well construct new semantic types based on public semantic prototypes, and further define domain-specific properties, relations, and functions. Public semantics provides a shareable foundation, and a dialect extends domain-specific semantics and execution capabilities on top of it.

This also leads to the core mechanism of the public semantic space: **General Semantic Prototypes**.

This does not require all execution layers to use the same technology stack, internal model, or execution logic, but it does require them to form a consistent understanding of the shared semantic foundation.

As long as different execution layers can understand the public semantic prototypes, they can parse the semantic types constructed based on those prototypes, without having to develop adaptation logic for each concrete type separately. The SchemaNode.Core `node` prototype family introduced in the next chapter is precisely a concrete implementation of this mechanism.

Besides public prototypes, the public semantic space also provides commonly used types and system-level semantic atomic functions for covering data conversion processing.

Based on public prototypes and commonly used semantic types, the data processing domain can be covered, which also ensures compatibility when different execution layers exchange data.

It should be emphasized that the public semantic space does not mean that every execution layer must implement all capabilities. An execution layer only needs to handle the semantics within its own scope of responsibility that it can interpret and consume. As long as the shared part follows a consistent convention, other semantics can be handled independently by the corresponding execution layers.

---

## Semantic Dialects

In the discussion above, the semantic prototypes that cannot be detached from an execution layer are some of the core concepts in that domain, and they also correspond to the **semantic dialect** of that domain.

A prototype only strips away the variable parts of these concepts, while the execution part is implemented by the execution layer.

This is a paradigm that requires a relatively large upfront investment but can gradually lower the subsequent construction cost as semantics accumulate.

A prototype family formed for a specific problem domain, together with its supporting semantic conventions, can constitute a set of **semantic dialects (Dialect)**.

Therefore, the public semantic space and semantic dialects bear different responsibilities:

- **The public semantic space** provides the shared basic semantic conventions across execution layers.
- **A semantic dialect** further describes the semantic structure and capabilities of a specific problem domain on top of the public foundation.
- **The execution layer** implements the processing capabilities agreed by the public semantics and the relevant dialects according to its own responsibilities.

Public semantics provides a common foundation, while semantic dialects allow different domains to continue extending. Working together, the two enable the semantic system to collaborate across execution layers without having to forcibly converge the semantics of all domains into one fixed standard.

Different organizations can construct different semantic dialects for the same problem domain. They need not adopt the same domain model or execution approach, but they can exchange compatible data and semantic definitions through the public semantic space. Of course, if developed in a community form, a dialect for a domain that is defined in a unified way can build a domain public semantic layer on top of the public semantics, forming a multi-level semantic space system and accelerating semantic exchange within the domain.

The next chapter will start with the `node` prototype family of SchemaNode.Core, and specifically explain how public semantic prototypes are defined, and how semantic types achieve bootstrapping through these prototypes.
