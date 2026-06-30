# SchemaNode.Core — 功能指南

SchemaNode.Core 四大支柱的实用示例。

---

## 1. 使用 Meta 定义 Schema Kind

通过 `[Meta<T>]` 注解声明新的 Schema Kind：

```csharp
[Meta<SchemaKind>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ORDER_STRUCT)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ORDER_STRUCT)]
[Meta<ValueSchemaKind>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ORDER_STRUCT)]
[Meta<NodeType>(typeof(RuntimeStructType))]
[Meta<SchemaGenerator>(typeof(StructGenerator))]
[Meta<SchemaType>("system.schema.struct.schema")]
[Meta<Attach>(SCHEMA_KIND_STRUCT)]
public sealed class StructSchema : ExtensibleSchema
{
    public StructFieldSchema[] Fields { get; set; } = [];
}
```

关键 Meta 声明：`SchemaKind`（注册到全局）、`NodeType`（映射运行时类型）、`SchemaGenerator`（代码生成）、`SchemaType`（自描述）、`Attach`（属性继承）。

---

## 2. 创建自定义 Property

### 基础 Property

```csharp
[Meta<ForSchema>("struct")]
[Meta<OfSchema>("property")]
public sealed class MyCustomDisplay : Property<string> { }
```

### 约束属性（验证）

```csharp
public sealed class RangeLimit : ConstraintProperty<(double Min, double Max)>
{
    public override async Task ValidateAsync(
        SchemaContext context, DataNode node, IValueTypeAccess? access)
    {
        if (node is NumericNode numNode && numNode.Value is double val)
        {
            if (val < Value.Min || val > Value.Max)
                node.SetViolated($"值 {val} 超出范围 [{Value.Min}, {Value.Max}]");
        }
    }
}

// 使用：[Meta<RangeLimit>((0, 100))]
```

---

## 3. 定义 Relation

### Assign（强制赋值）

```csharp
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, "$Kind", "assign")]
public class AssignProperty : Property<Assign>;
```

### Call（动态计算）

```csharp
[Relation<Display>("lookup_manager", "$store_id")]
public string ManagerName { get; set; }
```

### 自定义 Relation

```csharp
public class MyCustomRelation : IRelationProcess
{
    public Task LoadAsync(SchemaContext ctx, IValueTypeAccess owner) => Task.CompletedTask;
    public async Task<object?> ProcessAsync(SchemaContext ctx, IValueAccess owner)
    {
        var data = owner.GetAccessValue("someField");
        return await ComputeResult(data);
    }
}
```

---

## 4. 定义 Function

### 原子函数（C# 注册）

```csharp
public static class MyBusinessFunctions
{
    [Function("calc_tax", "myapp.calc_tax", "decimal", "decimal", "decimal")]
    public static decimal CalculateTax(decimal amount, decimal rate) => amount * rate;
}
```

### 语义函数（Schema 定义，等价于 `user.active && user.score >= threshold && !user.blocked`）

```json
{
  "kind": "function", "name": "is_eligible", "return": "bool",
  "args": [
    { "name": "user", "schemaType": "myapp.user" },
    { "name": "threshold", "schemaType": "int" }
  ],
  "exps": [{
    "func": "system.logic.and", "args": [
      { "func": "system.logic.and", "args": [
        { "field": "active" },
        { "func": "system.logic.ge", "args": [{ "field": "score" }, { "arg": "threshold" }] }
      ]},
      { "func": "system.logic.not", "args": [{ "field": "blocked" }] }
    ]
  }]
}
```

---

## 5. 实现自定义 CompileContext

```csharp
public class MyCompileContext : CompileContext
{
    public override async Task<SchemaExp> VisitSchemaExpAsync(SchemaExp exp)
    {
        if (exp is FuncCallExp { Function: "system.logic.eq" } eqExp)
            return new MyTargetEqualityExp(eqExp.Args[0], eqExp.Args[1]);
        return await base.VisitSchemaExpAsync(exp);
    }

    public override Task<Expression> CompileSchemaExpAsync(SchemaExp exp, Type? expectedType = null)
        => exp switch
        {
            MyTargetEqualityExp eq => /* 生成目标代码 */,
            _ => base.CompileSchemaExpAsync(exp, expectedType)
        };
}
```

---

## 6. 运行时操作

### 加载 Schema

```csharp
var userType = await context.GetNodeTypeAsync("myapp.user") as StructType;
foreach (var field in userType.Fields)
    Console.WriteLine($"{field.Name}: {field.SchemaType.Name}");
```

### 创建和验证数据

```csharp
var userData = new StructNode(userType);
userData.TrySetValue("name", "John Doe");
userData.TrySetValue("score", 85);

foreach (var constraint in userType.GetProperties<IConstraintProperty>())
    await constraint.ValidateAsync(context, userData, null);

if (userData.Violated.Length > 0)
    Console.WriteLine($"验证失败：{string.Join(", ", userData.Violated)}");
```

### 编译函数

```csharp
var funcType = await context.GetNodeTypeAsync("myapp.is_eligible") as FunctionType;
var compiledFunc = funcType.Compile<Func<StructNode, int, bool>>();
var result = compiledFunc(userData, 60);
```

---

## 7. 扩展点总结

| 扩展项 | 实现方式 | 示例 |
|--------|---------|------|
| 新 Schema Kind | `[Meta<SchemaKind>]` | 自定义 "workflow" Kind |
| 新 Property | 继承 `Property<T>` | `MyCustomDisplay` |
| 新约束 | 实现 `IConstraintProperty` | `RangeLimit` |
| 新 Relation | 实现 `IRelationProcess` | 自定义数据查询 |
| 新原子函数 | `[Function]` 静态方法 | `CalculateTax` |
| 新 CompileContext | 继承 `CompileContext` | GraphQL 转换器 |
