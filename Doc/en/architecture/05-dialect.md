# 05. Semantic Dialects

The previous chapter introduced the **definition domain** and **public semantic consensus**.

The definition domain solves how to define, compose, and process semantics; public semantic consensus stipulates the basic semantic capabilities that different execution layers must commonly understand, including shared prototypes, types, properties, and functions.

But public semantic consensus does not mean that all execution capabilities must be unified.

For example, data types, properties, relations, and functions belong to rather fundamental public semantics, while application containers, permissions, workflows, ETL, event handling, and so on are often tied to the specific execution environment.

If these capabilities ultimately fall back into code, then the coupling between semantics and execution code still exists.

Therefore, on top of public semantic consensus, a layer is still needed that can carry specific execution capabilities:

> Extract a class of stable execution capabilities and their variable structures from code implementations into semantic prototype families that are describable, composable, and executable.

This is the **Semantic Dialect**.

---

## 1. Definition of the Semantic Dialect

A semantic dialect is built on top of public semantic consensus, and, according to specific execution needs, further defines a set of semantic prototype families with clear execution meaning.

Special attention is needed here: "dialects" are not divided by industry domain.

Healthcare, finance, agriculture, and manufacturing are not dialects — they are just different combinations of business semantics. A true dialect corresponds to the abstraction of execution capabilities — for example, the App dialect provides the capabilities of the execution environment itself: Application, Field, Workflow, Event, Permission.

Therefore, the semantic dialect is closer to:

```text
Public semantic consensus
    ↓
Execution capability abstraction
    ↓
Semantic prototype family
    ↓
Concrete business semantics
```

It elevates part of the capabilities originally hidden in program frameworks, infrastructure, and execution code into objects that can be described by semantics.

A business domain is just one concrete combination of these semantic capabilities. Therefore, the same App dialect can be used for a carbon-sink system, a hospital management system, a manufacturing management system, or a financial system — because it defines execution capabilities, not business knowledge.

> Business domains describe "what to process"; semantic dialects describe "what execution capabilities to rely on for processing."

---

## 2. From Public Semantics to Execution Capabilities

Public semantic consensus provides the minimal semantic foundation that different execution layers can all understand.

For example:

```text
node
├── scalar
├── struct
├── array
├── function
└── ...
```

and:

```text
Property
Relation
Function
```

These semantics can be jointly interpreted by different execution layers.

But these capabilities alone are not enough to build a complete application execution environment.

For example:

```text
A user can log in to an application
An application contains multiple data tables
Data tables can configure row-level permissions
User operations can trigger workflows
Workflows can call functions
Events can trigger data processing
```

These semantics cannot be expressed by struct, array, or function themselves.

If traditional program design is used, these capabilities usually end up in framework code:

```text
ApplicationService
PermissionService
WorkflowService
EventHandler
...
```

Consequently, business semantics ultimately needs to invoke these services through code again.

What the semantic dialect does is separate the stable execution capabilities from the variable parts.

```text
Traditional execution framework

Code
 ├── Application
 ├── Permission
 ├── Workflow
 ├── Event
 └── ...
```

is transformed into:

```text
Semantic dialect

Public semantic consensus
    ↓
App dialect
 ├── Application
 ├── Field
 ├── Workflow
 ├── Event
 ├── Permission
 └── ...
    ↓
Business semantics
```

Where prototypes prescribe the semantic structures and execution capabilities, and the concrete business only needs to declare how these capabilities are combined.

This means:

The execution capabilities themselves are still provided by the execution layer, but the way of using execution capabilities no longer has to be expressed through code.

---

## 3. The SchemaNode App Semantic Dialect

SchemaNode Core provides the public semantic definition domain, while SchemaNode App is a concrete semantic dialect built on top of it.

For example, the App dialect provides the following semantic prototypes:

### 3.1 App

App is the container in the application execution environment.

According to configuration, it can determine: application boundaries, tenant isolation, organization context, user context, and application-level permissions.

These capabilities are themselves implemented by the execution layer, but their configuration and combination can be described through semantics.

### 3.2 App Field

App Field is the logical data object in an application.

Through semantic configuration, it can define: data fields, field types, data relations, data sources, physical table mappings, view mappings, and data permissions.

Therefore, App Field is not equivalent to a Table in a traditional database. It is closer to:

> A data entity within the application execution environment that can be described and processed by semantics.

It can ultimately be mapped to database tables, views, or even other data sources.

### 3.3 Workflow

Workflow is the process execution capability provided by the App dialect.

```text
Event
  ↓
Workflow
  ├── Conditions
  ├── Data processing
  ├── Function calls
  └── State changes
```

Traditional applications usually need a Workflow Engine, Controller, or Service Code to accomplish these logics. In the App dialect, these variable parts can become part of the semantic structure.

### 3.4 Event

Event is used to describe trigger conditions in the execution environment: data creation, data modification, data deletion, user operations, and scheduled triggers.

Concrete Event types can be further extended by the dialect. Therefore, Event itself is not a basic concept in the public semantic consensus, but a semantic capability provided by the App execution environment.

---

## 4. Semantic Properties in Dialects

A semantic dialect can not only define new prototypes, but also add new semantic properties based on public prototypes.

For example, the permission capability in the App dialect:

```text
App
 └── Auths

App Field
 └── Row Auth
```

Here Auths can describe application-level permissions, while Row Auth can describe data row-level access control.

These capabilities do not require modifying the public base semantics such as node and struct. They simply attach new Properties to the existing semantic prototypes.

