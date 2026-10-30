# SchemaNode

> **可执行结构化语义语言（Executable Structured Semantic Language）**
>
> 一个从语义实体的结构化描述出发，探索公共语义共识与多维语义空间的底层内核。

SchemaNode 不是低代码平台、表单引擎、ORM 或 API 文档工具。

它尝试将语义定义与具体执行解耦，使语义不再依附于任何单一执行层，而成为不同执行层共同遵循、独立解释和执行的基础。

🚀 **[5 分钟快速体验 Demo](./tryit/01-type.md)**

📖 **快速上手**
    - [语义类型配置](./useit/01-types.md)
    - [应用配置](./useit/02-apps.md)
    - [工作流配置](./useit/03-workflow.md)
    - [嵌入已有微服务](./useit/04-integration.md)
    - [前端视图定制和使用](./useit/05-views.md)

📖 **[架构文档](./architecture/01-why.md)**

📖 **[内核导读](./core/01-prototype.md)**

📖 **[App 方言导读](./app/01-overview.md)**

💬 **[设计讨论](https://github.com/kurapica/schema-node-csharp/discussions)**

---

## 核心思想

**SchemaNode 不是把业务规则写得更快，而是尝试让业务语义本身成为可以被描述、组合和执行的结构。**

- **语义与执行分离**：业务语义以结构化定义存在，具体执行由执行层负责。同一语义可以在不同执行环境中被独立解释和执行。
- **可组合的语义基础**：以 Meta、Property、Relation、Function 作为目前的基础语义原语，通过组合构造更高层的语义类型与语义方言。
- **公共语义共识**：为不同领域的语义定义提供共同的结构与处理基础，而不预先规定所有领域应该使用什么业务语义。

SchemaNode 不试图覆盖所有领域。它探索的是一套能够定义、承载和执行不同语义的共同基础。

## 来自真实系统的实践

一个农业碳汇认证系统第一期包含 700+ 张数据表的，涉及极度复杂的数据关联、计算约束、结果追溯和动态标准。

这是在没有 AI 辅助的年代完成的真实项目。事后评估，传统开发范式需要 120 人月以上，且因为极高的业务复杂度，在现实中几乎不可能平滑交付。

但在“语义与执行分离”的设计下，4 人团队 3 个月就完成了一期构建，全程无加班且 Bug 率极低。这促使我们进一步思考：当数据、约束、关联和规则由统一的结构化语义描述时，它们是否还需要被痛苦地重复实现于数据库、后端、接口与前端？

SchemaNode 正是沿着这个问题继续发展的。

## 当前实现

开源版本目前包含：

- **语义内核**：C# 与 TypeScript 实现，用于解释和执行公共语义。
- **App 方言**：面向通用数据处理与权限管理，已用于验证 CRUD、权限、数据关联和 ETL 等应用场景。
- **语义编辑器**：基于公共语义基础配置和处理语义类型，探索语义定义在不同执行平台之间的复用。

这些实现用于验证语义内核的实际能力，并不意味着 SchemaNode 的语义体系或多维语义空间已经完成。

## 深入探索

架构文档系统地介绍了 SchemaNode 的设计动机、语义原语、定义域、语义类型、语义方言与公共语义共识。

- 📖 [01. 语义偏移与执行投影：为什么需要可执行结构化语义](./architecture/01-why.md)
- 📖 [02. 如何构造多维语义空间](./architecture/02-how.md)
- 📖 [03. 语义原型: 和执行层的最小契约](./architecture/03-prototype.md)
- 📖 [04. 定义域和公共语义共识](./architecture/04-definition.md)
- 📖 [05. 语义方言](./architecture/05-dialect.md)
- 🧪 [在线 Demo 与执行验证](./tryit/01-type.md)
- 💻 [源码与贡献](https://github.com/kurapica/schema-node-csharp)

**我们目前只是打开了多维语义空间的一小部分。真正的多维语义空间，还有待继续探索。**

## 联系我们

您可以在 Discord 上加入我们的社区：[5xZuk7kMpz](https://discord.gg/5xZuk7kMpz)

也可以加入 QQ 群：[1043828069](https://qm.qq.com/cgi-bin/qm/qr?k=q7Fr8seFSdRzgAD-j5d3OZDKIWNNES3U)