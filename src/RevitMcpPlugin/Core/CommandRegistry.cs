using RevitMcpSdk;

namespace RevitMcpPlugin.Core;

public interface ICommandRegistry
{
    void RegisterCommand(IRevitCommand command);
    bool TryGetCommand(string commandName, out IRevitCommand command);
    void ClearCommands();
    IEnumerable<string> GetRegisteredCommands();
}

public class CommandRegistry : ICommandRegistry
{
    private readonly Dictionary<string, IRevitCommand> _commands = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterCommand(IRevitCommand command)
        => _commands[command.CommandName] = command;

    public bool TryGetCommand(string commandName, out IRevitCommand command)
        => _commands.TryGetValue(commandName, out command!);

    public void ClearCommands() => _commands.Clear();

    public IEnumerable<string> GetRegisteredCommands() => _commands.Keys;
}
