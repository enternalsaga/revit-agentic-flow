using System.Reflection;
using Newtonsoft.Json.Linq;

namespace RevitMcpPlugin.Core;

public static class CommandReflectionUtils
{
    private const string JObjectFullName = "Newtonsoft.Json.Linq.JObject";

    public static bool LooksLikeLegacyCommand(Type type)
        => type.GetProperty("CommandName") != null
            && GetLegacyExecuteMethod(type) != null;

    public static MethodInfo? GetLegacyExecuteMethod(Type type)
        => type.GetMethods()
            .FirstOrDefault(method =>
            {
                if (!method.Name.Equals("Execute", StringComparison.Ordinal))
                    return false;

                var parameters = method.GetParameters();
                return parameters.Length == 2
                    && parameters[0].ParameterType.FullName == JObjectFullName
                    && parameters[1].ParameterType == typeof(string);
            });

    public static object ConvertParametersForLegacyExecute(JObject parameters, MethodInfo executeMethod)
    {
        var targetType = executeMethod.GetParameters()[0].ParameterType;
        if (targetType.IsInstanceOfType(parameters))
            return parameters;

        var parseMethod = targetType.GetMethod(
            "Parse",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(string) },
            null);

        if (parseMethod == null)
            throw new InvalidOperationException($"Legacy JObject type '{targetType.AssemblyQualifiedName}' does not expose Parse(string).");

        return parseMethod.Invoke(null, new object[] { parameters.ToString() })!;
    }
}
