using Newtonsoft.Json.Linq;

namespace RevitMcpSdk;

public interface IRevitCommand
{
    string CommandName { get; }
    object Execute(JObject parameters, string requestId);
}
