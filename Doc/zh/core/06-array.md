# 06. 数组原型

数组原型描述同一类型元素组成的有序集合。它的核心定义是元素类型，但也支持主键、索引、尺寸约束和相对访问路径，使数组既可以表示普通列表，也可以表示具有主键和索引的数据集合。

与数组相关的实现集中在以下位置：

- `Schema/ArraySchema.cs`：数组原型、定义体和使用形式；
- `Runtime/Type/ArrayType.cs`：数组类型的运行时实现；
- `Node/ArrayNode.cs`：数组值节点；
- `Property/Array/`：`ArrayValue`、`Primary`、`Indexes`、`MinSize`、`MaxSize` 等属性；
- `Generator/StructGenerator.cs`：为具有主键或索引的结构体生成伴生数组。


## 1. 原型声明

```csharp
[Meta<SchemaKind>(SCHEMA_KIND_NODE_ARRAY, SCHEMA_KIND_ORDER_ARRAY)]
[Meta<NodeKind>(NODE_KIND_ARRAY, SCHEMA_KIND_ORDER_ARRAY)]
[Meta<NodeValueKind>(NODE_KIND_ARRAY, SCHEMA_KIND_ORDER_ARRAY)]
[Meta<NodeType>(typeof(RuntimeArrayType))]
[Meta<SchemaUsage>(typeof(ArrayUsage))]
[Meta<Append>(typeof(Generics), typeof(Relations))]
[Meta<ArrayValue>]
public sealed class ArrayKind;
```

数组原型的声明与枚举、结构体类似，指定了原型标识、节点分类、运行时类型和使用形式，并声明支持泛型、关系及数组校验属性。

与枚举、结构体不同，数组原型没有独立的 `SchemaGenerator`。C# 类型到 Schema 的转换由 `StructGenerator` 等现有生成逻辑处理；其中，带主键或索引的结构体还可以生成对应的伴生数组。


## 2. 数组定义体

数组定义体 `ArraySchema` 只有一个必填字段：`Element`，用于引用数组元素的语义类型。

```csharp
[Meta<SchemaKind>(SCHEMA_KIND_NODE_ARRAY_DEFINE, SCHEMA_KIND_ORDER_ARRAY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ARRAY}.schema")]
[Meta<Attach>(SCHEMA_KIND_NODE_ARRAY_DEFINE)]
public sealed class ArraySchema : PropertyOwner
{
    [Meta<SchemaType>(typeof(ElementType))]
    public required string Element { get; set; }
}
```

除了元素类型，数组还可以通过 `Primary` 和 `Indexes` 描述元素结构体上的主键字段和索引。

- `Primary` 保存主键字段名列表。
- `Indexes` 保存索引定义，每个索引包含名称、字段列表和唯一性标记。

这些属性不是数组定义体的固定字段，而是通过数组原型关联的语义属性提供。它们只在元素类型为结构体时可见，字段候选来源则由结构体类型的可索引字段决定。

例如：

```json
{
  "namespace": "example",
  "name": "persons",
  "kind": "array",
  "array": {
    "element": "example.person",
    "primary": ["id"],
    "indexes": [
      {
        "name": "byAge",
        "fields": ["age"],
        "isUnique": false
      }
    ]
  }
}
```

这里，`example.person` 是元素类型，`primary` 指定主键字段，`indexes` 声明索引。若元素是标量或枚举，主键和索引配置则不适用。

主键和索引的字段选择、重复字段约束等细节，可以直接查看 `Primary`、`DataIndex` 及其关联的 `Relation` 声明。

## 3. 数组使用形式

数组使用形式 `ArrayUsage` 不重新声明元素类型，而是在已有数组定义的基础上，为具体使用场景追加约束。

最常见的是 `MinSize` 和 `MaxSize`，分别限制数组的最小和最大元素数量：

```csharp
[Meta<ForSchema>(SCHEMA_KIND_NODE_ARRAY, SCHEMA_KIND_ARRAY_USAGE)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_ARRAY}.{nameof(MinSize)}")]
[Meta<LowLimitInt>(0L)]
[Relation<UpLimitInt, Call>(
    nameof(MinSize),
    $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}",
    $"@{nameof(MaxSize)}")]
public class MinSize : Property<long>, IConstraintProperty;

[Meta<ForSchema>(SCHEMA_KIND_NODE_ARRAY, SCHEMA_KIND_ARRAY_USAGE)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_ARRAY}.{nameof(MaxSize)}")]
[Meta<LowLimitInt>(0L)]
public class MaxSize : Property<long>, IConstraintProperty;
```

