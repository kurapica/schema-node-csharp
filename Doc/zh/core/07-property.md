# 07. 属性原型

属性原型描述语义类型可以携带哪些附加语义，以及这些语义如何存储、合并和被运行时消费。

在 SchemaNode 中，属性既是 Schema 中的一项声明，也是运行时的 `IProperty` 实例。约束、默认值、可见性、函数调用等不同意图，都可以通过属性表达，再由相应的执行逻辑处理。

与属性相关的实现主要集中在以下位置：

- `Schema/PropertySchema.cs`：属性原型及其定义体。
- `Property/Property.cs`：`IProperty` 与 `Property<T>`。
- `Property/PropertyOwner.cs`：属性集合的存取与合并。
- `Generator/PropertyGenerator.cs`：从 C# 属性类生成属性 Schema。
- `Property/` 下的其他文件：约束、类型引用、函数调用和排序等具体实现。

## 1. 原型声明与定义体

属性原型由 `PropertySchema` 声明：

```csharp
[Meta<SchemaKind>(SCHEMA_KIND_NODE_PROPERTY, SCHEMA_KIND_ORDER_PROP)]
[Meta<NodeKind>(NODE_KIND_PROPERTY, SCHEMA_KIND_ORDER_PROP)]
[Meta<SchemaGenerator>(typeof(PropertyGenerator))]
[Meta<NodeType>(typeof(Runtime.PropertyType))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP}.schema")]
[Meta<Attach>(SCHEMA_KIND_NODE_PROPERTY)]
[Meta<Append>(typeof(Relations))]
public class PropertySchema : PropertyOwner
{
    [Meta<UpLimitString>(PRIMARY_KEY_MAX_LEN)]
    public string Property { get; set; } = string.Empty;

    [Meta<SchemaType>(typeof(ValueType))]
    public string Type { get; set; } = string.Empty;

    [Meta<SchemaType>($"{NS_SYSTEM_SCHEMA}.kind")]
    public string[] ForSchemas { get; set; } = [];
}
```

这段声明同时确定了属性原型的分类、生成方式、运行时类型及定义体结构。其中，`Property`、`Type` 和 `ForSchemas` 是定义体的三个字段：

- `Property`：属性在 Schema 中使用的名称。
- `Type`：属性值对应的语义类型。
- `ForSchemas`：属性适用的 Schema 种类。

例如，`require` 是一个适用于结构体字段、值类型为布尔值的属性：

```json
{
  "namespace": "system.schema.prop.common",
  "name": "require",
  "kind": "property",
  "property": {
    "property": "require",
    "type": "system.bool",
    "forSchemas": ["node.struct.field"]
  }
}
```

外层描述属性类型本身，内层 `property` 才是属性原型的定义体。这里的内层键名由 `PropertyProperty` 的 `Alias` 指定。

属性定义中的 `Type` 由 `PropertyType` 约束，只能引用 `node.property` 分类下的类型：

```csharp
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP}.type")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_NODE_KIND,
    NODE_SELF, false, NODE_KIND_PROPERTY)]
public class PropertyType : AnyType;
```

因此，属性本身也是一种语义类型，但它的定义目标不是普通数据结构，而是描述其他语义类型可以携带什么属性。

> 注意，在各执行间传输时，我们只会传输 { require: true } 这种基于属性名称的值，并不会使用`system.schema.prop.common.require` 这样的完整路径。这是因为，属性类型是和原型挂钩的。当系统解析`require`时，会查找当前实体的原型，找到对应的属性定义再加载属性并消费。如果当前执行层没有对应的属性，则默认忽略。

> 不同的执行层实际可以用不同的兼容原型和其它属性类型来解释它，当然，也可以单纯保持一致，更好维护。


## 2. 属性值与存储

运行时属性通过 `IProperty` 统一访问，常见实现继承自 `Property<T>`：

