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
    private readonly string _revitVersion;
    private readonly Action<string>? _log;

    public CommandManager(
        ICommandRegistry registry,
        object? configurationManager,
        UIApplication uiApplication,
        string revitVersion,
        Action<string>? log = null)
    {
        _registry = registry;
        _configurationManager = configurationManager;
        _uiApplication = uiApplication;
        _revitVersion = revitVersion;
        _log = log;
    }

    public void LoadCommands()
    {
        var loadedCount = 0;

        foreach (var config in GetCommandConfigs())
        {
            try
            {
                if (!GetBool(config, "Enabled", true))
                    continue;

                var commandName = GetString(config, "CommandName");
                var assemblyPath = ResolveAssemblyPath(GetString(config, "AssemblyPath"));
                if (string.IsNullOrWhiteSpace(commandName))
                    continue;

                if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
                {
                    Log($"Command '{commandName}' skipped: assembly not found at '{assemblyPath}'.");
                    continue;
                }

                if (LoadCommandFromAssembly(commandName, assemblyPath))
                    loadedCount++;
            }
            catch (Exception ex)
            {
                Log($"Failed to load command: {ex.Message}");
            }
        }

        Log($"Loaded {loadedCount} Revit MCP command(s).");
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

    private bool LoadCommandFromAssembly(string commandName, string assemblyPath)
    {
        var assemblyDirectory = Path.GetDirectoryName(assemblyPath);
        ResolveEventHandler? assemblyResolveHandler = assemblyDirectory == null
            ? null
            : CreateAssemblyResolveHandler(assemblyDirectory, _revitVersion);

        if (assemblyResolveHandler != null)
            AppDomain.CurrentDomain.AssemblyResolve += assemblyResolveHandler;

        try
        {
            var assembly = Assembly.LoadFrom(assemblyPath);
            var scannedTypes = 0;
            var commandLikeSamples = new List<string>();

            foreach (var type in GetLoadableTypes(assembly))
            {
                if (type == null || type.IsInterface || type.IsAbstract)
                    continue;

                scannedTypes++;
                CaptureCommandLikeSample(type, commandLikeSamples);

                IRevitCommand? command;
                try
                {
                    command = CreateCommand(type);
                }
                catch (Exception ex)
                {
                    Log($"Skipped type '{type.FullName}': {ex.Message}");
                    continue;
                }

                if (command != null && command.CommandName.Equals(commandName, StringComparison.OrdinalIgnoreCase))
                {
                    _registry.RegisterCommand(command);
                    Log($"Registered command '{commandName}'.");
                    return true;
                }
            }

            var samples = commandLikeSamples.Count == 0
                ? "none"
                : string.Join("; ", commandLikeSamples.Take(3));
            Log($"Command '{commandName}' not found in assembly '{assemblyPath}'. Scanned {scannedTypes} concrete type(s). Command-like samples: {samples}.");
            return false;
        }
        finally
        {
            if (assemblyResolveHandler != null)
                AppDomain.CurrentDomain.AssemblyResolve -= assemblyResolveHandler;
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

            var constructor = GetUIApplicationConstructor(type);
            return constructor != null
                ? (IRevitCommand)constructor.Invoke(new object[] { _uiApplication })
                : (IRevitCommand)Activator.CreateInstance(type)!;
        }

        if (!CommandReflectionUtils.LooksLikeLegacyCommand(type))
            return null;

        var legacyConstructor = GetUIApplicationConstructor(type);
        var legacyCommand = legacyConstructor != null
            ? legacyConstructor.Invoke(new object[] { _uiApplication })
            : Activator.CreateInstance(type);

        return legacyCommand == null ? null : new LegacyRevitCommandAdapter(legacyCommand);
    }

    private string ResolveAssemblyPath(string assemblyPath)
        => CommandAssemblyPathResolver.Resolve(
            Configuration.PathManager.GetCommandsDirectoryPath(),
            assemblyPath,
            _revitVersion);

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

    private static ResolveEventHandler CreateAssemblyResolveHandler(string commandAssemblyDirectory, string revitVersion)
    {
        var searchDirectories = new[]
        {
            commandAssemblyDirectory,
            Configuration.PathManager.GetAppDataDirectoryPath(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Autodesk", $"Revit {revitVersion}")
        };

        return (_, args) =>
        {
            var requestedAssemblyName = new AssemblyName(args.Name);
            var loadedAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => AssemblyName.ReferenceMatchesDefinition(
                    assembly.GetName(),
                    requestedAssemblyName));
            if (loadedAssembly != null)
                return loadedAssembly;

            var fileName = requestedAssemblyName.Name + ".dll";
            foreach (var directory in searchDirectories)
            {
                var candidate = Path.Combine(directory, fileName);
                if (!File.Exists(candidate))
                    continue;

                try
                {
                    return Assembly.LoadFrom(candidate);
                }
                catch
                {
                    continue;
                }
            }

            return null;
        };
    }

    private static ConstructorInfo? GetUIApplicationConstructor(Type type)
        => type.GetConstructors()
            .FirstOrDefault(ctor =>
            {
                var parameters = ctor.GetParameters();
                return parameters.Length == 1
                    && parameters[0].ParameterType.FullName == typeof(UIApplication).FullName;
            });

    private static void CaptureCommandLikeSample(Type type, List<string> samples)
    {
        if (samples.Count >= 3)
            return;

        try
        {
            var commandNameProperty = type.GetProperty("CommandName");
            var executeMethod = CommandReflectionUtils.GetLegacyExecuteMethod(type);
            if (commandNameProperty == null && executeMethod == null)
                return;

            var commandName = commandNameProperty == null ? "<no CommandName>" : "<CommandName>";
            var execute = executeMethod == null
                ? "<no Execute(JObject,string)>"
                : string.Join(", ", executeMethod.GetParameters().Select(p => p.ParameterType.FullName));
            samples.Add($"{type.FullName}: {commandName}, {execute}");
        }
        catch (Exception ex)
        {
            samples.Add($"{type.FullName}: inspection failed: {ex.Message}");
        }
    }

    private void Log(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[CommandManager] {message}");
        _log?.Invoke($"[CommandManager] {message}");
    }

    private sealed class LegacyRevitCommandAdapter : IRevitCommand
    {
        private readonly object _inner;
        private readonly MethodInfo _executeMethod;

        public LegacyRevitCommandAdapter(object inner)
        {
            _inner = inner;
            _executeMethod = CommandReflectionUtils.GetLegacyExecuteMethod(inner.GetType())
                ?? throw new InvalidOperationException($"Legacy command '{inner.GetType().FullName}' does not expose Execute(JObject, string).");
        }

        public string CommandName
            => _inner.GetType().GetProperty("CommandName")?.GetValue(_inner)?.ToString() ?? "";

        public object Execute(JObject parameters, string requestId)
        {
            var convertedParameters = CommandReflectionUtils.ConvertParametersForLegacyExecute(parameters, _executeMethod);
            return _executeMethod.Invoke(_inner, new[] { convertedParameters, requestId })!;
        }
    }
}
