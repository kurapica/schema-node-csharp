# 04. 定义域和公共语义共识

前三章从代码与语义的关系出发，逐步构造了多维语义空间，并由 Meta、Property、Relation、Function 四种语义原语构成了语义实体。

随后，我们进一步引入 Prototype，使语义类型获得了稳定的结构和能力边界，并建立了语义与执行层之间的最小契约。

到这里，理论上的推演已经完成。

如前所述，我们无法为所有业务领域定义一组统一的语义原型，但我们可以围绕**原型的定义域**来构建一个**公共语义共识**。

---

## 1. 定义域

定义域是解决“如何在语义空间中定义实体”这一问题的工程领域。按照之前的讨论，它可以分为三个阶段：

1. 如何定义语义原型，以及如何管理语义原型。
2. 如何从语义原型定义语义类型，并对语义类型进行管理。
3. 如何从语义类型构造语义实体。

定义域需要覆盖未知领域的语义原型定义——这是它区别于固定业务模型的关键。

因为可被任何代码解析和执行的语义，其中间形式必然是结构化数据，所以定义域本身必须覆盖数据处理领域。

同时，作为构成语义的基本元素，Property、Relation、Function 也必须在定义域中实现。这就意味着：定义域不能只是“描述工具”，它自身必须能够被同一套机制描述。

从这三个要求出发，定义域在语义空间中必须完成自举。


## 2. 语义原型分类(Kind)

语义原型是用于实体的分类，原型是执行层确定如何执行的关键，执行层必须知道这个原型对应的具体Meta和可以挂载的Property，才能正确加载对应的实体类型和实体，无论语义空间还是执行层，都必须对原型存在内置的实现。

所以，我们只需要一个**唯一的标识字符串**就可以作为语义原型。实际在[03-语义原型](./03-prototype.md)中我们已经使用过`struct`, `struct.field`这类名字。

当我们认为一个实体需要附着属性提供语义，而目前的原型不足以完成它的分类，我们就可以尝试定义一个语义原型，但请注意，原型必须被执行层支持才有作用，原型是类型的抽象，正如类型是对象的抽象一样。


## 3. 语义类型管理





## 3. 命名空间

我们同样采用命名空间来管理类型。




### 1.2 数据类型





### 1.3 属性类型


### 1.4 关联类型


### 1.5 函数类型



## 2. 公共语义共识



---

## 1. 公共语义共识是什么

传统系统通常把数据类型、数据库结构、API 参数、UI 配置和业务规则分别定义。

而 SchemaNode 首先需要解决的是：

> **一个语义系统究竟需要具备哪些公共能力，才能完整地描述和处理结构化语义？**

因此，公共语义共识不是一套统一的业务模型。

它不规定：

```text
Person
Order
Product
Patient
...
```

这些业务实体应该如何设计。

它规定的是这些实体**如何能够被定义和理解**。

例如：

```text
struct
array
enum
function
Property
Relation
```

这些属于定义语义本身所需要的公共能力。

因此可以将公共语义共识理解为：

> **不同语义执行层为了能够定义、表达、处理和理解结构化语义，而共同需要的一组基础定义能力。**

SchemaNode Core 所实现的，正是这组公共定义域。

---

## 2. 从 Prototype 到具体类型

第三章已经说明，Prototype 是语义类型与执行层之间的最小契约。

因此 SchemaNode Core 首先需要实现的，就是一组基础 Prototype。

例如：

```text
scalar
enum
struct
array
function
```

它们并不是某个业务领域中的类型，而是构造语义类型所需要的基础类型原型。

它们分别规定不同类型实体的基本结构和执行方式。

例如：

```text
struct
    ↓
fields

array
    ↓
element type

enum
    ↓
enum values

function
    ↓
arguments / return
```

基于这些 Prototype，才可以继续定义具体的语义类型。

因此：

```text
Prototype
    ↓
Type
    ↓
Data
```

是 SchemaNode Core 中最基础的类型关系。

---