```csharp
public abstract class Property<T> : IProperty
{
    public T? Value { get; private set; }

    public virtual void SetValue<TValue>(TValue value)
        => Value = value.ConvertTo<T>();

    public virtual TV? GetValue<TV>(bool matchType = false)
        => HasValue && (!matchType || Value is TV)
            ? Value.ConvertTo<TV>()
            : default(TV?);

    public virtual bool Combine(
        IProperty other,
        ISchemaRuntime? runtime = null)
    {
        if (other.GetType() != GetType()) return false;
        if (HasValue || !other.HasValue) return false;

        SetValue(other.GetValue<object>());
        return true;
    }

    public virtual bool HasValue => !SystemLogic.isempty(Value);

    public Type Type => typeof(T);
}
```

这个基类提供了属性值的统一承载、类型转换和默认合并行为。具体属性可以重写这些操作，以满足自身的合并规则。

属性名称也由元数据决定：优先使用 `Alias`，没有声明时，再根据属性类名去掉 `Property` 后缀并转换为 camelCase。例如，`LowLimitProperty` 对应 `lowLimit`。

### 属性集合

所有原型的定义体类型都必须继承 `PropertyOwner`。例如`NodeSchema`, `IntSchema`等，这样属性才能正常被存储，因为我们无法实际在C#中动态定义属性。

`PropertyOwner` 为可以携带属性的对象提供统一的集合操作。属性值保存在 `Extensions` 中：

```csharp
[SchemaIgnore]
[JsonExtensionData]
public JsonObject? Extensions { get; internal set; }
```

这种方式将扩展属性直接序列化到所属对象的 JSON 中，而不必为每一种属性修改对象结构。

属性是否允许叠加，由 `Stackable` 决定。普通属性通常保存单个值；叠加属性可以保存多个值，例如一个字段可以声明多个 `valid` 校验：

```json
{
  "require": true,
  "valid": [
    ["system.logic.notempty"],
    ["example.checkAge", "@age", 18]
  ]
}
```

读取时，`GetProperty` 返回指定属性的单个值；对于叠加属性，它只返回第一个值。`GetProperties` 则用于获取全部值，适合需要执行多个约束的场景。

属性集合的主要操作如下：

| 操作 | 作用 |
| --- | --- |
| `SetProperty` | 写入属性值，并处理叠加属性 |
| `GetProperty<T>` | 获取指定属性 |
| `GetProperties<T>` | 获取指定属性的全部值 |
| `CombineProperties` | 按属性规则合并另一个对象的属性 |

合并时，叠加属性追加不重复的值；非叠加属性则调用属性自身的 `Combine`，由具体实现决定如何处理已有值。指定运行时和 Schema 种类后，合并还会参考该种类支持的属性集合。

这使属性的存储和合并机制不必依赖每一种具体属性的业务含义。


## 3. 属性的实现方式

属性的值类型和行为由具体属性类决定。`Property/` 目录中可以看到几种常见的实现方式。

### 3.1 约束属性

`IConstraintProperty` 为属性提供统一的校验入口，并根据目标节点类型分派到相应的校验逻辑。

例如：

- `Require` 检查节点是否为空。
- `Valid` 通过函数调用执行自定义校验。
- `MinSize` 和 `MaxSize` 检查数组长度。
- `Primary` 和 `Indexes` 为数组中的主键、索引提供相关约束。
- `StructValue` 和 `ArrayValue` 汇总结构体、数组的校验结果。

校验结果使用三态表示：

| 返回值 | 含义 |
| --- | --- |
| `true` | 校验通过 |
| `false` | 校验失败 |
| `null` | 当前校验不适用于该节点 |

这里需要区分语义声明与执行逻辑：属性表达需要满足的约束，具体节点及校验实现负责消费这些约束。

### 3.2 加载与类型引用

部分属性需要在类型加载阶段解析引用或访问运行时上下文。这类属性实现 `ILoadableProperty`：

```csharp
public interface ILoadableProperty
{
    Task LoadAsync(
        SchemaContext context,
        Runtime.ValueType? ownerType = null);
}
```

类型引用属性则通过 `ITypeRefProperty` 暴露所引用的语义类型，使运行时能够识别加载依赖。这个的用法是当请求类型时，或者应用之类，可以将所有非系统级语义类型打包发送，避免重复请求。


