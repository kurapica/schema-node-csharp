# 01. Semantic Drift and Execution Projections: Why We Need Executable Structured Semantics

The starting point of SchemaNode is not "how to write code faster," but: **in what form do business semantics actually exist in software?**

In traditional software development, we usually start from requirements.

Requirements describe objects, rules, relationships, and behaviors in the real world, and the software needs to transform them into database structures, backend code, API protocols, frontend state, and ultimately the user interface.

This process looks like merely "implementing requirements," but what actually happens is a series of continuous **semantic translations**.

---

## 1. Requirements Are Not Code

Suppose we need to implement a simple piece of business:

> A project has an owner, and the owner belongs to an organization; projects can only be viewed by organization members; some of a project's data is computed from other data; after the certification standard changes, new rules only affect new projects, while historical projects are still computed under the original standard.

To a human, these statements describe a relatively complete semantic structure:

* What entities exist;
* What properties the entities have;
* What relationships exist between entities;
* Under what conditions access is allowed;
* How data is produced;
* What rules constrain the data;
* When and within what scope the rules take effect.

But when we actually start writing software, these semantics usually get torn apart.

The database is responsible for storing data.

The backend is responsible for business logic.

APIs are responsible for data exchange.

The frontend is responsible for interaction and display.

The permission system is responsible for access control.

The computation module is responsible for derived data.

In the end, a once-complete business semantics is scattered across a large amount of code and configuration in many different forms.

---

## 2. Code Is an Execution Projection of Semantics

Code can certainly express these semantics.

The problem is that code is not the semantics itself.

In order for machines to execute it, we must project the complex semantics of the real world into one specific computational model.

For example, a "project owner" might ultimately appear as:

```text
project.owner_id
```

In the backend it might further become:

```text
if (project.OwnerId == currentUser.Id) ...
```

In the database it might appear as a foreign key.

In the frontend it might appear as a user selector.

The permission system might duplicate the check once more.

Each of these pieces of code is correct on its own, but what they express is in fact the same semantics:

> **The project has an owner, and the owner participates in the project's access relationship.**

From the business perspective, this is one semantic entity and its relationship.

From the programming perspective, it has already become multiple different data structures and execution logics.

Therefore, code can be understood as:

> **An executable projection of the original business semantics.**

There is nothing wrong with the projection itself.

The problem is that if only the projection exists and the original semantics are not retained, then the software system can hardly know:

> **These different pieces of code are actually describing the same thing.**

---

## 3. Semantic Drift Starts Here

When the same semantics needs to be implemented separately by multiple technical layers, translation happens over and over.

A single field may go through:

```text
Business requirement
   ↓
Database field
   ↓
Backend model
   ↓
API DTO
   ↓
Frontend state
   ↓
UI control
```

If permissions, validation, computation, caching, search, and data synchronization are also involved, the same semantics will enter even more systems.

Every translation may produce new conventions.

For example:

```text
Database: owner_id
Backend: OwnerId
API: ownerId
Frontend: owner
Permissions: currentUser.id == owner.id
```

There is no natural common semantic entity connecting these.

Developers can only keep them consistent through naming conventions, documentation, agreements, and code maintenance.

The larger the system, the higher this synchronization cost.

What is more troublesome is that many business rules do not even appear in entity definitions — they hide inside some function:

```text
if (...)
    ...
```

or:

```text
assert(...)
```

or:

```text
query.where(...)
```

The code can execute these rules, but it never tells the system:

> What business semantics does this actually represent?

As a result, what keeps growing in software systems is not only business complexity, but also a large amount of **accidental complexity** produced by keeping different implementations consistent with each other.

---

## 4. The Real Difficulty Is Not Code Volume, but Semantic Composition

This is also why some complex systems cannot simply be solved by "writing more code."

For example, a real business system may simultaneously have:

