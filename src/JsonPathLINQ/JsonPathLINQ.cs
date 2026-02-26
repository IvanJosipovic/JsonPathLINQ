using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace JsonPathLINQ;

public static class JsonPathLINQ
{
    private static readonly MethodInfo EnumerableFirstOrDefaultWithPredicate = typeof(Enumerable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m =>
            m.Name == nameof(Enumerable.FirstOrDefault) &&
            m.IsGenericMethodDefinition &&
            m.GetParameters() is var parameters &&
            parameters.Length == 2 &&
            parameters[1].ParameterType.IsGenericType &&
            parameters[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<,>));

    private static readonly MethodInfo EnumerableElementAt = typeof(Enumerable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m =>
            m.Name == nameof(Enumerable.ElementAt) &&
            m.IsGenericMethodDefinition &&
            m.GetParameters() is var parameters &&
            parameters.Length == 2 &&
            parameters[1].ParameterType == typeof(int));

    /// <summary>
    /// Returns a Expression representing the jsonPath
    /// </summary>
    /// <typeparam name="T">source object</typeparam>
    /// <param name="jsonPath">jsonPath</param>
    /// <param name="addNullChecks">add null checks</param>
    /// <returns></returns>
    public static Expression<Func<T, object>> GetExpression<T>(string jsonPath, bool addNullChecks = false)
    {
        return GetExpression<T, object>(jsonPath, addNullChecks);
    }

    /// <summary>
    /// Returns a Expression representing the jsonPath
    /// </summary>
    /// <typeparam name="T">source object</typeparam>
    /// <typeparam name="T2">return type</typeparam>
    /// <param name="jsonPath">jsonPath</param>
    /// <param name="addNullChecks">add null checks</param>
    /// <returns></returns>
    public static Expression<Func<T, T2>> GetExpression<T, T2>(string jsonPath, bool addNullChecks = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(jsonPath);

        if (jsonPath[0] != '{')
        {
            jsonPath = '{' + jsonPath;
        }

        if (jsonPath[^1] != '}')
        {
            jsonPath += '}';
        }

        var jp = Parser.Parse("query", jsonPath);

        if (jp.Root.Nodes.Count != 1)
        {
            throw new NotSupportedException("Only a single root action is supported.");
        }

        var parameter = Expression.Parameter(typeof(T), "x");
        Expression body = Generate(jp.Root.Nodes[0], parameter);

        if (addNullChecks)
        {
            body = CreateNullChecks(body);
        }

        if (typeof(T2) == typeof(object))
        {
            body = Expression.Convert(body, typeof(object));
        }
        else if (body.Type != typeof(T2))
        {
            body = Expression.Convert(body, typeof(T2));
        }

        return Expression.Lambda<Func<T, T2>>(body, parameter);
    }

    public static Expression Generate(INode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var parameter = Expression.Parameter(typeof(object), "x");
        return Generate(node, parameter);
    }

    public static Expression CreateNullChecks(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        expression = StripObjectConversion(expression);

        var candidates = new List<Expression>();
        CollectNullCheckCandidates(expression, candidates);
        if (candidates.Count == 0)
        {
            return expression;
        }

        Expression result = expression;
        for (var i = candidates.Count - 1; i >= 0; i--)
        {
            var candidate = candidates[i];
            var test = Expression.Equal(candidate, Expression.Constant(null, candidate.Type));
            var fallback = CreateFallback(result.Type);
            result = Expression.Condition(test, fallback, result);
        }

        return result;
    }

    private static Expression Generate(INode node, Expression current)
    {
        return node switch
        {
            ListNode listNode => GenerateList(listNode, current),
            FieldNode fieldNode => GenerateField(fieldNode, current),
            FilterNode filterNode => GenerateFilter(filterNode, current),
            ArrayNode arrayNode => GenerateArray(arrayNode, current),
            TextNode textNode => Expression.Constant(textNode.Text),
            BoolNode boolNode => Expression.Constant(boolNode.Value),
            IntNode intNode => Expression.Constant(intNode.Value),
            FloatNode floatNode => Expression.Constant(floatNode.Value),
            IdentifierNode identifierNode => throw new NotSupportedException($"Identifier node '{identifierNode.Name}' is not supported."),
            WildcardNode => throw new NotSupportedException("Wildcard is not supported in expression generation."),
            RecursiveNode => throw new NotSupportedException("Recursive descent is not supported in expression generation."),
            UnionNode => throw new NotSupportedException("Union is not supported in expression generation."),
            _ => throw new NotSupportedException($"Node type '{node.Type}' is not supported.")
        };
    }

    private static Expression GenerateList(ListNode node, Expression current)
    {
        var result = current;
        foreach (var child in node.Nodes)
        {
            result = Generate(child, result);
        }

        return result;
    }

