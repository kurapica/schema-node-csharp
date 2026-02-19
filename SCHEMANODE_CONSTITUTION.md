# SchemaNode 系统宪法文件

> **版本**: 1.0.0
> **创建日期**: 2026-02-18
> **目的**: 作为 SchemaNode 系统的权威架构参考，供 AI 快速理解和基于该系统构建应用

---

## 一、系统定位与核心价值

### 1.1 系统定位
SchemaNode 是一个**语义元数据管理框架**，作为连接 **API 软件世界** 和 **AI 智能体** 之间的**语义胶水层**。

### 1.2 核心价值主张
- **元数据驱动**: 从元数据管理出发，将 C# 类型和函数快速转换为语义 Schema
- **语义优先**: 通过 Schema 表达清晰的语义，使前端配置管理和 AI 理解成为可能
- **克制设计**: 引入简单的函数系统，避免功能过强导致的过度复杂化
- **函数语义化**: 函数系统更注重用函数表达语义，而非实现复杂逻辑
- **双向转换**: C# 类型 ↔ Schema ↔ 前端配置/AI 理解

### 1.3 最终目标
使 AI 能够：
1. 识别和理解 Schema 定义的语义
2. 在平台基础上构建类型和应用
3. 提供服务，实现从语义到应用的自动化

---

## 二、核心架构层次

### 2.1 架构分层

```
┌─────────────────────────────────────────────────────────┐
│               Schema API 协议层                          │
│    (JsonRpc, Default, Custom Protocols)                 │
├─────────────────────────────────────────────────────────┤
│               应用层 (Application Layer)                 │
│    AppType, AppFieldType, AppWorkflow                   │
├─────────────────────────────────────────────────────────┤
│               运行时层 (Runtime Layer)                   │
│    Type System, Function System, Workflow Engine        │
├─────────────────────────────────────────────────────────┤
│               Schema 定义层 (Schema Layer)               │
│    NodeSchema, TypeSystem, FunctionSchema               │
├─────────────────────────────────────────────────────────┤
│               组件层 (Components Layer)                  │
│    DataProvider, Event, Workflow, Policy                │
├─────────────────────────────────────────────────────────┤
│               基础设施层 (Infrastructure)                │
│    SchemaContext, DI, Storage Provider                  │
└─────────────────────────────────────────────────────────┘
```

### 2.2 核心上下文
- **SchemaContext**: 系统核心上下文，管理类型系统、应用系统、生命周期
- **WorkflowContext**: 工作流执行上下文，管理工作流状态和交互
- **SchemaNodeConfig**: 全局配置，包括时区、并发控制等

---

## 三、类型系统 (Type System)

### 3.1 类型层次结构

```
AnySchemaType (抽象基类)
├── ScalarType        - 标量类型 (int, string, bool, date, etc.)
├── EnumType          - 枚举类型 (支持层级枚举)
├── StructType        - 结构类型 (类似 C# class/struct)
├── ArrayType         - 数组类型 (带主键支持)
├── FunctionType      - 函数类型 (可执行的语义单元)
├── EventType         - 事件类型
├── WorkflowType      - 工作流类型
├── PolicyType        - 策略类型 (权限控制)
├── JsonType          - 动态 JSON 类型
└── TypeNamespace     - 命名空间类型
```

### 3.2 系统内置类型
- `system.bool`: 布尔类型
- `system.int`: 整数类型
- `system.string`: 字符串类型
- `system.list`: 列表类型
- `system.context`: 上下文类型

### 3.3 类型节点 (Type Nodes)
每个类型都有对应的节点表示运行时数据：
- `ScalarTypeNode`: 标量数据节点
- `EnumTypeNode`: 枚举值节点
- `StructTypeNode`: 结构数据节点 (包含字段集合)
- `ArrayTypeNode`: 数组数据节点
- `JsonTypeNode`: JSON 数据节点
- `AnySchemaNode`: 所有节点的基类

### 3.4 类型特性
- **继承**: StructType 支持 Base 继承
- **泛型**: 支持 T, T1, T2 等泛型参数
- **关系**: StructType 支持字段间关系定义 (Relations)
- **验证**: 支持字段级别的验证规则

---

## 四、函数系统 (Function System)

