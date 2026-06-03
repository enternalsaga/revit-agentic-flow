using System.ComponentModel;
using RevitMcpPlugin.Configuration;

namespace RevitMcpPlugin.UI;

public class CommandItemViewModel : INotifyPropertyChanged
{
    private bool _enabled;

    public CommandItemViewModel(CommandConfig config)
    {
        Config = config;
        _enabled = config.Enabled;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CommandConfig Config { get; }

    public string CommandName => Config.CommandName;

    public string Description => Config.Description;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
                return;

            _enabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled)));
        }
    }
}
