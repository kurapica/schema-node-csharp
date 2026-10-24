# 10. 语义运行时

前面介绍了 SchemaNode.Core 的核心概念与原型。本章最后介绍如何初始化语义运行时，以及如何通过运行时加载语义类型、创建和处理语义实体。

## 1. 语义运行时的生命周期

在 `SchemaNode.Example` 的 `Program.cs` 中，可以看到运行时的初始化过程：

```csharp
builder.Services.PrepareSchemaRuntime();

var app = builder.Build();

await app.Services.InitSchemaRuntimeAsync();
await app.Services.ActivateSchemaRuntimeAsync();
```

这三个方法分别负责准备、初始化和激活语义运行时，对应的实现位于 `SchemaNode.Core/Service/Service.cs`。

- **Prepare**：注册运行时所需的服务，并通知 `IRuntimeStageHandler` 实现进行准备工作。不同程序集可以通过这一机制参与运行时生命周期，为运行时扩展原型、语义类型及相关处理。
- **Init**：发现已注册的原型及其生成器，注册声明为 `SchemaType` 的语义类型，并通过生成器构造相应的语义声明。初始化过程包含 `SystemSchemaLoading`、`SystemSchemaLoaded`、`SchemaLoading` 和 `SchemaLoaded` 等阶段，各阶段通过 `IRuntimeStageHandler` 协同完成。
- **Activate**：通知各个生命周期处理器激活运行时。

运行时也提供对应的停用操作 `DeactivateSchemaRuntimeAsync`，用于通知各处理器执行停用逻辑。

同一服务可以承载多个语义运行时，各运行时可以拥有不同的原型和语义类型。因此，运行时不仅是静态注册表，也是特定语义定义与处理能力的承载环境。

## 2. 获取语义类型

运行时初始化并激活后，可以通过依赖注入获取 `ISchemaRuntime` 或 `ISchemaContext`。

通常，使用者更直接接触的是 `SchemaContext`。当前 `node` 原型族相关的运行时处理由它实现，通用的 `ISchemaContext` 接口本身并不保证提供这些能力。

例如，获取 `system.string` 对应的运行时类型：

```csharp
var schemaContext =
    app.Services.GetRequiredService<ISchemaContext>() as SchemaContext;

var runtimeType =
    await schemaContext.GetNodeTypeAsync("system.string");
```

`GetNodeTypeAsync` 根据语义类型名称获取相应的运行时类型。不同类型可以提供不同的运行时操作，例如 `FunctionType` 提供 `CallAsync` 来执行函数。

相关实现集中在 `Runtime` 目录中。运行时类型将前面介绍的语义定义转化为可供程序调用的操作能力。

## 3. 创建与处理数据语义实体

常见的数据语义类型包括 `node.bool`、`node.int` 和 `node.string` 等。它们不仅描述值的类型，还提供创建对应语义节点的能力。

例如，`IntType` 的核心实现如下：

```csharp
public sealed class IntType : ScalarType
{
    public override bool IsIndexable => true;

    public override IValueAccess Create(
        IValueAccess? parent = null,
        IPropertyProvider? propertyProvider = null)
        => new IntNode
        {
            Type = this,
            Parent = parent,
            PropertyProvider = propertyProvider ?? this
        };

    protected override ScalarSchema? GetScalarSchema()
        => GetProperty<IntProperty>()?.Value;
}
```

其中，`Create` 根据语义类型创建数据节点，并将类型、父节点和属性提供者等信息关联起来。不同数据语义类型通过各自的实现创建对应节点。

`IValueAccess` 提供数据节点的通用访问能力，包括路径访问、读取和设置值、判断空值，以及获取节点属性等。与数据约束相关的操作则用于处理语义声明中的校验要求。

例如，可以通过语义类型校验一个整数值：

```csharp
var runtimeType =
    await schemaContext.GetNodeTypeAsync("system.int");

var intNode =
    await runtimeType!.ValidateValueAsync(schemaContext, value);
```

校验结果可以通过 `IsValid` 判断，并通过 `GetViolatedConstraints` 获取违反的约束。

因此，数据节点可以理解为**携带语义类型与属性能力的 JSON 式数据实体**。它不只是保存值，还能够依据关联的语义声明执行相应的访问与校验操作。

数据节点并不要求所有语义属性都立即展开。后端通常只在需要时处理相关属性，以控制请求过程中的运行成本；而在前端等需要长期持有数据实体的环境中，可以持续展开属性和关联，从而在内存中形成更完整的语义空间。

## 4. 定义域语义类型

除了直接处理数据的类型，Core 还提供了用于定义和操作语义声明的辅助类型。它们主要位于 `system.schema` 定义域中，用于描述语义类型、访问类型信息，以及支持相关配置操作。

这部分实现以声明性代码为主，本章不再逐一展开。结合配置界面观察实际节点，再回到对应的类型定义中阅读，通常更容易理解它们的作用。

例如，在配置页面开启 Debug 模式后，可以查看节点实际使用的语义类型。命名空间选择节点使用的 `system.schema.namespace.type`，便可以作为进一步阅读源码的入口。

## 结尾

至此，SchemaNode.Core 的核心概念与运行时机制介绍完毕。

Core 提供了公共原型、语义类型加载机制，以及基于这些原型定义和构造语义类型所需的辅助设施。它本身不限定具体业务领域，而是为基于 C# 构建的不同方言提供共同的语义基础。

在这个基础上，方言可以声明自己的语义类型，并为其添加特定的执行能力。后续的 App 方言将进一步展示：当数据结构、属性、函数与关系已经由公共语义机制表达后，业务层可以在这些基础上组合出更具体的应用能力。

从实现方式看，Core 的主要机制并不依赖 C# 独有的语言特性；除函数最终编译为 `Delegate` 等实现细节外，其余核心概念均可以映射到其他语言。这也使现有实现能够作为在其他语言中构建语义运行时的参考。

SchemaNode.Core 是这套体系的语言底座。它负责建立语义定义与运行时之间的基本契约，而具体领域的语义积累和执行能力，则由其上的方言继续完成。