    private static Expression GenerateField(FieldNode node, Expression source)
    {
        if (TryGenerateDictionaryAccess(source, node.Value, out var dictionaryAccess))
        {
            return dictionaryAccess;
        }

        if (TryGenerateMemberAccess(source, node.Value, out var memberAccess))
        {
            return memberAccess;
        }

        throw new NotSupportedException($"Field '{node.Value}' was not found on type '{source.Type}'.");
    }

    private static Expression GenerateArray(ArrayNode node, Expression source)
    {
        if (node.Params.Length != 3)
        {
            throw new NotSupportedException("Array parameters are not supported.");
        }

        var start = node.Params[0];
        var end = node.Params[1];
        var step = node.Params[2];

        var isSingleIndex = start.Known && end.Known && end.Derived && !step.Known;
        if (!isSingleIndex)
        {
            throw new NotSupportedException("Only single array index access is supported.");
        }

        if (start.Value < 0)
        {
            throw new NotSupportedException("Negative indexes are not supported.");
        }

        var elementType = GetEnumerableElementType(source.Type) ?? throw new NotSupportedException($"'{source.Type}' is not enumerable.");
        var enumerable = EnsureEnumerable(source, elementType);
        return Expression.Call(EnumerableElementAt.MakeGenericMethod(elementType), enumerable, Expression.Constant(start.Value));
    }

    private static Expression GenerateFilter(FilterNode node, Expression source)
    {
        var elementType = GetEnumerableElementType(source.Type) ?? throw new NotSupportedException($"'{source.Type}' is not enumerable and cannot be filtered.");

        var parameter = Expression.Parameter(elementType, "y");
        var left = Generate(node.Left, parameter);

        Expression predicateBody;
        if (node.Operator == "exists")
        {
            predicateBody = CanBeNull(left.Type)
                ? Expression.NotEqual(left, Expression.Constant(null, left.Type))
                : Expression.Constant(true);
        }
        else
        {
            var right = Generate(node.Right, parameter);
            predicateBody = BuildFilterComparison(left, right, node.Operator);
        }

        var predicate = Expression.Lambda(predicateBody, parameter);
        var enumerable = EnsureEnumerable(source, elementType);

        return Expression.Call(
            EnumerableFirstOrDefaultWithPredicate.MakeGenericMethod(elementType),
            enumerable,
            predicate);
    }

    private static Expression BuildFilterComparison(Expression left, Expression right, string @operator)
    {
        (left, right) = AlignComparisonTypes(left, right);

        return @operator switch
        {
            "==" => Expression.Equal(left, right),
            "!=" => Expression.NotEqual(left, right),
            "<" => Expression.LessThan(left, right),
            ">" => Expression.GreaterThan(left, right),
            "<=" => Expression.LessThanOrEqual(left, right),
            ">=" => Expression.GreaterThanOrEqual(left, right),
            _ => throw new NotSupportedException($"Filter operator '{@operator}' is not supported.")
        };
    }

    private static (Expression Left, Expression Right) AlignComparisonTypes(Expression left, Expression right)
    {
        if (left.Type == right.Type)
        {
            return (left, right);
        }

        if (right is ConstantExpression rightConstant && rightConstant.Value is null && CanBeNull(left.Type))
        {
            return (left, Expression.Constant(null, left.Type));
        }

        if (left is ConstantExpression leftConstant && leftConstant.Value is null && CanBeNull(right.Type))
        {
            return (Expression.Constant(null, right.Type), right);
        }

        if (CanConvert(right.Type, left.Type))
        {
            return (left, Expression.Convert(right, left.Type));
        }

        if (CanConvert(left.Type, right.Type))
        {
            return (Expression.Convert(left, right.Type), right);
        }

        return (left, right);
    }

    private static bool TryGenerateMemberAccess(Expression source, string name, out Expression expression)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;

        var property = source.Type.GetProperty(name, flags);
        if (property != null && property.GetIndexParameters().Length == 0)
        {
            expression = Expression.Property(source, property);
            return true;
        }

        var field = source.Type.GetField(name, flags);
        if (field != null)
        {
            expression = Expression.Field(source, field);
            return true;
        }