### 4.1 设计原则
**克制与语义优先**：
- 函数系统不追求图灵完备
- 更注重通过函数表达语义
- 避免复杂的控制流和副作用
- 支持函数式组合和管道

### 4.2 函数定义 (FunctionSchema)
```csharp
class FunctionSchema {
    string Return;              // 返回类型
    FuncArg[] Args;             // 参数列表
    FuncExp[] Exps;             // 表达式序列
    string[]? Generic;          // 泛型类型约束
    FuncTraits Flags;           // 函数特征标志
}
```

### 4.3 函数特征 (FuncTraits)
- **Converter**: 作为类型转换器
- **Server**: 需要服务器端执行
- **NoCache**: 客户端不应缓存结果
- **SideEffect**: 具有副作用
- **WorkflowOnly**: 仅用于工作流

### 4.4 内置函数命名空间
- `system.math`: 数学运算
- `system.str`: 字符串操作
- `system.logic`: 逻辑运算
- `system.date`: 日期时间处理
- `system.data`: 数据访问
- `system.conv`: 类型转换
- `system.collection`: 集合操作

### 4.5 表达式系统 (FuncExp)
支持多种表达式类型：
- **Literal**: 字面值
- **Variable**: 变量引用
- **FunctionCall**: 函数调用
- **Conditional**: 条件表达式
- **Pipeline**: 管道组合

### 4.6 函数调用
通过 `FunctionType.CallAsync<T>()` 执行函数，支持：
- 参数类型验证
- 泛型类型推断
- 异步执行
- 结果类型转换
- **上下文注入**: 函数可通过 SchemaContext 访问上下文项 (如用户信息、权限数据等)

#### **CompileContext 编译上下文**
SchemaNode 的函数系统核心特性之一是编译上下文机制：
- **语境感知**: 同一函数可以在不同语境（上下文）下编译为不同的实现
- **配置简单**: 配置人员只需关注"做什么"，而非"怎么做"
- **语义优先**: 函数表达清晰的业务语义，实现细节由编译期决定

**CompileContext 应用示例**：
```
场景: system.data.query(app, field, filter)
- 开发环境: 编译为带详细日志的查询
- 生产环境: 编译为优化的查询
- 测试环境: 编译为使用 Mock 数据的查询
```

---

## 五、应用系统 (Application System)

### 5.1 应用类型 (AppType)
应用是 Schema 系统的数据容器：
```csharp
class AppType {
    string Name;                    // 应用名称
    AppScopePolicy? ScopePolicy;    // 作用域策略
    PolicyType? Auth;               // 认证策略
    PolicyItem[]? Auths;            // 数据级权限
    AppRelationSchema[]? Relations; // 字段关系
    AppFieldType[] Fields;          // 应用字段
    AppWorkflowType[] Workflows;    // 工作流
}
```

### 5.2 应用字段 (AppFieldType)
应用字段是应用的数据模型：
- **类型**: 关联到 Schema Type
- **存储**: 支持动态表存储 (EnableDynamicTable)
- **单例**: Single 标记单值字段
- **主键**: 通过 ArrayType.Primary 定义
- **过滤**: FieldFilterConfig 定义查询过滤规则
- **关系**: 字段间的关系映射

#### **动态类型字段 (system.json)**
通过 `system.json` 类型配合 `Relations` 实现动态类型机制：

**示例**：
```csharp
class Order {
    [Schema("system.enum.producttype")]
    string ProductType;         // 商品类型枚举

    [Schema("system.json")]
    JsonNode? ProductDetails;   // 商品详情 (动态类型)
}

// Relations 配置
Relations: [
    {
        Field: "ProductDetails",
        Type: RelationType.Type,
        Target: "ProductType"    // 根据 ProductType 确定实际类型
    }
]
```

**效果**：
- 当 `ProductType = "Physical"` 时，`ProductDetails` 使用 "PhysicalProductSchema"
- 当 `ProductType = "Virtual"` 时，`ProductDetails` 使用 "VirtualProductSchema"
- 前端自动渲染对应的表单结构
- EAV 存储时自动确定正确的 attribute 类型

### 5.3 作用域策略 (AppScopePolicy)
定义应用数据的隔离范围：
- **BusinessTarget**: 业务目标隔离
- **IsolationContext**: 上下文项隔离 (如租户、用户)
- **Global**: 全局共享
- **Dynamic**: 动态计算