### 3.3 函数调用属性

`FuncCallProperty` 同时实现 `ITypeRefProperty`，用于承载函数调用及其参数，并提供函数类型的引用信息。

同时它为了配合C#的Attribute系统的限制，提供了一个简化的使用方式。例如:

```C#
[Meta<EntrySourceProvider>($"{NS_SYSTEM_SCHEMA_REFLECT_FUNC}.getaccessentries", $"@{nameof(Args)}", $"@{nameof(Exps)}.{ARRAY_PREVIOUS}", NODE_SELF)]
```

这个属性是用于配置级联数据源，即一个函数调用方式，它的函数是`$"{NS_SYSTEM_SCHEMA_REFLECT_FUNC}.getaccessentries"`, 后续都是参数，如果参数以@开始，则表示它是访问路径，否则表示字面值。配置完成后类似:

```json
{
    "func": "system.schema.reflect.func.getaccessentries",
    "args": [
        { "source": "args" },
        { "source": "exps.$prev" },
        { "source": "$self" }
    ]
}
```

这里实际意思是，在定义函数的表达式时，这个表达式配置参数时，可以访问到的所有条目，包括所有的函数参数，在该表达式前的所有表达式。`$self`意思是当前输入的值，这样可以形成级联选择，例如选择`input.data.age`这样的深层访问路径。


### 3.4 记录与排序属性

`OrderProperty<T>` 为属性值提供排序信息；`RecordProperty<T>` 则在赋值时记录属性声明。

这类机制用于收集 C# 中声明的分类、枚举值、主键等信息，供生成器统一读取。它们主要服务于声明和生成阶段，与普通运行时属性的值承载有所区别。


## 4. 属性的元属性

属性本身也可以携带属性。这些元属性决定属性如何命名、适用于哪些 Schema 种类，以及如何叠加、合并和继承。

| 元属性 | 作用 |
| --- | --- |
| `ForSchema` | 声明属性适用的 Schema 种类 |
| `Append` | 声明原型支持的属性 |
| `Alias` | 指定属性在 Schema 中使用的名称 |
| `Static` | 标记属性值不能被关系系统修改 |
| `Stackable` | 允许同一属性在一个节点上出现多次 |
| `NoInherit` | 禁止属性沿访问链继承 |
| `PropertyValueType` | 指定属性值的语义类型 |

其中，`Append` 和 `ForSchema` 从两个方向描述属性与原型的适用关系：

- `Append` 声明在原型上，描述原型支持哪些属性。
- `ForSchema` 声明在属性上，描述属性适用于哪些 Schema 种类。

例如：

```csharp
[Meta<ForSchema>(SCHEMA_KIND_NODE_STRUCT_FIELD)]
public class Unpack : Property<bool>;
```

这表示 `Unpack` 适用于结构体字段。运行时可以据此筛选适用于特定 Schema 种类的属性。

在属性集合中，`Stackable` 决定是否允许多个值共存；非叠加属性则由自身的 `Combine` 决定如何合并。`NoInherit` 控制属性是否可以沿属性访问链继承，而 `Static` 用于标记不能由关系系统修改的属性值。

这些元属性使属性机制本身也具有可扩展性。新增属性时，可以通过元数据声明其名称、适用范围及合并行为，而不必为每一种属性单独修改集合管理逻辑。


## 5. 小结

属性原型将附加语义组织为几个相互配合的部分：

- **定义体**描述属性名称、值类型和适用种类。
- **`IProperty` 与 `Property<T>`**提供属性值的统一承载与基本操作。
- **元属性**描述属性自身的命名、适用范围、叠加、合并和继承规则。
- **`PropertyOwner`**管理属性集合的存取、序列化和合并。
- **运行时与生成器**分别负责加载属性定义，以及从 C# 声明生成 Schema。

属性机制将附加语义的声明与具体执行逻辑分开组织。新增属性可以复用已有的存储、序列化和集合管理机制，再通过自己的类型、元数据及相应执行逻辑表达新的语义，而不必改动整个属性体系。