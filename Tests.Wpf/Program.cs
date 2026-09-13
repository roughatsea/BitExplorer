using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfApp1;
using WpfApp1.Controls;
using WpfApp1.ViewModels;

internal static partial class Program
{
    private static int _passed;
    private static int _failed;
    private static readonly string ArtifactRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "test-artifacts"));
    private static readonly string ArtifactDirectory = Path.Combine(ArtifactRoot, Guid.NewGuid().ToString("N"));

    [STAThread]
    private static int Main()
    {
        // All commands use fake interactions. This harness never shows a window or
        // dialog, runs Application.Run, or operates a user's desktop.
        var app = new App();
        app.InitializeComponent();
        Directory.CreateDirectory(ArtifactDirectory);
        try
        {
            RunPanelTests();
            Test("grid hit testing shares formatter columns in byte, bit, and ASCII views", GridHitTesting);
            Test("grid fitting preserves explicit grouping and reflow preserves the top byte", GridFittingAndReflow);
            TestAsync("asynchronous file opens restore busy state on success, cancellation, and failure", OpenCommands);
            TestAsync("pending operations disable edits and reject a second open until errors are handled", BusyCommandGuards);
            TestAsync("disposing a pending open ignores its later result and errors", DisposedOpenCompletion);
            TestAsync("formatted export uses the current model and reports failures without staying busy", ExportCommands);
            Test("save decisions and canceled dialogs preserve unsaved work", SaveDecisions);
            Test("compiled resources and main-window bindings attach the shared view model", CompiledBindings);
        }
        finally
        {
            string target = Path.GetFullPath(ArtifactDirectory);
            if (target.StartsWith(ArtifactRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(target)) Directory.Delete(target, recursive: true);
        }
        Console.WriteLine($"\n{_passed} passed, {_failed} failed.");
        return _failed == 0 ? 0 : 1;
    }

    private static void CompiledBindings()
    {
        using var listener = new BindingErrorListener();
        var source = PresentationTraceSources.DataBindingSource;
        var previousLevel = source.Switch.Level;
        source.Switch.Level = SourceLevels.Error;
        source.Listeners.Add(listener);
        try
        {
            var dialogs = new FakeUserInteractionService();
            using var suppliedViewModel = new MainWindowViewModel(dialogs);
            var window = new MainWindow(suppliedViewModel);
            Equal(false, window.IsVisible);
            Equal(false, window.IsLoaded);
            window.Measure(new Size(1280, 900));
            window.Arrange(new Rect(0, 0, 1280, 900));
            window.UpdateLayout();
            FlushBindings();
            var viewModel = window.DataContext as MainWindowViewModel
                ?? throw new InvalidOperationException("MainWindow must bind to MainWindowViewModel.");
            var background = (SolidColorBrush)window.FindResource("CanvasBrush");
            Equal(Color.FromRgb(0x10, 0x15, 0x1E), background.Color);
            Equal(true, window.FindResource("AccentButton") is Style);
            var grid = FindLogical<ByteGrid>(window, "DataGrid");
            Equal(true, ReferenceEquals(viewModel.Session.Document, grid.Document));
            Equal(true, ReferenceEquals(viewModel.Session.Selection, grid.Selection));
            var hex = FindLogical<TextBlock>(window, "ByteHex");
            Equal(viewModel.Inspector.ByteHex, hex.Text);
            viewModel.Session.SelectRawBits(Enumerable.Range(8, 8).Select(bit => (long)bit));
            FlushBindings();
            Equal("6C", hex.Text);

            var input = FindLogical<TextBox>(window, "ByteInput");
            input.Text = "A5";
            BindingOperations.GetBindingExpression(input, TextBox.TextProperty)!.UpdateSource();
            Equal("A5", viewModel.Inspector.ByteInput);
            var enter = input.InputBindings.OfType<KeyBinding>().Single(binding => binding.Key == Key.Enter);
            Equal(true, ReferenceEquals(viewModel.Inspector.ApplyByteCommand, enter.Command));
            enter.Command.Execute(enter.CommandParameter);
            FlushBindings();
            Equal((byte)0xA5, viewModel.Session.Document.Data[1]);
            Equal("A5", hex.Text);

            var fields = FindLogical<ListBox>(window, "FieldList");
            Equal(true, ReferenceEquals(viewModel.Fields.Items, fields.ItemsSource));
            Equal(4, fields.Items.Count);
            var selected = viewModel.Fields.Items.Single(field => field.Name == "Message type");
            fields.SelectedItem = selected;
            BindingOperations.GetBindingExpression(fields, ListBox.SelectedItemProperty)!.UpdateSource();
            FlushBindings();
            Equal(selected.Id, viewModel.Session.SelectedField!.Id);
            Sequence(viewModel.Session.SelectedField.OrderedBits.Order(), grid.SelectedBits);
            var contextMenu = fields.ContextMenu!;
            contextMenu.PlacementTarget = fields;
            FlushBindings();
            var editItem = contextMenu.Items.OfType<MenuItem>().First();
            Equal(true, ReferenceEquals(viewModel.Fields.EditLabelCommand, editItem.Command));
            editItem.Command.Execute(null); // Fake dialog cancels, with the selected field as its target.
            Equal(selected.Id, dialogs.FieldDrafts.Single().Id);
            Equal("Message type", viewModel.Session.SelectedField.Name);
            var labelButton = LogicalDescendants(window).OfType<Button>()
                .Single(button => button.Content?.ToString() == "+  Label selected bits");
            Equal(true, ReferenceEquals(viewModel.Fields.AddLabelCommand, labelButton.Command));
            if (listener.Messages.Count > 0)
                throw new InvalidOperationException("WPF binding errors: " + string.Join("\n", listener.Messages));
        }
        finally
        {
            source.Listeners.Remove(listener);
            source.Switch.Level = previousLevel;
        }
    }

    private static T FindLogical<T>(DependencyObject element, string name) where T : FrameworkElement
    {
        if (element is T match && match.Name == name) return match;
        foreach (var child in LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>())
        {
            try { return FindLogical<T>(child, name); }
            catch (KeyNotFoundException) { }
        }
        throw new KeyNotFoundException($"Missing {typeof(T).Name} named {name} in the compiled logical tree.");
    }

    private static IEnumerable<DependencyObject> LogicalDescendants(DependencyObject element)
    {
        yield return element;
        foreach (var child in LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>())
            foreach (var descendant in LogicalDescendants(child)) yield return descendant;
    }

    private static void FlushBindings()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    private sealed class BindingErrorListener : TraceListener
    {
        public List<string> Messages { get; } = [];
        public override void Write(string? message) { if (!string.IsNullOrWhiteSpace(message)) Messages.Add(message); }
        public override void WriteLine(string? message) => Write(message);
    }

    private static string Artifact(string name) => Path.Combine(ArtifactDirectory, name);

    private static void TestAsync(string name, Func<Task> action) => Test(name, () =>
    {
        Task task = action();
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            var dispatcher = Dispatcher.CurrentDispatcher;
            var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            timeout.Tick += (_, _) => frame.Continue = false;
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
            timeout.Start();
            try { Dispatcher.PushFrame(frame); }
            finally { timeout.Stop(); }
        }
        if (!task.IsCompleted) throw new TimeoutException("The in-process command did not finish in 20 seconds.");
        task.GetAwaiter().GetResult();
    });

    private static void Test(string name, Action action)
    {
        try { action(); _passed++; Console.WriteLine($"PASS {name}"); }
        catch (Exception exception) { _failed++; Console.Error.WriteLine($"FAIL {name}: {exception}"); }
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected <{expected}>, got <{actual}>.");
    }

    private static void Sequence<T>(IEnumerable<T> expected, IEnumerable<T> actual)
    {
        if (!expected.SequenceEqual(actual))
            throw new InvalidOperationException($"Expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}].");
    }
}
