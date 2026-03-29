using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JsonPathLINQ;

namespace JsonPathLINQ.Tests;

internal static class JsonPathTemplateEvaluator
{
    public static string Evaluate(string template, object? input, bool allowMissingKeys, bool sortResults)
    {
        var parser = LegacyJsonPathAdapter.Parse("jsonpath", template);
        return EvaluateNodes(parser.Root.Nodes, 0, parser.Root.Nodes.Count, input, allowMissingKeys, sortResults);
    }

    private static string EvaluateNodes(
        IReadOnlyList<INode> nodes,
        int start,
        int end,
        object? current,
        bool allowMissingKeys,
        bool sortResults)
    {
        var output = new StringBuilder();

        for (var i = start; i < end; i++)
        {
            var node = nodes[i];

            if (node is TextNode textNode)
            {
                output.Append(textNode.Text);
                continue;
            }

            if (node is not ListNode listNode)
            {
                throw new JsonPathTemplateEvaluationException($"unsupported root node type '{node.Type}'");
            }

            if (TryGetIdentifier(listNode, out var identifier))
            {
                if (identifier == "range")
                {
                    var rangeEnd = FindRangeEnd(nodes, i + 1, end);
                    var values = EvaluatePath(listNode.Nodes.Skip(1).ToArray(), [current], allowMissingKeys);

                    foreach (var value in values)
                    {
                        output.Append(EvaluateNodes(nodes, i + 1, rangeEnd, value, allowMissingKeys, sortResults));
                    }

                    i = rangeEnd;
                    continue;
                }

                if (identifier == "end")
                {
                    throw new JsonPathTemplateEvaluationException("not in range");
                }

                throw new JsonPathTemplateEvaluationException($"unrecognized identifier {identifier}");
            }

            var results = EvaluatePath(listNode.Nodes, [current], allowMissingKeys);
            output.Append(FormatResults(results, sortResults));
        }

        return output.ToString();
    }

    private static int FindRangeEnd(IReadOnlyList<INode> nodes, int start, int end)
    {
        var depth = 0;

        for (var i = start; i < end; i++)
        {
            if (nodes[i] is not ListNode listNode || !TryGetIdentifier(listNode, out var identifier))
            {
                continue;
            }

            if (identifier == "range")
            {
                depth++;
                continue;
            }

            if (identifier != "end")
            {
                continue;
            }

            if (depth == 0)
            {
                return i;
            }

            depth--;
        }

        throw new JsonPathTemplateEvaluationException("unterminated range");
    }

    private static bool TryGetIdentifier(ListNode listNode, out string identifier)
    {
        if (listNode.Nodes.Count > 0 && listNode.Nodes[0] is IdentifierNode identifierNode)
        {
            identifier = identifierNode.Name;
            return true;
        }

        identifier = string.Empty;
        return false;
    }

    private static List<object?> EvaluatePath(IReadOnlyList<INode> nodes, List<object?> currentValues, bool allowMissingKeys)
    {
        var results = currentValues;

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            results = node switch
            {
                ListNode listNode => EvaluatePath(listNode.Nodes, results, allowMissingKeys),
                FieldNode fieldNode => ApplyField(results, fieldNode.Value, allowMissingKeys),
                ArrayNode arrayNode => ApplyArray(results, arrayNode, allowMissingKeys),
                FilterNode filterNode => ApplyFilter(results, filterNode, allowMissingKeys),
                WildcardNode => ApplyWildcard(results),
                RecursiveNode when i < nodes.Count - 1 => ApplyRecursive(results),
                RecursiveNode => results,
                UnionNode unionNode => ApplyUnion(results, unionNode, allowMissingKeys),
                TextNode textNode => [textNode.Text],
                IntNode intNode => [intNode.Value],
                FloatNode floatNode => [floatNode.Value],
                BoolNode boolNode => [boolNode.Value],
                IdentifierNode identifierNode => throw new JsonPathTemplateEvaluationException($"unrecognized identifier {identifierNode.Name}"),
                _ => throw new JsonPathTemplateEvaluationException($"unsupported node type '{node.Type}'")
            };
        }

