using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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
    private readonly TextBox _input;

    /// <summary>The current entered text; callers should consume it only after the dialog is accepted.</summary>
    public string Value => _input.Text;

    /// <summary>Builds a single-line prompt or a resizable multiline editor with consistent application styling.</summary>
    public TextInputWindow(Window owner, string title, string label, string initialText = "", bool multiline = false)
    {
        Owner = owner;
        Title = title;
        Width = 500;
        Height = multiline ? 420 : 245;
        MinWidth = 400;
        MinHeight = multiline ? 300 : 235;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        ResizeMode = multiline ? ResizeMode.CanResize : ResizeMode.NoResize;
        Background = (Brush)FindResource("CanvasBrush");
        Foreground = (Brush)FindResource("TextBrush");
        FontFamily = new FontFamily("Segoe UI");

        // Auto rows use the space needed by the label/buttons; the Star row gets the remaining height.
        // This lets a multiline editor grow while its explanation and action buttons remain visible.
        var layout = new Grid { Margin = new Thickness(24) };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var explanation = new TextBlock
        {
            Text = label,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("MutedBrush"),
            Margin = new Thickness(0, 0, 0, 12)
        };
        layout.Children.Add(explanation);
        // Multiline mode accepts Enter as text, wraps long lines, and scrolls vertically. Tab remains
        // available for keyboard focus navigation rather than inserting a tab into the input.
        _input = new TextBox
        {
            Text = initialText,
            AcceptsReturn = multiline,
            AcceptsTab = false,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
            VerticalAlignment = multiline ? VerticalAlignment.Stretch : VerticalAlignment.Top,
            VerticalContentAlignment = multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
            FontFamily = new FontFamily("Consolas"),
            MinHeight = 37
        };
        Grid.SetRow(_input, 1);
        // Expose the prompt as the accessible name for screen readers and UI-testing tools.
        AutomationProperties.SetName(_input, label);
        layout.Children.Add(_input);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };
        // IsCancel enables Escape. A single-line OK button is the default Enter action, but multiline
        // input needs Enter for line breaks, so its confirmation button is not the default.
        // Setting DialogResult closes the modal window; cancellation does not modify any document.
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 88, Margin = new Thickness(0, 0, 10, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var confirm = new Button { Content = "OK", IsDefault = !multiline, MinWidth = 88, Style = (Style)FindResource("AccentButton") };
        confirm.Click += (_, _) => DialogResult = true;
        buttons.Children.Add(cancel);
        buttons.Children.Add(confirm);
        Grid.SetRow(buttons, 2);
        layout.Children.Add(buttons);
        Content = layout;

        // Wait until WPF loads the controls before focusing/selecting the initial text for easy replacement.
        Loaded += (_, _) => { _input.Focus(); _input.SelectAll(); };
        // PreviewKeyDown runs before the focused text box processes the key. Ctrl+Enter accepts multiline
        // input; marking the event handled prevents the same keystroke from also inserting a newline.
        PreviewKeyDown += (_, e) =>
        {
            if (multiline && e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                DialogResult = true;
                e.Handled = true;
            }
        };
    }
}
