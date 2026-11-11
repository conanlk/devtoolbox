using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using DevToolBox.App.Platform;

namespace DevToolBox.App.Shell;

public partial class MainWindow : Window
{
    private const string SidebarCollapsedClass = "sidebar-collapsed";

    public MainWindow()
    {
        InitializeComponent();
        RegisterShortcuts();
    }

    /// <summary>Applies OS window styling and placement restore. Call once, before showing.</summary>
    public void Configure(IWindowChrome chrome, WindowPlacementTracker placement)
    {
        ArgumentNullException.ThrowIfNull(chrome);
        ArgumentNullException.ThrowIfNull(placement);
        chrome.Apply(this);
        placement.Attach(this);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ShellViewModel shell)
        {
            shell.PropertyChanged += OnShellPropertyChanged;
            UpdateSidebarClass(shell);
        }
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is ShellViewModel shell && e.PropertyName == nameof(ShellViewModel.IsSidebarCollapsed))
        {
            UpdateSidebarClass(shell);
        }
    }

    private void UpdateSidebarClass(ShellViewModel shell) => Classes.Set(SidebarCollapsedClass, shell.IsSidebarCollapsed);

    /// <summary>Cmd on macOS, Ctrl elsewhere: +K or +F focuses tool search, +B toggles the sidebar.</summary>
    private void RegisterShortcuts()
    {
        var modifier = Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers
                       ?? KeyModifiers.Control;
        SearchShortcutText.Text = modifier == KeyModifiers.Meta ? "⌘K" : "Ctrl K";

        KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.K, modifier),
            Command = new CommunityToolkit.Mvvm.Input.RelayCommand(FocusSearch),
        });

        KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.B, modifier),
            Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() => (DataContext as ShellViewModel)?.ToggleSidebar()),
        });
        KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.F, modifier),
            Command = new CommunityToolkit.Mvvm.Input.RelayCommand(FocusSearch),
        });
    }

    private void FocusSearch()
    {
        if (DataContext is ShellViewModel { IsSidebarCollapsed: true } shell)
        {
            shell.ToggleSidebar();
        }

        SearchBox.Focus();
        SearchBox.SelectAll();
    }
}
