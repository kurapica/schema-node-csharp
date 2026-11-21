# TODO: Overall Semantic Consistency Analysis

## 1. Goal

Explore the capability of the SchemaNode Runtime to perform continuous consistency analysis over the entire semantic space.

SchemaNode is responsible not only for semantic definition, compilation, and execution, but should also be able to understand the dependencies and impacts between different semantic objects, and judge whether their combination still conforms to the declared overall business intent.

Core goal:

**Extend from local semantic validity to consistency judgment of the overall business intent.**

This is not function correctness proof in the traditional sense, nor a formal verification system, but continuous impact analysis and conflict detection over the whole system, based on structured semantics and function intent.

## 2. Basic Approach

### 2.1 Analyze based on semantic functions

A semantic function is not only an executable unit, but also a carrier of intent that can be interpreted, transformed, and analyzed.

A function may express:

- Data reading and referencing
- Data modification and saving
- Data aggregation and derivation
- Data push and event triggering
- Function calls and dependencies
- State changes in a workflow

Although these operations can be combined into complex business behavior, their intent can still be obtained by unfolding and analyzing the function.

The existing Multi-target Compile and QueryFilterCompileContext already provide part of the foundation.

We can further explore adding derivation targets oriented toward consistency analysis, extracting a unified impact description from function intent.

### 2.2 Open analysis capability

Core does not predefine the business behavior of all App dialects.

For example, DataDerive, data push, and lineage tracking are all semantics of a specific dialect.

Core provides a generic function analysis foundation; a specific dialect extends the semantic analysis capability of Function to provide an explanation of its own behavior.

Therefore, consistency analysis should be an open, extensible capability rather than a fixed set of business rules.

## 3. Analysis Scope

Adopt staged analysis, prioritizing semantics with greater impact.

### Level 1: Basic structural consistency

The existing loading mechanism can already handle most deterministic constraints:

- Type and property validity
- Relation references
- Function arguments and return types
- Basic structural conflicts

This area is not the main direction of new work.

### Level 2: Intra-App consistency

Analyze the functions and data behavior within the same App:

- Business constraints between fields
- The data read and modification scope of functions
- The impact of multiple functions on the same data
- Dependencies between data derivation and aggregation
- Whether different operations may violate a common constraint

For example:

`balance >= 0` is the overall business constraint of an account.

All functions that may modify balance should be included in the analysis, instead of each function independently redefining this constraint.

### Level 3: Cross-App consistency

Further analyze:

- Foreign-key relations between Apps
- Data read and write dependencies
- Data push and event triggering
- State changes between workflows
- Impacts produced by combinations of cross-App functions
- Whether views interpret business semantics consistently

View presentation properties (for example displayOnly) have a lower impact and can be assigned a lower analysis priority.

### Level 4: Overall business intent consistency

Analyze whether combinations of multiple Apps, functions, and workflows contain behavior that violates the overall system constraints.

The focus is not proving that each function is correct, but checking whether, when combined, they jointly obey the declared business rules.

## 4. Runtime Design Direction

Consistency analysis should not be a one-off full-table scan, but should become a continuous state-maintenance capability of the Runtime.

Consider establishing a runtime semantic dependency network:

- Types, Apps, dialect entities, and functions registered into the Runtime participate in the relevant analysis.
- Semantic objects provide information about their own dependencies and impacts.
- Function analysis capability exposes data impacts and related constraints to the Runtime.
- The Runtime maintains semantic dependencies between objects.
- When types, functions, relations, and so on are modified, proactively trigger the relevant derivation.
- Re-analyze only the affected part.
- Continuously update the overall consistency state.

In the end, the Runtime holds not only semantic definitions and execution capability, but also the overall analysis state of the current semantic space.

### Conflict records

Conflict information is a continuously updatable state in the Runtime, not a temporary return value of a single analysis.

Records include:

- The semantic objects involved
- Related functions and dependency paths
- The business constraints in conflict
- The basis of the judgment
- The scope of impact
- The current judgment state

Circular dependencies, incremental propagation, version management, and atomic updates of analysis results need to be considered.

## 5. Conflict Judgment Principles

Consistency analysis is responsible for discovering problems, not for automatically repairing business behavior.

Distinguish different judgment results:

| Type | Meaning |
|---|---|
| Definite conflict | A semantic constraint is clearly violated |
| Potential conflict | A constraint violation is possible |
| Insufficient information | A judgment cannot be made at present |
| Pending human confirmation | May be a legitimate business exception |

Analysis results serve mainly as warnings and a basis for decisions.

Whether to reject execution, modify the workflow, adjust business rules, or have humans supply additional definitions is decided by the corresponding editing or execution mechanism.

## 6. Boundary with Traditional Software Engineering

The timing, concurrency, transactions, external system interaction, and concrete business rules at the execution layer remain the responsibility of human developers to design and verify.

This work still requires traditional testing and long-term engineering iteration.

Semantic consistency analysis addresses a different class of problem:

**Whether multiple local semantic definitions generated by AI, when combined, still express a consistent system.**

It does not replace traditional software testing, nor does it promise to prove that all runtime behavior is correct.

## 7. Relationship with AI

As AI gradually takes over semantic development of Apps, Functions, Workflows, and so on, an independent semantic constraint mechanism is needed.

The expected loop:

AI constructs and modifies semantics
→ SchemaNode compiles and performs consistency analysis
→ Runtime updates the overall semantic state
→ Conflicts are fed back to AI or developers
→ Modify and re-analyze

AI is responsible for semantic development, SchemaNode is responsible for semantic constraints, and the execution layer is responsible for deterministic implementation.

Consistency analysis is an important quality gate for AI semantic development.

## 8. Implementation Principles

- Do not introduce an independent general-purpose formal verification system.
- Prefer reusing the existing Function, CompileContext, and multi-target compilation mechanisms.
- Start from function impact analysis; do not attempt to cover all semantics at once.
- Prioritize high-impact behavior such as data writes, state changes, events, and workflows.
- Adopt open dialect extensions; do not predefine all business behavior.
- Adopt incremental derivation; do not rely on full scans.
- Clearly distinguish definite conflicts, potential risks, and cases that cannot be judged.
- Gradually expand analysis coverage as AI's semantic construction capability grows.

## 9. Long-Term Vision

The SchemaNode Runtime evolves from an executable semantic environment into an environment capable of continuously understanding, analyzing, and maintaining the overall semantic state.

It knows not only how the system executes, but can also analyze the relations between different intents in the system, and whether they hold jointly.

This is an important direction for SchemaNode's expansion toward software factory capabilities.

**Core principle: let overall consistency analysis emerge naturally from the existing semantic mechanisms, rather than constructing a separate paradigm in order to implement a verification feature.**