## 3. struct：构造结构化实体

`struct` 是 SchemaNode 中最基本的结构化 Prototype。

它描述一个实体由哪些字段组成。

例如：

```json
{
  "fields": [
    {
      "name": "name",
      "type": "string"
    },
    {
      "name": "age",
      "type": "int"
    }
  ]
}
```

但 SchemaNode 中的字段并不仅仅是传统意义上的：

```text
name + type
```

字段本身也是语义实体。

因此可以继续附加：

```json
{
  "name": "age",
  "type": "int",
  "display": "年龄",
  "lowlimit": 0,
  "uplimit": 200,
  "unit": "岁"
}
```

这里：

```text
name
type
```

属于字段的基本结构，而：

```text
display
lowlimit
uplimit
unit
```

则属于附加语义。

这也是 Prototype 与 Property 在实际实现中的第一次结合。

---

## 4. 字段本身也需要类型

如果一个 `struct` 的字段可以具有 Property，那么字段本身也必须成为一个可以被描述的语义实体。

因此 SchemaNode 不需要为“字段属性”设计一套特殊机制，而是继续使用 Prototype。

例如：

```text
struct
  │
  └── struct.field
```

同样，函数参数也遵循类似的方式：

```text
function
  │
  └── function.arg
```

这样，结构体字段和函数参数都可以使用统一的语义描述机制。

例如函数参数可以具有：

```text
Require
Default
Variadic
Display
Relation
...
```

这使函数参数不再只是编程语言意义上的参数列表，而成为可以参与语义空间的实体。

---

## 5. scalar、enum 和 array

在 `struct` 之外，Core 还提供基础的数据类型 Prototype。

### scalar

`scalar` 描述具有基本值语义的数据类型，例如：

```text
string
int
number
boolean
...
```

它们提供数据的基本表示和解释方式。

在此基础上，可以通过 Property 继续形成语义类型。

例如：

```text
string
  +
phone
```

并不是改变字符串的底层表示，而是在字符串之上增加新的语义约束。

---

### enum

`enum` 用于描述具有有限离散值的数据。

例如：

```text
status
 ├── pending
 ├── active
 └── closed
```

枚举值本身也是结构化定义的一部分，因此可以继续参与其他语义描述。

---

### array

`array` 描述元素具有统一语义类型的集合。

例如：

```text
array<string>
array<Person>
array<Order>
```

它的基本 Meta 只需要描述元素类型，但数组本身仍然可以继续附加 Property 和 Relation。

因此这些类型并不是孤立的特殊结构，而是统一 Prototype 机制下的不同类型。

---

## 6. Function：让定义本身可以执行

如果只有数据结构和 Property，语义仍然主要停留在描述层。

Relation 引入了语义之间的关联，而 Function 则使这种关联能够产生执行作用。

因此 Function 也是 Core 中非常重要的 Prototype。

一个 Function 至少需要描述：

```text
Arguments
Return
Execution
```

但这些本身仍然属于结构化语义。

例如：

```text
Function
 ├── Arguments
 │     ├── Argument
 │     └── Argument
 │
 └── Return
```

函数参数又可以继续拥有自己的 Property。

因此 Function 本身也处在同一个语义系统中。

---

## 7. Relation：定义语义之间如何作用

Relation 在 Core 中也不是某一种固定的业务关系。

它首先是一种语义能力：

> **描述一个语义如何关联到另一个语义。**

SchemaNode 提供基本的 Relation 执行方式，例如：

```text
assign
    直接赋值

call
    调用 Function

any
    多个 Function 中任意一个满足
```

这些只是公共定义域提供的基础执行能力。

具体的方言仍然可以在此基础上扩展自己的 Relation。

因此 Relation 本身也具有结构化定义，而不是执行层中散落的特殊代码。

---

## 8. Property 也属于定义域

Property 看起来像是附加在类型上的配置，但实际上它本身也是语义实体。

一个 Property 至少需要明确：

```text
Name
Value Type
Applicable Prototype
Semantic behavior
```

