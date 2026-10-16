# SchemaNode

> **Executable Structured Semantic Language**
>
> A low-level kernel that starts from the structured description of semantic entities and explores public semantic consensus and the multi-dimensional semantic space.

[点击查看中文版](https://github.com/kurapica/schema-node-csharp/blob/main/Doc/zh/Readme.md)

SchemaNode is not a low-code platform, form engine, ORM, or API documentation tool.

It attempts to decouple semantic definitions from concrete execution, so that semantics no longer depend on any single execution layer, but become a foundation that different execution layers jointly follow, interpret, and execute independently.

🚀 **[5-Minute Quick Demo](https://github.com/kurapica/schema-node-csharp/blob/main/Doc/en/tryit/01-type.md)**

📖 **[Architecture Documentation](https://github.com/kurapica/schema-node-csharp/blob/main/Doc/en/architecture/01-why.md)**

💬 **[Design Discussions](https://github.com/kurapica/schema-node-csharp/discussions)**

---

## Core Idea

**SchemaNode is not about writing business rules faster — it attempts to make business semantics themselves become structures that can be described, composed, and executed.**

- **Separation of Semantics and Execution**: Business semantics exist as structured definitions, while concrete execution is the responsibility of the execution layer. The same semantics can be independently interpreted and executed in different execution environments.

- **A Composable Semantic Foundation**: Meta, Property, Relation, and Function serve as the current fundamental semantic primitives, from which higher-level semantic types and semantic dialects are constructed through composition.

- **Public Semantic Consensus**: Provides a common structural and processing foundation for semantic definitions in different domains, without prescribing in advance what business semantics all domains should use.

SchemaNode does not attempt to cover all domains. What it explores is a common foundation capable of defining, carrying, and executing different semantics.

## Practice from a Real-World System

In an agricultural carbon-sink certification system, complex data associations, computation constraints, result traceability, and continuously updated certification standards required both the frontend and backend to jointly handle a large amount of interrelated business semantics.

A 4-person team completed the first phase of the system in 3 months with more than 700 data tables. This practice drove us to think further: when data structures, constraints, associations, and computation rules can all be described by a unified structured semantics, do they still need to be implemented and maintained separately in the database, backend, APIs, and frontend?

SchemaNode continues to develop along this line of thinking.

## Current Implementation

The open-source version currently includes:

- **Semantic Kernel**: Implemented in C# and TypeScript, used to interpret and execute public semantics.

- **App Dialect**: Oriented toward general data processing and permission management; already used to validate application scenarios such as CRUD, permissions, data associations, and ETL.

- **Semantic Editor**: Configures and processes semantic types based on the public semantic foundation, exploring the reuse of semantic definitions across different execution platforms.

These implementations are used to validate the actual capabilities of the semantic kernel, and do not mean that SchemaNode's semantic system or multi-dimensional semantic space is complete.

## Deeper Exploration

The architecture documentation systematically introduces SchemaNode's design motivations, semantic primitives, definition domain, semantic types, semantic dialects, and public semantic consensus.

- 📖 [01. Semantic Drift and Execution Projections: Why We Need Executable Structured Semantics](https://github.com/kurapica/schema-node-csharp/blob/main/Doc/en/architecture/01-why.md)
- 📖 [02. How to Construct the Multi-Dimensional Semantic Space](https://github.com/kurapica/schema-node-csharp/blob/main/Doc/en/architecture/02-how.md)
- 📖 [03. Semantic Prototypes: The Minimal Contract with the Execution Layer](https://github.com/kurapica/schema-node-csharp/blob/main/Doc/en/architecture/03-prototype.md)
- 📖 [04. The Definition Domain and Public Semantic Consensus](https://github.com/kurapica/schema-node-csharp/blob/main/Doc/en/architecture/04-definition.md)
- 📖 [05. Semantic Dialects](https://github.com/kurapica/schema-node-csharp/blob/main/Doc/en/architecture/05-dialect.md)
- 🧪 [Online Demo and Execution Validation](https://github.com/kurapica/schema-node-csharp/blob/main/Doc/en/tryit/01-type.md)
- 💻 [Source Code and Contributions](https://github.com/kurapica/schema-node-csharp)

**We have only opened up a small part of the multi-dimensional semantic space. The true multi-dimensional semantic space still awaits further exploration.**

## Contact

You can join the community on [Discord](https://discord.gg/5xZuk7kMpz).