* Hundreds of business tables;
* A large number of parameters and enums;
* Multi-level associations between data;
* Field-level and row-level permissions;
* Derived data;
* Cross-table computation;
* Complex frontend linkage;
* Different versions of business rules;
* Compatibility with historical data;
* Synchronization between different execution platforms.

None of these capabilities is difficult in isolation.

The difficulty comes from the **combinatorial relationships** between them.

A single field may simultaneously have:

```text
Data type
+
Display semantics
+
Value range
+
Permissions
+
Data source
+
Associations
+
Computation rules
+
Lifecycle
```

Traditional systems usually require different modules to carry these semantics separately.

Consequently, a single change may require modifications in multiple places.

This is also why, after AI has drastically reduced the cost of code generation, this problem actually deserves to be reconsidered even more.

> **If generating code is becoming cheaper and cheaper, then what is truly scarce is no longer code, but the correct semantics behind the code.**

---

## 5. Making Semantics a First-Class Entity

The direction SchemaNode attempts to take is neither letting AI write more code, nor building a more sophisticated code generator.

Rather, it is the reverse:

> **First make business semantics themselves structured entities, then let code become the way those semantics are executed.**

For example, a field is not simply:

```text
number
```

but can be further described:

```text
What semantics does this represent?
What constraints does it have?
Which entities is it related to?
Where does the data come from?
What happens when it changes?
How should it be executed?
```

This information no longer exists merely as conventions scattered across different code; it can become different dimensions of the same semantic entity.

Thus:

```text
Business semantics
    ↓
Structured semantic entities
    ↓
Execution layer
    ├── Database
    ├── Backend
    ├── Frontend
    ├── Query
    └── Other execution targets
```

A very important change has taken place:

**Business semantics no longer depend on any particular execution approach.**

---

## 6. Semantics and Execution Should Be Separated

If semantics itself becomes an independent structure, then the execution layer should no longer carry the definition of business semantics.

What the execution layer really needs to know is:

> **How to interpret a certain kind of semantics.**

For example, a semantic function expresses:

> Compute another value based on a certain relation.

It does not need to directly prescribe whether to:

```text
Generate SQL
```

or:

```text
Generate C# code
```

or:

```text
Generate TypeScript
```

or even be interpreted directly by some reasoning system in the future.

All of that belongs to the execution layer.

Therefore, the same semantics can be compiled into different execution forms according to different purposes:

```text
                Semantics
                 │
       ┌─────────┼─────────┐
       ↓         ↓         ↓
     SQL       C# / TS    Reasoning
       │         │         │
       └─────────┼─────────┘
                 ↓
             Execution result
```

This is also why SchemaNode understands **Function** as the execution intent of semantics, rather than a specific code implementation.

---

## 7. This Does Not Mean Building a "Universal Business Language"

If we tried to directly define:

> Which entities, which fields, and which business rules all software should use.

What we would end up with is still a fixed, lossy business model.

Real-world software domains obviously do not share a single language.

Healthcare, finance, manufacturing, agriculture, content systems, permission systems — each has its own semantics.

Therefore SchemaNode takes another direction:

> **Build a common semantic foundation, rather than prescribing that all domains must use the same business semantics.**

On top of this common foundation, different domains can form their own **semantic dialects**.

A dialect can define its own entities, properties, relations, and execution approaches.

And the execution layer only needs to understand the common foundation and the dialects it supports.

In this way, business semantics can keep expanding without constantly modifying the lowest-level execution kernel.

---

## 8. Why Public Semantic Consensus Is Also Needed

A deeper question arises here.

If a Schema is itself structured data, then the Schema DSL used to define Schemas is also structured data.

That is:

```text
Business data
    ↓
Business Schema
    ↓
DSL that defines Schemas
    ↓
Foundation that defines the DSL
```

All of these essentially need to be described, read, modified, and executed.

Therefore, what really needs to be unified is not all businesses, but:

> **What foundational capabilities structured semantics itself requires.**

