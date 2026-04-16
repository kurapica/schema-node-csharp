# Schema 系统设计

Schema 系统的设计目标是提供一个灵活、可扩展且高效的数据描述系统，用于对**数据结构**和**关联**的复杂描述进行建模，进而提供强大的数据验证、转换
和查询功能。

Schema 是跨平台的中间描述，通常采用JSON格式进行定义和传输。它可以在多端进行实现，基于统一的 Schema 语义描述，实现多端一体化开发模式，例如在
前端基于 Schema 实现自动渲染，数据自动关联和校验，后端则基于 Schema 实现数据校验，转换，汇总和自动存储等功能。

为了统一和规范 Schema 的定义和使用，C# 端提供了一个基于属性系统的 Schema 定义框架和一组 Schema 的实现，在C#中定义的各种类型，会按照语义功能
注册为不同的 Schema 类型以供使用和实现 C# 端的混合开发模式。开发者可以直接使用默认的 Schema 实现或者采用该框架进行自定义 Schema 的开发，C# 
输出的 Schema 定义会被转换成 JSON 格式的 Schema 定义，供前端和其它平台使用。

输出的 Schema 包含两部分，一部分是 Schema 的原型定义，包含 Schema 的核心属性和行为定义，并提供系统级的 Schema 定义，这部分构成 Schema 的
运行时基础框架，这部分会输出为文件供其他端（前端）使用。

另一方部分是基于原型和系统 Schema 扩展定义的 Schema，这部分由开发者根据业务需求进行定义和扩展，构成 Schema 的业务层定义，通常在前端配置界面
中定义，并作为数据保存和传播，通常这部分会基于 API 进行传输和管理。

Schema 系统采用了分层设计的架构，通过划分不同的抽象层级，使系统和第三方开发者能够在不同的层级上进行开发和扩展，同时保持系统的整体一致性和性能。

## 1. 抽象层级设计

### 1.1 Property 属性系统

Property 是 Schema 系统中的核心概念，代表了数据的基本单位。每种 Schema 的功能和行为都由其属性定义。属性系统支持多种数据类型和复杂的关系，
使得 Schema 可以灵活地表示各种数据结构。同时属性基于组合模式设计，核心只提供基础的属性类型和操作，开发者可以根据需要添加新的属性类型和功能。

以单位描述Unit为例，C#定义代码如下：

```csharp
[Meta<ForSchema>("scalar")] // 申明该属性是 Scalar 标量Schema类型的扩展属性
public class Unit : Property<LocaleString>; // 定义Unit扩展属性，它的类型是LocaleString本地化字符串
``

抽象后对应的一个 Property Schema 定义如下：

```json
{
    "name": "unit", // 属性名称
    "namespace": "system.schema.property", // 属性命名空间
    "kind: "property", // Schema Kind，表示这是一个属性Schema
    "property": {
        "for": [
            "scalar" // 该属性适用于哪些Schema类型，这里表示适用于Scalar标量Schema
        ],
        "type": "system.localestring" // 属性值的类型，这里表示属性值是一个LocaleString本地化字符串
    }
}
```

基于 Property 属性系统，开发者可以为任意的 Schema 类型进行动态属性扩展，例如前端可以定义 Color, Layout 之类的属性来控制组件的样式和布局，
而前后端可以共用一组 Constraint 约束属性来控制数据的校验规则和范围等。

### 1.2 Schema 类型系统

Schema 类型系统定义了各种 Schema 的核心属性和行为，Schema 可以作为其它 Schema 的属性进行组合和嵌套，从而形成复杂的数据结构描述。Core项目
提供了一整套基础的 Schema 类型定义，它定义了基础的 NodeSchema 作为所有 Schema Type的容器，通过属性系统将其它实际的 Schema 类型注册到 
NodeSchema 中，类似

```json
{
    "name": "example", // Schema名称
    "namespace": "system.schema", // Schema命名空间
    "kind": "scalar", // Schema Kind，实际类型
    "scalar": {}, // 标量Schema的核心属性和行为定义
    "enum": {}, // 枚举Schema的核心属性和行为定义
    "struct": {}, // 结构体Schema的核心属性和行为定义
    "array": {}, // 数组Schema的核心属性和行为定义
    "func": {} // 函数Schema的核心属性和行为定义
    "property": {} // 属性Schema定义
}
```

通过 Schema 和 Property 的组合，开发者可以定义出各种复杂的数据结构和关联关系，例如一个表单组件的 Schema 定义可能包含多个字段，每个字段又包含
不同的属性来描述其类型、验证规则、显示样式等。这部分构成的数据的静态描述。

为了实现数据的动态行为，同时提供一种 RelationSchema，它的描述如下：

```json
{
    "target": "scalar", // 关系的目标，通常是schema的字段访问路径等
    "prop": "visible", // 关系的属性，通常是目标的一个属性，用 Property 系统定义
    "stage": "load|input", // 关系的执行阶段，load代表在数据加载时执行，input代表在数据输入时执行
    "kind": "call", // 关系的执行方式，Call代表调用函数，而函数的参数会表明关联的来源
    "call": {
      "func": "system.logic.equal", // 关系调用的函数，这里表示一个等于函数
      "args": [ // 函数的参数列表，这里表示函数的参数是一个字段访问路径和一个常量值
        {
          "source": "kind" // 参数1，表示访问路径，这里指向kind属性
        },
        {
          "value": "scalar" // 参数2，表示一个常量值
        }
    }
}
```

这个规则描述了，当kind的值等于scalar时，scalar字段的visible属性为true，否则为false。通过 RelationSchema，开发者可以定义各种动态关联规则，
例如表单字段的显示和隐藏、数据的联动更新等。类似于为NodeSchema扩展Scalar属性来扩展一个新的Schema类型一样，开发者也可以基于 RelationSchema 
定义新的关系执行类型，系统默认提供call执行schema。