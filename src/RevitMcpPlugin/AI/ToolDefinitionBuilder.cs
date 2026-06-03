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
                ["input_schema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject(),
                    ["additionalProperties"] = true
                }
            });
        }
        return tools;
    }
}