### 5.4 应用数据操作
通过 `IAppDataProvider` 接口：
- `QueryDynamicTableAsync`: 查询数据
- `SaveDynamicTableDataAsync`: 保存数据
- `DeleteDynamicTableDataAsync`: 删除数据
- `ClearDynamicTableDataAsync`: 清空数据
- 支持事务: BeginTransaction, Commit, Rollback

---

## 六、工作流系统 (Workflow System)

### 6.1 工作流概念
工作流是应用内的业务流程编排：
- **节点化**: 每个 Workflow 是一个节点
- **有向图**: 通过 Previous/Next 连接
- **状态��理**: WorkflowContext 管理执行状态
- **Fork 支持**: 支持并行分支
- **持久化**: IWorkflowContextPersistence 接口

### 6.2 工作流定义 (AppWorkflowSchema)
```csharp
class AppWorkflowSchema {
    string Name;                // 工作流名称
    string? Payload;            // 负载类型
    string[]? Previous;         // 前置工作流
    string[]? Next;             // 后续工作流
    bool Fork;                  // 是否支持分叉
    string[]? ForkKey;          // 分叉键
    bool UnCancelable;          // 不可取消
    bool CancelPre;             // 取消前置
}
```

### 6.3 工作流执行
```csharp
abstract class Workflow {
    // 核心方法签名:
    // Task ProcessAsync(WorkflowContext context, ...args)
    // Task<Session> ProcessAsync<Session>(WorkflowContext context, Session, ...args)
}
```

### 6.4 工作流调度
- **DefaultWorkflowScheduler**: 使用 Quartz.NET 调度
- **延时触发**: 支持定时和延时启动
- **事件触发**: 通过 Event 系统触发
- **手动触发**: 通过 API 触发

---

## 七、事件系统 (Event System)

### 7.1 事件架构
```
Event (抽象基类)
  ↓
IEventDispatcher<TE>
  ↓
IEventSource (Kafka, RabbitMQ, Custom)
```

### 7.2 事件定义
```csharp
abstract class Event {
    AnySchemaNode? Payload;     // 事件负载
    DateTime Timestamp;          // 事件时间戳
}
```

### 7.3 事件操作
- **发布**: `context.RaiseEvent<TE>(payload)`
- **订阅**: `context.SubscribeEvent<TE>(handler)`
- **分发**: 通过 `IEventDispatcher` 自动路由

### 7.4 事件源集成
- **SchemaNode.Kafka**: Kafka 事件源
- **SchemaNode.RabbitMQ**: RabbitMQ 事件源
- 自定义: 实现 `IEventSource`

---

## 八、存储与数据提供者

### 8.1 Schema 存储 (ISchemaStorageProvider)
负责 Schema 定义的持久化：
- `LoadSchemaAsync`: 加载 Schema 定义
- `SaveSchemaAsync`: 保存 Schema 定义
- `DeleteSchemaAsync`: 删除 Schema
- **DynamicSchemaStorageProvider**: 使用应用数据表存储 Schema

### 8.2 应用数据提供者 (IAppDataProvider)
负责应用数据的 CRUD：
- **AppDataMySqlProvider**: MySQL 实现
- **InMemoryAppDataProvider**: 内存实现 (测试用)
- 动态表: 根据 AppFieldType 自动创建表结构
- 查询: 支持过滤、排序、分页
- 事务: 支持跨表事务

### 8.3 动态表 (DynamicTable)
```csharp
class DynamicTableSchema {
    string TableName;               // 表名
    AppFieldType AppFieldType;      // 字段类型
    bool Single;                    // 是否单值
    string[] PrimaryKeys;           // 主键字段
    FieldStorageTopology Topology;  // 存储拓扑
}
```

---

## 九、特性标注系统 (Attribute System)

### 9.1 Schema 标注
用于将 C# 类型转换为 Schema：

#### **@SchemaAttribute**
- 标记类/枚举为 Schema 类型
- 指定命名空间和显示名

#### **@SchemaAppAttribute**
- 标记类为应用数据模型
- 指定应用名和字段名

### 9.2 函数标注

#### **@ConverterAttribute**
- 标记函数为类型转换器

