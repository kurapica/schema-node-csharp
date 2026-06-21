# SchemaNode 平台架构说明

> 本文档面向平台架构师、框架设计者、领域建模负责人以及潜在生态贡献者，说明 `SchemaNode` 的核心设计理念、平台化方向与扩展机制。`SchemaNode.Core` 是当前最核心的 C# 参考实现，但本文重点不是某个具体 API，而是 SchemaNode 作为**社区型元数据平台**的架构思想。

## 1. 我们在构建什么

`SchemaNode` 并不试图成为另一套“更方便编写的 Schema 文件格式”。

它要构建的是一个：

- **语言无关**的元数据声明系统；
- **自解释**的元数据管理平台；
- **可扩展**的 schema type / schema property 平台；
- **可执行**的数据类型、操作和关联运行时；
- 能够同时服务后端、前端、平台层和应用层的统一内核。

从平台定位上说，SchemaNode 更接近：

> 一个以 `Property` 组合为核心的元数据语言内核，以及围绕这个内核逐步形成的开放生态。

`SchemaNode.Core` 是这套平台在 C# 中的核心实现；后续的 TypeScript 实现将作为前端核心，形成跨语言一致的元模型能力。

---

## 2. 为什么不是“再做一套 JSON Schema”

很多系统在遇到元数据问题时，第一反应是围绕 JSON 做一套 Schema 描述格式，然后在此基础上附加校验、UI、流程和业务语义。

SchemaNode 选择了相反的路径。

我们认为，真正困难的并不是“用 JSON 如何表达结构”，而是以下更高层的问题：

1. 如何统一描述**数据类型**；
2. 如何统一描述**操作与函数**；
3. 如何统一描述**数据关联与动态规则**；
4. 如何让**UI、校验、行为、识别、布局**等扩展能力进入同一元模型；
5. 如何让这些能力能被第三方以插件方式持续扩展，而不是被核心层写死。

因此，JSON 在 SchemaNode 中只是**一种表达和交换形式**，不是核心架构本身。

特别是：

> **JSON Schema 风格的应用层表达将主要在 `SchemaNode.App` 中引入，而不是 `SchemaNode.Core` 的中心任务。**

Core 层要解决的，是元数据平台的抽象模型与运行机制。

---

## 3. 平台设计的核心判断

SchemaNode 的核心判断可以概括为一句话：

> **Schema 的核心不应是固定字段集合，而应是 `Property` 组合机制。**

这意味着：

- schema type 可以扩展；
- schema property 也可以扩展；
- 第三方不只可以注册“新的数据模型”，也可以注册“定义数据模型的语言部件”；
- 元数据本身可以进入平台管理，成为可被发现、理解、组合和演化的对象。

这是 SchemaNode 能从一个开发库演进为平台的根本原因。

---

## 4. 第一核心：基于 Property 组合定义 Schema

### 4.1 Property 不是附属标签，而是基础构件

在 SchemaNode 中，`Property<T>` 不是某种“附加字段语法糖”，而是平台的基础能力单元。

一个 property 可以描述：

- 类型约束；
- 默认值；
- 是否必填；
- UI 展示；
- 布局语义；
- 主键与索引；
- 编译提示；
- 行为语义；
- 领域扩展规则。

换句话说，schema 的语义不是被硬编码在类定义里，而是通过 property 的组合逐步形成。

### 4.2 Property 组合优于固定字段穷举

传统 schema 系统通常会不断给核心类型增加新字段：

- 今天增加 `required`
- 明天增加 `displayName`
- 后天增加 `ui`
- 再后天增加 `layout`
- 最后再补 `validator`

这种方式的问题在于：

- 核心模型会越来越重；
- 很多能力只能由官方维护；
- 第三方扩展要么侵入核心，要么只能通过旁路配置实现；
- 不同扩展之间缺少统一依赖和覆盖规则。

SchemaNode 的做法是：

- 保持 schema 核心结构尽量稳定；
- 把扩展能力迁移到 property 系统；
- 用 `Depend` / `Override` / `ForSchema` / `ForType` 描述 property 之间和 property 与 schema 之间的关系；
- 让运行时统一解释这些组合语义。

