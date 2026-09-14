// READING THIS FILE
// A program is a set of instructions. This file defines some of those instructions;
// defining a method does not run it. A call such as Refresh() asks it to run.
// Comments explain the next instruction or the whole block introduced below them.
// Within a running block, instructions normally run from top to bottom. Braces { }
// group a body; a closing brace ends that group. Blank lines only separate ideas.
// A semicolon ends an instruction. A long instruction can continue on several lines;
// its commas, closing parentheses and braces belong to the explanation at its start.
// Names identify values or operations: x = y stores y in x; x == y compares them.
// A dot selects something belonging to an object, and (...) supplies inputs to a call.
// See docs/ReadingTheCode.md for types, symbols, examples, and the application map.

// Make names from System.Diagnostics available here without writing their full prefix each time. This does
// not run that library's code.
using System.Diagnostics;
// Make names from System.IO available here without writing their full prefix each time. This does not run
// that library's code.
using System.IO;
// Make names from System.Windows available here without writing their full prefix each time. This does not
// run that library's code.
using System.Windows;
// Make names from System.Windows.Controls available here without writing their full prefix each time. This
// does not run that library's code.
using System.Windows.Controls;
// Make names from System.Windows.Data available here without writing their full prefix each time. This does
// not run that library's code.
using System.Windows.Data;
// Make names from System.Windows.Input available here without writing their full prefix each time. This
// does not run that library's code.
using System.Windows.Input;
// Make names from System.Windows.Media available here without writing their full prefix each time. This
// does not run that library's code.
using System.Windows.Media;
// Make names from System.Windows.Threading available here without writing their full prefix each time. This
// does not run that library's code.
using System.Windows.Threading;
// Make names from WpfApp1 available here without writing their full prefix each time. This does not run
// that library's code.
using WpfApp1;
// Make names from WpfApp1.Controls available here without writing their full prefix each time. This does
// not run that library's code.
using WpfApp1.Controls;
// Make names from WpfApp1.ViewModels available here without writing their full prefix each time. This does
// not run that library's code.
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
    // Reserve _failed to hold a whole number; setup can supply its value, otherwise the type's default is
    // used.
    private static int _failed;
    // Each run gets its own random directory beneath the executable's output.
    // Tests can save/open real files without touching the user's documents.
    private static readonly string ArtifactRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "test-artifacts"));
    // Remember the result returned by Path.Combine(...) as ArtifactDirectory.
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
        // Build the controls described in the matching XAML file and connect their names and event handlers
        // to this C# object.
        app.InitializeComponent();
        // Call Directory.CreateDirectory(...); the values in parentheses are the inputs.
        Directory.CreateDirectory(ArtifactDirectory);
        // Attempt this work. A matching catch below handles a reported exception; a finally block, when
        // present, performs cleanup on the way out.
        try
        {
            // Call RunPanelTests: Registers the panel behavior checks with the shared pass/fail test
            // runner.
            RunPanelTests();
            // Run GridHitTesting as a test; the quoted text names the behavior reported in the results.
            Test("grid hit testing shares formatter columns in byte, bit, and ASCII views", GridHitTesting);
            // Run GridFittingAndReflow as a test; the quoted text names the behavior reported in the
            // results.
            Test("grid fitting preserves explicit grouping and reflow preserves the top byte", GridFittingAndReflow);
            // Call TestAsync: Runs an asynchronous scenario while allowing UI continuations to execute.
            TestAsync("asynchronous file opens restore busy state on success, cancellation, and failure", OpenCommands);
            // Call TestAsync: Runs an asynchronous scenario while allowing UI continuations to execute.
            TestAsync("pending operations disable edits and reject a second open until errors are handled", BusyCommandGuards);
            // Call TestAsync: Runs an asynchronous scenario while allowing UI continuations to execute.
            TestAsync("disposing a pending open ignores its later result and errors", DisposedOpenCompletion);
            // Call TestAsync: Runs an asynchronous scenario while allowing UI continuations to execute.
            TestAsync("formatted export uses the current model and reports failures without staying busy", ExportCommands);
            // Run SaveDecisions as a test; the quoted text names the behavior reported in the results.
            Test("save decisions and canceled dialogs preserve unsaved work", SaveDecisions);
            // Run CompiledBindings as a test; the quoted text names the behavior reported in the results.
            Test("compiled resources and main-window bindings attach the shared view model", CompiledBindings);
        }
        // Run this cleanup when leaving the try, whether the work succeeded or reported an exception.
        finally
        {
            // Even during failure, remove only this run's generated directory.
            // Resolving and checking the path before recursive deletion prevents
            // cleanup from accidentally targeting a directory outside our test area.
            string target = Path.GetFullPath(ArtifactDirectory);
            // If both target.StartsWith(ArtifactRoot + Path.DirectorySeparatorChar,
            // StringComparison.OrdinalIgnoreCase) is true and Directory.Exists(target) is true, call
            // Directory.Delete(...); the values in parentheses are the inputs.
            if (target.StartsWith(ArtifactRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                // Also require that the verified test folder still exists, then
                // remove that folder and its generated contents together.
                && Directory.Exists(target)) Directory.Delete(target, recursive: true);
        }
        // Write the supplied text followed by a line break to Console.
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
        // Remember PresentationTraceSources.DataBindingSource as source.
        var source = PresentationTraceSources.DataBindingSource;
        // Remember source.Switch.Level as previousLevel.
        var previousLevel = source.Switch.Level;
        // Set source.Switch.Level to SourceLevels.Error.
        source.Switch.Level = SourceLevels.Error;
        // Add listener to source.Listeners.
        source.Listeners.Add(listener);
        // Attempt this work. A matching catch below handles a reported exception; a finally block, when
        // present, performs cleanup on the way out.
        try
        {
            // Remember a new FakeUserInteractionService object as dialogs.
            var dialogs = new FakeUserInteractionService();
            // Remember a new MainWindowViewModel object using the inputs in parentheses as
            // suppliedViewModel. The using declaration releases it automatically when this scope ends, even
            // after an error.
            using var suppliedViewModel = new MainWindowViewModel(dialogs);
            // Remember a new MainWindow object using the inputs in parentheses as window.
            var window = new MainWindow(suppliedViewModel);
            // Check that window.IsVisible equals the expected false; a mismatch fails this test.
            Equal(false, window.IsVisible);
            // Check that window.IsLoaded equals the expected false; a mismatch fails this test.
            Equal(false, window.IsLoaded);
            // WPF's two layout phases ask how much space controls need (Measure)
            // and give them actual rectangles (Arrange), even without showing a window.
            window.Measure(new Size(1280, 900));
            // Call window.Arrange(...); the values in parentheses are the inputs.
            window.Arrange(new Rect(0, 0, 1280, 900));
            // Call window.UpdateLayout().
            window.UpdateLayout();
            // Call FlushBindings: Lets queued binding/layout work finish before checking what controls
            // display.
            FlushBindings();
            // Remember window.DataContext as MainWindowViewModel, falling back to throw new
            // InvalidOperationException("MainWindow must bind to MainWindowViewModel.") if it is null as
            // viewModel.
            var viewModel = window.DataContext as MainWindowViewModel
                ?? throw new InvalidOperationException("MainWindow must bind to MainWindowViewModel.");
            // Remember the result returned by window.FindResource(...), converted to SolidColorBrush as
            // background.
            var background = (SolidColorBrush)window.FindResource("CanvasBrush");
            // Check that background.Color equals the expected Color.FromRgb(0x10, 0x15, 0x1E); a mismatch
            // fails this test.
            Equal(Color.FromRgb(0x10, 0x15, 0x1E), background.Color);
            // Check that window.FindResource("AccentButton") is Style equals the expected true; a mismatch
            // fails this test.
            Equal(true, window.FindResource("AccentButton") is Style);
            // ReferenceEquals checks shared ownership: equal copies would not prove
            // that the grid and inspector observe the same selection object.
            var grid = FindLogical<ByteGrid>(window, "DataGrid");
            // Check that ReferenceEquals(viewModel.Session.Document, grid.Document) equals the expected
            // true; a mismatch fails this test.
            Equal(true, ReferenceEquals(viewModel.Session.Document, grid.Document));
            // Check that ReferenceEquals(viewModel.Session.Selection, grid.Selection) equals the expected
            // true; a mismatch fails this test.
            Equal(true, ReferenceEquals(viewModel.Session.Selection, grid.Selection));
            // Remember the result returned by FindLogical<TextBlock>(...) as hex.
            var hex = FindLogical<TextBlock>(window, "ByteHex");
            // Check that hex.Text equals the expected viewModel.Inspector.ByteHex; a mismatch fails this
            // test.
            Equal(viewModel.Inspector.ByteHex, hex.Text);
            // Request selection of the supplied physical bit addresses; the selection code checks its
            // bounds and limit.
            viewModel.Session.SelectRawBits(Enumerable.Range(8, 8).Select(bit => (long)bit));
            // Call FlushBindings: Lets queued binding/layout work finish before checking what controls
            // display.
            FlushBindings();
            // Check that hex.Text equals the expected "6C"; a mismatch fails this test.
            Equal("6C", hex.Text);

            // Simulate an edit through the real binding and Enter command. Explicit
            // UpdateSource sends the TextBox's value back to the bound draft property.
            var input = FindLogical<TextBox>(window, "ByteInput");
            // Set input.Text to the text "A5".
            input.Text = "A5";
            // Call BindingOperations.GetBindingExpression(input, TextBox.TextProperty)!.UpdateSource().
            BindingOperations.GetBindingExpression(input, TextBox.TextProperty)!.UpdateSource();
            // Check that viewModel.Inspector.ByteInput equals the expected "A5"; a mismatch fails this
            // test.
            Equal("A5", viewModel.Inspector.ByteInput);
            // Remember the only matching item; finding none or more than one reports an error as enter.
            var enter = input.InputBindings.OfType<KeyBinding>().Single(binding => binding.Key == Key.Enter);
            // Check that ReferenceEquals(viewModel.Inspector.ApplyByteCommand, enter.Command) equals the
            // expected true; a mismatch fails this test.
            Equal(true, ReferenceEquals(viewModel.Inspector.ApplyByteCommand, enter.Command));
            // Invoke enter.Command.Execute to perform the action stored in that command; what changes
            // depends on which command it is.
            enter.Command.Execute(enter.CommandParameter);
            // Call FlushBindings: Lets queued binding/layout work finish before checking what controls
            // display.
            FlushBindings();
            // Check that viewModel.Session.Document.Data[1] equals the expected (byte)0xA5; a mismatch
            // fails this test.
            Equal((byte)0xA5, viewModel.Session.Document.Data[1]);
            // Check that hex.Text equals the expected "A5"; a mismatch fails this test.
            Equal("A5", hex.Text);

            // Select a real list item and verify its mapped bits reach the grid.
            var fields = FindLogical<ListBox>(window, "FieldList");
            // Check that ReferenceEquals(viewModel.Fields.Items, fields.ItemsSource) equals the expected
            // true; a mismatch fails this test.
            Equal(true, ReferenceEquals(viewModel.Fields.Items, fields.ItemsSource));
            // Check that fields.Items.Count equals the expected 4; a mismatch fails this test.
            Equal(4, fields.Items.Count);
            // Remember the only matching item; finding none or more than one reports an error as selected.
            var selected = viewModel.Fields.Items.Single(field => field.Name == "Message type");
            // Set fields.SelectedItem to selected.
            fields.SelectedItem = selected;
            // Call BindingOperations.GetBindingExpression(fields,
            // ListBox.SelectedItemProperty)!.UpdateSource().
            BindingOperations.GetBindingExpression(fields, ListBox.SelectedItemProperty)!.UpdateSource();
            // Call FlushBindings: Lets queued binding/layout work finish before checking what controls
            // display.
            FlushBindings();
            // Check that viewModel.Session.SelectedField!.Id equals the expected selected.Id; a mismatch
            // fails this test.
            Equal(selected.Id, viewModel.Session.SelectedField!.Id);
            // Check that grid.SelectedBits has the same items in the same order as
            // viewModel.Session.SelectedField.OrderedBits.Order().
            Sequence(viewModel.Session.SelectedField.OrderedBits.Order(), grid.SelectedBits);
            // A context menu is a separate popup tree. Supplying PlacementTarget
            // checks its binding without opening a desktop menu or a native dialog.
            var contextMenu = fields.ContextMenu!;
            // Set contextMenu.PlacementTarget to fields.
            contextMenu.PlacementTarget = fields;
            // Call FlushBindings: Lets queued binding/layout work finish before checking what controls
            // display.
            FlushBindings();
            // Remember the result returned by contextMenu.Items.OfType<MenuItem>().First() as editItem.
            var editItem = contextMenu.Items.OfType<MenuItem>().First();
            // Check that ReferenceEquals(viewModel.Fields.EditLabelCommand, editItem.Command) equals the
            // expected true; a mismatch fails this test.
            Equal(true, ReferenceEquals(viewModel.Fields.EditLabelCommand, editItem.Command));
            // Invoke editItem.Command.Execute to perform the action stored in that command; what changes
            // depends on which command it is.
            editItem.Command.Execute(null); // Fake dialog cancels, with the selected field as its target.
            // Check that dialogs.FieldDrafts.Single().Id equals the expected selected.Id; a mismatch fails
            // this test.
            Equal(selected.Id, dialogs.FieldDrafts.Single().Id);
            // Check that viewModel.Session.SelectedField.Name equals the expected "Message type"; a
            // mismatch fails this test.
            Equal("Message type", viewModel.Session.SelectedField.Name);
            // The inspector's label button reaches across to the shell's Fields
            // view model with an ancestor binding; ensure it resolves to that command.
            var labelButton = LogicalDescendants(window).OfType<Button>()
                .Single(button => button.Content?.ToString() == "+  Label selected bits");
            // Check that ReferenceEquals(viewModel.Fields.AddLabelCommand, labelButton.Command) equals the
            // expected true; a mismatch fails this test.
            Equal(true, ReferenceEquals(viewModel.Fields.AddLabelCommand, labelButton.Command));
            // If listener.Messages.Count is greater than 0, stop this normal path by reporting
            // InvalidOperationException; the arguments carry the error details.
            if (listener.Messages.Count > 0)
                // Stop this normal path by reporting InvalidOperationException; the arguments carry the
                // error details.
                throw new InvalidOperationException("WPF binding errors: " + string.Join("\n", listener.Messages));
        }
        // Run this cleanup when leaving the try, whether the work succeeded or reported an exception.
        finally
        {
            // Restore the process-wide tracing configuration for subsequent tests.
            source.Listeners.Remove(listener);
            // Set source.Switch.Level to previousLevel.
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
        // Take each item from LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>() in turn,
        // call the current item child, and run the following grouped instructions.
        foreach (var child in LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>())
        {
            // Attempt this work. A matching catch below handles a reported exception; a finally block, when
            // present, performs cleanup on the way out.
            try { return FindLogical<T>(child, name); }
            // If the preceding try reports KeyNotFoundException, refer to it as ; run this recovery path.
            catch (KeyNotFoundException) { }
        }
        // Stop this normal path by reporting KeyNotFoundException; the arguments carry the error details.
        throw new KeyNotFoundException($"Missing {typeof(T).Name} named {name} in the compiled logical tree.");
    }

    /// <summary>Visits a control and all of its logical descendants, one at a time.</summary>
    private static IEnumerable<DependencyObject> LogicalDescendants(DependencyObject element)
    {
        // yield return produces an enumerable sequence without first building a list.
        yield return element;
        // Take each item from LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>() in turn,
        // call the current item child, and run the following instruction.
        foreach (var child in LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>())
            // Take each item from LogicalDescendants(child) in turn, call the current item descendant, and
            // run the following instruction.
            foreach (var descendant in LogicalDescendants(child)) yield return descendant;
    }

    /// <summary>Lets queued binding/layout work finish before checking what controls display.</summary>
    private static void FlushBindings()
    {
        // Remember a new DispatcherFrame object as frame.
        var frame = new DispatcherFrame();
        // A dispatcher is WPF's queue of UI work. Queue the stop action at a low
        // priority, allowing already queued binding updates to run first.
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false);
        // Call Dispatcher.PushFrame(...); the values in parentheses are the inputs.
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
        // Remember the result returned by action() as task.
        Task task = action();
        // If it is not the case that task.IsCompleted is true, run the following grouped instructions.
        if (!task.IsCompleted)
        {
            // Remember a new DispatcherFrame object as frame.
            var frame = new DispatcherFrame();
            // Remember Dispatcher.CurrentDispatcher as dispatcher.
            var dispatcher = Dispatcher.CurrentDispatcher;
            // Bound the wait so a bug reports a failure instead of hanging the suite.
            var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            // Add a small operation described after =>; it is saved for the receiving code to call to
            // timeout.Tick and keep the result there (+=).
            timeout.Tick += (_, _) => frame.Continue = false;
            // Completion may occur on a worker thread. Post the stop action back
            // to this dispatcher rather than doing UI-thread work from that worker.
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
            // Call timeout.Start().
            timeout.Start();
            // Attempt this work. A matching catch below handles a reported exception; a finally block, when
            // present, performs cleanup on the way out.
            try { Dispatcher.PushFrame(frame); }
            // Run this cleanup when leaving the try, whether the work succeeded or reported an exception.
            finally { timeout.Stop(); }
        }
        // If it is not the case that task.IsCompleted is true, stop this normal path by reporting
        // TimeoutException; the arguments carry the error details.
        if (!task.IsCompleted) throw new TimeoutException("The in-process command did not finish in 20 seconds.");
        // The task has finished; this retrieves its result or rethrows its failure.
        task.GetAwaiter().GetResult();
    });

    /// <summary>Records one named scenario, converting any failed assertion into a FAIL line.</summary>
    private static void Test(string name, Action action)
    {
        // Attempt this work. A matching catch below handles a reported exception; a finally block, when
        // present, performs cleanup on the way out.
        try { action(); _passed++; Console.WriteLine($"PASS {name}"); }
        // If the preceding try reports Exception, refer to it as exception; run this recovery path.
        catch (Exception exception) { _failed++; Console.Error.WriteLine($"FAIL {name}: {exception}"); }
    }

    /// <summary>Asserts that two individual values agree; the message shows both on failure.</summary>
    private static void Equal<T>(T expected, T actual)
    {
        // If it is not the case that EqualityComparer<T>.Default.Equals(expected, actual) is true, stop
        // this normal path by reporting InvalidOperationException; the arguments carry the error details.
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            // Stop this normal path by reporting InvalidOperationException; the arguments carry the error
            // details.
            throw new InvalidOperationException($"Expected <{expected}>, got <{actual}>.");
    }

    /// <summary>Checks every element in order; equal membership alone is insufficient for mappings.</summary>
    private static void Sequence<T>(IEnumerable<T> expected, IEnumerable<T> actual)
    {
        // If it is not the case that expected.SequenceEqual(actual) is true, stop this normal path by
        // reporting InvalidOperationException; the arguments carry the error details.
        if (!expected.SequenceEqual(actual))
            // Stop this normal path by reporting InvalidOperationException; the arguments carry the error
            // details.
            throw new InvalidOperationException($"Expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}].");
    }
}