#### **@ServerOnlyAttribute**
- 函数必须在服务器端执行

#### **@NoCacheAttribute**
- 客户端不应缓存结果

#### **@SideEffectAttribute**
- 函数有副作用

#### **@WorkflowOnlyAttribute**
- 函数仅用于工作流

### 9.3 字段标注

#### **@DefaultAttribute**
- 字段默认值

#### **@IndexAttribute**
- 数据库索引标记

---

## 十、API 协议层

### 10.1 协议抽象 (ISchemaApiProtocol)
支持多种 API 协议：
- **DefaultSchemaApiProtocol**: 标准 HTTP JSON API
- **JsonRpcSchemaApiProtocol**: JSON-RPC 2.0 协议
- 自定义: 实现 `ISchemaApiProtocol`

### 10.2 API 基类
```csharp
abstract class SchemaApi<TRequest, TResponse> {
    protected SchemaContext SchemaContext;
    protected Task<TResponse?> ExecuteAsync(TRequest, CancellationToken);
}
```

### 10.3 内置 API

#### Schema 管理 API
- `LoadSchema`: 加载 Schema 定义
- `SaveSchema`: 保存 Schema
- `LoadEnumSubList`: 加载枚举子列表
- `CallFunction`: 调用函数

#### 应用数据 API
- `BatchQueryAppData`: 批量查询应用数据
- `PushAppData`: 推送应用数据
- `GetSourceTarget`: 获取数据源目标
- `SetSourceTarget`: 设置数据源目标

#### 工作流 API
- `WorkflowInfo`: 获取工作流信息
- `Interaction`: 工作流交互

---

## 十一、MCP (Model Context Protocol) 集成

### 11.1 SchemaNode.McpHost
为 AI 提供 MCP 协议支持，使 AI 能够：
- 浏览 Schema 类型系统
- 调用函数
- 操作应用数据
- 管理 Schema 定义

### 11.2 MCP Tools
```csharp
[McpServerToolType]
class SchemaTools {
    LoadSchema(name)                    // 浏览 Schema
    LoadEnumSubList(name, value)        // 加载枚举
    LoadEnumAccessList(name, value)     // 加载枚举值的访问链
    CallFunction(functionName, args)    // 调用函数
    SaveSchema(nodeSchema)              // 保存 Schema
    SaveEnumSubList(name, value, list)  // 保存枚举子列表
    LoadApplication(app)                // 加载应用
    SaveApplication(appSchema)          // 保存应用
    QueryAppData(app, field, filter)    // 查询应用数据
    SaveAppData(app, field, data)       // 保存应用数据
}
```

---

## 十二、策略与权限系统 (Policy System)

### 12.1 策略类型 (PolicyType)
用于权限控制和认证：
- **函数策略**: 返回 bool 的函数
- **参数**: 可接收上下文参数
- **组合**: 支持 AND/OR 逻辑组合

### 12.2 应用级权限
- **Auth**: 应用级认证策略
- **Auths**: 数据级权限策略数组
- **PolicyItem**: 策略项，包含策略函数和参数

### 12.3 Schema 级权限
- **NodeSchema.Auth**: Schema 类型的访问控制

---

## 十三、上下文项系统 (Context Item)

### 13.1 ISchemaContextItem
提供上下文数据注入，使函数能够访问运行时上下文：
```csharp
interface ISchemaContextItem {
    string ItemName { get; }
    Task<AnySchemaNode?> GetContextItemAsync(SchemaContext);
}
```

### 13.2 设计目的
上下文项系统主要用于：
- **前端配置函数时访问上下文**: 当在前端配置界面定义函数调用时，可以引用上下文数据
- **运行时注入**: 函数执行时自动注入上下文项数据
- **项目定制**: 具体的上下文项由使用 SchemaNode 的项目决定和定义

### 13.3 典型上下文项示例
- **Access**: 访问上下文 (Target, User, etc.)
- **UserInfo**: 用户信息
- **Tenant**: 租户信息
- **Permission**: 权限数据
- 自定义: 根据业务需求实现 `ISchemaContextItem`

### 13.4 使用场景
- **权限校验**: 基于当前用户进行权限验证
- **数据隔离**: 根据租户或用户范围过滤数据
- **业务逻辑**: 在函数中获取业务相关的上下文信息
- **审计追踪**: 记录操作者信息

