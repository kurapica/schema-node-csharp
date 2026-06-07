using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Utility;
using ExpressionType = System.Linq.Expressions.ExpressionType;

namespace SchemaNode.Data;

/// <summary>
/// The visitor that converts expression trees into AppSchemaDataFilter objects.
/// </summary>
public sealed class AppSchemaDataFilterVisitor : ExpressionVisitor
{
    static readonly IReadOnlyDictionary<ExpressionType, LogicType> BinaryLogicMap = new Dictionary<ExpressionType, LogicType>
    {
        { ExpressionType.Equal, LogicType.Equal },
        { ExpressionType.NotEqual, LogicType.NotEqual },
        { ExpressionType.GreaterThan, LogicType.GreaterThan },
        { ExpressionType.GreaterThanOrEqual, LogicType.GreaterEqual },
        { ExpressionType.LessThan, LogicType.LessThan },
        { ExpressionType.LessThanOrEqual, LogicType.LessEqual },
    };

    readonly Stack<AppSchemaDataFilter> _filters = new();
    string? _parameterName;

    /// <summary>
    /// Converts the given expression into an AppSchemaDataFilter.
    /// </summary>
    public static AppSchemaDataFilter Build<T>(Expression<Func<T, bool>> predicate) => Build((LambdaExpression)predicate);

    /// <summary>
    /// Converts the given expression into an AppSchemaDataFilter.
    /// </summary>
    public static AppSchemaDataFilter Build(LambdaExpression predicate)
    {
        var visitor = new AppSchemaDataFilterVisitor();
        visitor.Visit(predicate);
        AppSchemaDataFilter filter = visitor.GetFilter();
        if (filter.Transform(out AppSchemaDataFilter? transformed) && transformed != null && transformed is not AppSchemaDataFilterValue)
            return transformed;
        throw new NotSupportedException("The lambda expression cannot be transformed into an AppSchemaDataFilter.");
    }

