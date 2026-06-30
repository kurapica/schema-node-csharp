# SchemaNode — Positioning & Vision

> SchemaNode is not a low-code platform. It is a **system for eliminating uncertainty from all participants** — human developers, domain modelers, and AI agents alike.

---

## 1. What Problem We Actually Solve

Most platforms promise to make development "faster" or "easier." SchemaNode solves a different problem: **reliability that does not depend on the reliability of any participant.**

### The Uncertainty Problem

Every software project faces four sources of uncertainty:

| Source | Manifestation | Traditional Fix |
|--------|--------------|-----------------|
| **Developer error** | Bugs in hand-written business logic | Code review, testing, senior oversight |
| **Communication breakdown** | Frontend/backend contract mismatches | API specs, meetings, coordination |
| **Platform lock-in** | Cannot evolve beyond vendor's roadmap | Accept the limitation or migrate |
| **AI hallucination** | Generated code is plausible but wrong | Human review of every AI output |

Each traditional fix adds cost and requires **the very human reliability it's trying to compensate for**.

### The SchemaNode Approach

SchemaNode addresses these at the system level, not the participant level:

| Source | SchemaNode's Fix | Why It Works |
|--------|-----------------|--------------|
| Developer error | Declarative schema, generic runtime | No hand-written business code → no business bugs |
| Communication breakdown | Schema as single source of truth | Frontend auto-renders, backend auto-CRUDs — no negotiation needed |
| Platform lock-in | Minimal Core, replaceable App layer | Core is just Meta+Property+Relation+Function; App is one family among many |
| AI hallucination | AI generates schema configs, not code | Configs are runtime-constrained; cannot call unregistered functions |

The unifying principle: **don't make participants more reliable. Make the system not require their reliability.**

---

## 2. Market: The Hidden Quadrant

Enterprise software serves three of four market quadrants. One remains unserved:

```
                         Low Data Complexity          High Data Complexity
                    ┌─────────────────────┬─────────────────────────┐
   High Budget      │  SaaS approval      │   SAP, Custom ERP       │
   (Enterprise)     │  workflows          │   (well-served)         │
                    │  (well-served)      │                         │
   ─────────────────┼─────────────────────┼─────────────────────────┤
                    │                     │                         │
   Low Budget       │  Google Forms       │   ← SchemaNode          │
   (SMB)            │  Airtable           │   (NOBODY SERVES THIS)  │
                    │  (well-served)      │                         │
                    └─────────────────────┴─────────────────────────┘
```

The bottom-right — projects with **high data complexity but low budgets** — is unserved. Not because it's rare, but because traditional cost structures make it unprofitable: development cost scales with table count and cross-table relations, while project budgets don't. Low-code platforms require expensive consultants for complex data linkage.

Yet these projects are everywhere: wholesale inventory with parametric SKUs, agricultural cooperatives, engineering cost estimation, testing lab sample tracking, trade order management. Each has dozens to hundreds of interrelated tables and a budget measured in thousands, not hundreds of thousands.

SchemaNode's cost structure is fundamentally different: **Cost ≈ Σ(schema definitions)** — the runtime is generic.

### What We Deliberately Don't Do

- **❌ Approval workflows** — DingTalk, Feishu, Teams provide these for free. No value in duplicating.
- **❌ Drag-and-drop UI builders** — SchemaNode auto-renders forms from schemas. Visual layout is secondary to data correctness.
- **❌ Enterprise RBAC matrices** — An enterprise problem; SchemaNode's Auth is functional but deliberately minimal.

---

## 3. Three-Layer Vision

### Layer 1 — High-Complexity Data Management (Today, Validated)

Replace the 5,000-Excel-file chaos. Define data structures, relations, functions as schema. Frontend auto-renders forms with validation and field linkage. Backend auto-manages table structures and CRUD.

**Validated:** 4+ years in production. 700 tables, 3 people, 3 months. New核算 standards (~70 tables each) deployed within one week.

### Layer 2 — Semantic Integration (Enabled Today)

Legacy microservices register capabilities as schema functions — one line of C#:

```csharp
[Meta<Function>("erp.check_stock", Returns = "int")]
```

Events from RabbitMQ/Kafka become schema events. Workflows orchestrate across systems that were never designed to interoperate. AI agents read the entire semantic network through MCP.

**Validated:** 2-hour migration replacing a commercial low-code platform, including microservice proxy registration and message queue subscription.

### Layer 3 — AI-Native Ephemeral Applications (Future)

AI reads enterprise semantics via MCP → creates temporary App schemas with workflows → executes → destroys → logs everything as an auditable record.

```
User: "Summarize customer purchases by region for Q2, highlight accounts over 100K"
AI:   Reads crm.accounts, erp.orders, erp.order_items and their relations
      → Creates ephemeral App "tmp.q2_summary"
      → Executes push function for regional aggregates
      → Returns result, destroys App, logs App+input+output as audit trail
```

Critical difference from "AI generates code": the output is a **schema configuration**, not executable code. It is human-readable, runtime-constrained (can only call registered functions), auditable (App definition + execution log = complete record), and disposable (destroyed after execution, zero system residue) — regardless of whether the participant behind it is a human or an LLM.

---

## 4. Design Philosophy

### Reliability Through Constraint, Not Capability

The dominant approach in enterprise software is capability accumulation: more features, more power, more flexibility. SchemaNode takes the opposite approach: **constraint at the system level creates reliability regardless of participant capability.**

| Constraint | Reliability Guarantee |
|------------|----------------------|
| Semantic functions (non-Turing-complete) | Statically analyzable; no hidden side effects |
| CompileContext system | One function definition → multiple compilation targets; no duplicated logic |
| Declarative schema, generic runtime | No hand-written business code → no business logic bugs |
| Property-based extensibility | New capabilities without Core changes; no regression risk |
| Schema families as extension boundary | Third parties create new families without forking Core |

### The "Unique Truth" Principle

A business rule should exist exactly once and be compilable into every form it's needed:

- `score >= threshold` → data entry constraint AND query filter — same function, different CompileContext
- `SUM(orders.amount)` → aggregation logic AND cache invalidation dependency graph — one push function

This eliminates the most common source of bugs in business systems: **divergent implementations of the same rule in different system layers.**

### Built for Teams That Cannot Afford Mistakes

SchemaNode was not designed in an architecture review room. It was designed by a 3-person team that could not afford for anything to go wrong — no time for bug fixes, no bandwidth for communication overhead, no margin for platform lock-in.

The result is a platform where team size, skill level, and AI participation do not determine system reliability.

---

## 5. What SchemaNode Is Not

- **❌ Not a low-code platform.** Competes on cost structure for complex data, not visual drag-and-drop.
- **❌ Not an approval workflow tool.** DingTalk, Feishu, and Teams have solved this.
- **❌ Not a database ORM.** Schema is the product; the database is an implementation detail.
- **❌ Not an AI agent framework.** Provides the semantic substrate that AI agents read and reason about; doesn't manage agent orchestration.
- **❌ Not a replacement for legacy systems.** Layers semantics on top of existing systems; migration optional.

---

## Further Reading

- [PROJECT_HISTORY.en.md](./PROJECT_HISTORY.en.md) — The real-world story behind every design decision
- [PLATFORM_ARCHITECTURE.en.md](./PLATFORM_ARCHITECTURE.en.md) — Technical deep dive into the four-pillar architecture
- [FEATURE_GUIDE.en.md](./FEATURE_GUIDE.en.md) — Practical code examples for all extension points
- [SchemaNode.App Documentation](../SchemaNode.App/Doc/README.en.md) — The reference application layer
