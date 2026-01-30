using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aspire.Hosting.AWS.Integ.Tests.Deployment;

internal static class CloudFormationJsonUtilities
{
    /// <summary>
    /// Gets a JsonElement at the specified path. Path segments are separated by '/'.
    /// Supports array indexing with numeric segments (e.g., "Resources/MyResource/Properties/ContainerDefinitions/0/Environment").
    /// </summary>
    /// <param name="cfTemplate">The CloudFormation template document.</param>
    /// <param name="path">The path to the element (e.g., "Resources/ProjectWebApp2/Properties/PrimaryContainer/Environment").</param>
    /// <returns>The JsonElement at the path, or null if not found.</returns>
    internal static JsonElement? GetElementAtPath(JsonDocument cfTemplate, string path)
    {
        return GetElementAtPath(cfTemplate.RootElement, path);
    }

    /// <summary>
    /// Gets a JsonElement at the specified path starting from a given element.
    /// Path segments are separated by '/'.
    /// Supports:
    /// - Array indexing with numeric segments (e.g., "ContainerDefinitions/0/Environment")
    /// - Array element matching with property filter (e.g., "Environment/{Name = services__WebApp1__https__0}/Value")
    /// </summary>
    /// <param name="root">The starting JsonElement.</param>
    /// <param name="path">The path to the element.</param>
    /// <returns>The JsonElement at the path, or null if not found.</returns>
    internal static JsonElement? GetElementAtPath(JsonElement root, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return root;
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = root;

        foreach (var segment in segments)
        {
            // Check if segment is an array element filter: {PropertyName = Value}
            if (segment.StartsWith('{') && segment.EndsWith('}'))
            {
                var filterContent = segment[1..^1]; // Remove { and }
                var equalsIndex = filterContent.IndexOf('=');

                if (equalsIndex < 0)
                {
                    return null; // Invalid filter syntax
                }

                var propertyName = filterContent[..equalsIndex].Trim();
                var propertyValue = filterContent[(equalsIndex + 1)..].Trim();

                if (current.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                var found = false;
                foreach (var item in current.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object &&
                        item.TryGetProperty(propertyName, out var prop) &&
                        prop.ValueKind == JsonValueKind.String &&
                        prop.GetString() == propertyValue)
                    {
                        current = item;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return null;
                }
            }
            // Check if segment is an array index
            else if (int.TryParse(segment, out var index))
            {
                if (current.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                var array = current.EnumerateArray().ToList();
                if (index < 0 || index >= array.Count)
                {
                    return null;
                }

                current = array[index];
            }
            else
            {
                if (current.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                if (!current.TryGetProperty(segment, out var next))
                {
                    return null;
                }

                current = next;
            }
        }

        return current;
    }

    /// <summary>
    /// Gets all CloudFormation resources of the specified type.
    /// </summary>
    /// <param name="cfTemplate">The CloudFormation template document.</param>
    /// <param name="resourceType">The CloudFormation resource type (e.g., "AWS::EC2::VPC", "AWS::ECS::Service").</param>
    /// <returns>A list of tuples containing the logical ID and the resource element.</returns>
    internal static IReadOnlyList<(string LogicalId, JsonElement Resource)> GetResourcesOfType(JsonDocument cfTemplate, string resourceType)
    {
        var results = new List<(string LogicalId, JsonElement Resource)>();

        if (!cfTemplate.RootElement.TryGetProperty("Resources", out var resources))
        {
            return results;
        }

        foreach (var resource in resources.EnumerateObject())
        {
            if (resource.Value.TryGetProperty("Type", out var type) &&
                type.ValueKind == JsonValueKind.String &&
                type.GetString() == resourceType)
            {
                results.Add((resource.Name, resource.Value));
            }
        }

        return results;
    }

    /// <summary>
    /// Asserts that an element exists at the specified path.
    /// </summary>
    internal static JsonElement AssertElementExistsAtPath(JsonDocument cfTemplate, string path)
    {
        var element = GetElementAtPath(cfTemplate, path);
        Assert.True(element.HasValue, $"Element not found at path '{path}'");
        return element.Value;
    }

    /// <summary>
    /// Asserts that an element exists at the specified path and returns it.
    /// </summary>
    internal static JsonElement AssertElementExistsAtPath(JsonElement root, string path)
    {
        var element = GetElementAtPath(root, path);
        Assert.True(element.HasValue, $"Element not found at path '{path}'");
        return element.Value;
    }

    /// <summary>
    /// Compares two JsonElements for deep equality.
    /// </summary>
    /// <param name="expected">The expected JsonElement.</param>
    /// <param name="actual">The actual JsonElement.</param>
    /// <returns>True if the elements are equal, false otherwise.</returns>
    internal static bool JsonElementEquals(JsonElement expected, JsonElement actual)
    {
        if (expected.ValueKind != actual.ValueKind)
        {
            return false;
        }

        switch (expected.ValueKind)
        {
            case JsonValueKind.String:
                return expected.GetString() == actual.GetString();

            case JsonValueKind.Number:
                return expected.GetRawText() == actual.GetRawText();

            case JsonValueKind.True:
            case JsonValueKind.False:
                return expected.GetBoolean() == actual.GetBoolean();

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return true;

            case JsonValueKind.Array:
                var expectedArray = expected.EnumerateArray().ToList();
                var actualArray = actual.EnumerateArray().ToList();

                if (expectedArray.Count != actualArray.Count)
                {
                    return false;
                }

                for (int i = 0; i < expectedArray.Count; i++)
                {
                    if (!JsonElementEquals(expectedArray[i], actualArray[i]))
                    {
                        return false;
                    }
                }
                return true;

            case JsonValueKind.Object:
                var expectedProps = expected.EnumerateObject().ToList();
                var actualProps = actual.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

                if (expectedProps.Count != actualProps.Count)
                {
                    return false;
                }

                foreach (var prop in expectedProps)
                {
                    if (!actualProps.TryGetValue(prop.Name, out var actualValue))
                    {
                        return false;
                    }

                    if (!JsonElementEquals(prop.Value, actualValue))
                    {
                        return false;
                    }
                }
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Asserts that two JsonElements are equal, with a descriptive failure message.
    /// </summary>
    internal static void AssertJsonElementEquals(JsonElement expected, JsonElement actual, string? message = null)
    {
        if (!JsonElementEquals(expected, actual))
        {
            var failMessage = message ?? "JsonElements are not equal";
            Assert.Fail($"{failMessage}\nExpected: {expected.GetRawText()}\nActual: {actual.GetRawText()}");
        }
    }

    /// <summary>
    /// Asserts that the actual JsonElement equals the expected JSON string.
    /// </summary>
    internal static void AssertJsonEquals(string expectedJson, JsonElement actual, string? message = null)
    {
        using var expectedDoc = JsonDocument.Parse(expectedJson);
        AssertJsonElementEquals(expectedDoc.RootElement, actual, message);
    }
}