This is why public semantic consensus exists.

It provides the common **definition domain** of semantics, not a unified business domain.

With this common foundation, different semantic dialects can — while keeping their own differences — understand, transform, and exchange shared data and definitions.

Therefore, the core of SchemaNode is not to provide a fixed business model, but to provide a set of infinitely extensible, self-describing fundamental grammar (the four primitives).

---

## 9. From Data Types to Semantic Types

Traditional type systems mainly answer:

> **In what form does this value exist?**

For example:

```text
string
number
boolean
array
object
```

But real business usually also needs to know:

> **What meaning does this value have in the system?**

For example, a `number` can simultaneously have:

```text
Display precision
Value range
Default value
Read-only
Data source
Access permissions
Computation rules
State semantics
```

None of these is expressed by the data type `number` itself.

They are different **semantic properties** attached to the entity. These properties, together with the data type, jointly constitute the semantic entity.


## 10. The Multi-Dimensional Semantic Space: Entities, Properties, Relations, Functions

When SchemaNode speaks of "multi-dimensional semantics," it does not first mean that an entity has many kinds of data structures, but rather:

> **The same structured entity can simultaneously carry multiple independent, composable semantic properties.**

It can be understood as:

```text
                 Property
              ┌─────┼─────┐
              ↓     ↓     ↓
        Constraint Permission Display
              \     │     /
               \    │    /
                ↓   ↓   ↓
             Semantic entity
                   │
          ┌────────┴────────┐
          ↓                 ↓
       Relation          Relation
          │                 │
          ↓                 ↓
      Other entities    Other entities
          │                 │
          └────── Function ─┘
                    ↓
          Modify / produce semantics
```

Where:

* **Structure** provides the basic form of the entity;
* **Property** gives the entity composable semantic dimensions;
* **Relation** establishes semantic associations between entities;
* **Function** describes how these semantics produce deterministic execution effects.

Particularly important is that Relation is not simply recording a "link."

A relation can act on another entity through a Function, changing one of its Properties.

For example:

```text
A
 └─ relation ──→ B
                  │
                  └─ property
```

A relation can express:

> A certain value of A determines a certain property of B. Of course, multiple entities can also jointly determine a property of another entity.

In this way, semantics is no longer confined within a single entity, but can propagate and compose between entities along relations.

Therefore, the multi-dimensional semantic space is not about piling a lot of information onto one object, but rather:

> **Entities gain multiple composable semantic dimensions through Property, and then establish executable semantic associations with other entities through Relation.**

This is also why moving from traditional data types to semantic types is not simply "adding more metadata to types."

What really changes is:

> **An entity that previously only had structure begins to possess semantic dimensions that can be composed, propagated, and executed.**

From this perspective, a `number` by itself is merely a data type, while a `number` that simultaneously has value constraints, access control, data sources, and computation relations has already become a true **semantic entity**.

The multi-dimensional semantic space that SchemaNode explores starts exactly from here.


---

## 11. Why Natural Language Cannot Replace the Semantic Layer

In the AI era, an increasingly common view is:

> **Since large models can understand natural language, there is no longer any need for new programming languages or a semantic layer.**

From the perspective of "understanding," this judgment has some merit.

Natural language itself can describe very complex business:

> Projects belong to an organization, and organization members can view projects; some project data is computed from other data; after the standard changes, new projects use the new rules while historical projects are still executed under the original standard.

Humans can directly understand such descriptions.

But **being understandable does not mean being suitable as the long-term semantic carrier of a running system.**

### Natural Language Lacks Stable Structural Boundaries

Semantics in natural language usually depend on context.

For example:

> Only organization members can view projects.

This sentence looks very clear, but actual execution still requires knowing:

* How exactly is "organization member" defined?
* Which organization does the project belong to?
* Which organization does the current user belong to?
* How are multi-organization scenarios handled?
* Does the project have other access conditions?
* Does this rule apply to all projects or only to a certain kind of project?
* When does the rule take effect?

