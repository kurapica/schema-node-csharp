# SchemaNode

> 一种面向企业业务的语义共识（Semantic Consensus）与语义执行（Execution Layer）开发范式。

## 为什么需要 SchemaNode？

过去几十年，软件工程不断提高编程语言的抽象能力。

从机器码，到汇编，再到高级语言，我们始终在做同一件事情：

**让程序员使用更符合人类思维的方式描述计算。**

然而，当软件进入企业业务领域之后，又出现了新的问题。

企业真正关心的并不是类、接口、数据库或者微服务，而是：

* 客户是什么？
* 合同是什么？
* 审批如何流转？
* 数据之间有什么关系？
* 一项业务如何影响另一项业务？

这些内容构成了企业真正的业务语义。

遗憾的是，目前大多数系统都将这些语义直接散落在：

* Controller
* Service
* SQL
* Workflow
* 微服务接口
* 前端代码

随着业务不断演化，这些语义最终演变为大量重复实现、特殊判断以及历史兼容逻辑，并且散落各处，使系统越来越难维护。

SchemaNode 希望解决的，并不是编程语言的问题，而是**企业业务语义缺乏统一表达的问题。**

---

# Semantic Consensus（语义共识）

SchemaNode 的核心思想，是在自然语言与程序执行之间，引入一层稳定的**语义共识层（Semantic Consensus）**。

这一层并不关心程序如何执行，而负责描述：

* 数据是什么
* 数据之间的关系是什么
* 哪些能力可以作用于数据
* 不同系统之间如何共享同一套语义

语义共识并不是数据库模型，也不是代码生成模型。

它是整个企业业务共享的一套语义描述。

它能够被：

* 人理解
* AI 理解
* 前端理解
* 后端理解
* 不同编程语言共同理解

因此，SchemaNode 并不是一种新的编程语言，而是一种跨语言、跨平台的业务语义组织方式。

---

# Execution Layer（语义执行层）

如果说 Semantic Consensus 定义了"业务是什么"，

那么 Execution Layer 负责回答：

**如何执行这些语义。**

Execution Layer 本身并不了解企业业务。

它只负责解释 Schema 所表达的语义，并结合所在平台完成实际执行。

因此，不同平台可以拥有完全不同的 Execution Layer。

例如：

* C# 可以负责存储、查询、工作流、编译优化等能力。
* TypeScript 可以负责运行时展示、配置编辑、交互以及前端函数执行。
* AI 可以基于 Semantic Consensus 进行分析，而无需理解具体实现细节。

Execution Layer 可以不断演进。

Semantic Consensus 则保持稳定。

两者共同构成完整的软件系统。

---

# 为什么分离？

传统开发中，业务语义与执行实现通常混杂在一起。

例如：

* 一个 Service 同时承担业务规则、数据库访问以及流程控制。
* 一个 API 同时承担接口协议、业务逻辑和数据组织。
* 一个前端页面同时承担展示、业务规则和状态管理。

最终导致：

* 业务难以迁移
* AI 难以理解
* 前后端重复实现
* 系统越来越依赖历史代码

SchemaNode 将它们彻底分离。

业务属于 Semantic Consensus。

实现属于 Execution Layer。

执行方式可以变化。

业务语义保持稳定。

---

# Schema Family（Schema 族）

Semantic Consensus 并不是由单一 Schema 构成。

不同领域可以定义不同的 Schema Family。

例如：

* Struct
* Entity
* Workflow
* Application
* Policy
* Event

未来也可以扩展：

* GIS
* IoT
* Robot
* Knowledge Graph

Core 并不知道这些具体业务。

Core 只提供构建它们所需要的统一基础能力。

因此，新的 Schema Family 可以持续扩展，而无需修改 Core。

---

# Core 的职责

SchemaNode Core 并不是一个业务框架。

它提供的是构建 Semantic Consensus 所需要的基础能力。

包括：

* Node Schema
* Meta
* Property
* Relation
* Function

这些概念共同构成所有 Schema Family 的基础表达能力。

Core 不关心：

* 数据库存储
* 查询优化
* Workflow
* Data Push
* BI
* 权限
* AI
* UI

这些都属于 Execution Layer 或更高层应用。

因此，Core 十分稳定。

它只负责提供跨语言共享的统一语义表达能力。

---

# App 的职责

企业真正的业务，并不属于 Core。

例如：

* Data Push
* Query Compile
* Storage
* Workflow Runtime
* Aggregate
* Data Combine
* Authorization

这些能力全部建立在 Semantic Consensus 之上。

不同企业可以拥有完全不同的实现方式。

因此，SchemaNode 不限制企业如何实现业务。

它只提供统一的语义基础。

---

# AI 在 SchemaNode 中的位置

SchemaNode 并不是为了 AI 而设计。

相反，它首先解决的是企业软件长期存在的复杂度问题。

AI 的出现，使 Semantic Consensus 拥有了新的价值。

AI 可以：

* 分析业务语义
* 构建 Schema
* 生成配置
* 编写测试
* 构建查询
* 辅助设计

但 AI 不直接参与 Execution Layer。

Execution Layer 依然保持确定性。

这样既发挥 AI 的能力，又保证企业系统的可靠性。

---

# 设计原则

SchemaNode 坚持几个原则：

**语义与执行分离。**

业务描述属于 Semantic Consensus。

平台负责 Execution Layer。

---

**Core 尽可能小。**

Core 不解决具体业务。

只提供稳定的基础概念。

---

**语义优先。**

实现可以变化。

语义保持稳定。

---

**跨语言。**

不同语言拥有不同 Runtime。

共享同一套 Semantic Consensus。

---

**开放扩展。**

任何人都可以定义新的 Schema Family。

无需修改 Core。

---

# 项目目标

SchemaNode 并不是一个低代码平台。

也不是一个 ORM。

更不是一个 AI Agent Framework。

它尝试回答一个更基础的问题：

> 当编程语言已经解决了程序员之间的表达问题之后，
>
> 企业业务是否也应该拥有一层稳定、统一、可共享的语义共识？

如果答案是肯定的，

那么 SchemaNode 希望成为这层语义共识的基础设施。
