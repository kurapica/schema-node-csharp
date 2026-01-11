using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Nodes;
using SchemaNode.Utility;

namespace SchemaNode.Components;

/// <summary>
/// The entity schema condition visitor, only support equal, since schema is designed without complex query
/// </summary>
internal class EntityConditionVisitor: ExpressionVisitor
{
    
    #region Implementations

    /// <inheritdoc />
    protected override Expression VisitBinary(BinaryExpression node)
    {
        base.VisitBinary(node);
        try
        {
            ExpNode right = _expNodes.Pop();
            ExpNode left = _expNodes.Pop();
            ExpNode opNode = new();
            dynamic lVal = left.Value!;
            dynamic rVal = right.Value!;
            bool isEval = !left.Type.HasValue && !right.Type.HasValue;
            switch (node.NodeType)
            {
                case ExpressionType.AndAlso:
                    if (isEval)
                    {
                        opNode.Value = lVal && rVal;
                    }
                    else
                    {
                        opNode.Type = node.NodeType;
                        opNode.Left = left;
                        opNode.Right = right;
                    }
                    break;
                case ExpressionType.Equal:
                    if (isEval)
                    {
                        opNode.Value = lVal == rVal;
                    }
                    else
                    {
                        opNode.Type = node.NodeType;
                        opNode.Left = left;
                        opNode.Right = right;
                    }
                    break;
                default:
                    throw new NotSupportedException($"[{nameof(VisitBinary)}]{node}");
            }
            _expNodes.Push(opNode);
        }
        catch (Exception ex)
        {
            throw new NotSupportedException(ex.Message, ex);
        }
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitConstant(ConstantExpression node)
    {
        Expression result = base.VisitConstant(node);
        _expNodes.Push(new ExpNode
        {
            Value = node.Value
        });
        return result;
    }

    /// <inheritdoc />
    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression == null)
        {
            if (node.Member.MemberType == MemberTypes.Property)
            {
                PropertyInfo info = (PropertyInfo)node.Member;
                object? value = info.GetValue(null);
                _expNodes.Push(new ExpNode
                {
                    Value = value
                });
                return Expression.Constant(value, node.Type);
            }
            if (node.Member.MemberType == MemberTypes.Field)
            {
                FieldInfo info = (FieldInfo)node.Member;
                object? value = info.GetValue(null);
                _expNodes.Push(new ExpNode()
                {
                    Value = value
                });
                return Expression.Constant(value, node.Type);
            }
            throw new NotSupportedException($"[{nameof(VisitMember)}]{node}");
        }
        else
        {
            // Eval the member access
            switch (node.Expression?.NodeType)
            {
                case ExpressionType.Constant:
                case ExpressionType.MemberAccess:
                {
                    ConstantExpression cleanNode = GetMemberConstant(node);
                    _expNodes.Push(new ExpNode
                    {
                        Value = cleanNode.Value
                    });
                    return cleanNode;
                }
            }
        }
        _expNodes.Push(new ExpNode
        {
            Type = ExpressionType.MemberAccess,
            Value = node.Member.Name
        });
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        //The start point
        if (_expNodes.Count > 0)
            throw new NotSupportedException($"[{nameof(VisitLambda)}]{node}");
        _paramName = node.Parameters[0].Name;
        Expression result = base.VisitLambda(node);
        return result;
    }

    /// <inheritdoc />
    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (node.Name != _paramName)
            throw new NotSupportedException($"[{nameof(VisitParameter)}]{node}");
        return base.VisitParameter(node);
    }

    static ConstantExpression GetMemberConstant(MemberExpression node)
    {
        object? value;
        if (node.Member.MemberType == MemberTypes.Field)
        {
            value = GetFieldValue(node);
        }
        else if (node.Member.MemberType == MemberTypes.Property)
        {
            value = GetPropertyValue(node);
        }
        else
        {
            throw new NotSupportedException();
        }
        return Expression.Constant(value, node.Type);
    }

    static object? GetFieldValue(MemberExpression node)
    {
        FieldInfo fieldInfo = (FieldInfo)node.Member;
        object? instance = (node.Expression == null) ? null : TryEvaluate(node.Expression).Value;
        return fieldInfo.GetValue(instance);
    }

    static object? GetPropertyValue(MemberExpression node)
    {
        PropertyInfo propertyInfo = (PropertyInfo)node.Member;
        object? instance = (node.Expression == null) ? null : TryEvaluate(node.Expression).Value;
        return propertyInfo.GetValue(instance, null);
    }

    static ConstantExpression TryEvaluate(Expression expression)
    {
        if (expression.NodeType == ExpressionType.Constant)
        {
            return (ConstantExpression)expression;
        }
        else if (expression.NodeType == ExpressionType.MemberAccess)
        {
            ConstantExpression cleanNode = GetMemberConstant((MemberExpression)expression);
            return cleanNode;
        }
        throw new NotSupportedException();
    }

    #endregion

    #region Inner Type

    /// <summary>
    /// The expression node used to generate the final sql
    /// </summary>
    public class ExpNode
    {
        public ExpressionType? Type { get; set; }

        public ExpNode? Left { get; set; }

        public ExpNode? Right { get; set; }

        public object? Value { get; set; }

        /// <summary>
        /// Convert the expressions to the condition sql
        /// </summary>
        public JsonNode ToNode()
        {
            if (Type.HasValue)
            {
                switch (Type.Value)
                {
                    case ExpressionType.AndAlso:
                    {
                        JsonNode? left = Left?.ToNode();
                        JsonNode? right = Right?.ToNode();
                        if (left is JsonObject leftObj && right is JsonObject rightObj)
                        {
                            foreach (KeyValuePair<string, JsonNode?> pair in rightObj)
                            {
                                if (pair.Value != null)
                                    leftObj.Add(pair.Key, pair.Value.DeepClone());
                            }

                            return leftObj;
                        }
                        break;
                    }
                    case ExpressionType.Equal:
                    {
                        JsonNode? left = Left?.ToNode();
                        JsonNode? right = Right?.ToNode();
                        if (left is JsonValue leftObj && right is JsonValue rightObj)
                        {
                            return new JsonObject
                            {
                                { leftObj.ToString(), rightObj.DeepClone() }
                            };
                        }
                        break;
                    }
                    case ExpressionType.MemberAccess:
                    {
                        //Convert the property Name to field name
                        string? prop = Value?.ToString();
                        if (prop != null) return JsonValue.Create(prop.ToCamelCase());
                        break;
                    }
                    case ExpressionType.Label:
                    {
                        JsonNode? left = Left?.ToNode();
                        JsonNode? right = Right?.ToNode();
                        if (left is JsonValue leftObj && right is JsonValue rightObj)
                        {
                            return new JsonObject
                            {
                                { leftObj.ToString(), rightObj.DeepClone() }
                            };
                        }
                        break;
                    }
                    default:
                        throw new NotSupportedException($"[{nameof(ExpNode.ToString)}]{Type}{Value}");
                }
            }
            
            return Value?.ToLiteral()!;
        }
    }

    #endregion

    #region Utility

    readonly Stack<ExpNode> _expNodes = new();
    string? _paramName = "";

    /// <summary>
    /// The final condition
    /// </summary>
    public JsonNode Condition => _expNodes.Pop().ToNode();

    #endregion
}