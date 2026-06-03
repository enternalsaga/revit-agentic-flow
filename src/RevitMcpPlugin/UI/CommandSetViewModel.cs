using System.Collections.ObjectModel;

namespace RevitMcpPlugin.UI;

public class CommandSetViewModel
{
    public CommandSetViewModel(string name, IEnumerable<CommandItemViewModel> commands)
    {
        Name = name;
        Commands = new ObservableCollection<CommandItemViewModel>(commands);
    }

    public string Name { get; }

    public ObservableCollection<CommandItemViewModel> Commands { get; }

    public override string ToString() => Name;
}