```text
Public semantics
    ↓
node.struct
    ↓
Property
    ↑
App dialect extension
```

A dialect does not need to redefine the entire semantic system; it adds its own execution capabilities on top of the public semantics. This is also why semantic prototypes are extensible.

---

## 5. From Program Frameworks to Semantic Infrastructure

From this perspective, the semantic dialect is actually re-dividing part of the responsibilities of past program frameworks.

Traditional program frameworks usually directly provide: CRUD, permissions, ETL, Workflow, Event, task scheduling, and data access. These capabilities exist in the form of APIs, Services, Controllers, Middleware, etc. Business code accomplishes actual requirements by invoking these APIs.

The semantic dialect, by contrast, attempts to abstract the stable execution capabilities into semantic prototypes, and to transform the frequently changing parts into semantic declarations.

```text
Traditional approach

Business requirement
   ↓
Business code
   ↓
Framework API
   ↓
Execution capability
```

Whereas the semantic dialect aims to form:

```text
Business requirement
   ↓
Business semantics
   ↓
Semantic dialect
   ↓
Execution capability
```

The execution layer still exists. The difference is:

> Business changes no longer have to be expressed by modifying execution code; they can be made through semantic declarations.

Therefore, the semantic dialect is not simply "configuration-izing" the program framework. More precisely, it is:

> Elevating a class of reusable execution capabilities into a set of describable, composable, executable prototypes in the semantic space.

---

## 6. A Dialect Is Itself a Language

If public semantic consensus is the foundation commonly understood by all execution layers, then the semantic dialect is the concrete language formed on this foundation.

```text
   Public semantic consensus
        ↓
    Semantic
        ↓
   ┌────┴────┐
   ↓         ↓
App Dialect  Other Dialects
   ↓
Application
   ↓
Concrete business semantics
```

A dialect has its own prototypes, Properties, Relations, Functions, and execution rules. Therefore it is itself a language. It just does not define a language from scratch, but is built on top of public semantic consensus.

This is also the meaning of the name "dialect":

> It shares the basic vocabulary and grammatical capabilities of the public semantics, while adding its own expressive power for a specific execution environment.

---

## 7. Semantic Dialects and the Execution Layer

The semantic dialect has not eliminated the execution layer.

```text
App Workflow
      ↓
Workflow Runtime

App Field
      ↓
Data Provider

    Auth
      ↓
Permission Runtime

  Function
      ↓
Function Provider
```

The dialect defines: how these execution capabilities are described by semantics, how they are combined, and which parts are allowed to change.

The execution layer is responsible for: how to interpret, how to execute, how to access resources, and how to interact with external systems.

Therefore, the semantic dialect still follows the principle established earlier:

```text
Semantics
  ↓
Execution
```

rather than:

```text
Business semantics
  ↓
Direct binding
  ↓
One specific set of code
```

This makes it possible, in theory, for the same dialect to have different execution layer implementations.

---

## 8. Relationships Between Dialects

If different execution environments are all built on the same public semantic consensus, they do not need to share all semantics.

```text
                Public semantic consensus
                      │
          ┌───────────┼───────────┐
          ↓           ↓           ↓
     App dialect   Data dialect   Workflow dialect
          │           │           │
          ↓           ↓           ↓
   Application       Data        Process
   execution layer  execution layer  execution layer
```

Different dialects can independently define their own prototypes, extend Properties, define Relations, provide Functions, and develop execution layers. As long as they still abide by the public semantic consensus, they can form compositions at a higher level.

This also means that in the future, a complete system does not necessarily have to use only one dialect. For example:

```text
App
 ├── Data
 ├── Workflow
 └── Permission
```

Here different capabilities can come from different semantic dialects and be combined through public semantics.

---

## 9. From Public Consensus to Business Semantics

At this point, the relationships among the previous layers can be fully connected:

```text
  Definition domain
    ↓
Public semantic consensus
    ↓
  Semantic dialect
    ↓
  Business semantics
    ↓
Concrete semantic entities
```

Where:

**Definition domain**: solves how semantics is defined.

**Public semantic consensus**: solves which basic semantics must be commonly understood between different execution layers.

**Semantic dialect**: solves how a certain class of execution environment further provides describable, composable execution capabilities.

**Business semantics**: builds on these capabilities to truly describe concrete business objects, rules, and processes.

Therefore, business development ultimately no longer needs to start from code:

```text
Requirement
 ↓
Business code
 ↓
Framework
 ↓
Execution
```

but can gradually transform into:

```text
Requirement
 ↓
Business semantics
 ↓
Semantic dialect
 ↓
Public semantics
 ↓
Execution layer
```

This is also a further result produced after the separation of semantics and execution:

> Execution capabilities themselves can also become semantics.

---

## 10. Unsolved Problems

The semantic dialect solves the extension problem above the public semantics, but it also raises three unsolved problems:

* Composition: how do different dialects combine? When the App dialect's Workflow invokes the Data dialect's processing capability, which semantics is shared?

* Discovery: when one execution layer supports a certain dialect, how do other execution layers know which prototypes, Properties, Relations, and Functions it supports?

* Compatibility: when different execution layers provide semantically equivalent but differently implemented dialects, how is interoperability guaranteed?

These problems go beyond the internal definition scope of a single execution layer, entering the domain of semantic protocols.

At the current stage, what SchemaNode mainly solves is:

> How to establish public semantics within an executable definition domain, and construct semantic dialects on top of it that can actually run.

As for how different execution layers exchange and negotiate these semantics, that is left to later discussions on the protocol layer.
