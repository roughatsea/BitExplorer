using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfApp1.Dialogs;

public sealed class TextInputWindow : Window
{
    private readonly TextBox _input;

    public string Value => _input.Text;

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
        AutomationProperties.SetName(_input, label);
        layout.Children.Add(_input);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 88, Margin = new Thickness(0, 0, 10, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var confirm = new Button { Content = "OK", IsDefault = !multiline, MinWidth = 88, Style = (Style)FindResource("AccentButton") };
        confirm.Click += (_, _) => DialogResult = true;
        buttons.Children.Add(cancel);
        buttons.Children.Add(confirm);
        Grid.SetRow(buttons, 2);
        layout.Children.Add(buttons);
        Content = layout;

        Loaded += (_, _) => { _input.Focus(); _input.SelectAll(); };
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
