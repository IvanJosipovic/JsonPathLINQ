using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClientGo.JsonPath;

public interface IUnstructured
{
    IDictionary<string, object?> UnstructuredContent();
}

public sealed class JsonPathPrinter
{
    private readonly string _rawTemplate;
    private readonly JsonPath _jsonPath;
    private static readonly JsonSerializerOptions DebugSerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private JsonPathPrinter(string template, JsonPath jsonPath)
    {
        _rawTemplate = template;
        _jsonPath = jsonPath;
    }

    public static JsonPathPrinter Create(string template)
    {
        if (template is null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        var jsonPath = new JsonPath("out");
        jsonPath.Parse(template);
        return new JsonPathPrinter(template, jsonPath);
    }

    public JsonPathPrinter AllowMissingKeys(bool allow)
    {
        _jsonPath.AllowMissingKeys(allow);
        return this;
    }

    public void EnableJsonOutput(bool enable) => _jsonPath.EnableJsonOutput(enable);

    public void PrintObj(object? obj, TextWriter writer)
    {
        if (writer is null)
        {
            throw new ArgumentNullException(nameof(writer));
        }

        if (obj is null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        var type = obj.GetType();
        var packagePath = type.Namespace ?? string.Empty;
        if (ObjectSourceChecker.InternalObjectPreventer.IsForbidden(packagePath))
        {
            throw new InvalidOperationException(ObjectSourceChecker.InternalObjectPrinterErr);
        }

        object? queryObj = obj;
        if (obj is IUnstructured unstructured)
        {
            queryObj = unstructured.UnstructuredContent();
        }
        else
        {
            var json = JsonSerializer.Serialize(obj);
            queryObj = JsonSerializer.Deserialize<JsonElement>(json);
        }

        try
        {
            _jsonPath.Execute(writer, queryObj);
        }
        catch (Exception ex)
        {
            var message = BuildDebugMessage(ex, queryObj);
            throw new InvalidOperationException($"error executing jsonpath \"{_rawTemplate}\": {message}\n", ex);
        }
    }

    private string BuildDebugMessage(Exception exception, object? queryObj)
    {
        var builder = new StringBuilder();
        builder.AppendFormat("Error executing template: {0}. Printing more information for debugging the template:\n", exception.Message);
        builder.AppendLine("\ttemplate was:");
        builder.Append('\t').Append('\t').AppendLine(_rawTemplate);
        builder.AppendLine("\tobject given to jsonpath engine was:");
        builder.Append('\t').Append('\t').AppendLine(SerializeForDebug(queryObj));
        builder.AppendLine();
        return builder.ToString();
    }

    private static string SerializeForDebug(object? value)
    {
        try
        {
            if (value is JsonElement element)
            {
                return JsonSerializer.Serialize(element, DebugSerializerOptions);
            }

            return JsonSerializer.Serialize(value, DebugSerializerOptions);
        }
        catch
        {
            return value?.ToString() ?? "null";
        }
    }
}

public static class ObjectSourceChecker
{
    public const string InternalObjectPrinterErr = "a versioned object must be passed to a printer";

    public static IllegalPackageSourceChecker InternalObjectPreventer { get; } = new(new[]
    {
        "k8s.io/kubernetes/pkg/apis/",
    });

    public static bool IsInternalObjectError(Exception? error) =>
        error is not null && error.Message == InternalObjectPrinterErr;
}

public sealed class IllegalPackageSourceChecker
{
    private readonly string[] _disallowedPrefixes;

    public IllegalPackageSourceChecker(IEnumerable<string> disallowedPrefixes)
    {
        if (disallowedPrefixes is null)
        {
            throw new ArgumentNullException(nameof(disallowedPrefixes));
        }

        _disallowedPrefixes = disallowedPrefixes.ToArray();
    }

    public bool IsForbidden(string? packagePath)
    {
        if (string.IsNullOrEmpty(packagePath))
        {
            return false;
        }

        foreach (var prefix in _disallowedPrefixes)
        {
            if (packagePath.StartsWith(prefix, StringComparison.Ordinal) ||
                packagePath.Contains($"/vendor/{prefix}", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