        return results;
    }

    private static List<object?> ApplyField(IEnumerable<object?> values, string fieldName, bool allowMissingKeys)
    {
        var results = new List<object?>();

        foreach (var value in values)
        {
            if (TryGetFieldValue(value, fieldName, out var fieldValue))
            {
                results.Add(fieldValue);
                continue;
            }

            if (!allowMissingKeys)
            {
                throw new JsonPathTemplateEvaluationException($"field '{fieldName}' is not found");
            }
        }

        return results;
    }

    private static bool TryGetFieldValue(object? source, string fieldName, out object? value)
    {
        if (source is null)
        {
            value = null;
            return false;
        }

        if (source is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty(fieldName, out var property))
                {
                    value = property.Clone();
                    return true;
                }

                foreach (var candidate in element.EnumerateObject())
                {
                    if (!string.Equals(candidate.Name, fieldName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    value = candidate.Value.Clone();
                    return true;
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

        var propertyInfo = sourceType.GetProperty(fieldName, flags);
        if (propertyInfo != null && propertyInfo.GetIndexParameters().Length == 0)
        {
            value = propertyInfo.GetValue(source);
            return true;
        }

        var fieldInfo = sourceType.GetField(fieldName, flags);
        if (fieldInfo != null)
        {
            value = fieldInfo.GetValue(source);
            return true;
        }

        foreach (var candidateProperty in sourceType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (candidateProperty.GetIndexParameters().Length != 0)
            {
                continue;
            }

            var jsonName = candidateProperty.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
            if (!string.Equals(jsonName, fieldName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value = candidateProperty.GetValue(source);
            return true;
        }

        value = null;
        return false;
    }

    private static List<object?> ApplyArray(IEnumerable<object?> values, ArrayNode arrayNode, bool allowMissingKeys)
    {
        var results = new List<object?>();

        foreach (var value in values)
        {
            if (value is null)
            {
                if (allowMissingKeys)
                {
                    continue;
                }

                throw new JsonPathTemplateEvaluationException("null is not array or slice");
            }

            if (!TryEnumerateSequence(value, out var sequence))
            {
                throw new JsonPathTemplateEvaluationException($"{value.GetType().Name} is not array or slice");
            }

            results.AddRange(SelectArrayItems(sequence, arrayNode));
        }

        return results;
    }

    private static bool TryEnumerateSequence(object value, out List<object?> sequence)
    {
        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                sequence = [];
                foreach (var arrayItem in element.EnumerateArray())
                {
                    sequence.Add(arrayItem.Clone());
                }

                return true;
            }

            sequence = [];
            return false;
        }

        if (value is string || value is IDictionary || value is not IEnumerable enumerable)
        {
            sequence = [];
            return false;
        }

        sequence = [];
        foreach (var item in enumerable)
        {
            sequence.Add(item);
        }

        return true;
    }

    private static List<object?> SelectArrayItems(List<object?> values, ArrayNode arrayNode)
    {
        if (arrayNode.Params.Length != 3)
        {
            throw new JsonPathTemplateEvaluationException("invalid array expression");
        }

        var first = arrayNode.Params[0];
        var second = arrayNode.Params[1];
        var third = arrayNode.Params[2];
        var length = values.Count;

        var singleIndex = first.Known && second.Known && second.Derived && !third.Known;
        if (singleIndex)
        {
            var index = ResolveIndex(first.Value, length);
            if (index < 0 || index >= length)
            {
                throw new JsonPathTemplateEvaluationException("array index is out of bounds");
            }

            return [values[index]];
        }

        var step = third.Known ? third.Value : 1;
        if (step <= 0)
        {
            throw new JsonPathTemplateEvaluationException("step must be greater than zero");
        }

        var start = first.Known ? ResolveIndex(first.Value, length) : 0;
        var end = second.Known ? ResolveIndex(second.Value, length) : length;
        start = Math.Clamp(start, 0, length);
        end = Math.Clamp(end, 0, length);

        if (start > end)
        {
            throw new JsonPathTemplateEvaluationException("start index cannot be greater than end index");
        }

        var items = new List<object?>();
        for (var i = start; i < end; i += step)
        {
            items.Add(values[i]);
        }

        return items;
    }

    private static int ResolveIndex(int value, int length) => value < 0 ? length + value : value;

    private static List<object?> ApplyFilter(IEnumerable<object?> values, FilterNode filterNode, bool allowMissingKeys)
    {
        var results = new List<object?>();

        foreach (var value in values)
        {
            if (value is null)
            {
                if (allowMissingKeys)
                {
                    continue;
                }

                throw new JsonPathTemplateEvaluationException("null is not array or slice");
            }

            if (!TryEnumerateSequence(value, out var candidates))
            {
                throw new JsonPathTemplateEvaluationException($"{value.GetType().Name} is not array or slice");
            }

            foreach (var candidate in candidates)
            {
                if (MatchesFilter(candidate, filterNode, allowMissingKeys))
                {
                    results.Add(candidate);
                }
            }
        }

        return results;
    }

    private static bool MatchesFilter(object? candidate, FilterNode filterNode, bool allowMissingKeys)
    {
        if (filterNode.Operator == "exists")
        {
            return EvaluatePath(filterNode.Left.Nodes, [candidate], true).Count > 0;
        }

        if (filterNode.Operator is not ("==" or "!=" or "<" or ">" or "<=" or ">="))
        {
            throw new JsonPathTemplateEvaluationException($"unrecognized filter operator {filterNode.Operator}");
        }

        var leftValues = EvaluatePath(filterNode.Left.Nodes, [candidate], allowMissingKeys);
        var rightValues = EvaluatePath(filterNode.Right.Nodes, [candidate], allowMissingKeys);

        if (leftValues.Count == 0 || rightValues.Count == 0)
        {
            if (allowMissingKeys)
            {
                return false;
            }

            throw new JsonPathTemplateEvaluationException("field is not found");
        }

        foreach (var leftValue in leftValues)
        {
            foreach (var rightValue in rightValues)
            {
                if (CompareFilterValues(leftValue, rightValue, filterNode.Operator))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool CompareFilterValues(object? left, object? right, string @operator)
    {
        left = NormalizeValue(left);
        right = NormalizeValue(right);

        var comparison = JsonPath.CompareNormalizedValues(left, right);
        return @operator switch
        {
            "==" => comparison == 0,
            "!=" => comparison != 0,
            "<" => comparison < 0,
            ">" => comparison > 0,
            "<=" => comparison <= 0,
            ">=" => comparison >= 0,
            _ => throw new JsonPathTemplateEvaluationException($"unrecognized filter operator {@operator}")
        };
    }

    private static object? NormalizeValue(object? value)
    {
        if (value is not JsonElement element)
        {
            return value;
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => JsonPath.TryGetJsonNumber(element),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            _ => element.GetRawText()
        };
    }

    private static List<object?> ApplyWildcard(IEnumerable<object?> values)
    {
        var results = new List<object?>();

        foreach (var value in values)
        {
            results.AddRange(ExpandWildcardValues(value));
        }

        return results;
    }

    private static List<object?> ApplyRecursive(IEnumerable<object?> values)
    {
        var results = new List<object?>();

        foreach (var value in values)
        {
            results.Add(value);
            CollectDescendants(value, results);
        }

        return results;
    }

    private static void CollectDescendants(object? value, List<object?> values)
    {
        foreach (var child in ExpandWildcardValues(value))
        {
            values.Add(child);
            CollectDescendants(child, values);
        }
    }

    private static List<object?> ApplyUnion(List<object?> values, UnionNode unionNode, bool allowMissingKeys)
    {
        var results = new List<object?>();
        foreach (var childPath in unionNode.Nodes)
        {
            results.AddRange(EvaluatePath(childPath.Nodes, [.. values], allowMissingKeys));
        }

        return results;
    }

    private static IEnumerable<object?> ExpandWildcardValues(object? value)
    {
        if (value is null)
        {
            yield break;
        }

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    yield return property.Value.Clone();
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    yield return item.Clone();
                }
            }

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
        if (IsSimpleType(valueType))
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

    private static bool IsSimpleType(Type type)
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

    private static string FormatResults(List<object?> values, bool sortResults)
    {
        if (values.Count == 0)
        {
            return string.Empty;
        }

        var formatted = values.Select(FormatValue).ToList();
        if (sortResults)
        {
            formatted.Sort(StringComparer.Ordinal);
        }

        return string.Join(" ", formatted);
    }

    private static string FormatValue(object? value)
    {
        if (value is null)
        {
            return "null";
        }

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                JsonValueKind.Undefined => "null",
                _ => JsonSerializer.Serialize(element)
            };
        }

        if (value is string stringValue)
        {
            return stringValue;
        }

        if (value is bool boolValue)
        {
            return boolValue ? "true" : "false";
        }

        if (IsNumericType(value.GetType()))
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        return JsonSerializer.Serialize(value);
    }

    private static bool IsNumericType(Type type)
    {
        var coreType = Nullable.GetUnderlyingType(type) ?? type;
        return coreType == typeof(byte) ||
               coreType == typeof(sbyte) ||
               coreType == typeof(short) ||
               coreType == typeof(ushort) ||
               coreType == typeof(int) ||
               coreType == typeof(uint) ||
               coreType == typeof(long) ||
               coreType == typeof(ulong) ||
               coreType == typeof(float) ||
               coreType == typeof(double) ||
               coreType == typeof(decimal);
    }
}

internal sealed class JsonPathTemplateEvaluationException : Exception
{
    public JsonPathTemplateEvaluationException(string message)
        : base(message)
    {
    }
}