---

## 十四、依赖注入与生命周期

### 14.1 服务注册
```csharp
services
    .AddSchemaNode<TProtocol>(config)
    .AddSchemaStorageProvider<TStorage>()
    .AddAppSchemaDataProvider<TProvider>()
    .AddSchemaMcpHost();
```

### 14.2 生命周期
- **Singleton**:
  - ICriticalRegionProvider
  - IExpVisitor
  - ILoggerFactory
- **Scoped**:
  - SchemaContext (核心)
  - ISchemaContextItem
- **Transient**:
  - WorkflowContext
  - ISchemaApiProtocol

### 14.3 程序集扫描
自动扫描标记的程序集：
- `AddSchemaAssemblies(assemblies)`
- 自动注册: Schema 类型、函数、API

---

## 十五、扩展模块

### 15.1 SchemaNode.Excel
Excel 模板导入导出：
- `ExcelTemplate`: 模板定义
- `TemplateManager`: 模板管理
- 与 AppFieldType 集成

### 15.2 SchemaNode.Kafka
Kafka 事件集成：
- `KafkaEventSource`: Kafka 事件源
- `@KafkaTopicAttribute`: Topic 标注

### 15.3 SchemaNode.RabbitMQ
RabbitMQ 事件集成：
- `RabbitEventSource`: RabbitMQ 事件源
- `@RabbitQueueAttribute`: Queue 标注
- `@RabbitBindingAttribute`: Binding 标注

### 15.4 SchemaNode.MySql
MySQL 数据提供者：
- `AppDataMySqlProvider`: MySQL 应用数据实现
- `MySqlProvider`: 基础 MySQL 访问

---

## 十六、关键设计模式

### 16.1 类型安全的动态系统
- 编译时: C# 类型系统
- 运行时: Schema 类型系统
- 双向转换: Attribute → Schema → Node

### 16.2 延迟加载
- Schema 类型按需加载
- 应用容器按需加载
- 枚举子列表按需加载

### 16.3 缓存策略
- SchemaContext 级别缓存
- ConcurrentDictionary 保证线程安全
- 可重置缓存: `ResetTypeNamespace()`

### 16.4 错误处理
- SchemaNodeStatus: 状态码表示错误
- 验证: 类型加载时验证引用
- 友好错误: LocaleString 支持多语言

---

## 十七、AI 集成最佳实践

### 17.1 Schema 识别
AI 应该：
1. 从根命名空间开始浏览 `LoadSchema("")`
2. 递归加载子 Schema
3. 识别类型关系 (Base, Fields, Relations)
4. 理解函数语义 (通过命名和 Display)

### 17.2 应用构建
AI 可以：
1. 创建 AppSchema 定义应用
2. 定义 AppFieldSchema 设计数据模型
3. 创建 AppWorkflowSchema 编排业务流程
4. 调用 `SaveApplication` 持久化

### 17.3 数据操作
AI 可以：
1. 查询应用数据: `QueryAppData`
2. 保存数据: `SaveAppData`
3. 调用函数: `CallFunction`
4. 触发工作流: 通过事件或 API

### 17.4 语义理解
- **Display**: LocaleString 提供多语言显示名
- **Desc**: 描述信息
- **函数名**: 语义化命名 (如 `getappdata`)
- **命名空间**: 分层语义组织 (如 `system.data`)

---

## 十八、系统约束与限制

### 18.1 函数系统限制
- **非图灵完备**: 不支持复杂循环和递归
- **无状态**: 函数应避免副作用
- **类型安全**: 严格的类型检查

### 18.2 应用系统限制
- **主键必需**: ArrayType 字段必须定义 Primary
- **作用域不可变**: ScopePolicy 定义后难以更改
- **关系约束**: Relations 必须引用存在的字段

### 18.3 性能限制
- **批量操作**: MAX_COMBINE_CASE_COUNT 限制组合查询
- **并发**: Quartz MaxConcurrency 配置
- **缓存**: NoCache 标记函数不缓存

---

## 十九、开发指南

### 19.1 创建自定义 Schema 类型
```csharp
[Schema("myns.mytype")]
public class MyType {
    [Schema("system.string")]
    public string Name { get; set; }
}
```

