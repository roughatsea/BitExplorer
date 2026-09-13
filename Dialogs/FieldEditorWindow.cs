using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using BitExplorer.Core;

namespace WpfApp1.Dialogs;

public sealed class FieldEditorWindow : Window
{
    private readonly NamedField _draft;
    private readonly long _fileBitLength;
    private readonly TextBox _name;
    private readonly TextBox _mapping;
    private readonly TextBox _notes;
    private readonly ComboBox _color;
    private readonly TextBlock _summary;
    private readonly TextBlock _error;

    public NamedField Result { get; private set; }

    public FieldEditorWindow(Window owner, NamedField draft, long fileBitLength)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentOutOfRangeException.ThrowIfNegative(fileBitLength);
        Owner = owner;
        _draft = draft.Clone();
        Result = draft.Clone();
        _fileBitLength = fileBitLength;
        Title = "Define a bit field";
        Width = 570;
        Height = 640;
        MinWidth = 480;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Background = (Brush)FindResource("CanvasBrush");
        Foreground = (Brush)FindResource("TextBrush");
        FontFamily = new FontFamily("Segoe UI");

        var layout = new Grid { Margin = new Thickness(24, 20, 24, 20) };
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var form = new StackPanel();
        var scroll = new ScrollViewer
        {
            Content = form,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        layout.Children.Add(scroll);
        form.Children.Add(new TextBlock { Text = "Give these bits meaning", FontSize = 22, FontWeight = FontWeights.SemiBold });
        form.Children.Add(Muted("Name a flag, packed value, or a field assembled from separate ranges.", new Thickness(0, 6, 0, 18)));

        var identity = new Grid();
        identity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        identity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(175) });
        var namePanel = new StackPanel { Margin = new Thickness(0, 0, 14, 0) };
        namePanel.Children.Add(Label("FIELD NAME"));
        _name = new TextBox { Text = _draft.Name, MaxLength = 128 };
        AutomationProperties.SetName(_name, "Field name");
        namePanel.Children.Add(_name);
        identity.Children.Add(namePanel);
        var colorPanel = new StackPanel();
        colorPanel.Children.Add(Label("COLOR"));
        _color = new ComboBox();
        AutomationProperties.SetName(_color, "Field color");
        var colors = new List<ColorChoice>
        {
            new("Mint", "#54BFA6"), new("Blue", "#6EABFF"),
            new("Amber", "#D9AD68"), new("Lilac", "#CB98EE"),
            new("Coral", "#EE8C98"), new("Sky", "#66C9DD")
        };
        var selectedColor = colors.FirstOrDefault(c => c.Hex.Equals(_draft.Color, StringComparison.OrdinalIgnoreCase));
        if (selectedColor == null && IsColor(_draft.Color))
        {
            selectedColor = new ColorChoice("Custom", _draft.Color.ToUpperInvariant());
            colors.Add(selectedColor);
        }
        foreach (var choice in colors)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(new Border
            {
                Background = (Brush)new BrushConverter().ConvertFromString(choice.Hex)!,
                Width = 12, Height = 12, CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 0, 9, 0)
            });
            content.Children.Add(new TextBlock { Text = choice.Name, VerticalAlignment = VerticalAlignment.Center });
            var item = new ComboBoxItem { Content = content, Tag = choice.Hex };
            _color.Items.Add(item);
            if (choice == selectedColor) _color.SelectedItem = item;
        }
        if (_color.SelectedIndex < 0) _color.SelectedIndex = 0;
        colorPanel.Children.Add(_color);
        Grid.SetColumn(colorPanel, 1);
        identity.Children.Add(colorPanel);
        form.Children.Add(identity);

        var mappingLabel = Label("SOURCE BITS · VALUE MSB TO LSB");
        mappingLabel.Margin = new Thickness(0, 20, 0, 6);
        form.Children.Add(mappingLabel);
        _mapping = new TextBox
        {
            Text = FormatMapping(_draft.OrderedBits),
            FontFamily = new FontFamily("Consolas"),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalContentAlignment = VerticalAlignment.Top,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Height = 75,
            MaxLength = 65536
        };
        form.Children.Add(_mapping);
        AutomationProperties.SetName(_mapping, "Source bit mapping");
        form.Children.Add(Muted("Use decimal positions and inclusive ranges: 5-10, 24-27, 20. Descending ranges are allowed. Physical bit 0 is the MS bit of byte 0; these addresses stay the same when ruler numbering changes.", new Thickness(0, 7, 0, 10)));
        var transforms = new WrapPanel();
        AddTransform(transforms, "File order", "Arrange source bits from the start of the file to the end.", bits => bits.Order().ToList());
        AddTransform(transforms, "Reverse bytes", "Reverse byte groups while retaining MS-to-LS physical bit order within each byte.", bits => bits.GroupBy(bit => bit / 8).Reverse().SelectMany(group => group.Order()).ToList());
        AddTransform(transforms, "Reverse bits", "Reverse the complete source bit sequence.", bits => bits.AsEnumerable().Reverse().ToList());
        form.Children.Add(transforms);
        _summary = Muted("", new Thickness(0, 10, 0, 18));
        _summary.FontFamily = new FontFamily("Consolas");
        form.Children.Add(_summary);

        form.Children.Add(Label("NOTES · OPTIONAL"));
        _notes = new TextBox
        {
            Text = _draft.Notes, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            VerticalContentAlignment = VerticalAlignment.Top, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinHeight = 65, MaxHeight = 125, MaxLength = 65536
        };
        form.Children.Add(_notes);
        AutomationProperties.SetName(_notes, "Field notes");
        _error = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(255, 160, 164)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 10),
            MinHeight = 20
        };
        Grid.SetRow(_error, 1);
        layout.Children.Add(_error);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 88, Margin = new Thickness(0, 0, 10, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var done = new Button { Content = "Done", IsDefault = true, MinWidth = 100, Style = (Style)FindResource("AccentButton") };
        done.Click += (_, _) => SaveDraft();
        actions.Children.Add(cancel);
        actions.Children.Add(done);
        Grid.SetRow(actions, 2);
        layout.Children.Add(actions);
        Content = layout;

        _mapping.TextChanged += (_, _) => UpdateSummary();
        Loaded += (_, _) => { _name.Focus(); _name.SelectAll(); UpdateSummary(); };
    }

    private TextBlock Label(string text) => new()
    {
        Text = text,
        Foreground = (Brush)FindResource("MutedBrush"),
        FontSize = 10,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 0, 0, 6)
    };

    private TextBlock Muted(string text, Thickness margin) => new()
    {
        Text = text, Margin = margin, Foreground = (Brush)FindResource("MutedBrush"),
        TextWrapping = TextWrapping.Wrap, FontSize = 12
    };

    private void AddTransform(Panel panel, string text, string tooltip, Func<List<long>, List<long>> transform)
    {
        var button = new Button { Content = text, ToolTip = tooltip, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(10, 6, 10, 6) };
        button.Click += (_, _) =>
        {
            if (!TryReadMapping(out var bits, out var error)) { _error.Text = error; return; }
            _mapping.Text = FormatMapping(transform(bits));
            UpdateSummary();
        };
        panel.Children.Add(button);
    }

    private void UpdateSummary()
    {
        if (!TryReadMapping(out var bits, out var error))
        {
            _summary.Text = "Enter a valid source mapping to preview significance.";
            _error.Text = error;
            return;
        }
        long most = bits[0], least = bits[^1];
        _summary.Text = $"{bits.Count:N0} bits · {bits.Select(bit => bit / 8).Distinct().Count():N0} source bytes\n" +
            $"Value MSB ← physical {most:N0} (byte {most / 8:N0})\n" +
            $"Value LSB ← physical {least:N0} (byte {least / 8:N0})";
        _error.Text = "";
    }

    private bool TryReadMapping(out List<long> bits, out string error)
    {
        bits = [];
        error = "";
        if (_fileBitLength == 0) { error = "Open a nonempty file before defining a field."; return false; }
        if (string.IsNullOrWhiteSpace(_mapping.Text)) { error = "Select at least one source bit."; return false; }
        var seen = new HashSet<long>();
        var tokens = _mapping.Text.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) { error = "Select at least one source bit."; return false; }
        foreach (string token in tokens)
        {
            var match = Regex.Match(token, @"^\s*(\d+)\s*(?:-\s*(\d+))?\s*$", RegexOptions.CultureInvariant);
            if (!match.Success || !long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out long first))
            { error = $"Invalid range: {Abbreviate(token)}. Use decimal positions such as 5-10, 20."; return false; }
            long last = first;
            if (match.Groups[2].Success && !long.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out last))
            { error = "A bit position is too large."; return false; }
            if (first >= _fileBitLength || last >= _fileBitLength)
            { error = $"Source positions must be between 0 and {_fileBitLength - 1:N0}."; return false; }
            long distance = Math.Abs(last - first);
            if (distance >= DocumentModel.MaxFieldBits - bits.Count)
            { error = $"A field may contain at most {DocumentModel.MaxFieldBits:N0} bits."; return false; }
            long direction = last >= first ? 1 : -1;
            for (long position = first; ; position += direction)
            {
                if (!seen.Add(position)) { error = $"Physical bit {position:N0} appears more than once."; return false; }
                bits.Add(position);
                if (position == last) break;
            }
        }
        return true;
    }

    private void SaveDraft()
    {
        string name = _name.Text.Trim();
        if (name.Length is < 1 or > 128 || name.Any(char.IsControl))
        { _error.Text = "Give the field a name of 1–128 characters, without control characters."; _name.Focus(); return; }
        if (!TryReadMapping(out var bits, out var error)) { _error.Text = error; _mapping.Focus(); return; }
        string? color = (_color.SelectedItem as ComboBoxItem)?.Tag as string;
        if (!IsColor(color)) { _error.Text = "Choose a valid field color."; return; }
        Result = _draft.Clone();
        Result.Name = name;
        Result.Color = color!;
        Result.Notes = _notes.Text;
        Result.OrderedBits = bits;
        DialogResult = true;
    }

    private static bool IsColor(string? color) => color is { Length: 7 } && color[0] == '#' && color.Skip(1).All(char.IsAsciiHexDigit);

    private static string FormatMapping(IEnumerable<long> source)
    {
        var bits = source.ToArray();
        var ranges = new List<string>();
        for (int start = 0; start < bits.Length;)
        {
            int end = start;
            long step = start + 1 < bits.Length ? bits[start + 1] - bits[start] : 0;
            if (step is 1 or -1)
                while (end + 1 < bits.Length && bits[end + 1] - bits[end] == step) end++;
            ranges.Add(end == start ? bits[start].ToString(CultureInfo.InvariantCulture)
                : $"{bits[start].ToString(CultureInfo.InvariantCulture)}-{bits[end].ToString(CultureInfo.InvariantCulture)}");
            start = end + 1;
        }
        return string.Join(", ", ranges);
    }

    private static string Abbreviate(string text) => text.Length > 32 ? text[..29] + "..." : text;
    private sealed record ColorChoice(string Name, string Hex);
}