This information can of course continue to be written in natural language.

But as rules multiply, we will ultimately need to break them down into:

**Entities, properties, relations, constraints, conditions, and execution behaviors.**

That is, we still need some kind of structure.

---

### Natural Language Is Good at Expressing Intent, Not at Carrying the Entire Execution Contract

The greatest advantage of natural language is its expressive power.

It is suited to describing:

> **What we want.**

But machine execution needs to know:

> **Which definite structures and operations this intent corresponds to.**

For example:

> "Make the organization of the project owner the default organization of the project."

This is a very natural business description.

But the execution system must further know:

```text id="g7c0ec"
Project
 └─ owner
     └─ organization
          ↓
      project.organization
```

as well as:

* Which relation provides `owner`;
* Which relation provides `organization`;
* What happens when the relation does not exist;
* Whether the value is recomputed after a change;
* How the database queries it;
* How the frontend displays it;
* How the permission system uses this relation.

Natural language can describe all of this, but if AI had to re-interpret everything from natural language before every execution, it would mean:

> **Semantic interpretation itself becomes an uncertainty at runtime.**

And what software systems usually need is precisely a stable, verifiable, traceable execution contract.

---

### Natural Language Can Generate Semantics, but Should Not Replace Semantics

Therefore, SchemaNode is not competing with natural language.

The more reasonable relationship should be:

```text id="n2v8ck"
Natural language
   ↓
AI understanding and modeling
   ↓
Structured semantics
   ↓
Semantic validation / composition / reasoning
   ↓
Different execution layers
   ├── SQL
   ├── C#
   ├── TypeScript
   ├── UI
   └── Other execution targets
```

Natural language can serve as the **entry point into the semantic space**.

AI can be responsible for converting human intent into structured semantics, and can also understand, modify, and reason on top of structured semantics.

But once execution begins, deterministic contracts should be carried by structured semantics as much as possible.

This is actually quite similar to the evolution of programming languages.

We do not consider programming languages meaningless just because natural language can describe algorithms.

One of the values of programming languages is precisely:

> **Converting human intent into a form with explicit structure, rules, and execution semantics.**

What SchemaNode explores is merely a layer closer to business semantics:

> **Before entering concrete code, can there first exist a structured semantics that machines can process?**

---

### Structured Semantics Has Another Capability That Natural Language Can Hardly Replace: Composability

A software system does not execute a requirement description once and for all.

It constantly needs to:

* Add a property;
* Modify a constraint;
* Establish a relation;
* Reuse a semantic type;
* Combine multiple semantics together;
* Propagate a change to the entities that depend on it;
* Project the same semantics onto multiple execution targets.

These operations are essentially **manipulating semantic structures**.

For example:

```text id="w5tq9h"
Semantic type A
    +
Relation B
    +
Constraint C
    ↓
New semantic entity D
```

If all of this existed only as natural-language text, the machine would have to re-parse the context every time.

Structured semantics, by contrast, can directly become composable data.

This is also why SchemaNode connects "semantics" with "language":

> **Natural language is responsible for openly expressing human intent; structured semantics is responsible for stably carrying meaning that machines can operate on.**

The two do not replace each other — they occupy different positions.

---

### The Stronger Natural Language Becomes, the More Important Structured Semantics Gets

Therefore, AI will not render the semantic layer meaningless.

On the contrary, as AI becomes better and better at understanding human needs, we will increasingly need a stable intermediate layer that converts this open-ended intent into:

> Contracts that can be verified, composed, traced, and consistently interpreted by different execution layers.

---

## 12. Why This Question Is Being Raised Again in the AI Era

AI is rapidly reducing the cost of code generation.

In the past, a business requirement required programmers to progressively translate it into data structures, business logic, APIs, and interaction interfaces. In the future, more and more of this translation work may be done by AI.