这就是 SchemaNode 的开放式元模型基础。

---

## 5. 第二核心：Schema Property 本身也是平台对象

如果说“用 property 组合 schema”解决的是**表达能力**问题，那么“property 自身进入 schema 系统”解决的是**平台自解释能力**问题。

在 SchemaNode 中，property 本身会被注册为 `PropertySchema`。

这意味着系统能够理解：

- 一个 property 的名称是什么；
- 它的值类型是什么；
- 它适用于哪些 schema kind；
- 它适用于哪些值类型；
- 它依赖哪些 property；
- 它覆盖哪些 property；
- 它是静态 property 还是运行时参与行为的 property。

这使 SchemaNode 最终变成一个：

> **不仅管理业务元数据，也管理元数据定义本身的自解释平台。**

这是吸引生态共建的关键，因为平台参与者不再只能“使用元数据”，还可以“贡献元数据语言能力”。

---

## 6. NodeSchema 族的真正定位

在这个架构里，`NodeSchema` 族依然非常重要，但它的定位需要准确：

> `NodeSchema` 族不是平台唯一的中心，它是 SchemaNode 的**核心领域模型承载层**。

也就是说，它负责承载平台最核心的四类对象：

1. **数据类型**
2. **操作**
3. **属性定义**
4. **数据关联**

### 6.1 数据类型族

由以下 schema family 构成：

- `ScalarSchema`
- `EnumSchema`
- `StructSchema`
- `ArraySchema`

它们构成统一的数据类型系统，是所有扩展 schema family 的公共语义基础。

### 6.2 操作族

由 `FunctionSchema` 构成统一操作模型。

它不仅描述函数签名，还连接：

- 表达式 DSL；
- 编译器；
- 泛型推导；
- 系统函数；
- 远程函数；
- 多目标执行路径。

### 6.3 属性定义族

由 `PropertySchema` 承担，负责把 property 正式纳入元数据系统。

### 6.4 数据关联族

由 `RelationSchema` 承担，负责为已注册 property 提供动态联动与可变能力。

因此，从平台视角理解：

- `Property` 是扩展核心；
- `NodeSchema` 族是核心元数据领域模型；
- 两者结合，才构成完整的 SchemaNode 平台架构。

---

## 7. 第三核心：统一的数据类型、操作与关联模型

SchemaNode 并不是只解决“类型描述”问题。

它试图为未来所有第三方 schema family 提供一套共同底座：

### 7.1 统一数据类型模型

`Scalar / Enum / Struct / Array` 共同定义：

- 值是什么；
- 如何校验；
- 如何访问；
- 如何转换；
- 如何序列化；
- 如何组合为更复杂结构。

### 7.2 统一操作模型

`FunctionSchema` 与编译执行系统共同定义：

- 如何描述操作；
- 如何推导参数；
- 如何编译表达式；
- 如何执行系统函数、自定义函数和远程函数；
- 如何让不同 schema family 共享统一操作语言。

### 7.3 统一关联模型

`RelationSchema` 与 relation process 共同定义：

- property 如何联动；
- property 如何被动态计算；
- 校验、显示、默认值等行为如何在运行期联结；
- 扩展 schema family 如何复用统一动态规则机制。

这三个部分共同保证了一点：

> **未来无论第三方以 DLL、远程注册还是前端注册方式扩展 schema family，都不必重新发明自己的类型系统、操作系统和规则系统。**

---

## 8. 为第三方生态而设计的扩展机制

SchemaNode 从一开始就不是封闭式系统。

它在架构上预留了多个开放扩展面：

### 8.1 新的 schema family

第三方可以定义新的 schema kind，并注册：

- 对应 schema；
- 对应 runtime type；
- 对应 generator；
- 对应 provider；
- 对应 property 集合。

### 8.2 新的 schema properties

第三方可以为已有 schema family 增加：

- UI 属性；
- 识别属性；
- 审批属性；
- 布局属性；
- 校验属性；
- 行为属性；
- 与垂直领域相关的各种 property。

