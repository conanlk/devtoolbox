using System.Windows.Input;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DevToolBox.App.Platform;

namespace DevToolBox.App.Shared.Behaviors;

/// <summary>
/// Attached behavior: <c>behaviors:FileDrop.Command="{Binding OpenFileCommand}"</c> makes any control
/// accept a dropped file and passes it to the command as a <see cref="PickedFile"/>.
/// While a file hovers, the control gets the "drag-over" style class.
/// </summary>
public static class FileDrop
{
    public const string DragOverClass = "drag-over";

    public static readonly AttachedProperty<ICommand?> CommandProperty =
        AvaloniaProperty.RegisterAttached<Interactive, ICommand?>("Command", typeof(FileDrop));

    static FileDrop()
    {
        CommandProperty.Changed.AddClassHandler<Interactive>((element, e) =>
        {
            if (e.OldValue is null && e.NewValue is not null)
            {
                DragDrop.SetAllowDrop(element, true);
                element.AddHandler(DragDrop.DragOverEvent, OnDragOver);
                element.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
                element.AddHandler(DragDrop.DropEvent, OnDrop);
            }
        });
    }

    public static ICommand? GetCommand(Interactive element) => element.GetValue(CommandProperty);

    public static void SetCommand(Interactive element, ICommand? value) => element.SetValue(CommandProperty, value);

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        var hasFile = e.DataTransfer.Contains(DataFormat.File);
        e.DragEffects = hasFile ? DragDropEffects.Copy : DragDropEffects.None;
        SetDragOver(sender, hasFile);
        e.Handled = true;
    }

    private static void OnDragLeave(object? sender, DragEventArgs e) => SetDragOver(sender, false);

    private static async void OnDrop(object? sender, DragEventArgs e)
    {
        SetDragOver(sender, false);
        if (sender is not Interactive element || GetCommand(element) is not { } command)
        {
            return;
        }

        if (e.DataTransfer.TryGetFile() is IStorageFile file)
        {
            e.Handled = true;
            var picked = await PickedFile.FromStorageFileAsync(file);
            if (command.CanExecute(picked))
            {
                command.Execute(picked);
            }
        }
    }

    private static void SetDragOver(object? sender, bool value)
    {
        if (sender is StyledElement element)
        {
            element.Classes.Set(DragOverClass, value);
        }
    }
}