But this does not eliminate the semantics problem.

On the contrary, it makes the problem more visible:

> **What semantics is AI-generated code actually based on?**

If the input is still just natural language and the output is still just code scattered across different execution layers, then AI has merely sped up the efficiency of manual semantic translation. It has not changed the fact that business semantics are projected onto different execution layers and produce deviations in the conversion process.

The declining cost of code generation does not mean the cost of semantic construction will decline in sync.

On the contrary, as code becomes easier and easier to generate, how to express requirements, constrain the generated results, and ensure that different execution layers remain consistent on the same business semantics becomes even more important.

But if business semantics already exist in a structured semantic space, then the objects AI operates on are no longer just code text, but:

```text
Entities
Properties
Relations
Functions
Constraints
Dialects
Execution targets
```

These objects have explicit structures and semantic associations, and can be composed, checked, and interpreted under established contracts. AI therefore does not need to re-derive the entire system from natural language every time, and can directly participate in the construction, extension, and adjustment of existing semantics.

This is not to say that AI will automatically solve the semantic modeling problem.

Semantic structures themselves still need to be designed, and semantic declarations may still contain errors, ambiguities, or incompleteness. The significance of structured semantics lies in enabling these problems to be identified and verified based on explicit definitions, relations, and contracts — rather than having to be found indirectly in the generated code.

> **The stronger AI becomes, the more we need a structure that can carry and constrain semantics.**

Traditional code generation typically takes code text as its primary output; whether the generated result matches the requirement must be judged through code analysis, testing, and actual execution. In a structured semantic space, AI can directly generate semantic declarations with explicit structure, and the definition domain, prototype contracts, and execution environments provide corresponding checking and verification capabilities.

This does not mean that code becomes an unexplainable black box, nor that structured semantics are naturally correct. The real difference is: **semantics no longer have to be reverse-inferred from code — they can be expressed, checked, and evolved as independent structures.**

Therefore, what SchemaNode focuses on is not just how to make AI generate code faster, but how to change the objects through which AI participates in software construction.

When semantics and execution are separated, AI can participate in defining semantic types, implementing specific prototype capabilities, generating interaction interfaces, and extending the system within explicit contract boundaries. The context, verification methods, and architectural judgment required by different tasks can thereby be differentiated.

The next question to discuss is how this structure changes the way AI works, which tasks can be localized, verified, and iterated, and which still require holistic architectural judgment.


## 13. The Layered Participation of AI in the Semantic Space

Once semantics and execution are decoupled through the prototype contract, the way AI participates in software development changes accordingly.

In traditional development, AI typically generates code from requirements. But code often contains data structures, business rules, execution logic, and interaction behavior all at once. To correctly modify any one part, AI still needs to understand a large amount of implicit context and constraints between different modules.

SchemaNode splits these responsibilities across distinct semantic layers, and establishes explicit contracts through mechanisms like Prototype, Property, Relation, and Function. This allows AI to participate at different levels based on task boundaries, without having to re-understand the entire system each time.

```text
┌────────────────────────────────────────────────┐
│ Prototype family implementation                │
│ Property / Relation / Function                 │
│ AI participation: High                         │
│ Clear contracts, locally implementable &       │
│ verifiable                                     │
├────────────────────────────────────────────────┤
│ Semantic type definition                       │
│ Schema declaration                             │
│ AI participation: High                         │
│ Constrained structure, but still requires      │
│ business semantics understanding               │
├────────────────────────────────────────────────┤
│ Interaction UI                                 │
│ Layout / Style / Interaction flow              │
│ AI participation: High                         │
│ Mature visual & interaction patterns,          │
│ relatively independent responsibilities        │
├────────────────────────────────────────────────┤
│ Prototype family boundary design               │
│ Semantic boundary / Contract design            │
│ AI participation: Limited                      │
│ Requires global understanding, trade-offs,     │
│ and continuous validation                      │
├────────────────────────────────────────────────┤
│ Cross-dialect composition / Semantic network   │
│ protocol                                       │
│ AI participation: Yet to be validated          │
│ Requires solving cross-execution-layer         │
│ compatibility & collaboration                  │
└────────────────────────────────────────────────┘
```