### 19.2 创建应用数据模型
```csharp
[SchemaApp(app: "myapp", field: "users")]
public class User {
    [Key]
    public string UserId { get; set; }
    public string UserName { get; set; }
}
```

### 19.3 创建函数
```csharp
// 简单函数
[Schema("myns.myfunc")]
public static int Add(int a, int b) => a + b;

// 带上下文访问的函数 (用于权限校验等)
[Schema("myns.checkpermission")]
public static bool CheckPermission(SchemaContext context, string resource) {
    var userInfo = context.GetSchemaContextItem("userinfo");
    // 基于用户信息进行权限验证
    return true; // 权限验证逻辑
}
```

### 19.4 创建工作流
```csharp
public class MyWorkflow : Workflow {
    public async Task ProcessAsync(WorkflowContext context, string input) {
        // 业务逻辑
        SetPayload(context, result);
    }
}
```

### 19.5 创建 API
```csharp
public class MyApi : SchemaApi<MyRequest, MyResponse> {
    protected override async Task<MyResponse?> ExecuteAsync(
        MyRequest request, CancellationToken cancellationToken) {
        // API 逻辑
    }
}
```

---

## 二十、技术栈

### 20.1 核心依赖
- **.NET 9.0**: 目标框架
- **ASP.NET Core**: Web 框架
- **Microsoft.Extensions.DependencyInjection**: DI 容器
- **Microsoft.Extensions.Logging**: 日志
- **System.Reactive**: 响应式扩展

### 20.2 调度与任务
- **Quartz.NET**: 任务调度
- **Quartz.Extensions.Hosting**: 托管集成

### 20.3 API 文档
- **Swashbuckle.AspNetCore**: Swagger/OpenAPI

### 20.4 工具库
- **TimeZoneConverter**: 时区转换

### 20.5 数据库
- **MySqlConnector**: MySQL 驱动 (通过 SchemaNode.MySql)

---

## 二十一、系统初始化流程

### 21.1 启动顺序
```
1. AddSchemaNode<T>() 注册服务
   ↓
2. 注册程序集 (RegisterAssemblys)
   ↓
3. 扫描并注册 Schema 类型和函数
   ↓
4. UseSchemaApis() 注册 API 端点
   ↓
5. PreLoadSchemaNodes() 预加载 Schema
   ↓
6. InitSystemContextAsync() 初始化系统类型
```

### 21.2 SchemaContext 生命周期
```
每次请求:
  1. 创建 SchemaContext (Scoped)
  2. 注入 IServiceProvider
  3. 设置 Locale (可选)
  4. 延迟加载 Schema 类型
  5. 执行业务逻辑
  6. 销毁 SchemaContext
```

---

## 二十二、命名约定

### 22.1 Schema 命名
- **命名空间**: 小写点分隔 (如 `system.data`)
- **类型**: 驼峰命名 (如 `nodeschema`)
- **函数**: 小写无分隔符 (如 `getappdata`)

### 22.2 C# 命名
- **类**: PascalCase (如 `SchemaContext`)
- **方法**: PascalCase (如 `GetSchemaTypeAsync`)
- **字段**: camelCase (如 `_loggerThunk`)

### 22.3 常量命名
- **全大写下划线**: UPPER_SNAKE_CASE
- **命名空间前缀**: NS_ (如 `NS_SYSTEM_DATA`)
- **正则表达式前缀**: REGEX_ (如 `REGEX_GENERIC_TYPE`)

---

## 二十三、测试与调试

### 23.1 示例项目
- **SchemaNode.Example**: 完整示例应用
- 包含: MySQL 集成、Swagger、MCP Host
- 配置: appsettings.json

### 23.2 内存测试
```csharp
services.AddAppSchemaDataProvider<InMemoryAppDataProvider>();
```

### 23.3 调试工具
- **Swagger UI**: Schema API 测试
- **MCP Host**: AI 交互测试
- **日志**: ILogger<T> 集成

---

## 二十四、性能优化

### 24.1 缓存策略
- Schema 类型缓存在 SchemaContext
- 函数调用结果可选缓存 (NoCache 控制)
- 枚举值缓存

### 24.2 并发控制
- **ICriticalRegionProvider**: 分布式锁
- **Quartz MaxConcurrency**: 调度并发
- **ConcurrentDictionary**: 线程安全缓存