两个属性都是非负整数，并通过关系约束 `MinSize` 不超过 `MaxSize`。其校验对象是整个数组，而不是单个元素。

例如：

```json
{
  "kind": "array.usage",
  "array": {
    "minSize": 1,
    "maxSize": 100
  }
}
```

此外，当数组作为 `node` 的属性出现时，`ArrayProperty` 可以根据元素类型推导默认名称和显示文本。因此，自动生成伴生数组时，不必重复配置这些信息。


## 4. 运行时与访问路径

`ArrayType` 负责加载元素类型、处理数组相关属性，并提供类型兼容性、访问路径和主键操作等能力。数组本身不需要预先知道元素的具体结构，只需能够加载元素类型并创建相应节点。

数组提供两个特殊访问路径：

| 路径 | 含义 |
|---|---|
| `$prev` | 当前元素之前的部分数组 |
| `$ele` | 当前元素 |

`$ele` 进入当前元素；如果元素类型是结构体，还可以继续访问它的字段。`$prev` 则表示当前元素之前的数组部分，可用于依赖前序元素的关系计算。

例如，枚举定义中可以通过 `Values.$prev` 计算条目的默认值，主键字段列表也可以借助 `Primary.$prev` 避免重复选择。

这些路径由数组类型提供访问结构和目标值类型，再由数组节点完成实际取值。关系机制因此不需要内置针对数组的特殊业务规则。


## 5. 数组节点与校验

`ArrayNode` 是数组的运行时值节点，内部维护元素列表，并提供索引访问、赋值、遍历、比较、克隆和主键过滤等操作。

赋值时，它既可以接受已有的值节点，也可以从 JSON 或 CLR 集合逐项创建元素节点。数组元素必须符合声明的元素类型，索引器支持读取、替换和末尾追加，但不允许跳过中间位置。

数组校验由 `ArrayValue` 组织：

```csharp
public async Task<bool?> ValidateArrayAsync(
    SchemaContext context,
    ArrayNode node)
{
    ArrayType type = (node.Type as ArrayType)!;
    if (type.Element == null) return null;

    foreach (IConstraintProperty prop in type.Constraints)
    {
        foreach (IValueAccess element in node)
        {
            bool? result = await prop.ValidateAsync(context, element);
            if (result.HasValue)
                element.RecordConstraint(prop, result.Value);
        }
    }

    await type.ValidateWithRelationsAsync(context, node);
    return null;
}
```

校验分为两个部分：

- 将数组类型上的约束属性应用到各个元素；
- 执行数组类型声明的关系校验。

`MinSize` 和 `MaxSize` 则直接检查数组的元素数量。由此，数组级约束、元素约束和关系校验可以分别声明，再由相应的运行时逻辑组织执行。


## 6. 从结构体生成伴生数组

当 `StructGenerator` 处理具有主键或索引的结构体时，可以同时生成一个以该结构体为元素类型的伴生数组。

例如，定义一个带主键的结构体：

```csharp
public class Person
{
    [Meta<PrimaryIndex>]
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
```

除了 `person` 结构体类型，还可以生成对应的数组类型：

```json
{
  "namespace": "example",
  "name": "persons",
  "kind": "array",
  "array": {
    "element": "example.person",
    "primary": ["id"]
  }
}
```

伴生数组的元素类型指向原结构体，主键和索引则来自结构体字段或类型上的相关元数据。数组名称和显示文本可以根据元素类型自动生成。

这也解释了结构体字段上的 `PrimaryIndex` 为什么会与数组原型关联：它描述的是字段在集合场景中的主键用途，而数组负责承载相应的集合配置和操作能力。


## 7. 小结

数组原型围绕元素类型构建有序集合，并通过语义属性扩展出不同用途：

- **定义体**描述元素类型；
- **数组属性**描述主键和索引；
- **使用形式**提供最小、最大尺寸等场景约束；
- **运行时**负责元素访问、值处理、校验和主键操作。

`$prev` 与 `$ele` 则为关系计算提供了数组上下文中的访问路径，使关系能够引用当前元素、前序元素及其子字段。

数组不需要了解元素的具体业务语义。只要元素类型能够被加载和创建，数组就可以容纳标量、枚举、结构体或其他数组，从而以统一的机制组合不同的语义类型。