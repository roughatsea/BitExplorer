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

// Make names from System.Windows available here without writing their full prefix each time. This does not
// run that library's code.
using System.Windows;
// Make names from System.Windows.Automation available here without writing their full prefix each time.
// This does not run that library's code.
using System.Windows.Automation;
// Make names from System.Windows.Controls available here without writing their full prefix each time. This
// does not run that library's code.
using System.Windows.Controls;
// Make names from System.Windows.Input available here without writing their full prefix each time. This
// does not run that library's code.
using System.Windows.Input;
// Make names from System.Windows.Media available here without writing their full prefix each time. This
// does not run that library's code.
using System.Windows.Media;

// Place this file's definitions in the WpfApp1.Dialogs naming group, which prevents clashes with names in
// other groups.
namespace WpfApp1.Dialogs;

/// <summary>
/// A reusable WPF dialog for short text or multiline pasted data. It collects text only; the caller
/// decides whether that text is a valid offset, sequence of hexadecimal bytes, or another expected input.
/// </summary>
/// <remarks>
/// The controls are assembled in C# rather than XAML. ShowDialog, called by the interaction service,
/// pauses input to the owner until this window reports true (accepted) or false (cancelled).
/// </remarks>
public sealed class TextInputWindow : Window
{
    // Reserve _input to hold an object or value of type TextBox; setup can supply its value, otherwise the
    // type's default is used.
    private readonly TextBox _input;

    /// <summary>The current entered text; callers should consume it only after the dialog is accepted.</summary>
    public string Value => _input.Text;