The participation level here is not an absolute rating of AI capability, but a judgment based on current architectural conditions regarding task decomposability, context scope, and verification difficulty. The clearer the contract and the more limited the dependency scope, the more reliably AI can typically produce results within a localized task.

### Why Prototype Family Implementation Suits AI Participation

Prototype families in SchemaNode are not conceptual module divisions — they are structurally decoupled.

The kernel does not need to know that a `struct` has `fields`, nor that an `appfield` has a `dataSource`. That knowledge is encapsulated in the respective `NodeType` implementations and semantic declarations, interpreted by the corresponding prototype family.

Therefore, extending a new prototype family can typically be broken down into the following work:

1. Define a new `kind` and its corresponding `NodeType`.
2. Declare which Properties the prototype supports.
3. Register the necessary Relation handlers.
4. Implement the required Functions.
5. Validate its compatibility with existing semantics, execution mechanisms, and public semantic consensus.

Provided the existing contracts can accommodate the requirements, none of this work requires modifying the kernel's loader, context, or caching mechanisms.

This means AI does not need to master the entire system on every development task. It only needs to understand the current prototype family's contract, relevant dependencies, and validation rules to complete a substantial portion of the implementation work.

**Architectural decoupling reduces the context scope AI needs for localized tasks, while explicit contracts constrain the boundaries the implementation must respect.**

Of course, module independence does not mean the implementation is naturally correct. AI can still misunderstand semantics, miss compatibility constraints, or generate code that fails validation. Therefore, prototype family implementation requires not only clear contracts but also testing and runtime verification.

By contrast, **prototype family boundary design** is a different kind of problem.

Whether a semantics belongs in `node.struct`, `node.appfield`, or a standalone `node.auth` prototype family cannot be decided based solely on the local needs of one module. It involves the partitioning principles of the entire semantic space, as well as compatibility, extensibility, and implementation cost across different execution layers.

These principles usually take shape through collisions across multiple execution layers and real-scale application. AI can participate in candidate generation, dependency analysis, conflict detection, and test result summarization — but how to draw the boundaries still requires architectural judgment and continuous iteration.

### After the Semantic Layer Absorbs Interaction Logic

In traditional frontend development, a form component is often responsible not only for display and interaction, but also for local validation, error messages, submission transforms, API calls, cache updates, dependent refreshes, and permission checks.

When these responsibilities are scattered across frontend code, the UI must not only understand how to display data but also re-implement a portion of the business rules.

SchemaNode delegates data constraints, semantic relations, computation rules, and permission semantics to the corresponding semantic declarations and execution environments.

```text
User input
    ↓
Semantic-layer data change
    ├── Property: Constraints & validation
    │   └── lowlimit / uplimit / regex
    ├── Relation: Semantic association & propagation
    │   └── assign / call
    ├── Function: Computation & data processing
    │   └── getcontext / aggregation
    └── Permission semantics
        └── Auths / Row Auth
              ↓
Presentation & interaction layer
    ├── Data rendering
    ├── Layout & style
    └── User interaction & flow control
```

This does not mean all business processing must run in a single semantic layer. Rather, the frontend no longer needs to independently re-implement these semantic rules. Specific rules are interpreted and executed by the corresponding execution environments, and critical constraints like permission checks must be guaranteed by trusted execution ends.

The frontend can therefore focus more on presentation and interaction:

- **Layout**: Grid, Flex, responsive breakpoints, and page structure.
- **Style**: Theme variables, spacing, typography, and motion effects.
- **Interaction**: Input feedback, component state, modals, and step transitions.
- **UI flow**: Page navigation, loading states, and the organization of interaction sequences.