### 24.3 查询优化
- **分页**: skip/take 参数
- **索引**: IndexAttribute 标记
- **批量操作**: BatchQueryAppData

---

## 二十五、安全考虑

### 25.1 认证与授权
- **PolicyType**: 函数级权限控制
- **Auth**: 应用/Schema 级认证
- **Auths**: 数据级权限数组

### 25.2 数据隔离
- **AppScopePolicy**: 多租户隔离
- **ISchemaContextItem**: 上下文注入
- **Target**: 数据目标隔离

### 25.3 输入验证
- **类型验证**: Schema 类型系统
- **字段验证**: StringLength, Required 等
- **函数参数验证**: 自动类型检查

---

## 二十六、未来扩展方向

### 26.1 编译上下文语境扩展
基于现有的 CompileContext 机制，未来可以扩展更多语境支持：
- **多数据源语境**: 根据数据规模自动选择单库或分库分表
- **性能分析语境**: 根据实时性能指标动态调整查询策略
- **安全级别语境**: 根据数据敏感度编译不同的加密和脱敏逻辑
- **地域语境**: 根据用户地理位置编译本地化的业务规则

### 26.2 动态类型机制扩展
基于现有的 system.json + RelationType.Type 机制，可以扩展：
- **多层级类型推断**: 支持多级字段关系的类型推断
- **类型推断缓存**: 优化类型推断性能
- **类型变更追踪**: 记录和追踪动态类型的变更历史
- **类型建议系统**: AI 辅助推荐合适的类型映射关系

### 26.3 函数系统语义增强
在保持简单的基础上，增强语义表达能力：
- **参数校验增强**: 更丰富的内置参数类型和范围校验
- **语义组合模式**: 预定义的函数组合模式库
- **声明式验证**: 更强大的声明式数据验证能力
- **函数文档生成**: 自动从函数签名生成文档和示例

### 26.4 AI 集成深化
- **自然语言查询**: NL to Schema Query，AI 理解语义后生成配置
- **Schema 推荐**: AI 辅助 Schema 设计，建议字段和关系
- **自动优化**: AI 分析使用模式优化数据模型
- **语义理解增强**: AI 通过 CompileContext 理解和建议更多语境
- **配置生成**: AI 根据需求描述自动生成完整的应用配置

### 26.5 配置体验优化
- **可视化配置器**: 图形化的 Schema 和应用配置界面
- **智能提示**: 基于上下文的智能提示和自动完成
- **配置验证**: 实时验证配置有效性，提供友好错误提示
- **配置模板**: 常见场景的配置模板和最佳实践
- **版本管理**: Schema 和应用配置的版本控制和回滚

### 26.6 性能与可观测性
- **查询优化器**: 自动分析和优化复杂查询
- **性能监控**: 实时监控 Schema 操作性能
- **分布式追踪**: 跨服务的请求追踪和性能分析
- **智能预警**: 基于历史数据的性能预警和建议

---

## 二十七、关键接口清单

### 27.1 核心接口
```csharp
ISchemaApiProtocol          - API 协议抽象
ISchemaStorageProvider      - Schema 存储
IAppDataProvider            - 应用数据提供者
IEventDispatcher<TE>        - 事件分发器
IEventSource                - 事件源
ISchemaContextItem          - 上下文项
ICriticalRegionProvider     - 临界区提供者
IWorkflowScheduler          - 工作流调度器
IWorkflowContextPersistence - 工作流持久化
IExpVisitor                 - 表达式访问器
```

### 27.2 数据接口
```csharp
AnySchemaType               - 类型基类
AnySchemaNode               - 节点基类
StructType                  - 结构类型
ArrayType                   - 数组类型
FunctionType                - 函数类型
AppType                     - 应用类型
AppFieldType                - 应用字段类型
```

---

## 二十八、快速参考

### 28.1 获取 Schema 类型
```csharp
AnySchemaType? type = await context.GetSchemaTypeAsync("namespace.typename");
```

### 28.2 获取应用类型
```csharp
AppType? app = await context.GetAppTypeAsync("appname");
```

### 28.3 调用函数
```csharp
FunctionType func = await context.GetSchemaTypeAsync<FunctionType>("func.name");
var result = await func.CallAsync<int>(context, arg1, arg2);
```