    /// <summary>Builds a single-line prompt or a resizable multiline editor with consistent application styling.</summary>
    public TextInputWindow(Window owner, string title, string label, string initialText = "", bool multiline = false)
    {
        // Set Owner to owner.
        Owner = owner;
        // Set Title to title.
        Title = title;
        // Set Width to 500.
        Width = 500;
        // Set Height to 420 when multiline is true; otherwise 245.
        Height = multiline ? 420 : 245;
        // Set MinWidth to 400.
        MinWidth = 400;
        // Set MinHeight to 300 when multiline is true; otherwise 235.
        MinHeight = multiline ? 300 : 235;
        // Set WindowStartupLocation to WindowStartupLocation.CenterOwner.
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        // Set ShowInTaskbar to false (no).
        ShowInTaskbar = false;
        // Set ResizeMode to ResizeMode.CanResize when multiline is true; otherwise ResizeMode.NoResize.
        ResizeMode = multiline ? ResizeMode.CanResize : ResizeMode.NoResize;
        // Set Background to the result returned by FindResource(...), converted to Brush.
        Background = (Brush)FindResource("CanvasBrush");
        // Set Foreground to the result returned by FindResource(...), converted to Brush.
        Foreground = (Brush)FindResource("TextBrush");
        // Set FontFamily to a new FontFamily object using the inputs in parentheses.
        FontFamily = new FontFamily("Segoe UI");

        // Auto rows use the space needed by the label/buttons; the Star row gets the remaining height.
        // This lets a multiline editor grow while its explanation and action buttons remain visible.
        var layout = new Grid { Margin = new Thickness(24) };
        // Add new RowDefinition { Height = GridLength.Auto } to layout.RowDefinitions.
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        // Add new RowDefinition { Height = new GridLength(1, GridUnitType.Star) } to layout.RowDefinitions.
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        // Add new RowDefinition { Height = GridLength.Auto } to layout.RowDefinitions.
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        // Remember a new TextBlock object; the entries in braces set its initial contents or properties as
        // explanation.
        var explanation = new TextBlock
        {
            // Set this new object's Text entry to label.
            Text = label,
            // Set this new object's TextWrapping entry to TextWrapping.Wrap.
            TextWrapping = TextWrapping.Wrap,
            // Set this new object's Foreground entry to the result returned by FindResource(...), converted
            // to Brush.
            Foreground = (Brush)FindResource("MutedBrush"),
            // Set this new object's Margin entry to a new Thickness object using the inputs in parentheses.
            Margin = new Thickness(0, 0, 0, 12)
        };
        // Add explanation to layout.Children.
        layout.Children.Add(explanation);
        // Multiline mode accepts Enter as text, wraps long lines, and scrolls vertically. Tab remains
        // available for keyboard focus navigation rather than inserting a tab into the input.
        _input = new TextBox
        {
            // Set this new object's Text entry to initialText.
            Text = initialText,
            // Set this new object's AcceptsReturn entry to multiline.
            AcceptsReturn = multiline,
            // Set this new object's AcceptsTab entry to false (no).
            AcceptsTab = false,
            // Set this new object's TextWrapping entry to TextWrapping.Wrap when multiline is true;
            // otherwise TextWrapping.NoWrap.
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            // Set this new object's VerticalScrollBarVisibility entry to ScrollBarVisibility.Auto when
            // multiline is true; otherwise ScrollBarVisibility.Disabled.
            VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
            // Set this new object's VerticalAlignment entry to VerticalAlignment.Stretch when multiline is
            // true; otherwise VerticalAlignment.Top.
            VerticalAlignment = multiline ? VerticalAlignment.Stretch : VerticalAlignment.Top,
            // Set this new object's VerticalContentAlignment entry to VerticalAlignment.Top when multiline
            // is true; otherwise VerticalAlignment.Center.
            VerticalContentAlignment = multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
            // Set this new object's FontFamily entry to a new FontFamily object using the inputs in
            // parentheses.
            FontFamily = new FontFamily("Consolas"),
            // Set this new object's MinHeight entry to 37.
            MinHeight = 37
        };
        // Call Grid.SetRow(...); the values in parentheses are the inputs.
        Grid.SetRow(_input, 1);
        // Expose the prompt as the accessible name for screen readers and UI-testing tools.
        AutomationProperties.SetName(_input, label);
        // Add _input to layout.Children.
        layout.Children.Add(_input);

        // Remember a new StackPanel object; the entries in braces set its initial contents or properties as
        // buttons.
        var buttons = new StackPanel
        {
            // Set this new object's Orientation entry to Orientation.Horizontal.
            Orientation = Orientation.Horizontal,
            // Set this new object's HorizontalAlignment entry to HorizontalAlignment.Right.
            HorizontalAlignment = HorizontalAlignment.Right,
            // Set this new object's Margin entry to a new Thickness object using the inputs in parentheses.
            Margin = new Thickness(0, 18, 0, 0)
        };
        // IsCancel enables Escape. A single-line OK button is the default Enter action, but multiline
        // input needs Enter for line breaks, so its confirmation button is not the default.
        // Setting DialogResult closes the modal window; cancellation does not modify any document.
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 88, Margin = new Thickness(0, 0, 10, 0) };
        // Register the operation after => as a listener for cancel.Click; the listener runs when that event
        // is raised.
        cancel.Click += (_, _) => DialogResult = false;
        // Remember a new Button object; the entries in braces set its initial contents or properties as
        // confirm.
        var confirm = new Button { Content = "OK", IsDefault = !multiline, MinWidth = 88, Style = (Style)FindResource("AccentButton") };
        // Register the operation after => as a listener for confirm.Click; the listener runs when that
        // event is raised.
        confirm.Click += (_, _) => DialogResult = true;
        // Add cancel to buttons.Children.
        buttons.Children.Add(cancel);
        // Add confirm to buttons.Children.
        buttons.Children.Add(confirm);
        // Call Grid.SetRow(...); the values in parentheses are the inputs.
        Grid.SetRow(buttons, 2);
        // Add buttons to layout.Children.
        layout.Children.Add(buttons);
        // Set Content to layout.
        Content = layout;

        // Wait until WPF loads the controls before focusing/selecting the initial text for easy replacement.
        Loaded += (_, _) => { _input.Focus(); _input.SelectAll(); };
        // PreviewKeyDown runs before the focused text box processes the key. Ctrl+Enter accepts multiline
        // input; marking the event handled prevents the same keystroke from also inserting a newline.
        PreviewKeyDown += (_, e) =>
        {
            // If both both multiline is true and e.Key equals Key.Enter and
            // Keyboard.Modifiers.HasFlag(ModifierKeys.Control) is true, run the following grouped
            // instructions.
            if (multiline && e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                // Set DialogResult to true (yes).
                DialogResult = true;
                // Set e.Handled to true (yes).
                e.Handled = true;
            }
        };
    }
}
