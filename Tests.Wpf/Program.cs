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

/// <summary>
/// Entry point and shared helpers for the Windows regression tests. A regression
/// test checks an existing promise so later changes cannot silently break it.
/// This partial class continues in the other test files; it is one compiled class.
/// </summary>
internal static partial class Program
{
    // Accumulate results instead of stopping at the first failed scenario.
    private static int _passed;
    private static int _failed;
    // Each run gets its own random directory beneath the executable's output.
    // Tests can save/open real files without touching the user's documents.
    private static readonly string ArtifactRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "test-artifacts"));
    private static readonly string ArtifactDirectory = Path.Combine(ArtifactRoot, Guid.NewGuid().ToString("N"));

    /// <summary>Initializes WPF resources, runs all scenarios, cleans up, and returns an exit code.</summary>
    // WPF requires a single-threaded apartment (STA), a Windows threading convention
    // for UI/COM interaction. This attribute marks the test program's entry thread.
    [STAThread]
    private static int Main()
    {
        // All commands use fake interactions. This harness never shows a window or
        // dialog, runs Application.Run, or operates a user's desktop.
        // Loading App.xaml makes its real styles available to the unshown controls.
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
            // Even during failure, remove only this run's generated directory.
            // Resolving and checking the path before recursive deletion prevents
            // cleanup from accidentally targeting a directory outside our test area.
            string target = Path.GetFullPath(ArtifactDirectory);
            if (target.StartsWith(ArtifactRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(target)) Directory.Delete(target, recursive: true);
        }
        Console.WriteLine($"\n{_passed} passed, {_failed} failed.");
        // Shells and build systems conventionally treat exit code 0 as success.
        return _failed == 0 ? 0 : 1;
    }

    /// <summary>
    /// Exercises the real compiled XAML, not a mock view: values flow from models
    /// to controls, and text/selection/commands flow from controls back to models.
    /// </summary>
    private static void CompiledBindings()
    {
        // A misspelled binding path can display a blank control without throwing.
        // Capture WPF's diagnostic channel so that this test treats it as a failure.
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
            // WPF's two layout phases ask how much space controls need (Measure)
            // and give them actual rectangles (Arrange), even without showing a window.
            window.Measure(new Size(1280, 900));
            window.Arrange(new Rect(0, 0, 1280, 900));
            window.UpdateLayout();
            FlushBindings();
            var viewModel = window.DataContext as MainWindowViewModel
                ?? throw new InvalidOperationException("MainWindow must bind to MainWindowViewModel.");
            var background = (SolidColorBrush)window.FindResource("CanvasBrush");
            Equal(Color.FromRgb(0x10, 0x15, 0x1E), background.Color);
            Equal(true, window.FindResource("AccentButton") is Style);
            // ReferenceEquals checks shared ownership: equal copies would not prove
            // that the grid and inspector observe the same selection object.
            var grid = FindLogical<ByteGrid>(window, "DataGrid");
            Equal(true, ReferenceEquals(viewModel.Session.Document, grid.Document));
            Equal(true, ReferenceEquals(viewModel.Session.Selection, grid.Selection));
            var hex = FindLogical<TextBlock>(window, "ByteHex");
            Equal(viewModel.Inspector.ByteHex, hex.Text);
            viewModel.Session.SelectRawBits(Enumerable.Range(8, 8).Select(bit => (long)bit));
            FlushBindings();
            Equal("6C", hex.Text);

            // Simulate an edit through the real binding and Enter command. Explicit
            // UpdateSource sends the TextBox's value back to the bound draft property.
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

            // Select a real list item and verify its mapped bits reach the grid.
            var fields = FindLogical<ListBox>(window, "FieldList");
            Equal(true, ReferenceEquals(viewModel.Fields.Items, fields.ItemsSource));
            Equal(4, fields.Items.Count);
            var selected = viewModel.Fields.Items.Single(field => field.Name == "Message type");
            fields.SelectedItem = selected;
            BindingOperations.GetBindingExpression(fields, ListBox.SelectedItemProperty)!.UpdateSource();
            FlushBindings();
            Equal(selected.Id, viewModel.Session.SelectedField!.Id);
            Sequence(viewModel.Session.SelectedField.OrderedBits.Order(), grid.SelectedBits);
            // A context menu is a separate popup tree. Supplying PlacementTarget
            // checks its binding without opening a desktop menu or a native dialog.
            var contextMenu = fields.ContextMenu!;
            contextMenu.PlacementTarget = fields;
            FlushBindings();
            var editItem = contextMenu.Items.OfType<MenuItem>().First();
            Equal(true, ReferenceEquals(viewModel.Fields.EditLabelCommand, editItem.Command));
            editItem.Command.Execute(null); // Fake dialog cancels, with the selected field as its target.
            Equal(selected.Id, dialogs.FieldDrafts.Single().Id);
            Equal("Message type", viewModel.Session.SelectedField.Name);
            // The inspector's label button reaches across to the shell's Fields
            // view model with an ancestor binding; ensure it resolves to that command.
            var labelButton = LogicalDescendants(window).OfType<Button>()
                .Single(button => button.Content?.ToString() == "+  Label selected bits");
            Equal(true, ReferenceEquals(viewModel.Fields.AddLabelCommand, labelButton.Command));
            if (listener.Messages.Count > 0)
                throw new InvalidOperationException("WPF binding errors: " + string.Join("\n", listener.Messages));
        }
        finally
        {
            // Restore the process-wide tracing configuration for subsequent tests.
            source.Listeners.Remove(listener);
            source.Switch.Level = previousLevel;
        }
    }

    /// <summary>
    /// Searches the logical control tree for a named control of type T. Each
    /// UserControl has its own name scope, so Window.FindName alone is insufficient.
    /// </summary>
    private static T FindLogical<T>(DependencyObject element, string name) where T : FrameworkElement
    {
        // Recursion checks this node, then asks each child to search its own children.
        if (element is T match && match.Name == name) return match;
        foreach (var child in LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>())
        {
            try { return FindLogical<T>(child, name); }
            catch (KeyNotFoundException) { }
        }
        throw new KeyNotFoundException($"Missing {typeof(T).Name} named {name} in the compiled logical tree.");
    }

    /// <summary>Visits a control and all of its logical descendants, one at a time.</summary>
    private static IEnumerable<DependencyObject> LogicalDescendants(DependencyObject element)
    {
        // yield return produces an enumerable sequence without first building a list.
        yield return element;
        foreach (var child in LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>())
            foreach (var descendant in LogicalDescendants(child)) yield return descendant;
    }

    /// <summary>Lets queued binding/layout work finish before checking what controls display.</summary>
    private static void FlushBindings()
    {
        var frame = new DispatcherFrame();
        // A dispatcher is WPF's queue of UI work. Queue the stop action at a low
        // priority, allowing already queued binding updates to run first.
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    /// <summary>Stores binding diagnostic messages so the test can report them together.</summary>
    private sealed class BindingErrorListener : TraceListener
    {
        /// <summary>The nonempty diagnostic fragments captured during this test.</summary>
        public List<string> Messages { get; } = [];
        /// <summary>Receives one diagnostic fragment from .NET tracing.</summary>
        public override void Write(string? message) { if (!string.IsNullOrWhiteSpace(message)) Messages.Add(message); }
        /// <summary>Uses the same capture logic for line-based diagnostics.</summary>
        public override void WriteLine(string? message) => Write(message);
    }

    /// <summary>Builds a filename within this run's disposable test-output directory.</summary>
    private static string Artifact(string name) => Path.Combine(ArtifactDirectory, name);

    /// <summary>
    /// Runs an asynchronous scenario while allowing UI continuations to execute.
    /// Simply blocking the STA thread could deadlock an await that must resume there.
    /// </summary>
    private static void TestAsync(string name, Func<Task> action) => Test(name, () =>
    {
        Task task = action();
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            var dispatcher = Dispatcher.CurrentDispatcher;
            // Bound the wait so a bug reports a failure instead of hanging the suite.
            var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            timeout.Tick += (_, _) => frame.Continue = false;
            // Completion may occur on a worker thread. Post the stop action back
            // to this dispatcher rather than doing UI-thread work from that worker.
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
            timeout.Start();
            try { Dispatcher.PushFrame(frame); }
            finally { timeout.Stop(); }
        }
        if (!task.IsCompleted) throw new TimeoutException("The in-process command did not finish in 20 seconds.");
        // The task has finished; this retrieves its result or rethrows its failure.
        task.GetAwaiter().GetResult();
    });

    /// <summary>Records one named scenario, converting any failed assertion into a FAIL line.</summary>
    private static void Test(string name, Action action)
    {
        try { action(); _passed++; Console.WriteLine($"PASS {name}"); }
        catch (Exception exception) { _failed++; Console.Error.WriteLine($"FAIL {name}: {exception}"); }
    }

    /// <summary>Asserts that two individual values agree; the message shows both on failure.</summary>
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected <{expected}>, got <{actual}>.");
    }

    /// <summary>Checks every element in order; equal membership alone is insufficient for mappings.</summary>
    private static void Sequence<T>(IEnumerable<T> expected, IEnumerable<T> actual)
    {
        if (!expected.SequenceEqual(actual))
            throw new InvalidOperationException($"Expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}].");
    }
}
