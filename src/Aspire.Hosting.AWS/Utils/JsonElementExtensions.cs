// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using System.Text.Json;

namespace Aspire.Hosting.AWS.Utils;

internal static class JsonElementExtensions
{
    /// <summary>
    /// Utility method to convert a JsonElement into a dictionary/array/primitive object. This is required
    /// when sending a parsed JSON document over to CDK to avoid unmapped JSII types. 
    /// </summary>
    /// <param name="element"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    internal static object ConvertToDotNetPrimitives(this JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object>();
                foreach (var prop in element.EnumerateObject())
                {
                    dict[prop.Name] = ConvertToDotNetPrimitives(prop.Value);
                }
                return dict;

            case JsonValueKind.Array:
                var list = new List<object>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(ConvertToDotNetPrimitives(item));
                }
                return list.ToArray();

            case JsonValueKind.String:
                return element.GetString()!;

            case JsonValueKind.Number:
                // CDK context supports both int and double
                if (element.TryGetInt64(out var l))
                    return l;
                return element.GetDouble();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null!;

            default:
                throw new NotSupportedException($"Unsupported JSON token: {element.ValueKind}");
        }
    }
}