### 8.3 新的函数和编译能力

第三方可以引入：

- 新系统函数命名空间；
- 新表达式访问器；
- 新编译策略；
- 新远程函数执行端。

### 8.4 新的关系执行器

第三方可以引入自己的 relation process，使 property 联动从“约束”扩展到更丰富的动态行为。

这套扩展机制的目标非常明确：

> **让平台的价值随着参与者增加而提升，而不是随着核心层复杂度增加而失控。**

### 8.5 Schema Kind、Schema 来源与运行时生命周期

SchemaNode 需要区分两个容易被混淆的概念：

- **Schema Kind**：由系统在代码中定义和注册，是运行时识别 schema family 的固定入口；
- **Schema 实例**：某个具体的数据类型、函数、属性定义或关系定义，可以来自不同来源。

也就是说，kind 是平台语义骨架，schema 是平台运行时装载的具体内容。当前设计中：

1. `Schema Kind` 默认由系统代码定义；
2. 但第三方代码仍然可以扩展新的 kind、对应 property、runtime type 和 generator；
3. 具体 schema 可以同时来自：
   - 由代码反射/Meta 抽象出的 **system schema**；
   - 从数据库、中心 schema 管理服务或其他远端来源加载的 **动态 schema**；
4. system schema 与远端 schema 可以在 runtime 中合并使用。

这意味着 SchemaNode 并不是“所有 schema 都写死在代码里”的系统。相反，在平台落地时，**绝大多数实际执行的 schema 往往是动态定义的**，通常由前端配置、中心服务管理或运行中下发。

从运行时能力上，SchemaNode 的目标是支持：

- 实时加载；
- 实时更新；
- 实时卸载；
- system schema 与动态 schema 的按需合并；
- provider 驱动的远端获取与局部重建。

这也是平台能够支撑“配置即模型、模型即运行时”的关键前提。

---

## 9. 核心抽象关系图

下面这张图不是 UML，而是面向架构讨论的“核心抽象关系图”，用于快速说明 SchemaNode 平台中各层抽象如何协同：

```text
┌──────────────────────────────────────────────────────────────────────┐
│                         SchemaNode Platform                         │
├──────────────────────────────────────────────────────────────────────┤
│  Extension Axis A: Schema Families                                  │
│  Extension Axis B: Schema Properties                                │
└──────────────────────────────────────────────────────────────────────┘

				declares / constrains / augments
┌──────────────────────┐    attaches to    ┌──────────────────────────┐
│      Property<T>     │ ─────────────────▶│     ExtensibleSchema     │
│  IProperty metadata  │                   │  extension data carrier  │
└──────────────────────┘                   └─────────────┬────────────┘
														 │
														 │ materializes as
														 ▼
										┌──────────────────────────────────┐
										│         NodeSchema Family        │
										│ Scalar / Enum / Struct / Array  │
										│ Function / Property / Relation  │
										└────────────────┬─────────────────┘
														 │
								registered by            │ loaded by
														 ▼
						   ┌──────────────────────────────────────────────┐
						   │       SchemaRuntime / SchemaContext          │
						   │ kind registry / property registry / merge    │
						   └────────────────┬─────────────────────────────┘
											│
											│ interprets as executable model
											▼
						   ┌──────────────────────────────────────────────┐
						   │            NodeType / ValueType              │
						   │ compatibility / validation / references      │
						   └───────────────┬───────────────┬──────────────┘
										   │               │
										   │ creates       │ executes
										   ▼               ▼
						   ┌──────────────────────┐   ┌───────────────────┐
						   │       DataNode       │   │   FunctionType /  │
						   │ runtime value model  │   │  CompileContext   │
						   └──────────┬───────────┘   └─────────┬─────────┘
									  │                         │
									  │ linked dynamically by   │
									  └──────────┬──────────────┘
												 ▼
									  ┌─────────────────────────┐
									  │      RelationSchema     │
									  │ dynamic property rules  │
									  └─────────────────────────┘
```

这张图强调了五个关键事实：