These tasks may still involve their own state management and flow logic, but they should not redundantly carry the data constraints, business computations, and permission rules that the semantic layer already handles.

Frontend code shifts from being the primary carrier of business logic to becoming **the presentation and interaction entry point of semantic state in the interaction environment**.

For AI, this change further narrows the business context needed for UI development. It can generate configuration interfaces based on existing semantic types, and adjust layout and interaction around established data structures — without having to re-implement business rules in every component.

### This Layering Is Structural, Not Temporary

The layering above does not depend on the specific capabilities of any particular AI model — it comes from SchemaNode's architectural design itself.

As long as semantics and execution remain separated, and the contracts between prototype families stay explicit, AI can participate in implementation, semantic declaration, and UI generation within bounded scopes. Improvements in model capability will expand the range of tasks it can handle independently, but will not eliminate the responsibility differences between layers.

Conversely, AI cannot guarantee the soundness of the overall semantic architecture merely by producing locally correct implementations. Prototype family boundary design, the formation of public semantic consensus, cross-dialect composition, and semantic network protocols all involve compatibility and long-term evolution concerns beyond localized implementation. These tasks can be assisted by AI analysis and validation, but still require real-world feedback to test.

Therefore, SchemaNode's significance for AI is not just making it easier for AI to generate code — it is about using explicit semantic structures and execution contracts to **transform a portion of development tasks that previously required global understanding into work that can be localized, verified, and iterated.**

Prototype family implementation, semantic type definition, and interaction UI constitute the main areas where AI can participate deeply; prototype family boundary design and cross-execution-layer collaboration still require continuous architectural exploration and practical validation.

This is not to say that AI can only do implementation while humans can only do architecture. Rather: **the clearer the architectural boundaries, the more reliably AI can work within them; and how the boundaries themselves are formed still requires judgment from holistic design and real-world practice.**

---

## 14: What SchemaNode Is Exploring

Therefore, SchemaNode is not attempting to replace existing programming languages.

Nor is it attempting to define the business standard for all domains.

It explores a different layer:

> **Can we establish an executable structured semantics, so that business semantics can be separated from concrete code and become objects that can be described, composed, executed, exchanged, and continuously accumulated?**

The current exploration starts from four fundamental semantic primitives:

**Meta / Property / Relation / Function**

Built on top of them:

**Semantic types → Semantic dialects → Public semantic consensus → Execution layers**

This is still only a beginning.

A true multi-dimensional semantic space requires more domains, more execution modes, and more practice to gradually take shape.

But if this path holds, then the fundamental objects of software development may change:

> From "writing code to implement business," gradually toward "constructing semantics, and letting different execution layers interpret these semantics."

**This is exactly the direction SchemaNode is exploring.**


## 15: On the Scope of the Semantic Space Discussed Here

What this article and the following chapters discuss is the **Structured Semantic Space**: organizing the semantic structures of requirements into a describable, composable, and executable space through explicitly definable entities, properties, relations, and behaviors.

This does not mean that real-world semantics can be fully described by a finite set of structural dimensions. Real-world semantics may also contain continuous states, probabilities, uncertainty, context, and high-dimensional associations that are hard to define discretely. The representation and execution of such a **Continuous Semantic Space** is not covered in this article and remains a question worthy of further research.

Therefore, "multi-dimensional" in this article is first of all a structural concept: it emphasizes that semantics is no longer compressed into the single representation form of code, but allows multiple interrelated yet relatively independent semantic dimensions to exist simultaneously.

```text
                 Semantic space
                    │
          ┌─────────┴─────────┐
          │                   │
  Structured semantic    Continuous semantic
       space                  space
          │                   │
    Explicit structure    Probabilistic approximation
          │                   │
    Verifiable/executable  Creative/derivative
          │                   │
    AI deterministic      AI controlled
      implementation         creativity
```