例如：

```text
lowlimit
    value type → number

display
    value type → string

Static
    value type → boolean
```

Property 还可以具有自己的 Property。

例如：

```text
Static
```

可以表示某个 Property 不允许被 Relation 修改。

于是 Property 不只是“一个键值”，而是一个具有自身语义的实体。

这也是公共定义域能够继续扩展的重要基础。

---

## 9. 定义域同时覆盖“定义处理”

到这里可以看到，SchemaNode 的定义域实际上已经超过了传统意义上的数据类型系统。

它不仅需要描述：

```text
数据
```

还需要描述：

```text
类型
Property
Relation
Function
Function Argument
Schema
```

并对这些定义进行处理。

因此：

> **定义域不仅定义数据类型，也定义了处理这些定义所需要的语义。**

这也是为什么 SchemaNode 中大量 Core Function 并不是传统意义上的业务函数，而是用于：

```text
读取定义
解析定义
查询类型
获取 Property
执行 Relation
解析 Function
构造运行时语义
```

它们共同构成了定义域的执行能力。

---

## 10. Schema DSL 本身也是结构化语义

这里会得到一个非常重要的结果。

SchemaNode 使用 Schema 描述类型。

但 Schema 本身也是结构化数据。

因此：

> **描述 Schema 的 DSL，本身也属于定义域。**

它并没有站在整个系统之外。

可以表示为：

```text
SchemaNode
    │
    ├── 定义 Type
    │
    ├── 定义 Property
    │
    ├── 定义 Relation
    │
    └── 定义 Function
             │
             ↓
           Schema
             │
             ↓
      Schema 也是结构化数据
             │
             ↓
       继续由 SchemaNode 描述
```

因此 SchemaNode 的定义语言本身也可以被 SchemaNode 处理。

这使得之前理论章节中提到的“自举”真正落到了实现上。

---

## 11. System Schema

SchemaNode Core 最终通过 System Schema 将这些公共能力组织起来。

System Schema 不是业务 Schema。

它描述的是：

> **执行层本身能够理解和执行哪些语义。**

因此可以把它看成公共定义域的结构化表达。

例如：

```text
System Schema
    │
    ├── Prototype
    ├── Type
    ├── Property
    ├── Relation
    ├── Function
    └── Definition Processing
```

执行层根据这些 Schema 获得自己的语义能力。

因此，SchemaNode 中的代码并不是唯一的语义来源。

更准确地说：

> **System Schema 规定执行层能够理解的语义结构，执行层代码负责将这些语义结构落实为确定的运行时行为。**

---

## 12. 公共定义域与执行层

到这里，SchemaNode 的公共语义共识可以完整地表示为：

```text
公共语义共识
       │
       ↓
   Prototype
       │
       ↓
      Type
       │
 ┌─────┼──────────┐
 ↓     ↓          ↓
Meta Property Relation
              │
              ↓
           Function
              │
              ↓
          Runtime
```

公共定义域负责规定：

> **语义应该如何被表达和组织。**

执行层负责规定：

> **这些语义如何被真正执行。**

因此，同一套语义定义并不需要绑定唯一的执行实现。

不同执行层可以采用不同的实现方式，只要它们能够遵守公共语义共识。

---

## 13. 从公共语义到语义方言

公共定义域解决的是语义语言中最基础、最通用的部分。

但一个具体问题域显然还需要更多能力。

例如 App 方言可能需要：

```text
CRUD
Permission
Row Auth
EntrySource
View
ETL
Workflow
...
```

这些并不是重新定义：

```text
string
struct
array
function
```

而是在公共语义之上，定义：

> **这些语义在具体问题域中如何被消费和执行。**

因此下一章开始进入：

> **如何实现一个语义方言。**

公共语义共识提供共同的语言基础，语义方言则让不同领域在这个基础上表达自己的执行方式。

至此，SchemaNode 从前三章推导出的语义模型，已经完整落到了一个实际可执行的定义域中。