### 28.4 查询应用数据
```csharp
var (data, total) = await context.GetAppFieldDataAsync(
    fieldType, target, AppSchemaDataResult.List, filter, skip, take);
```

### 28.5 发布事件
```csharp
context.RaiseEvent<MyEvent>(payload);
```

### 28.6 订阅事件
```csharp
context.SubscribeEvent<MyEvent>(async (ctx, evt) => {
    // 处理事件
});
```

---

## 二十九、故障排查

### 29.1 常见状态码
- `SchemaNodeStatus.Ready`: 正常
- `SchemaNodeStatus.NoDefinition`: 未定义
- `SchemaNodeStatus.StructWrongBase`: 错误的基类
- `SchemaNodeStatus.StructMemberWrongType`: 字段类型错误
- `SchemaNodeStatus.ApplicationInvalidField`: 应用字段无效

### 29.2 日志级别
```csharp
context.LogInformation(message);
context.LogWarning(message);
context.LogError(exception, message);
```

### 29.3 调试技巧
1. 检查 Schema LoadState
2. 验证类型引用 (Namespace, Base, Field Type)
3. 查看 Status 字段
4. 启用详细日志

---

## 三十、版本控制

### 30.1 当前版本
- **SchemaNode**: 1.0.0
- **Target Framework**: net9.0
- **API Version**: v1

### 30.2 兼容性
- Schema 定义向后兼容
- API 协议版本化
- 数据迁移支持 (通过应用数据)

---

## 附录 A: 核心常量

```csharp
// Schema 命名空间
NS_SYSTEM_BOOL              = "system.bool"
NS_SYSTEM_INT               = "system.int"
NS_SYSTEM_STRING            = "system.string"
NS_SYSTEM_LIST              = "system.list"
NS_SYSTEM_CONTEXT           = "system.context"
NS_SYSTEM_DATA              = "system.data"
NS_SYSTEM_SCHEMA_VALUE_TYPE = "system.schema.valuetype"
NS_SYSTEM_SCHEMA_FUNC_TYPE  = "system.schema.functype"
NS_SYSTEM_SCHEMA_APP        = "system.schema.app"
NS_SYSTEM_SCHEMA_POLICY_TYPE= "system.schema.policytype"

// 表名和字段
ENTITY_PRIMARY_KEY_MAX_LEN  = 128

// 限制
MAX_COMBINE_CASE_COUNT      = 限制组合查询数量
```

---

## 附录 B: 关键枚举

```csharp
SchemaType              - Namespace, Scalar, Enum, Struct, Array, Func, Event, Workflow, Policy, Json
SchemaNodeStatus        - Ready, NoDefinition, Loading, Error...
AppScopeType            - BusinessTarget, IsolationContext, Global, Dynamic
FuncTraits              - Converter, Server, NoCache, SideEffect, WorkflowOnly
SchemaLoadState         - System, Server, Client
FieldStorageTopology    - Normal, SingleRecord, NoStorage
DateFormatMode          - ISO8601, Timestamp, Custom
LogicType               - And, Or, Not, Equal, Contains...
RelationType            - Type, Source, Filter... (字段关系类型，其中 Type 用于动态类型推断)
```

---

## 结语

SchemaNode 是一个**语义优先、克制设计、AI 友好**的元数据管理框架。

它的核心价值在于：
1. **C# 到 Schema 的无缝转换**
2. **简单而富有表达力的函数系统**
3. **为 AI 设计的语义层**
4. **灵活的应用数据模型**
5. **可扩展的架构**
6. **动态类型机制** (system.json + RelationType.Type)
7. **编译上下文** (CompileContext - 同一函数不同语境不同实现)

通过本宪法文件，AI 应该能够：
- 快速理解 SchemaNode 的设计哲学
- 识别和操作 Schema 类型系统
- 构建应用和工作流
- 通过 MCP 协议与系统交互
- 基于 SchemaNode 开发新功能
- 理解动态类型和编译上下文的强大语义能力

**记住**: SchemaNode 不是要成为一个全能框架，而是要成为**连接语义和实现的桥梁**。它通过简单的配置表达复杂的语义，通过编译期处理隐藏实现复杂性，让配置保持简单快速。