1. `Property<T>` 是扩展起点，而不是附属注解；
2. `ExtensibleSchema` 是 property 组合的统一承载层；
3. `NodeSchema` 族负责承载核心元数据领域对象；
4. `SchemaRuntime` / `SchemaContext` / `NodeType` 负责把元数据解释为可执行运行时；
5. `FunctionSchema` 和 `RelationSchema` 使平台不仅能“描述”，还能“操作”和“联动”。

---

## 10. 运行时为什么重要

如果没有运行时，SchemaNode 仍然只是一个抽象元模型。

`SchemaNode.Core` 的重要价值之一，就是把这套元模型真正做成一个可执行系统。

### 9.1 `SchemaRuntime`

全局维护：

- schema kind 注册；
- property 注册；
- kind 到 runtime type 的映射；
- 系统 schema；
- 数组 schema 缓存；
- CLR 对应关系。

### 9.2 `SchemaContext`

负责：

- 按需加载；
- 泛型解析；
- provider 合并；
- 上下文对象；
- 生命周期内缓存。

### 9.3 `NodeType` / `ValueType`

负责把 schema 上的 property 组合语义翻译成运行时行为，包括：

- 类型兼容；
- 值创建；
- 值校验；
- 引用关系；
- 转换函数；
- 数组关联。

### 9.4 `DataNode`

负责承载运行期真实数据，使 schema 不只“被定义”，还能“被执行”。

这让 SchemaNode 平台拥有真正的平台级能力：

- 描述
- 装载
- 推导
- 校验
- 执行
- 联动

而不仅仅是“导出一个描述文件”。

---

## 11. 跨语言实现与分层边界

为了成为平台，SchemaNode 必须保持清晰的分层边界。

### 10.1 `SchemaNode.Core` 的职责

`SchemaNode.Core` 负责：

- 定义元模型；
- 定义 property 组合机制；
- 定义运行时加载和执行模型；
- 提供 C# 参考实现；
- 为其他语言实现提供语义基线。

### 10.2 `SchemaNode.App` 的职责

`SchemaNode.App` 更适合承载：

- JSON Schema 风格表达；
- App 级工作流语义；
- 更贴近业务产品的 UI/交互协议；
- 应用层装配逻辑。

### 10.3 前端 TypeScript 核心的意义

前端实现的目标，不是“简单解析后端吐出的 JSON”，而是：

- 共享同一套元模型；
- 共享同一套 property 解释逻辑；
- 共享同一套 schema family 扩展语义；
- 让平台的前后端真正围绕同一个元数据核心协同工作。

这也是 SchemaNode 真正具备平台潜力的关键标志之一。

### 10.4 遗留系统的语义化接入

SchemaNode 的一个重要平台价值，是可以通过 `Meta` 描述把既有系统中的：

- 类型；
- API；
- 微服务接口；
- 甚至其他语言实现的旧系统能力；

注册为 SchemaNode 中的数据类型和函数，也就是注册为 **system schema** 的一部分。

这意味着平台可以对旧系统完成一层“语义化转换”：

- 老类型可以被注册为 schema 数据类型；
- 老 API 可以被声明为 `FunctionSchema` 对应的系统函数调用；
- 微服务接口可以通过注册层适配后直接进入 schema 操作系统；
- 不同语言的遗留系统，只要存在一层可调用适配，就能被吸纳进统一语义模型。

它的意义在于：

> **SchemaNode 并不要求企业重写旧系统，而是允许旧系统先被“语义注册”，再逐步纳入统一平台。**

这让平台演进路径更现实，也更适合大型组织和历史系统复杂的企业环境。

### 10.5 面向 AI 的语义输出与临时应用

SchemaNode 中的 schema 天然是语义化对象，而不是仅面向某一语言运行时的内部结构。

因此，这些 schema 可以被进一步输出或映射为适合 AI 理解的结构，例如：

- MCP 能力描述；
- JSON Schema 风格结构；
- Ontology / 知识图谱语义结构；
- 面向特定模型的领域语义上下文。

这带来几个非常关键的可能性：

