using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using ClientGo.JsonPath;
using Xunit;

namespace ClientGo.JsonPath.Tests;

public sealed class JsonPathPrinterTests
{
    private const string Template = "{.metadata.name}";

    public static IEnumerable<object[]> PrintTestCases()
    {
        yield return new object[]
        {
            "pod",
            (Func<object>)(() => new Pod
            {
                Metadata = new ObjectMeta { Name = "pod" }
            }),
            false,
        };

        yield return new object[] { "emptyPodList", (Func<object>)(() => new PodList()), true };

        yield return new object[]
        {
            "nonEmptyPodList",
            (Func<object>)(() =>
            {
                var list = new PodList();
                list.Items.Add(new Pod());
                return list;
            }),
            true,
        };

        yield return new object[]
        {
            "endpoints",
            (Func<object>)(() => new Endpoints
            {
                Subsets =
                {
                    new EndpointSubset
                    {
                        Addresses =
                        {
                            new EndpointAddress { Ip = "127.0.0.1" },
                            new EndpointAddress { Ip = "localhost" },
                        },
                        Ports = { new EndpointPort { Port = 8080 } },
                    }
                }
            }),
            true,
        };
    }

    [Theory]
    [MemberData(nameof(PrintTestCases))]
    public void PrintObjMatchesGoBehavior(string name, Func<object> objectFactory, bool expectError)
    {
        var printer = JsonPathPrinter.Create(Template);
        var writer = new StringWriter();
        var exception = Record.Exception(() => printer.PrintObj(objectFactory(), writer));
        if (expectError)
        {
            Assert.True(exception is not null, $"Expected an exception for {name}.");
        }
        else
        {
            Assert.True(exception is null, $"Unexpected exception for {name}: {exception}");
            Assert.Equal("pod", writer.ToString());
        }
    }

    [Theory]
    [MemberData(nameof(PrintTestCases))]
    public void PrintObjMatchesGoBehaviorWithJsonOutput(string name, Func<object> objectFactory, bool expectError)
    {
        var printer = JsonPathPrinter.Create(Template);
        printer.EnableJsonOutput(true);
        var writer = new StringWriter();
        var exception = Record.Exception(() => printer.PrintObj(objectFactory(), writer));
        if (expectError)
        {
            Assert.True(exception is not null, $"Expected an exception for {name}.");
        }
        else
        {
            Assert.True(exception is null, $"Unexpected exception for {name}: {exception}");
            Assert.Equal("[\n  \"pod\"\n]\n", writer.ToString());
        }
    }

    private sealed class ObjectMeta
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private sealed class Pod
    {
        [JsonPropertyName("metadata")]
        public ObjectMeta? Metadata { get; set; }
    }

    private sealed class PodList
    {
        [JsonPropertyName("items")]
        public List<Pod> Items { get; } = new();
    }

    private sealed class EndpointAddress
    {
        [JsonPropertyName("ip")]
        public string? Ip { get; set; }
    }

    private sealed class EndpointPort
    {
        [JsonPropertyName("port")]
        public int Port { get; set; }
    }

    private sealed class EndpointSubset
    {
        [JsonPropertyName("addresses")]
        public List<EndpointAddress> Addresses { get; init; } = new();

        [JsonPropertyName("ports")]
        public List<EndpointPort> Ports { get; init; } = new();
    }

    private sealed class Endpoints
    {
        [JsonPropertyName("subsets")]
        public List<EndpointSubset> Subsets { get; init; } = new();
    }
}
