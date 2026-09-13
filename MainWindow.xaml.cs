using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using WpfApp1.Controls;
using WpfApp1.Services;
using WpfApp1.ViewModels;

namespace WpfApp1;

/// <summary>Adapts WPF focus, input, and window lifecycle to the application view model.</summary>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow() : this(null) { }

    public MainWindow(MainWindowViewModel? viewModel)
    {
        InitializeComponent();
        Width = Math.Min(1540, SystemParameters.WorkArea.Width);
        Height = Math.Min(960, SystemParameters.WorkArea.Height);
        _viewModel = viewModel ?? new MainWindowViewModel(new WpfUserInteractionService(this));
        DataContext = _viewModel;
        DataGrid.Document = _viewModel.Session.Document;
        DataGrid.Selection = _viewModel.Session.Selection;
        _viewModel.Session.DocumentReplaced += DocumentReplaced;
        _viewModel.Session.Selection.Changed += SelectionChanged;
        _viewModel.PropertyChanged += ViewModelPropertyChanged;
        _viewModel.GridFocusRequested += GridFocusRequested;
        Closed += Window_Closed;
        if (viewModel is null) Loaded += OpenCommandLineFile;
    }

    private async void OpenCommandLineFile(object sender, RoutedEventArgs e)
    {
        Loaded -= OpenCommandLineFile;
        var path = Environment.GetCommandLineArgs().Skip(1).FirstOrDefault();
        if (path is not null && File.Exists(path)) await _viewModel.OpenPathAsync(path, false);
    }

    private void DocumentReplaced(object? sender, EventArgs e) => DataGrid.Document = _viewModel.Session.Document;
    private void SelectionChanged(object? sender, EventArgs e) => DataGrid.ScrollToByte(_viewModel.Session.Selection.ActiveByteOffset);
    private void GridFocusRequested(object? sender, EventArgs e) => DataGrid.Focus();
    private void Grid_EditRequested(object? sender, ByteEditEventArgs e) => InspectorPanel.FocusByteEditor();
    private void Grid_LayoutSettingsChanged(object? sender, EventArgs e) => _viewModel?.NotifyGridLayoutChanged();
    private void DisplayOptions_Click(object sender, RoutedEventArgs e) => InspectorPanel.ToggleDisplayOptions();

    private void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(MainWindowViewModel.IsBusy))
            Cursor = _viewModel.IsBusy ? Cursors.Wait : null;
    }

    private void More_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button) return;
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        bool textFocus = Keyboard.FocusedElement is TextBox;
        bool editingText = Keyboard.FocusedElement is TextBox { IsReadOnly: false };
        ICommand? command = control ? e.Key switch
        {
            Key.O => _viewModel.OpenCommand,
            Key.S => shift ? _viewModel.SaveProjectCommand : _viewModel.SaveBinaryCommand,
            Key.E => _viewModel.ExportTextCommand,
            Key.G => _viewModel.GoToOffsetCommand,
            Key.Z when !editingText => _viewModel.UndoCommand,
            Key.Y when !editingText => _viewModel.RedoCommand,
            Key.C when !textFocus => _viewModel.CopyCommand,
            _ => null
        } : e.Key == Key.Space && DataGrid.IsKeyboardFocusWithin ? _viewModel.FlipSelectionCommand : null;
        if (command is null) return;
        e.Handled = true;
        if (command.CanExecute(null)) command.Execute(null);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = !_viewModel.IsBusy && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (!_viewModel.IsBusy && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths)
            await _viewModel.OpenPathAsync(paths[0]);
    }

    private void Window_Closing(object? sender, CancelEventArgs e) => e.Cancel = !_viewModel.ConfirmClose();

    private void Window_Closed(object? sender, EventArgs e)
    {
        _viewModel.Session.DocumentReplaced -= DocumentReplaced;
        _viewModel.Session.Selection.Changed -= SelectionChanged;
        _viewModel.PropertyChanged -= ViewModelPropertyChanged;
        _viewModel.GridFocusRequested -= GridFocusRequested;
        _viewModel.Dispose();
        DataGrid.Document = null;
        DataGrid.Selection = new BitExplorer.Core.BitSelection();
    }
}