        expression = null!;
        return false;
    }

    private static bool TryGenerateDictionaryAccess(Expression source, string key, out Expression expression)
    {
        if (TryGetGenericDictionary(source.Type, out var dictionaryType))
        {
            var args = dictionaryType.GetGenericArguments();
            var keyType = args[0];
            if (TryConvertStringKey(key, keyType, out var convertedKey))
            {
                var indexer = source.Type.GetProperty("Item", new[] { keyType }) ??
                              dictionaryType.GetProperty("Item", new[] { keyType });
                if (indexer != null)
                {
                    var declaringType = indexer.DeclaringType!;
                    var typedSource = declaringType.IsAssignableFrom(source.Type)
                        ? source
                        : Expression.Convert(source, declaringType);
                    var call = Expression.Call(typedSource, indexer.GetMethod!, Expression.Constant(convertedKey, keyType));
                    expression = Expression.Convert(call, typeof(object));
                    return true;
                }
            }
        }

        if (typeof(IDictionary).IsAssignableFrom(source.Type))
        {
            var typedSource = source.Type == typeof(IDictionary) ? source : Expression.Convert(source, typeof(IDictionary));
            var indexer = typeof(IDictionary).GetProperty("Item", [typeof(object)])!;
            var call = Expression.Call(typedSource, indexer.GetMethod!, Expression.Constant(key, typeof(object)));
            expression = Expression.Convert(call, typeof(object));
            return true;
        }

        expression = null!;
        return false;
    }

    private static bool TryGetGenericDictionary(Type type, out Type dictionaryType)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDictionary<,>))
        {
            dictionaryType = type;
            return true;
        }

        var iface = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));

        if (iface != null)
        {
            dictionaryType = iface;
            return true;
        }

        dictionaryType = null!;
        return false;
    }

    private static bool TryConvertStringKey(string key, Type keyType, out object converted)
    {
        if (keyType == typeof(string))
        {
            converted = key;
            return true;
        }

        try
        {
            converted = Convert.ChangeType(key, keyType, CultureInfo.InvariantCulture)!;
            return true;
        }
        catch
        {
            converted = default!;
            return false;
        }
    }

    private static Type? GetEnumerableElementType(Type type)
    {
        if (type == typeof(string))
        {
            return null;
        }

        if (type.IsArray)
        {
            return type.GetElementType();
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return type.GetGenericArguments()[0];
        }

        return type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            ?.GetGenericArguments()[0];
    }

    private static Expression EnsureEnumerable(Expression source, Type elementType)
    {
        var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
        return enumerableType.IsAssignableFrom(source.Type)
            ? source
            : Expression.Convert(source, enumerableType);
    }

    private static bool CanConvert(Type source, Type destination)
    {
        if (destination.IsAssignableFrom(source))
        {
            return true;
        }

        var sourceCore = Nullable.GetUnderlyingType(source) ?? source;
        var destinationCore = Nullable.GetUnderlyingType(destination) ?? destination;

        if (IsNumeric(sourceCore) && IsNumeric(destinationCore))
        {
            return true;
        }

        return false;
    }

    private static bool IsNumeric(Type type)
    {
        return type == typeof(byte) || type == typeof(sbyte) ||
               type == typeof(short) || type == typeof(ushort) ||
               type == typeof(int) || type == typeof(uint) ||
               type == typeof(long) || type == typeof(ulong) ||
               type == typeof(float) || type == typeof(double) ||
               type == typeof(decimal);
    }

    private static Expression StripObjectConversion(Expression expression)
    {
        while (expression is UnaryExpression unary &&
               (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked) &&
               unary.Type == typeof(object))
        {
            expression = unary.Operand;
        }

        return expression;
    }

    private static void CollectNullCheckCandidates(Expression expression, List<Expression> candidates)
    {
        switch (expression)
        {
            case MemberExpression memberExpression:
                if (memberExpression.Expression != null)
                {
                    CollectNullCheckCandidates(memberExpression.Expression, candidates);
                }

                if (CanBeNull(memberExpression.Type))
                {
                    candidates.Add(memberExpression);
                }

                break;
            case MethodCallExpression methodCallExpression:
                if (methodCallExpression.Object != null)
                {
                    CollectNullCheckCandidates(methodCallExpression.Object, candidates);
                }
                else if (methodCallExpression.Arguments.Count > 0)
                {
                    CollectNullCheckCandidates(methodCallExpression.Arguments[0], candidates);
                }

                if (CanBeNull(methodCallExpression.Type))
                {
                    candidates.Add(methodCallExpression);
                }

                break;
            case IndexExpression indexExpression:
                if (indexExpression.Object != null)
                {
                    CollectNullCheckCandidates(indexExpression.Object, candidates);
                }

                if (CanBeNull(indexExpression.Type))
                {
                    candidates.Add(indexExpression);
                }

                break;
            case UnaryExpression unaryExpression
                when unaryExpression.NodeType == ExpressionType.Convert || unaryExpression.NodeType == ExpressionType.ConvertChecked:
                CollectNullCheckCandidates(unaryExpression.Operand, candidates);
                break;
        }
    }

    private static Expression CreateFallback(Type type)
    {
        if (type == typeof(string))
        {
            return Expression.Constant(string.Empty, typeof(string));
        }

        return Expression.Default(type);
    }

    private static bool CanBeNull(Type type)
    {
        return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
    }
}
