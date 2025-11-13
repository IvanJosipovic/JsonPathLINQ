using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace ClientGo.JsonPath;

public sealed class JsonPath
{
    private readonly string _name;
    private Parser? _parser;
    private int _beginRange;
    private int _inRange;
    private int _endRange;
    private INode? _lastEndNode;
    private bool _allowMissingKeys;
    private bool _outputJson;

    public JsonPath(string name)
    {
        _name = name;
    }

    public static JsonPath Create(string name) => new(name);

    public JsonPath AllowMissingKeys(bool allow)
    {
        _allowMissingKeys = allow;
        return this;
    }

    public void EnableJsonOutput(bool enable) => _outputJson = enable;

    public void Parse(string template)
    {
        _parser = Parser.Parse(_name, template);
    }

    public void Execute(TextWriter writer, object? data)
    {
        if (writer is null)
        {
            throw new ArgumentNullException(nameof(writer));
        }

        var results = FindResults(data);
        foreach (var result in results)
        {
            PrintResults(writer, result);
        }
    }

    public List<List<object?>> FindResults(object? data)
    {
        if (_parser is null)
        {
            throw new InvalidOperationException($"{_name} is an incomplete jsonpath template");
        }

        var normalized = Normalize(data);
        var current = new List<object?> { normalized };
        var nodes = _parser.Root.Nodes.ToList();
        var fullResult = new List<List<object?>>();

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var results = Walk(current, node);

            if (_endRange > 0 && _endRange <= _inRange)
            {
                _endRange--;
                _lastEndNode = node;
                break;
            }

            if (_beginRange > 0)
            {
                _beginRange--;
                _inRange++;

                if (results.Count > 0)
                {
                    foreach (var value in results)
                    {
                        var originalNodes = _parser.Root.Nodes.ToList();
                        try
                        {
                            _parser.Root.ReplaceNodes(nodes.Skip(i + 1));
                            var nested = FindResults(value);
                            fullResult.AddRange(nested);
                        }
                        finally
                        {
                            _parser.Root.ReplaceNodes(originalNodes);
                        }
                    }
                }
                else
                {
                    var originalNodes = _parser.Root.Nodes.ToList();
                    try
                    {
                        _parser.Root.ReplaceNodes(nodes.Skip(i + 1));
                        _ = FindResults(null);
                    }
                    finally
                    {
                        _parser.Root.ReplaceNodes(originalNodes);
                    }
                }

                _inRange--;

                for (var k = i + 1; k < nodes.Count; k++)
                {
                    if (ReferenceEquals(nodes[k], _lastEndNode))
                    {
                        i = k;
                        break;
                    }
                }

                continue;
            }

            fullResult.Add(results);
        }

