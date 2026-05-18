using System.IO;
using System.Reflection;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMcpSdk;

namespace RevitMcpPlugin.Core;

public class CommandManager
{
    private readonly ICommandRegistry _registry;
    private readonly object? _configurationManager;
    private readonly UIApplication _uiApplication;

    public CommandManager(ICommandRegistry registry, object? configurationManager, UIApplication uiApplication)
    {
        _registry = registry;
        _configurationManager = configurationManager;
        _uiApplication = uiApplication;
    }

    public void LoadCommands()
    {
        foreach (var config in GetCommandConfigs())
        {
            try
            {
                if (!GetBool(config, "Enabled", true))
                    continue;

                var commandName = GetString(config, "CommandName");
                var assemblyPath = ResolveAssemblyPath(GetString(config, "AssemblyPath"));
                if (string.IsNullOrWhiteSpace(commandName) || string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
                    continue;

                LoadCommandFromAssembly(commandName, assemblyPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CommandManager] Failed to load command: {ex.Message}");
            }
        }
    }

    private IEnumerable<object> GetCommandConfigs()
    {
        if (_configurationManager == null)
            yield break;

        var config = _configurationManager.GetType().GetProperty("Config")?.GetValue(_configurationManager);
        var commands = config?.GetType().GetProperty("Commands")?.GetValue(config) as System.Collections.IEnumerable;
        if (commands == null)
            yield break;

        foreach (var command in commands)
        {
            if (command != null)
                yield return command;
        }
    }

    private void LoadCommandFromAssembly(string commandName, string assemblyPath)
    {
        var assembly = Assembly.LoadFrom(assemblyPath);
        foreach (var type in GetLoadableTypes(assembly))
        {
            if (type == null || type.IsInterface || type.IsAbstract)
                continue;

            var command = CreateCommand(type);
            if (command != null && command.CommandName.Equals(commandName, StringComparison.OrdinalIgnoreCase))
            {
                _registry.RegisterCommand(command);
                return;
            }
        }
    }

    private IRevitCommand? CreateCommand(Type type)
    {
        if (typeof(IRevitCommand).IsAssignableFrom(type))
        {
            if (typeof(IRevitCommandInitializable).IsAssignableFrom(type))
            {
                var command = (IRevitCommand)Activator.CreateInstance(type)!;
                ((IRevitCommandInitializable)command).Initialize(_uiApplication);
                return command;
            }

            var constructor = type.GetConstructor(new[] { typeof(UIApplication) });
            return constructor != null
                ? (IRevitCommand)constructor.Invoke(new object[] { _uiApplication })
                : (IRevitCommand)Activator.CreateInstance(type)!;
        }

        if (!ImplementsLegacyCommand(type))
            return null;

        var legacyConstructor = type.GetConstructor(new[] { typeof(UIApplication) });
        var legacyCommand = legacyConstructor != null
            ? legacyConstructor.Invoke(new object[] { _uiApplication })
            : Activator.CreateInstance(type);

        return legacyCommand == null ? null : new LegacyRevitCommandAdapter(legacyCommand);
    }

    private static string ResolveAssemblyPath(string assemblyPath)
    {
        if (Path.IsPathRooted(assemblyPath))
            return assemblyPath;

        var version = assemblyPath.Replace("{VERSION}", "2025", StringComparison.OrdinalIgnoreCase);
        return Path.Combine(AppContext.BaseDirectory, "Commands", version);
    }

    private static string GetString(object source, string propertyName)
        => source.GetType().GetProperty(propertyName)?.GetValue(source)?.ToString() ?? "";

    private static bool GetBool(object source, string propertyName, bool defaultValue)
        => source.GetType().GetProperty(propertyName)?.GetValue(source) as bool? ?? defaultValue;

    private static IEnumerable<Type?> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type != null);
        }
    }

    private static bool ImplementsLegacyCommand(Type type)
        => type.GetInterfaces().Any(i => i.FullName == "RevitMCPSDK.API.Interfaces.IRevitCommand");

    private sealed class LegacyRevitCommandAdapter : IRevitCommand
    {
        private readonly object _inner;
        private readonly MethodInfo _executeMethod;

        public LegacyRevitCommandAdapter(object inner)
        {
            _inner = inner;
            _executeMethod = inner.GetType().GetMethod("Execute", new[] { typeof(JObject), typeof(string) })
                ?? throw new InvalidOperationException($"Legacy command '{inner.GetType().FullName}' does not expose Execute(JObject, string).");
        }

        public string CommandName
            => _inner.GetType().GetProperty("CommandName")?.GetValue(_inner)?.ToString() ?? "";

        public object Execute(JObject parameters, string requestId)
            => _executeMethod.Invoke(_inner, new object[] { parameters, requestId })!;
    }
}
