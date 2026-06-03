using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using RevitMcpPlugin.Configuration;
using Ellipse = System.Windows.Shapes.Ellipse;

namespace RevitMcpPlugin.UI;

public class SettingsWindow : Window
{
    private readonly ConfigurationManager _configurationManager;
    private readonly Func<bool> _isServiceRunning;
    private readonly Func<Action<string>, bool> _startService;
    private readonly Action _stopService;
    private readonly ListBox _commandSetListBox = new();
    private readonly DataGrid _commandGrid = new();
    private readonly Ellipse _statusIndicator = new() { Width = 12, Height = 12 };
    private readonly TextBlock _statusText = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _switchButton = new() { MinWidth = 86, Height = 28 };
    private readonly TextBox _consoleBox = new();
    private readonly ObservableCollection<CommandSetViewModel> _commandSets = [];

    public SettingsWindow(
        ConfigurationManager configurationManager,
        Func<bool> isServiceRunning,
        Func<Action<string>, bool> startService,
        Action stopService)
    {
        _configurationManager = configurationManager;
        _isServiceRunning = isServiceRunning;
        _startService = startService;
        _stopService = stopService;

        Title = "Revit MCP Settings";
        Width = 980;
        Height = 700;
        MinWidth = 760;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        Content = BuildContent();
        LoadCommandSets();
        Loaded += (_, _) =>
        {
            UpdateConnectionStatus();
            AppendLog("Settings window opened.");
        };
    }

    private UIElement BuildContent()
    {
        var root = new DockPanel { Margin = new Thickness(12) };

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
        };
        DockPanel.SetDock(footer, Dock.Bottom);

        footer.Children.Add(CreateButton("Open CommandSet Folder", OpenFolderButton_Click));
        footer.Children.Add(CreateButton("Select All", SelectAllButton_Click));
        footer.Children.Add(CreateButton("Deselect All", UnselectAllButton_Click));
        footer.Children.Add(CreateButton("Refresh", RefreshButton_Click));
        footer.Children.Add(CreateButton("Save", SaveButton_Click));
        root.Children.Add(footer);

        var consolePanel = BuildConsolePanel();
        DockPanel.SetDock(consolePanel, Dock.Bottom);
        root.Children.Add(consolePanel);

        var header = new TextBlock
        {
            Text = "MCP Settings",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var subheader = new TextBlock
        {
            Text = "Configure command sets and MCP connection",
            Margin = new Thickness(0, 0, 0, 12)
        };
        DockPanel.SetDock(subheader, Dock.Top);
        root.Children.Add(subheader);

        var statusPanel = BuildStatusPanel();
        DockPanel.SetDock(statusPanel, Dock.Top);
        root.Children.Add(statusPanel);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = new DockPanel();
        Grid.SetColumn(left, 0);

        var listHeader = new TextBlock
        {
            Text = "Available Command Sets",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6)
        };
        DockPanel.SetDock(listHeader, Dock.Top);
        left.Children.Add(listHeader);

        _commandSetListBox.DisplayMemberPath = nameof(CommandSetViewModel.Name);
        _commandSetListBox.SelectionChanged += CommandSetListBox_SelectionChanged;
        left.Children.Add(_commandSetListBox);
        grid.Children.Add(left);

