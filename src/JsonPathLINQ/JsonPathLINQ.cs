using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace JsonPathLINQ;

public static class JsonPath
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

    private static readonly MethodInfo GetJsonElementPropertyMethod = GetMethod(nameof(GetJsonElementProperty));
    private static readonly MethodInfo GetJsonNodePropertyMethod = GetMethod(nameof(GetJsonNodeProperty));
    private static readonly MethodInfo GetLateBoundMemberMethod = GetMethod(nameof(GetLateBoundMember));
    private static readonly MethodInfo GetExtensionDataValueMethod = GetMethod(nameof(GetExtensionDataValue));
    private static readonly MethodInfo GetDynamicArrayIndexMethod = GetMethod(nameof(GetDynamicArrayIndex));
    private static readonly MethodInfo EnumerateDynamicMethod = GetMethod(nameof(EnumerateDynamic));
    private static readonly MethodInfo CompareDynamicValuesMethod = GetMethod(nameof(CompareDynamicValues));
    private static readonly MethodInfo GetJsonElementValueOrSelfMethod = GetMethod(nameof(GetJsonElementValueOrSelf));
    private static readonly MethodInfo GetJsonNodeValueOrSelfMethod = GetMethod(nameof(GetJsonNodeValueOrSelf));
    private static readonly MethodInfo GetJsonDocumentValueOrSelfMethod = GetMethod(nameof(GetJsonDocumentValueOrSelf));
    private static readonly MethodInfo EvaluateRuntimePathMethod = GetMethod(nameof(EvaluateRuntimePath));

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
    /// <typeparam name="TResult">return type</typeparam>
    /// <param name="jsonPath">jsonPath</param>
    /// <param name="addNullChecks">add null checks</param>
    /// <returns></returns>
    public static Expression<Func<T, TResult>> GetExpression<T, TResult>(string jsonPath, bool addNullChecks = false)
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
        Expression body;
        if (RequiresRuntimeEvaluation(jp.Root.Nodes[0]))
        {
            body = Expression.Call(
                EvaluateRuntimePathMethod,
                Expression.Constant(jp.Root.Nodes[0], typeof(INode)),
                Expression.Convert(parameter, typeof(object)));
        }
        else
        {
            body = Generate(jp.Root.Nodes[0], parameter);
            body = NormalizeTerminalExpression(body);
        }

        if (addNullChecks)
        {
            body = CreateNullChecks(body);
        }

        if (typeof(TResult) == typeof(object))
        {
            body = Expression.Convert(body, typeof(object));
        }
        else if (body.Type != typeof(TResult))
        {
            body = Expression.Convert(body, typeof(TResult));
        }

        return Expression.Lambda<Func<T, TResult>>(body, parameter);
    }

    public static Expression Generate(INode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var parameter = Expression.Parameter(typeof(object), "x");
        return RequiresRuntimeEvaluation(node)
            ? Expression.Call(EvaluateRuntimePathMethod, Expression.Constant(node, typeof(INode)), parameter)
            : Generate(node, parameter);
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
            IdentifierNode identifierNode => GenerateIdentifier(identifierNode),
            WildcardNode => throw new NotSupportedException("Wildcard is not supported in expression generation."),
            RecursiveNode => throw new NotSupportedException("Recursive descent is not supported in expression generation."),
            UnionNode => throw new NotSupportedException("Union is not supported in expression generation."),
            _ => throw new NotSupportedException($"Node type '{node.Type}' is not supported.")
        };
    }

    private static bool RequiresRuntimeEvaluation(INode node)
    {
        return node switch
        {
            WildcardNode => true,
            RecursiveNode => true,
            UnionNode => true,
            ArrayNode arrayNode => arrayNode.Params.Length != 3 || !IsSupportedSingleIndex(arrayNode) || arrayNode.Params[0].Value < 0,
            FilterNode filterNode => RequiresRuntimeEvaluation(filterNode.Left) || RequiresRuntimeEvaluation(filterNode.Right),
            ListNode listNode => listNode.Nodes.Any(RequiresRuntimeEvaluation),
            _ => false
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

    private static Expression GenerateIdentifier(IdentifierNode node)
    {
        return node.Name switch
        {
            "null" => Expression.Constant(null, typeof(object)),
            _ => throw new NotSupportedException($"Identifier node '{node.Name}' is not supported.")
        };
    }

    private static Expression GenerateField(FieldNode node, Expression source)
    {
        if (source.Type == typeof(JsonDocument))
        {
            return GenerateField(node, Expression.Property(source, nameof(JsonDocument.RootElement)));
        }

        if (TryGenerateJsonAccess(source, node.Value, out var jsonAccess))
        {
            return jsonAccess;
        }

        if (TryGenerateDictionaryAccess(source, node.Value, out var dictionaryAccess))
        {
            return dictionaryAccess;
        }

        if (TryGenerateMemberAccess(source, node.Value, out var memberAccess))
        {
            return memberAccess;
        }

        if (source.Type != typeof(object) && TryGetExtensionDataMember(source.Type, out _))
        {
            return Expression.Call(
                GetExtensionDataValueMethod,
                Expression.Convert(source, typeof(object)),
                Expression.Constant(node.Value));
        }

        if (source.Type == typeof(object))
        {
            return Expression.Call(GetLateBoundMemberMethod, source, Expression.Constant(node.Value));
        }

        throw new NotSupportedException($"Field '{node.Value}' was not found on type '{source.Type}'.");
    }

    internal static Expression GenerateArray(ArrayNode node, Expression source)
    {
        if (node.Params.Length != 3)
        {
            throw new NotSupportedException("Array parameters are not supported.");
        }

        if (!IsSupportedSingleIndex(node))
        {
            throw new NotSupportedException("Only single array index access is supported.");
        }

        var start = node.Params[0];
        if (start.Value < 0)
        {
            throw new NotSupportedException("Negative indexes are not supported.");
        }

        if (source.Type == typeof(JsonDocument))
        {
            return GenerateArray(node, Expression.Property(source, nameof(JsonDocument.RootElement)));
        }

        if (RequiresDynamicArrayAccess(source.Type))
        {
            return Expression.Call(
                GetDynamicArrayIndexMethod,
                Expression.Convert(source, typeof(object)),
                Expression.Constant(start.Value));
        }

        var elementType = GetEnumerableElementType(source.Type) ?? throw new NotSupportedException($"'{source.Type}' is not enumerable.");
        var enumerable = EnsureEnumerable(source, elementType);
        return Expression.Call(EnumerableElementAt.MakeGenericMethod(elementType), enumerable, Expression.Constant(start.Value));
    }

    private static bool IsSupportedSingleIndex(ArrayNode node)
    {
        if (node.Params.Length != 3)
        {
            return false;
        }

        var start = node.Params[0];
        var end = node.Params[1];
        var step = node.Params[2];
        return start.Known && end.Known && end.Derived && !step.Known;
    }

    private static Expression GenerateFilter(FilterNode node, Expression source)
    {
        if (source.Type == typeof(JsonDocument))
        {
            return GenerateFilter(node, Expression.Property(source, nameof(JsonDocument.RootElement)));
        }

        Expression enumerable;
        Type elementType;
        if (RequiresDynamicEnumeration(source.Type))
        {
            elementType = typeof(object);
            enumerable = Expression.Call(EnumerateDynamicMethod, Expression.Convert(source, typeof(object)));
        }
        else
        {
            elementType = GetEnumerableElementType(source.Type) ?? throw new NotSupportedException($"'{source.Type}' is not enumerable and cannot be filtered.");
            enumerable = EnsureEnumerable(source, elementType);
        }

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

        return Expression.Call(
            EnumerableFirstOrDefaultWithPredicate.MakeGenericMethod(elementType),
            enumerable,
            predicate);
    }

    internal static Expression BuildFilterComparison(Expression left, Expression right, string @operator)
    {
        if (RequiresDynamicComparison(left.Type, right.Type))
        {
            return Expression.Call(
                CompareDynamicValuesMethod,
                Expression.Convert(left, typeof(object)),
                Expression.Convert(right, typeof(object)),
                Expression.Constant(@operator));
        }

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

    internal static (Expression Left, Expression Right) AlignComparisonTypes(Expression left, Expression right)
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

    private static bool TryGenerateJsonAccess(Expression source, string name, out Expression expression)
    {
        if (source.Type == typeof(JsonElement))
        {
            expression = Expression.Call(GetJsonElementPropertyMethod, source, Expression.Constant(name));
            return true;
        }

        if (typeof(JsonNode).IsAssignableFrom(source.Type))
        {
            expression = Expression.Call(GetJsonNodePropertyMethod, Expression.Convert(source, typeof(JsonNode)), Expression.Constant(name));
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

    internal static bool TryConvertStringKey(string key, Type keyType, out object converted)
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

    internal static Type? GetEnumerableElementType(Type type)
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

    internal static Expression EnsureEnumerable(Expression source, Type elementType)
    {
        var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
        return enumerableType.IsAssignableFrom(source.Type)
            ? source
            : Expression.Convert(source, enumerableType);
    }

    internal static bool CanConvert(Type source, Type destination)
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

    private static bool RequiresDynamicEnumeration(Type type)
    {
        return type == typeof(object) || type == typeof(JsonElement) || typeof(JsonNode).IsAssignableFrom(type);
    }

    private static bool RequiresDynamicArrayAccess(Type type)
    {
        return type == typeof(object) || type == typeof(JsonElement) || typeof(JsonNode).IsAssignableFrom(type);
    }

    private static bool RequiresDynamicComparison(Type leftType, Type rightType)
    {
        return leftType == typeof(object) ||
               rightType == typeof(object) ||
               leftType == typeof(JsonElement) ||
               rightType == typeof(JsonElement) ||
               typeof(JsonNode).IsAssignableFrom(leftType) ||
               typeof(JsonNode).IsAssignableFrom(rightType);
    }

    private static Expression NormalizeTerminalExpression(Expression expression)
    {
        if (expression.Type == typeof(JsonElement))
        {
            return Expression.Call(GetJsonElementValueOrSelfMethod, expression);
        }

        if (expression.Type == typeof(JsonDocument))
        {
            return Expression.Call(GetJsonDocumentValueOrSelfMethod, expression);
        }

        if (typeof(JsonNode).IsAssignableFrom(expression.Type))
        {
            return Expression.Call(GetJsonNodeValueOrSelfMethod, Expression.Convert(expression, typeof(JsonNode)));
        }

        return expression;
    }

    private static MethodInfo GetMethod(string name)
    {
        return typeof(JsonPath).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
               ?? throw new MissingMethodException(typeof(JsonPath).FullName, name);
    }

    internal static object? GetLateBoundMember(object? source, string name)
    {
        if (source is null)
        {
            return null;
        }

        if (source is JsonDocument document)
        {
            return GetJsonElementProperty(document.RootElement, name);
        }

        if (source is JsonElement element)
        {
            return GetJsonElementProperty(element, name);
        }

        if (source is JsonNode node)
        {
            return GetJsonNodeProperty(node, name);
        }

        if (source is IDictionary dictionary)
        {
            return dictionary.Contains(name) ? dictionary[name] : null;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;
        var sourceType = source.GetType();

        var property = sourceType.GetProperty(name, flags);
        if (property != null && property.GetIndexParameters().Length == 0)
        {
            return property.GetValue(source);
        }

        var field = sourceType.GetField(name, flags);
        if (field != null)
        {
            return field.GetValue(source);
        }

        foreach (var candidateProperty in sourceType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (candidateProperty.GetIndexParameters().Length != 0)
            {
                continue;
            }

            var jsonName = candidateProperty.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
            if (string.Equals(jsonName, name, StringComparison.OrdinalIgnoreCase))
            {
                return candidateProperty.GetValue(source);
            }
        }

        return GetExtensionDataValue(source, name);
    }

    internal static object? GetExtensionDataValue(object? source, string name)
    {
        if (source is null || !TryGetExtensionDataMember(source.GetType(), out var member))
        {
            return null;
        }

        var extensionData = member switch
        {
            PropertyInfo property when property.GetIndexParameters().Length == 0 => property.GetValue(source),
            FieldInfo field => field.GetValue(source),
            _ => null,
        };

        return TryGetDictionaryValue(extensionData, name, out var value) ? value : null;
    }

    private static bool TryGetExtensionDataMember(Type sourceType, out MemberInfo member)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

        member = sourceType.GetProperties(flags)
            .Where(property => property.GetIndexParameters().Length == 0)
            .FirstOrDefault(property => property.GetCustomAttribute<JsonExtensionDataAttribute>() != null)!;
        if (member != null)
        {
            return true;
        }

        member = sourceType.GetFields(flags)
            .FirstOrDefault(field => field.GetCustomAttribute<JsonExtensionDataAttribute>() != null)!;
        return member != null;
    }

    private static bool TryGetDictionaryValue(object? dictionary, string key, out object? value)
    {
        if (dictionary is IDictionary nonGenericDictionary && nonGenericDictionary.Contains(key))
        {
            value = nonGenericDictionary[key];
            return true;
        }

        if (dictionary is not null && TryGetGenericDictionary(dictionary.GetType(), out var dictionaryType))
        {
            var keyType = dictionaryType.GetGenericArguments()[0];
            var valueType = dictionaryType.GetGenericArguments()[1];
            if (TryConvertStringKey(key, keyType, out var convertedKey))
            {
                var indexer = dictionaryType.GetProperty("Item", [keyType]);
                if (indexer?.GetMethod != null)
                {
                    var tryGetValue = dictionaryType.GetMethod("TryGetValue", [keyType, valueType.MakeByRefType()]);
                    if (tryGetValue != null)
                    {
                        var arguments = new object?[] { convertedKey, null };
                        if (tryGetValue.Invoke(dictionary, arguments) is true)
                        {
                            value = arguments[1];
                            return true;
                        }
                    }
                }
            }
        }

        value = null;
        return false;
    }

    internal static object? GetJsonElementProperty(JsonElement source, string name)
    {
        if (source.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (source.TryGetProperty(name, out var exactProperty))
        {
            return ConvertJsonElementValue(exactProperty);
        }

        foreach (var property in source.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return ConvertJsonElementValue(property.Value);
            }
        }

        return null;
    }

    internal static object? GetJsonNodeProperty(JsonNode? source, string name)
    {
        if (source is not JsonObject jsonObject)
        {
            return null;
        }

        if (jsonObject.TryGetPropertyValue(name, out var exactValue))
        {
            return ConvertJsonNodeValue(exactValue);
        }

        foreach (var property in jsonObject)
        {
            if (string.Equals(property.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return ConvertJsonNodeValue(property.Value);
            }
        }

        return null;
    }

    internal static object? GetDynamicArrayIndex(object? source, int index)
    {
        if (source is null)
        {
            return null;
        }

        if (source is JsonDocument document)
        {
            return GetDynamicArrayIndex(document.RootElement, index);
        }

        if (source is JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Array)
            {
                throw new NotSupportedException($"'{nameof(JsonElement)}' is not an array.");
            }

            if (index < 0 || index >= element.GetArrayLength())
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return ConvertJsonElementValue(element[index]);
        }

        if (source is JsonArray jsonArray)
        {
            if (index < 0 || index >= jsonArray.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return ConvertJsonNodeValue(jsonArray[index]);
        }

        if (source is JsonNode jsonNode)
        {
            if (jsonNode is not JsonArray array)
            {
                throw new NotSupportedException($"'{jsonNode.GetType()}' is not an array.");
            }

            if (index < 0 || index >= array.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return ConvertJsonNodeValue(array[index]);
        }

        if (source is string || source is IDictionary || source is not IEnumerable enumerable)
        {
            throw new NotSupportedException($"'{source.GetType()}' is not enumerable.");
        }

        var current = 0;
        foreach (var item in enumerable)
        {
            if (current == index)
            {
                return item;
            }

            current++;
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    internal static IEnumerable<object?> EnumerateDynamic(object? source)
    {
        if (source is null)
        {
            yield break;
        }

        if (source is JsonDocument document)
        {
            foreach (var item in EnumerateDynamic(document.RootElement))
            {
                yield return item;
            }

            yield break;
        }

        if (source is JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Array)
            {
                throw new NotSupportedException($"'{nameof(JsonElement)}' is not enumerable.");
            }

            foreach (var item in element.EnumerateArray())
            {
                yield return ConvertJsonElementValue(item);
            }

            yield break;
        }

        if (source is JsonArray jsonArray)
        {
            foreach (var item in jsonArray)
            {
                yield return ConvertJsonNodeValue(item);
            }

            yield break;
        }

        if (source is JsonNode jsonNode)
        {
            if (jsonNode is not JsonArray array)
            {
                throw new NotSupportedException($"'{jsonNode.GetType()}' is not enumerable.");
            }

            foreach (var item in array)
            {
                yield return ConvertJsonNodeValue(item);
            }

            yield break;
        }

        if (source is string || source is IDictionary || source is not IEnumerable enumerable)
        {
            throw new NotSupportedException($"'{source.GetType()}' is not enumerable.");
        }

        foreach (var item in enumerable)
        {
            yield return item;
        }
    }

    internal static bool CompareDynamicValues(object? left, object? right, string @operator)
    {
        left = NormalizeDynamicValue(left);
        right = NormalizeDynamicValue(right);

        var comparison = CompareNormalizedValues(left, right);
        return @operator switch
        {
            "==" => comparison == 0,
            "!=" => comparison != 0,
            "<" => comparison < 0,
            ">" => comparison > 0,
            "<=" => comparison <= 0,
            ">=" => comparison >= 0,
            _ => throw new NotSupportedException($"Filter operator '{@operator}' is not supported.")
        };
    }

    internal static object? EvaluateRuntimePath(INode node, object? input)
    {
        ArgumentNullException.ThrowIfNull(node);

        var results = EvaluateRuntimeNodes(node is ListNode listNode ? listNode.Nodes : [node], [input]);
        return CollapseRuntimeResults(results);
    }

    private static List<object?> EvaluateRuntimeNodes(IReadOnlyList<INode> nodes, List<object?> currentValues)
    {
        var results = currentValues;

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            results = node switch
            {
                ListNode listNode => EvaluateRuntimeNodes(listNode.Nodes, results),
                FieldNode fieldNode => ApplyRuntimeField(results, fieldNode.Value),
                ArrayNode arrayNode => ApplyRuntimeArray(results, arrayNode),
                FilterNode filterNode => ApplyRuntimeFilter(results, filterNode),
                WildcardNode => ApplyRuntimeWildcard(results),
                RecursiveNode when i < nodes.Count - 1 => ApplyRuntimeRecursive(results),
                RecursiveNode => results,
                UnionNode unionNode => ApplyRuntimeUnion(results, unionNode),
                TextNode textNode => [textNode.Text],
                IntNode intNode => [intNode.Value],
                FloatNode floatNode => [floatNode.Value],
                BoolNode boolNode => [boolNode.Value],
                IdentifierNode identifierNode => ApplyRuntimeIdentifier(identifierNode),
                _ => throw new NotSupportedException($"Node type '{node.Type}' is not supported.")
            };
        }

        return results;
    }

    private static List<object?> ApplyRuntimeField(IEnumerable<object?> values, string fieldName)
    {
        var results = new List<object?>();

        foreach (var value in values)
        {
            if (TryGetRuntimeFieldValue(value, fieldName, out var fieldValue))
            {
                results.Add(fieldValue);
            }
        }

        return results;
    }

    private static bool TryGetRuntimeFieldValue(object? source, string fieldName, out object? value)
    {
        if (source is null)
        {
            value = null;
            return false;
        }

        if (source is JsonDocument document)
        {
            return TryGetRuntimeFieldValue(document.RootElement, fieldName, out value);
        }

        if (source is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty(fieldName, out var elementProperty))
                {
                    value = ConvertJsonElementValue(elementProperty);
                    return true;
                }

                foreach (var candidate in element.EnumerateObject())
                {
                    if (string.Equals(candidate.Name, fieldName, StringComparison.OrdinalIgnoreCase))
                    {
                        value = ConvertJsonElementValue(candidate.Value);
                        return true;
                    }
                }
            }

            value = null;
            return false;
        }

        if (source is JsonNode node)
        {
            if (node is JsonObject jsonObject)
            {
                if (jsonObject.TryGetPropertyValue(fieldName, out var nodeProperty))
                {
                    value = ConvertJsonNodeValue(nodeProperty);
                    return true;
                }

                foreach (var candidate in jsonObject)
                {
                    if (string.Equals(candidate.Key, fieldName, StringComparison.OrdinalIgnoreCase))
                    {
                        value = ConvertJsonNodeValue(candidate.Value);
                        return true;
                    }
                }
            }

            value = null;
            return false;
        }

        if (source is IDictionary dictionary)
        {
            if (dictionary.Contains(fieldName))
            {
                value = dictionary[fieldName];
                return true;
            }

            value = null;
            return false;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;
        var sourceType = source.GetType();

        var property = sourceType.GetProperty(fieldName, flags);
        if (property != null && property.GetIndexParameters().Length == 0)
        {
            value = property.GetValue(source);
            return true;
        }

        var field = sourceType.GetField(fieldName, flags);
        if (field != null)
        {
            value = field.GetValue(source);
            return true;
        }

        foreach (var candidateProperty in sourceType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (candidateProperty.GetIndexParameters().Length != 0)
            {
                continue;
            }

            var jsonName = candidateProperty.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
            if (string.Equals(jsonName, fieldName, StringComparison.OrdinalIgnoreCase))
            {
                value = candidateProperty.GetValue(source);
                return true;
            }
        }

        if (TryGetExtensionDataMember(sourceType, out var extensionDataMember))
        {
            var extensionData = extensionDataMember switch
            {
                PropertyInfo extensionProperty when extensionProperty.GetIndexParameters().Length == 0 => extensionProperty.GetValue(source),
                FieldInfo extensionField => extensionField.GetValue(source),
                _ => null,
            };

            if (TryGetDictionaryValue(extensionData, fieldName, out value))
            {
                return true;
            }
        }

        value = null;
        return false;
    }

    private static List<object?> ApplyRuntimeArray(IEnumerable<object?> values, ArrayNode arrayNode)
    {
        var results = new List<object?>();

        foreach (var value in values)
        {
            if (value is null)
            {
                continue;
            }

            var sequence = EnumerateRuntimeSequence(value);
            results.AddRange(SelectRuntimeArrayItems(sequence, arrayNode));
        }

        return results;
    }

    private static List<object?> EnumerateRuntimeSequence(object value)
    {
        if (value is JsonDocument document)
        {
            return EnumerateRuntimeSequence(document.RootElement);
        }

        if (value is JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Array)
            {
                throw new NotSupportedException($"'{nameof(JsonElement)}' is not enumerable.");
            }

            return [.. element.EnumerateArray().Select(ConvertJsonElementValue)];
        }

        if (value is JsonArray jsonArray)
        {
            return [.. jsonArray.Select(ConvertJsonNodeValue)];
        }

        if (value is JsonNode jsonNode)
        {
            if (jsonNode is not JsonArray array)
            {
                throw new NotSupportedException($"'{jsonNode.GetType()}' is not enumerable.");
            }

            return [.. array.Select(ConvertJsonNodeValue)];
        }

        if (value is string || value is IDictionary || value is not IEnumerable enumerable)
        {
            throw new NotSupportedException($"'{value.GetType()}' is not enumerable.");
        }

        var sequence = new List<object?>();
        foreach (var item in enumerable)
        {
            sequence.Add(item);
        }

        return sequence;
    }

    private static List<object?> SelectRuntimeArrayItems(List<object?> values, ArrayNode arrayNode)
    {
        if (arrayNode.Params.Length != 3)
        {
            throw new NotSupportedException("Array parameters are not supported.");
        }

        var first = arrayNode.Params[0];
        var second = arrayNode.Params[1];
        var third = arrayNode.Params[2];
        var length = values.Count;

        if (IsSupportedSingleIndex(arrayNode))
        {
            var index = ResolveRuntimeIndex(first.Value, length);
            if (index < 0 || index >= length)
            {
                throw new ArgumentOutOfRangeException(nameof(arrayNode));
            }

            return [values[index]];
        }

        var step = third.Known ? third.Value : 1;
        if (step <= 0)
        {
            throw new NotSupportedException("Array step must be greater than zero.");
        }

        var start = first.Known ? ResolveRuntimeIndex(first.Value, length) : 0;
        var end = second.Known ? ResolveRuntimeIndex(second.Value, length) : length;
        start = Math.Clamp(start, 0, length);
        end = Math.Clamp(end, 0, length);

        if (start > end)
        {
            throw new NotSupportedException("Array start index cannot be greater than end index.");
        }

        var items = new List<object?>();
        for (var i = start; i < end; i += step)
        {
            items.Add(values[i]);
        }

        return items;
    }

    private static int ResolveRuntimeIndex(int value, int length) => value < 0 ? length + value : value;

    private static List<object?> ApplyRuntimeFilter(IEnumerable<object?> values, FilterNode filterNode)
    {
        var results = new List<object?>();

        foreach (var value in values)
        {
            if (value is null)
            {
                continue;
            }

            foreach (var candidate in EnumerateRuntimeSequence(value))
            {
                if (MatchesRuntimeFilter(candidate, filterNode))
                {
                    results.Add(candidate);
                    break;
                }
            }
        }

        return results;
    }

    private static bool MatchesRuntimeFilter(object? candidate, FilterNode filterNode)
    {
        var leftValues = EvaluateRuntimeNodes(filterNode.Left.Nodes, [candidate]);
        if (filterNode.Operator == "exists")
        {
            return leftValues.Count > 0;
        }

        var rightValues = EvaluateRuntimeNodes(filterNode.Right.Nodes, [candidate]);
        if (leftValues.Count == 0 || rightValues.Count == 0)
        {
            return false;
        }

        foreach (var leftValue in leftValues)
        {
            foreach (var rightValue in rightValues)
            {
                if (CompareDynamicValues(leftValue, rightValue, filterNode.Operator))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static List<object?> ApplyRuntimeWildcard(IEnumerable<object?> values)
    {
        var results = new List<object?>();

        foreach (var value in values)
        {
            results.AddRange(ExpandWildcardValues(value));
        }

        return results;
    }

    private static List<object?> ApplyRuntimeRecursive(IEnumerable<object?> values)
    {
        var results = new List<object?>();

        foreach (var value in values)
        {
            results.Add(value);
            CollectRuntimeDescendants(value, results);
        }

        return results;
    }

    private static void CollectRuntimeDescendants(object? value, List<object?> values)
    {
        foreach (var child in ExpandWildcardValues(value))
        {
            values.Add(child);
            CollectRuntimeDescendants(child, values);
        }
    }

    private static List<object?> ApplyRuntimeUnion(List<object?> values, UnionNode unionNode)
    {
        var results = new List<object?>();
        foreach (var childPath in unionNode.Nodes)
        {
            results.AddRange(EvaluateRuntimeNodes(childPath.Nodes, [.. values]));
        }

        return results;
    }

    private static List<object?> ApplyRuntimeIdentifier(IdentifierNode node)
    {
        return node.Name switch
        {
            "null" => [null],
            _ => throw new NotSupportedException($"Identifier node '{node.Name}' is not supported.")
        };
    }

    private static IEnumerable<object?> ExpandWildcardValues(object? value)
    {
        if (value is null)
        {
            yield break;
        }

        if (value is JsonDocument document)
        {
            foreach (var item in ExpandWildcardValues(document.RootElement))
            {
                yield return item;
            }

            yield break;
        }

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    yield return ConvertJsonElementValue(property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    yield return ConvertJsonElementValue(item);
                }
            }

            yield break;
        }

        if (value is JsonArray jsonArray)
        {
            foreach (var item in jsonArray)
            {
                yield return ConvertJsonNodeValue(item);
            }

            yield break;
        }

        if (value is JsonObject jsonObject)
        {
            foreach (var property in jsonObject)
            {
                yield return ConvertJsonNodeValue(property.Value);
            }

            yield break;
        }

        if (value is JsonNode)
        {
            yield break;
        }

        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                yield return entry.Value;
            }

            yield break;
        }

        if (value is IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                yield return item;
            }

            yield break;
        }

        var valueType = value.GetType();
        if (IsSimpleRuntimeType(valueType))
        {
            yield break;
        }

        foreach (var property in valueType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                     .Where(x => x.GetIndexParameters().Length == 0)
                     .OrderBy(x => x.MetadataToken))
        {
            yield return property.GetValue(value);
        }

        foreach (var field in valueType.GetFields(BindingFlags.Instance | BindingFlags.Public).OrderBy(x => x.MetadataToken))
        {
            yield return field.GetValue(value);
        }
    }

    private static bool IsSimpleRuntimeType(Type type)
    {
        var coreType = Nullable.GetUnderlyingType(type) ?? type;
        return coreType.IsPrimitive ||
               coreType.IsEnum ||
               coreType == typeof(string) ||
               coreType == typeof(decimal) ||
               coreType == typeof(DateTime) ||
               coreType == typeof(DateTimeOffset) ||
               coreType == typeof(Guid) ||
               coreType == typeof(TimeSpan);
    }

    private static object? CollapseRuntimeResults(List<object?> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        if (values.Count == 1)
        {
            return NormalizeRuntimeResult(values[0]);
        }

        return values.Select(NormalizeRuntimeResult).ToArray();
    }

    private static object? NormalizeRuntimeResult(object? value)
    {
        return value switch
        {
            JsonDocument document => GetJsonDocumentValueOrSelf(document),
            JsonElement element => GetJsonElementValueOrSelf(element),
            JsonNode node => GetJsonNodeValueOrSelf(node),
            _ => value
        };
    }

    internal static object? GetJsonElementValueOrSelf(JsonElement value) => ConvertJsonElementValue(value);

    internal static object? GetJsonNodeValueOrSelf(JsonNode? value) => ConvertJsonNodeValue(value);

    internal static object? GetJsonDocumentValueOrSelf(JsonDocument? value)
    {
        return value is null ? null : ConvertJsonElementValue(value.RootElement);
    }

    internal static object? ConvertJsonElementValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => TryGetJsonNumber(value),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            _ => value.Clone()
        };
    }

    internal static object? ConvertJsonNodeValue(JsonNode? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonObject or JsonArray)
        {
            return value;
        }

        if (value is not JsonValue jsonValue)
        {
            return value;
        }

        if (jsonValue.TryGetValue<string>(out var stringValue))
        {
            return stringValue;
        }

        if (jsonValue.TryGetValue<bool>(out var boolValue))
        {
            return boolValue;
        }

        if (jsonValue.TryGetValue<int>(out var intValue))
        {
            return intValue;
        }

        if (jsonValue.TryGetValue<long>(out var longValue))
        {
            return longValue;
        }

        if (jsonValue.TryGetValue<decimal>(out var decimalValue))
        {
            return decimalValue;
        }

        if (jsonValue.TryGetValue<double>(out var doubleValue))
        {
            return doubleValue;
        }

        return jsonValue.ToJsonString();
    }

    internal static object? NormalizeDynamicValue(object? value)
    {
        if (value is JsonDocument document)
        {
            return NormalizeDynamicValue(document.RootElement);
        }

        if (value is JsonElement element)
        {
            return element.ValueKind is JsonValueKind.Object or JsonValueKind.Array
                ? element.GetRawText()
                : ConvertJsonElementValue(element);
        }

        if (value is JsonNode node)
        {
            return node is JsonObject or JsonArray ? node.ToJsonString() : ConvertJsonNodeValue(node);
        }

        return value;
    }

    internal static object TryGetJsonNumber(JsonElement value)
    {
        if (value.TryGetInt32(out var intValue))
        {
            return intValue;
        }

        if (value.TryGetInt64(out var longValue))
        {
            return longValue;
        }

        if (value.TryGetDecimal(out var decimalValue))
        {
            return decimalValue;
        }

        return value.GetDouble();
    }

    internal static int CompareNormalizedValues(object? left, object? right)
    {
        if (left is null && right is null)
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        if (TryConvertToDecimal(left, out var leftDecimal) && TryConvertToDecimal(right, out var rightDecimal))
        {
            return leftDecimal.CompareTo(rightDecimal);
        }

        if (left is bool leftBool && right is bool rightBool)
        {
            return leftBool.CompareTo(rightBool);
        }

        var leftText = Convert.ToString(left, CultureInfo.InvariantCulture) ?? string.Empty;
        var rightText = Convert.ToString(right, CultureInfo.InvariantCulture) ?? string.Empty;
        return string.Compare(leftText, rightText, StringComparison.Ordinal);
    }

    internal static bool TryConvertToDecimal(object value, out decimal result)
    {
        switch (value)
        {
            case byte byteValue:
                result = byteValue;
                return true;
            case sbyte sbyteValue:
                result = sbyteValue;
                return true;
            case short shortValue:
                result = shortValue;
                return true;
            case ushort ushortValue:
                result = ushortValue;
                return true;
            case int intValue:
                result = intValue;
                return true;
            case uint uintValue:
                result = uintValue;
                return true;
            case long longValue:
                result = longValue;
                return true;
            case ulong ulongValue:
                result = ulongValue;
                return true;
            case float floatValue:
                result = (decimal)floatValue;
                return true;
            case double doubleValue:
                result = (decimal)doubleValue;
                return true;
            case decimal decimalValue:
                result = decimalValue;
                return true;
            case string text when decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed):
                result = parsed;
                return true;
            default:
                result = default;
                return false;
        }
    }
}