        return fullResult;
    }

    public void PrintResults(TextWriter writer, IReadOnlyList<object?> results)
    {
        if (_outputJson)
        {
            var array = results.Select(Normalize).ToList();
            var json = JsonSerializer.Serialize(array, SerializerOptionsIndented);
            writer.Write(json);
            if (!json.EndsWith("\n", StringComparison.Ordinal))
            {
                writer.WriteLine();
            }

            return;
        }

        for (var i = 0; i < results.Count; i++)
        {
            var value = results[i];
            var text = EvaluateOutput(value, out _);
            writer.Write(text);

            if (i != results.Count - 1)
            {
                writer.Write(' ');
            }
        }
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly JsonSerializerOptions SerializerOptionsIndented = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static string EvaluateOutput(object? value, out bool outputAsJson)
    {
        var normalized = Normalize(value);
        if (normalized is null)
        {
            outputAsJson = false;
            return "null";
        }

        if (normalized is string s)
        {
            outputAsJson = false;
            return s;
        }

        if (normalized is bool b)
        {
            outputAsJson = false;
            return b ? "true" : "false";
        }

        if (normalized is IFormattable formattable && IsNumber(normalized))
        {
            outputAsJson = false;
            return formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        if (normalized is IEnumerable && normalized is not string)
        {
            var json = JsonSerializer.Serialize(normalized, SerializerOptions);
            outputAsJson = true;
            return json;
        }

        var type = normalized.GetType();
        if (type.IsClass || (type.IsValueType && !IsPrimitive(type)))
        {
            var json = JsonSerializer.Serialize(normalized, SerializerOptions);
            outputAsJson = true;
            return json;
        }

        outputAsJson = false;
        return Convert.ToString(normalized, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private List<object?> Walk(List<object?> values, INode node)
    {
        return node switch
        {
            ListNode listNode => EvalList(values, listNode),
            TextNode textNode => values.Select(_ => (object?)textNode.Text).ToList(),
            FieldNode fieldNode => EvalField(values, fieldNode),
            ArrayNode arrayNode => EvalArray(values, arrayNode),
            FilterNode filterNode => EvalFilter(values, filterNode),
            IntNode intNode => values.Select(_ => (object?)intNode.Value).ToList(),
            FloatNode floatNode => values.Select(_ => (object?)floatNode.Value).ToList(),
            BoolNode boolNode => values.Select(_ => (object?)boolNode.Value).ToList(),
            WildcardNode wildcardNode => EvalWildcard(values),
            RecursiveNode recursiveNode => EvalRecursive(values),
            UnionNode unionNode => EvalUnion(values, unionNode),
            IdentifierNode identifierNode => EvalIdentifier(values, identifierNode),
            _ => throw new InvalidOperationException($"unexpected node {node}")
        };
    }

    private List<object?> EvalList(List<object?> values, ListNode node)
    {
        var current = values;
        foreach (var child in node.Nodes)
        {
            current = Walk(current, child);
        }

        return current;
    }

    private List<object?> EvalIdentifier(List<object?> values, IdentifierNode node)
    {
        switch (node.Name)
        {
            case "range":
                _beginRange++;
                return values;
            case "end":
                if (_inRange > 0)
                {
                    _endRange++;
                    return new List<object?>();
                }

                throw new InvalidOperationException("not in range, nothing to end");
            default:
                throw new InvalidOperationException($"unrecognized identifier {node.Name}");
        }
    }

    private List<object?> EvalArray(List<object?> input, ArrayNode node)
    {
        var result = new List<object?>();
        foreach (var value in input)
        {
            var indirect = Indirect(Normalize(value), out var isNil);
            if (isNil)
            {
                continue;
            }

            if (!TryGetList(indirect, out var list))
            {
                throw new InvalidOperationException($"{GetTypeName(indirect)} is not array or slice");
            }

            var parameters = node.Params.Select(p => p).ToArray();
            if (!parameters[0].Known)
            {
                parameters[0].Value = 0;
            }

            if (parameters[0].Value < 0)
            {
                parameters[0].Value += list.Count;
            }

            if (!parameters[1].Known)
            {
                parameters[1].Value = list.Count;
            }

            if (parameters[1].Value < 0 || (parameters[1].Value == 0 && parameters[1].Derived))
            {
                parameters[1].Value += list.Count;
            }

            var sliceLength = list.Count;
            if (parameters[1].Value != parameters[0].Value)
            {
                if (parameters[0].Value >= sliceLength || parameters[0].Value < 0)
                {
                    throw new InvalidOperationException($"array index out of bounds: index {parameters[0].Value}, length {sliceLength}");
                }

                if (parameters[1].Value > sliceLength || parameters[1].Value < 0)
                {
                    throw new InvalidOperationException($"array index out of bounds: index {parameters[1].Value - 1}, length {sliceLength}");
                }

                if (parameters[0].Value > parameters[1].Value)
                {
                    throw new InvalidOperationException($"starting index {parameters[0].Value} is greater than ending index {parameters[1].Value}");
                }
            }
            else
            {
                continue;
            }

            var step = 1;
            if (parameters[2].Known)
            {
                if (parameters[2].Value <= 0)
                {
                    throw new InvalidOperationException("step must be > 0");
                }

                step = parameters[2].Value;
            }

            for (var i = parameters[0].Value; i < parameters[1].Value && i < list.Count; i += step)
            {
                result.Add(list[i]);
            }
        }

        return result;
    }

    private List<object?> EvalUnion(List<object?> input, UnionNode node)
    {
        var result = new List<object?>();
        foreach (var listNode in node.Nodes)
        {
            var values = EvalList(input, listNode);
            result.AddRange(values);
        }

        return result;
    }

    private List<object?> EvalField(List<object?> input, FieldNode node)
    {
        var results = new List<object?>();
        var hadValues = false;
        if (input.Count == 0)
        {
            return results;
        }

        foreach (var value in input)
        {
            var normalized = Normalize(value);
            var indirect = Indirect(normalized, out var isNil);
            if (isNil)
            {
                continue;
            }

            hadValues = true;

            if (TryGetDictionaryValue(indirect, node.Value, out var dictValue))
            {
                results.Add(dictValue);
                continue;
            }

            if (TryGetMemberValue(indirect, node.Value, out var memberValue))
            {
                results.Add(memberValue);
            }
        }

        if (results.Count == 0)
        {
            if (!hadValues)
            {
                return results;
            }

            if (!_allowMissingKeys)
            {
                throw new InvalidOperationException($"{node.Value} is not found");
            }
        }

        return results;
    }

    private List<object?> EvalWildcard(List<object?> input)
    {
        var results = new List<object?>();
        foreach (var value in input)
        {
            var normalized = Normalize(value);
            var indirect = Indirect(normalized, out var isNil);
            if (isNil)
            {
                continue;
            }

            if (indirect is null)
            {
                continue;
            }

            switch (indirect)
            {
                case IDictionary dictionary:
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        results.Add(entry.Value);
                    }

                    break;
                case IEnumerable enumerable when indirect is not string:
                    foreach (var item in enumerable)
                    {
                        results.Add(item);
                    }

                    break;
                default:
                    foreach (var member in GetMemberValues(indirect))
                    {
                        results.Add(member);
                    }

                    break;
            }
        }

        return results;
    }

    private List<object?> EvalRecursive(List<object?> input)
    {
        var results = new List<object?>();
        foreach (var value in input)
        {
            var normalized = Normalize(value);
            var indirect = Indirect(normalized, out var isNil);
            if (isNil)
            {
                continue;
            }

            if (indirect is null)
            {
                continue;
            }

            var children = new List<object?>();
            switch (indirect)
            {
                case IDictionary dictionary:
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        children.Add(entry.Value);
                    }

                    break;
                case IEnumerable enumerable when indirect is not string:
                    foreach (var item in enumerable)
                    {
                        children.Add(item);
                    }

                    break;
                default:
                    children.AddRange(GetMemberValues(indirect));
                    break;
            }

            if (children.Count > 0)
            {
                results.Add(indirect);
                var deeper = EvalRecursive(children);
                results.AddRange(deeper);
            }
        }

        return results;
    }

    private List<object?> EvalFilter(List<object?> input, FilterNode node)
    {
        var results = new List<object?>();
        foreach (var value in input)
        {
            var indirect = Indirect(Normalize(value), out _);
            if (!TryGetList(indirect, out var list))
            {
                throw new InvalidOperationException($"{GetTypeName(indirect)} is not array or slice and cannot be filtered");
            }

            for (var i = 0; i < list.Count; i++)
            {
                var item = list[i];
                var temp = new List<object?> { item };
                List<object?> lefts;
                try
                {
                    lefts = EvalList(temp, node.Left);
                }
                catch (InvalidOperationException ex) when (IsMissingKeyException(ex))
                {
                    continue;
                }

                if (node.Operator == "exists")
                {
                    if (lefts.Count > 0)
                    {
                        results.Add(item);
                    }

                    continue;
                }

                if (lefts.Count == 0)
                {
                    continue;
                }

                if (lefts.Count > 1)
                {
                    throw new InvalidOperationException("can only compare one element at a time");
                }

                var left = Normalize(lefts[0]);
                List<object?> rights;
                try
                {
                    rights = EvalList(temp, node.Right);
                }
                catch (InvalidOperationException ex) when (IsMissingKeyException(ex))
                {
                    continue;
                }
                if (rights.Count == 0)
                {
                    continue;
                }

                if (rights.Count > 1)
                {
                    throw new InvalidOperationException("can only compare one element at a time");
                }

                var right = Normalize(rights[0]);
                var pass = CompareFilter(left, right, node.Operator);
                if (pass)
                {
                    results.Add(item);
                }
            }
        }

        return results;
    }

    private static bool CompareFilter(object? left, object? right, string op)
    {
        return op switch
        {
            "<" => Compare(left, right) < 0,
            ">" => Compare(left, right) > 0,
            "==" => EqualsValue(left, right),
            "!=" => !EqualsValue(left, right),
            "<=" => Compare(left, right) <= 0,
            ">=" => Compare(left, right) >= 0,
            _ => throw new InvalidOperationException($"unrecognized filter operator {op}")
        };
    }

    private static bool EqualsValue(object? left, object? right)
    {
        left = Normalize(left);
        right = Normalize(right);

        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (IsNumber(left) && IsNumber(right))
        {
            return Convert.ToDouble(left, CultureInfo.InvariantCulture).Equals(
                Convert.ToDouble(right, CultureInfo.InvariantCulture));
        }

        return left.Equals(right);
    }

    private static int Compare(object? left, object? right)
    {
        left = Normalize(left);
        right = Normalize(right);

        if (left is null || right is null)
        {
            throw new InvalidOperationException("cannot compare nil values");
        }

        if (IsNumber(left) && IsNumber(right))
        {
            var l = Convert.ToDouble(left, CultureInfo.InvariantCulture);
            var r = Convert.ToDouble(right, CultureInfo.InvariantCulture);
            return l.CompareTo(r);
        }

        if (left is string ls && right is string rs)
        {
            return string.Compare(ls, rs, StringComparison.Ordinal);
        }

        if (left is bool lb && right is bool rb)
        {
            return lb.CompareTo(rb);
        }

        if (left is IComparable comparable && right is not null && right.GetType().IsAssignableFrom(left.GetType()))
        {
            return comparable.CompareTo(right);
        }

        throw new InvalidOperationException("values are not comparable");
    }

    private static bool IsNumber(object? value)
    {
        if (value is null)
        {
            return false;
        }

        var type = value.GetType();
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) ||
               type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong) ||
               type == typeof(float) || type == typeof(double) || type == typeof(decimal);
    }

    private static bool IsPrimitive(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive || type.IsEnum || type == typeof(decimal) || type == typeof(string);
    }

    private static bool IsMissingKeyException(Exception ex) =>
        ex is InvalidOperationException invalid &&
        invalid.Message.EndsWith("is not found", StringComparison.Ordinal);

    private static object? Normalize(object? value)
    {
        switch (value)
        {
            case JsonElement element:
                return NormalizeJsonElement(element);
            case JsonNode node:
                return Normalize(JsonSerializer.Deserialize<object?>(node.ToJsonString()));
            default:
                return value;
        }
    }

    private static object? NormalizeJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Undefined:
            case JsonValueKind.Null:
                return null;
            case JsonValueKind.True:
            case JsonValueKind.False:
                return element.GetBoolean();
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var l))
                {
                    return l;
                }

                return element.GetDouble();
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object?>();
                foreach (var property in element.EnumerateObject())
                {
                    dict[property.Name] = NormalizeJsonElement(property.Value);
                }

                return dict;
            case JsonValueKind.Array:
                var list = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(NormalizeJsonElement(item));
                }

                return list;
            default:
                return null;
        }
    }

    private static object? Indirect(object? value, out bool isNil)
    {
        if (value is null)
        {
            isNil = true;
            return null;
        }

        var type = value.GetType();
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
        {
            var hasValue = (bool)type.GetProperty("HasValue")!.GetValue(value)!;
            if (!hasValue)
            {
                isNil = true;
                return null;
            }

            value = type.GetProperty("Value")!.GetValue(value);
        }

        isNil = value is null;
        return value;
    }

    private static bool TryGetList(object? value, out IList<object?> list)
    {
        switch (value)
        {
            case null:
                list = Array.Empty<object?>();
                return false;
            case IList<object?> typedList:
                list = typedList;
                return true;
            case IList nonGeneric:
                var temp = new List<object?>();
                foreach (var item in nonGeneric)
                {
                    temp.Add(item);
                }

                list = temp;
                return true;
            case IEnumerable enumerable when value is not string && value is not IDictionary:
                var results = new List<object?>();
                foreach (var item in enumerable)
                {
                    results.Add(item);
                }

                list = results;
                return true;
            default:
                list = Array.Empty<object?>();
                return false;
        }
    }

    private static bool TryGetDictionaryValue(object? value, string key, out object? result)
    {
        switch (value)
        {
            case null:
                result = null;
                return false;
            case IDictionary<string, object?> stringDict:
                if (stringDict.TryGetValue(key, out var val))
                {
                    result = val;
                    return true;
                }

                break;
            case IDictionary dictionary:
                foreach (DictionaryEntry entry in dictionary)
                {
                    var convertedKey = ConvertKey(entry.Key, key);
                    if (convertedKey)
                    {
                        result = entry.Value;
                        return true;
                    }
                }

                break;
        }

        result = null;
        return false;
    }

    private static bool ConvertKey(object? candidate, string key)
    {
        if (candidate is null)
        {
            return false;
        }

        if (candidate is string s)
        {
            return string.Equals(s, key, StringComparison.Ordinal);
        }

        try
        {
            var converted = Convert.ChangeType(key, candidate.GetType(), CultureInfo.InvariantCulture);
            return candidate.Equals(converted);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetMemberValue(object? value, string name, out object? result)
    {
        if (value is null)
        {
            result = null;
            return false;
        }

        var type = value.GetType();
        var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;
        var property = type.GetProperty(name, bindingFlags);
        if (property != null)
        {
            result = property.GetValue(value);
            return true;
        }

        var field = type.GetField(name, bindingFlags);
        if (field != null)
        {
            result = field.GetValue(value);
            return true;
        }

        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var jsonName = prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? GetDataMemberName(prop);
            if (!string.IsNullOrEmpty(jsonName) && string.Equals(jsonName, name, StringComparison.Ordinal))
            {
                result = prop.GetValue(value);
                return true;
            }
        }

        foreach (var fld in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            var jsonName = fld.GetCustomAttributes(typeof(JsonPropertyNameAttribute), inherit: true)
                .Cast<JsonPropertyNameAttribute>()
                .FirstOrDefault()?.Name;
            jsonName ??= GetDataMemberName(fld);
            if (!string.IsNullOrEmpty(jsonName) && string.Equals(jsonName, name, StringComparison.Ordinal))
            {
                result = fld.GetValue(value);
                return true;
            }
        }

        result = null;
        return false;
    }

    private static string? GetDataMemberName(MemberInfo member)
    {
        var dataMemberType = Type.GetType("System.Runtime.Serialization.DataMemberAttribute");
        if (dataMemberType is null)
        {
            return null;
        }

        var attribute = member.GetCustomAttributes(dataMemberType, inherit: true).FirstOrDefault();
        if (attribute is null)
        {
            return null;
        }

        var nameProperty = dataMemberType.GetProperty("Name");
        return nameProperty?.GetValue(attribute) as string;
    }

    private static IEnumerable<object?> GetMemberValues(object value)
    {
        var bindingFlags = BindingFlags.Instance | BindingFlags.Public;
        foreach (var property in value.GetType().GetProperties(bindingFlags))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            yield return property.GetValue(value);
        }

        foreach (var field in value.GetType().GetFields(bindingFlags))
        {
            yield return field.GetValue(value);
        }
    }

    private static string GetTypeName(object? value) => value?.GetType().FullName ?? "null";
}