        _commandGrid.AutoGenerateColumns = false;
        _commandGrid.CanUserAddRows = false;
        _commandGrid.HeadersVisibility = DataGridHeadersVisibility.Column;
        _commandGrid.Columns.Add(new DataGridCheckBoxColumn
        {
            Header = "Enable",
            Binding = new Binding(nameof(CommandItemViewModel.Enabled))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            },
            Width = new DataGridLength(80)
        });
        _commandGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Command",
            Binding = new Binding(nameof(CommandItemViewModel.CommandName)),
            IsReadOnly = true,
            Width = new DataGridLength(220)
        });
        _commandGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Description",
            Binding = new Binding(nameof(CommandItemViewModel.Description)),
            IsReadOnly = true,
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
        Grid.SetColumn(_commandGrid, 2);
        grid.Children.Add(_commandGrid);

        root.Children.Add(grid);
        return root;
    }

    private UIElement BuildStatusPanel()
    {
        var panel = new Border
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 12)
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(_statusIndicator);
        row.Children.Add(new TextBlock
        {
            Text = "Status:",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(10, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        row.Children.Add(_statusText);

        _switchButton.Margin = new Thickness(18, 0, 0, 0);
        _switchButton.Click += SwitchButton_Click;
        row.Children.Add(_switchButton);

        panel.Child = row;
        return panel;
    }

    private UIElement BuildConsolePanel()
    {
        var panel = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        var label = new TextBlock
        {
            Text = "Console",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6)
        };
        DockPanel.SetDock(label, Dock.Top);
        panel.Children.Add(label);

        _consoleBox.Height = 150;
        _consoleBox.IsReadOnly = true;
        _consoleBox.AcceptsReturn = true;
        _consoleBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _consoleBox.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        _consoleBox.FontFamily = new FontFamily("Consolas");
        _consoleBox.FontSize = 12;
        panel.Children.Add(_consoleBox);

        return panel;
    }

    private static Button CreateButton(string text, RoutedEventHandler clickHandler)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 86,
            Height = 28,
            Margin = new Thickness(6, 0, 0, 0)
        };
        button.Click += clickHandler;
        return button;
    }

    private void LoadCommandSets()
    {
        _configurationManager.LoadConfiguration();
        _commandSets.Clear();

        var grouped = _configurationManager.Config.Commands
            .OrderBy(c => c.AssemblyPath)
            .ThenBy(c => c.CommandName)
            .GroupBy(GetCommandSetName);

        foreach (var group in grouped)
        {
            _commandSets.Add(new CommandSetViewModel(
                group.Key,
                group.Select(config => new CommandItemViewModel(config))));
        }

        _commandSetListBox.ItemsSource = _commandSets;
        _commandSetListBox.SelectedIndex = _commandSets.Count > 0 ? 0 : -1;
    }

    private static string GetCommandSetName(CommandConfig config)
    {
        var assemblyPath = config.AssemblyPath.Replace('/', Path.DirectorySeparatorChar);
        var firstSeparator = assemblyPath.IndexOf(Path.DirectorySeparatorChar);
        return firstSeparator > 0 ? assemblyPath.Substring(0, firstSeparator) : "RevitMCPCommandSet";
    }

    private void CommandSetListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _commandGrid.ItemsSource = (_commandSetListBox.SelectedItem as CommandSetViewModel)?.Commands;
    }

    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        SetSelectedCommandsEnabled(true);
    }

    private void UnselectAllButton_Click(object sender, RoutedEventArgs e)
    {
        SetSelectedCommandsEnabled(false);
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        LoadCommandSets();
        AppendLog("Command settings refreshed.");
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var directory = PathManager.GetCommandsDirectoryPath();
        Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var command in _commandSets.SelectMany(set => set.Commands))
        {
            command.Config.Enabled = command.Enabled;
        }

        _configurationManager.SaveConfiguration();
        AppendLog("Settings saved.");
        MessageBox.Show(this, "Settings saved.", "Revit MCP", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SwitchButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isServiceRunning())
            {
                _stopService();
                AppendLog("Stop requested from settings.");
            }
            else
            {
                AppendLog("Start requested from settings.");
                _startService(AppendLog);
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Connection switch failed: {ex.Message}");
            MessageBox.Show(this, ex.Message, "Revit MCP", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        UpdateConnectionStatus();
    }

    private void SetSelectedCommandsEnabled(bool enabled)
    {
        if (_commandSetListBox.SelectedItem is not CommandSetViewModel commandSet)
            return;

        foreach (var command in commandSet.Commands)
        {
            command.Enabled = enabled;
        }
    }

    private void UpdateConnectionStatus()
    {
        var running = _isServiceRunning();
        _statusIndicator.Fill = running
            ? new SolidColorBrush(Color.FromRgb(0, 120, 215))
            : new SolidColorBrush(Color.FromRgb(196, 43, 28));
        _statusText.Text = running ? "Connected / Running" : "Disconnected / Stopped";
        _switchButton.Content = running ? "Stop" : "Start";
    }

    private void AppendLog(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => AppendLog(message));
            return;
        }

        _consoleBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        _consoleBox.ScrollToEnd();
    }
}
