using System.IO;
using System.Reflection;
using Autodesk.Revit.UI;
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
        foreach (var type in assembly.GetTypes())
        {
            if (!typeof(IRevitCommand).IsAssignableFrom(type) || type.IsInterface || type.IsAbstract)
                continue;

            var command = CreateCommand(type);
            if (command.CommandName.Equals(commandName, StringComparison.OrdinalIgnoreCase))
            {
                _registry.RegisterCommand(command);
                return;
            }
        }
    }

    private IRevitCommand CreateCommand(Type type)
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
}