    // <inheritdoc/>
    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        if (node.Parameters.Count !=1)
            throw new NotSupportedException("Only single-parameter lambda expressions are supported.");
        _parameterName = node.Parameters[0].Name;
        return base.VisitLambda(node);
    }

    // <inheritdoc/>
    protected override Expression VisitParameter(ParameterExpression node)
    {
        return node.Name != _parameterName ? throw new NotSupportedException($"Unexpected parameter '{node.Name}'.") : base.VisitParameter(node);
    }

    // <inheritdoc/>
    protected override Expression VisitBinary(BinaryExpression node)
    {
        base.VisitBinary(node);

        AppSchemaDataFilter right = Pop();
        AppSchemaDataFilter left = Pop();

        switch (node.NodeType)
        {
            case ExpressionType.AndAlso:
                _filters.Push(left.AndAlso(right));
                break;
            case ExpressionType.OrElse:
                _filters.Push(left.OrElse(right));
                break;
            case ExpressionType.Add:
                _filters.Push(new AppSchemaDataFilterArith(ArithmeticType.Add, left, right));
                break;
            case ExpressionType.Subtract:
                _filters.Push(new AppSchemaDataFilterArith(ArithmeticType.Subtract, left, right));
                break;
            case ExpressionType.Multiply:
                _filters.Push(new AppSchemaDataFilterArith(ArithmeticType.Multiply, left, right));
                break;
            default:
                if (!BinaryLogicMap.TryGetValue(node.NodeType, out LogicType logicType))
                    throw new NotSupportedException($"Binary expression '{node.NodeType}' is not supported.");
                _filters.Push(new AppSchemaDataFilterBinary(logicType, left, right));
            break;
        }

        return node;
    }

    // <inheritdoc/>
    protected override Expression VisitUnary(UnaryExpression node)
    {
        Expression result = base.VisitUnary(node);
        switch (node.NodeType)
        {
            case ExpressionType.Convert:
            case ExpressionType.ConvertChecked:
            case ExpressionType.Quote:
                return result;
            case ExpressionType.Not:
                AppSchemaDataFilter operand = Pop();
                _filters.Push(Negate(operand));
                return result;
            default:
                throw new NotSupportedException($"Unary expression '{node.NodeType}' is not supported.");
        }
    }

    // <inheritdoc/>
    protected override Expression VisitConstant(ConstantExpression node)
    {
        _filters.Push(new AppSchemaDataFilterValue(node.Value!));
        return node;
    }

    // <inheritdoc/>
    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression is ParameterExpression parameter && parameter.Name == _parameterName)
        {
            _filters.Push(new AppSchemaDataFilterField(node.Member.Name.ToCamelCase()));
            return node;
        }

        if (node.Expression == null || !ContainsParameter(node.Expression))
        {
            object? value = EvaluateMember(node);
            _filters.Push(new AppSchemaDataFilterValue(value!));
            return node;
        }

        throw new NotSupportedException($"Member access '{node}' is not supported.");
    }

    // <inheritdoc/>
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Object != null)
            Visit(node.Object);
        foreach (Expression argument in node.Arguments)
            Visit(argument);

        AppSchemaDataFilter[] args = TrimStringComparison(PopArguments(node.Arguments.Count));
        AppSchemaDataFilter? instance = node.Object != null ? Pop() : null;

        if (node.Method.DeclaringType == typeof(string))
        {
            HandleStringMethod(node.Method.Name, instance, args);
            return node;
        }

        if (node.Method.Name == nameof(string.Contains) || node.Method.Name == nameof(Enumerable.Contains))
        {
            HandleCollectionContains(instance, args);
            return node;
        }

        if (node.Method.Name == nameof(object.Equals) && args.Length ==2)
        {
            _filters.Push(new AppSchemaDataFilterBinary(LogicType.Equal, args[0], args[1]));
            return node;
        }

        throw new NotSupportedException($"Method call '{node.Method}' is not supported.");
    }

    // Gets the final filter from the stack.
    void HandleStringMethod(string methodName, AppSchemaDataFilter? instance, AppSchemaDataFilter[] args)
    {
        (AppSchemaDataFilter target, AppSchemaDataFilter operand) = ResolveInstanceAndOperand(instance, args);

        LogicType logic = methodName switch {
            nameof(string.StartsWith) => LogicType.StartsWith,
            nameof(string.EndsWith) => LogicType.EndsWith,
            nameof(string.Contains) => LogicType.Match,
            nameof(string.Equals) => LogicType.Equal,
            _ => throw new NotSupportedException($"String method '{methodName}' is not supported.")
        };

        _filters.Push(new AppSchemaDataFilterBinary(logic, target, operand));
    }

    // Gets the final filter from the stack.
    void HandleCollectionContains(AppSchemaDataFilter? instance, AppSchemaDataFilter[] args)
    {
        (AppSchemaDataFilterValue collection, AppSchemaDataFilter field) = ResolveCollectionAndField(instance, args);
        _filters.Push(new AppSchemaDataFilterBinary(LogicType.Contains, collection, field));
    }

    // Resolves the target and operand for instance methods.
    (AppSchemaDataFilter target, AppSchemaDataFilter operand) ResolveInstanceAndOperand(AppSchemaDataFilter? instance, AppSchemaDataFilter[] args)
    {
        if (instance != null)
        {
            if (args.Length <1)
                throw new NotSupportedException("String method requires at least one argument.");
            return (instance, args[0]);
        }

        if (args.Length <2)
            throw new NotSupportedException("Static string method requires at least two arguments.");

        return (args[0], args[1]);
    }

    // Resolves the collection and field for Contains method.
    (AppSchemaDataFilterValue collection, AppSchemaDataFilter field) ResolveCollectionAndField(AppSchemaDataFilter? instance, AppSchemaDataFilter[] args)
    {
        AppSchemaDataFilter[] candidates = BuildCandidates(instance, args).ToArray();
        AppSchemaDataFilterValue? valueCandidate = candidates.OfType<AppSchemaDataFilterValue>()
            .FirstOrDefault(v => IsEnumerable(v.Value));
        AppSchemaDataFilter? fieldCandidate = candidates.FirstOrDefault(f => f is AppSchemaDataFilterField);

        if (valueCandidate == null || fieldCandidate == null)
            throw new NotSupportedException("Contains must compare an enumerable with a field.");

        return (EnsureEnumerable(valueCandidate), fieldCandidate);
    }

    // Negates the given filter.
    static IEnumerable<AppSchemaDataFilter> BuildCandidates(AppSchemaDataFilter? instance, AppSchemaDataFilter[] args)
    {
        if (instance != null)
            yield return instance;
        foreach (AppSchemaDataFilter arg in args)
            yield return arg;
    }

    // Negates the given filter.
    static bool IsEnumerable(object? value) => value is ArrayNode or IEnumerable and not string;

    // Negates the given filter.
    static AppSchemaDataFilterValue EnsureEnumerable(AppSchemaDataFilterValue value)
    {
        if (value.Value is ArrayNode)
            return value;

        if (value.Value is IEnumerable enumerable && value.Value is not string)
        {
            object?[] array = enumerable.Cast<object?>().ToArray();
            return new AppSchemaDataFilterValue(array);
        }

        throw new NotSupportedException("The collection provided to Contains must be enumerable.");
    }

    // Negates the given filter.
    AppSchemaDataFilter[] PopArguments(int count)
    {
        AppSchemaDataFilter[] items = new AppSchemaDataFilter[count];
        for (int i = count -1; i >=0; i--)
            items[i] = Pop();
        return items;
    }

    // Trims StringComparison arguments from the end of the arguments list.
    AppSchemaDataFilter Pop()
    {
        if (_filters.Count ==0)
            throw new NotSupportedException("The expression stack is empty.");
        return _filters.Pop();
    }

    // Negates the given filter.
    AppSchemaDataFilter GetFilter()
    {
        if (_filters.Count !=1)
            throw new NotSupportedException("The expression tree did not collapse to a single filter.");
        return _filters.Pop();
    }

    // Negates the given filter.
    static AppSchemaDataFilter[] TrimStringComparison(AppSchemaDataFilter[] args)
    {
        if (args.Length >0 && args[^1] is AppSchemaDataFilterValue { Value: StringComparison })
            return args.Take(args.Length -1).ToArray();
        return args;
    }

    // Evaluates a member expression to get its value.
    static AppSchemaDataFilter Negate(AppSchemaDataFilter operand) => operand switch {
        AppSchemaDataFilterValue value when value.Value is bool boolValue => new AppSchemaDataFilterValue(!boolValue),
        AppSchemaDataFilterField field => new AppSchemaDataFilterBinary(LogicType.Equal, field, new AppSchemaDataFilterValue(false)),
        AppSchemaDataFilterBinary binary => new AppSchemaDataFilterBinary(Negate(binary.Type), binary.Left, binary.Right),
        _ => throw new NotSupportedException("The operand cannot be negated.")
    };

    // Negates the given logic type.
    static LogicType Negate(LogicType type) => type switch {
        LogicType.Equal => LogicType.NotEqual,
        LogicType.NotEqual => LogicType.Equal,
        LogicType.GreaterThan => LogicType.LessEqual,
        LogicType.GreaterEqual => LogicType.LessThan,
        LogicType.LessThan => LogicType.GreaterEqual,
        LogicType.LessEqual => LogicType.GreaterThan,
        LogicType.Contains => LogicType.NotContains,
        LogicType.NotContains => LogicType.Contains,
        LogicType.StartsWith => LogicType.NotStartsWith,
        LogicType.NotStartsWith => LogicType.StartsWith,
        LogicType.EndsWith => LogicType.NotEndsWith,
        LogicType.NotEndsWith => LogicType.EndsWith,
        LogicType.Match => LogicType.NotMatch,
        LogicType.NotMatch => LogicType.Match,
        _ => throw new NotSupportedException($"The logic type '{type}' cannot be negated.")
    };

    // Evaluates a member expression to get its value.
    static object? EvaluateMember(MemberExpression node)
    {
        object? instance = node.Expression != null ? Evaluate(node.Expression) : null;
        return node.Member switch {
            FieldInfo fieldInfo => fieldInfo.GetValue(instance),
            PropertyInfo propertyInfo => propertyInfo.GetValue(instance),
            _ => throw new NotSupportedException($"Unsupported member '{node.Member.Name}'.")
        };
    }

    // Evaluates an expression to get its value.
    static object? Evaluate(Expression expression) => expression switch {
        ConstantExpression constant => constant.Value,
        MemberExpression member => EvaluateMember(member),
        _ => Expression.Lambda(expression).Compile().DynamicInvoke()
    };

    // Stack of filters being built.
    bool ContainsParameter(Expression? expression)
    {
        if (expression == null || string.IsNullOrEmpty(_parameterName))
            return false;

        return expression switch {
            ParameterExpression parameter => parameter.Name == _parameterName,
            MemberExpression member => ContainsParameter(member.Expression),
            UnaryExpression unary => ContainsParameter(unary.Operand),
            BinaryExpression binary => ContainsParameter(binary.Left) || ContainsParameter(binary.Right),
            MethodCallExpression method => (method.Object != null && ContainsParameter(method.Object)) || method.Arguments.Any(ContainsParameter),
            ConditionalExpression conditional => ContainsParameter(conditional.Test) || ContainsParameter(conditional.IfTrue) || ContainsParameter(conditional.IfFalse),
            InvocationExpression invocation => ContainsParameter(invocation.Expression) || invocation.Arguments.Any(ContainsParameter),
            LambdaExpression lambda => lambda.Parameters.Any(p => p.Name == _parameterName) || ContainsParameter(lambda.Body),
            NewArrayExpression newArray => newArray.Expressions.Any(ContainsParameter),
            NewExpression newExpression => newExpression.Arguments.Any(ContainsParameter),
            ListInitExpression listInit => ContainsParameter(listInit.NewExpression) || listInit.Initializers.SelectMany(i => i.Arguments).Any(ContainsParameter),
            MemberInitExpression memberInit => ContainsParameter(memberInit.NewExpression) || memberInit.Bindings.OfType<MemberAssignment>().Any(binding => ContainsParameter(binding.Expression)),
            _ => false 
        };
    }
}