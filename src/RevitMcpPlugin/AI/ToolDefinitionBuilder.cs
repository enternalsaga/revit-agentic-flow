using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace RevitMcpPlugin.AI;

public class ToolDefinitionBuilder
{
    public JArray BuildFromCommandJson(string commandJsonPath)
    {
        var json = JObject.Parse(File.ReadAllText(commandJsonPath));
        var commands = json["commands"] as JArray ?? new JArray();
        var tools = new JArray();
        foreach (var cmd in commands)
        {
            tools.Add(new JObject
            {
                ["name"] = cmd["commandName"],
                ["description"] = cmd["description"],
                ["input_schema"] = BuildInputSchema(cmd["commandName"]?.ToString() ?? string.Empty)
            });
        }
        return tools;
    }

    private static JObject BuildInputSchema(string commandName)
    {
        if (string.Equals(commandName, "search_revit_api", StringComparison.Ordinal))
        {
            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["query"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "Revit API class, member, or keyword search query."
                    }
                },
                ["required"] = new JArray("query"),
                ["additionalProperties"] = false
            };
        }

        return new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject(),
            ["additionalProperties"] = true
        };
    }
}