1. AI 可以基于 schema 语义理解系统能力，而不是只面对分散 API 文档；
2. AI 可以在语义层定制或修改模型，而不是直接改业务代码；
3. AI 可以在运行期生成临时应用、临时模型或临时流程，执行完成后销毁；
4. 应用本身也是数据，因此“执行时使用的应用 + 输入输出数据”可以一起作为日志沉淀下来；
5. 平台因此可以把旧 API 世界和 AI 语义化编程连接起来。

这种设计的重要意义之一，是降低“AI 直接改代码”带来的不可回归问题。

与让 AI 反复生成、修改、覆盖源代码相比，SchemaNode 更适合让 AI 工作在：

- 语义模型层；
- 应用装配层；
- 临时运行时层；
- 可审计、可回放的元数据执行层。

这为 AI 参与企业系统构建提供了更稳定的工程边界。

### 10.6 多端一体化配置与 API 收敛

SchemaNode 的统一元模型还有一个非常现实的收益：**多端一体化配置**。

当前大量业务系统在后端、前端、流程层、表单层、报表层之间存在重复建模和反复沟通。SchemaNode 试图把这些配置收敛到同一个语义核心上，从而带来：

- 开发沟通成本下降；
- 前后端对齐成本下降；
- 微服务之间接口语义更集中；
- 系统维护和二次开发成本下降。

在理想状态下，即使系统背后是复杂的微服务拓扑，对上层平台来说也不一定需要暴露海量 API，而是可以通过较少的一组稳定语义 API + 动态 schema 完成大部分业务编排。

这使平台兼具两方面优势：

1. 像低代码平台一样具备高抽象和快速装配能力；
2. 又不像传统低代码那样把扩展能力封死，仍然保留充分的二次开发和深度集成空间。

---

## 12. 为什么这会吸引高级架构参与者

高级架构人员通常不只关心“这个库能不能用”，而关心：

- 它是否有明确的抽象边界；
- 是否允许长期演进；
- 是否有稳定的扩展面；
- 是否支持生态共建；
- 是否可以承载多团队、多产品、多终端的协同。

SchemaNode 的吸引力正在于：

1. **它把元数据问题上升到了平台级抽象，而不是局部工具级抽象；**
2. **它同时开放 schema family 与 schema property 两条扩展轴；**
3. **它具备自解释能力，便于治理、注册、协作与生态发现；**
4. **它有运行时，而不仅有静态描述；**
5. **它天然适合跨语言、多端共享与插件化演进。**

这也是为什么 SchemaNode 的文档必须以“平台架构说明”的方式呈现，而不是简单停留在“库设计笔记”。

---

## 13. 对外架构结论

如果从对外平台展示的角度概括 SchemaNode，可以得出以下结论：

1. **SchemaNode 是一个语言无关的元数据平台内核，不是单一格式的 Schema 工具。**
2. **它的第一核心是 `Property` 组合定义 schema 的机制。**
3. **它的第二核心是 property 本身也被纳入 schema 管理，从而形成自解释平台。**
4. **`NodeSchema` 族承载数据类型、操作、属性定义和数据关联，是核心领域模型层。**
5. **`FunctionSchema` 和编译系统使平台具备统一操作语言。**
6. **`RelationSchema` 使已注册 property 获得动态联动能力。**
7. **平台扩展的重点不是不断修改核心字段，而是开放新的 schema family 和 schema property。**
8. **`SchemaNode.Core` 是当前最核心的 C# 参考实现，未来将与 TypeScript 核心共同构成跨语言生态。**

---

## 14. 总结

用一句话概括 SchemaNode 的平台价值：

> `SchemaNode` 正在构建的是一个以 `Property` 组合为核心、以 `PropertySchema` 实现自解释、以 `FunctionSchema` 提供统一操作、以 `RelationSchema` 提供动态联动、并允许第三方持续扩展 schema family 与 schema property 的跨语言元数据平台。

这份架构的意义，不只是让开发者“少写几份配置”，而是为未来的：

- 社区共建
- 插件生态
- 多端共享元模型
- 平台级治理
- 垂直领域语言扩展

提供一个可持续演进的统一基础。
