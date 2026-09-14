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

// Make names from System.Globalization available here without writing their full prefix each time. This
// does not run that library's code.
using System.Globalization;
// Make names from System.Text.RegularExpressions available here without writing their full prefix each
// time. This does not run that library's code.
using System.Text.RegularExpressions;
// Make names from System.Windows available here without writing their full prefix each time. This does not
// run that library's code.
using System.Windows;
// Make names from System.Windows.Automation available here without writing their full prefix each time.
// This does not run that library's code.
using System.Windows.Automation;
// Make names from System.Windows.Controls available here without writing their full prefix each time. This
// does not run that library's code.
using System.Windows.Controls;
// Make names from System.Windows.Media available here without writing their full prefix each time. This
// does not run that library's code.
using System.Windows.Media;
// Make names from BitExplorer.Core available here without writing their full prefix each time. This does
// not run that library's code.
using BitExplorer.Core;

// Place this file's definitions in the WpfApp1.Dialogs naming group, which prevents clashes with names in
// other groups.
namespace WpfApp1.Dialogs;

/// <summary>
/// A WPF dialog for naming a bit field and defining which physical bits build its value.
/// The UI is assembled in C# here rather than in a separate XAML markup file.
/// </summary>
/// <remarks>
/// The dialog edits copies, never the live field. Done produces a validated Result; Cancel leaves
/// the original unchanged. The caller decides whether to add or update that result in the document,
/// so document changes still go through the application's normal undo/redo mechanism.
/// </remarks>
public sealed class FieldEditorWindow : Window
{
    // The draft preserves the field's identity while the controls hold the user's unfinished edits.
    // File length bounds the mapping: a 2-byte file has 16 addressable physical bits, numbered 0..15.
    private readonly NamedField _draft;
    // Reserve _fileBitLength to hold a whole number with room for larger values; setup can supply its
    // value, otherwise the type's default is used.
    private readonly long _fileBitLength;
    // Reserve _name to hold an object or value of type TextBox; setup can supply its value, otherwise the
    // type's default is used.
    private readonly TextBox _name;
    // Reserve _mapping to hold an object or value of type TextBox; setup can supply its value, otherwise
    // the type's default is used.
    private readonly TextBox _mapping;
    // Reserve _notes to hold an object or value of type TextBox; setup can supply its value, otherwise the
    // type's default is used.
    private readonly TextBox _notes;
    // Reserve _color to hold an object or value of type ComboBox; setup can supply its value, otherwise the
    // type's default is used.
    private readonly ComboBox _color;
    // Reserve _summary to hold an object or value of type TextBlock; setup can supply its value, otherwise
    // the type's default is used.
    private readonly TextBlock _summary;
    // Reserve _error to hold an object or value of type TextBlock; setup can supply its value, otherwise
    // the type's default is used.
    private readonly TextBlock _error;

    /// <summary>The validated edited copy. The caller should use it only when ShowDialog returns true.</summary>
    public NamedField Result { get; private set; }

    /// <summary>
    /// Creates the form around an independent copy of the supplied field. The owner keeps this dialog
    /// associated with its main window; ShowDialog, used by the caller, makes it a modal interaction.
    /// </summary>
    public FieldEditorWindow(Window owner, NamedField draft, long fileBitLength)
    {
        // Reject draft immediately if it is null (no object was supplied).
        ArgumentNullException.ThrowIfNull(draft);
        // Reject fileBitLength if it is below zero.
        ArgumentOutOfRangeException.ThrowIfNegative(fileBitLength);
        // Set Owner to owner.
        Owner = owner;
        // Separate copies ensure that typing, transforming a mapping, or cancelling cannot mutate
        // the existing field or a previously returned result.
        _draft = draft.Clone();
        // Set Result to a copy returned by draft.Clone().
        Result = draft.Clone();
        // Set _fileBitLength to fileBitLength.
        _fileBitLength = fileBitLength;
        // Set Title to the text "Define a bit field".
        Title = "Define a bit field";
        // Set Width to 570.
        Width = 570;
        // Set Height to 640.
        Height = 640;
        // Set MinWidth to 480.
        MinWidth = 480;
        // Set MinHeight to 560.
        MinHeight = 560;
        // Set WindowStartupLocation to WindowStartupLocation.CenterOwner.
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        // Set ShowInTaskbar to false (no).
        ShowInTaskbar = false;
        // Set Background to the result returned by FindResource(...), converted to Brush.
        Background = (Brush)FindResource("CanvasBrush");
        // Set Foreground to the result returned by FindResource(...), converted to Brush.
        Foreground = (Brush)FindResource("TextBrush");
        // Set FontFamily to a new FontFamily object using the inputs in parentheses.
        FontFamily = new FontFamily("Segoe UI");

        // A Grid divides space into rows/columns. Star sizing takes the remaining space; Auto uses
        // only the size its content needs. Keeping errors and buttons outside the scroll area makes
        // them accessible even when the form is taller than the available window height.
        var layout = new Grid { Margin = new Thickness(24, 20, 24, 20) };
        // Add new RowDefinition { Height = new GridLength(1, GridUnitType.Star) } to layout.RowDefinitions.
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        // Add new RowDefinition { Height = GridLength.Auto } to layout.RowDefinitions.
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        // Add new RowDefinition { Height = GridLength.Auto } to layout.RowDefinitions.
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        // Remember a new StackPanel object as form.
        var form = new StackPanel();
        // Remember a new ScrollViewer object; the entries in braces set its initial contents or properties
        // as scroll.
        var scroll = new ScrollViewer
        {
            // Set this new object's Content entry to form.
            Content = form,
            // Set this new object's VerticalScrollBarVisibility entry to ScrollBarVisibility.Auto.
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            // Set this new object's HorizontalScrollBarVisibility entry to ScrollBarVisibility.Disabled.
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        // Add scroll to layout.Children.
        layout.Children.Add(scroll);
        // Add new TextBlock { Text = "Give these bits meaning", FontSize = 22, FontWeight =
        // FontWeights.SemiBold } to form.Children.
        form.Children.Add(new TextBlock { Text = "Give these bits meaning", FontSize = 22, FontWeight = FontWeights.SemiBold });
        // Add Muted("Name a flag, packed value, or a field assembled from separate ranges.", new
        // Thickness(0, 6, 0, 18)) to form.Children.
        form.Children.Add(Muted("Name a flag, packed value, or a field assembled from separate ranges.", new Thickness(0, 6, 0, 18)));

        // Name and color share a row. StackPanel lays its children out sequentially, top-to-bottom
        // unless Orientation is set to Horizontal. Margins separate neighboring controls.
        var identity = new Grid();
        // Add new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) } to
        // identity.ColumnDefinitions.
        identity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        // Add new ColumnDefinition { Width = new GridLength(175) } to identity.ColumnDefinitions.
        identity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(175) });
        // Remember a new StackPanel object; the entries in braces set its initial contents or properties as
        // namePanel.
        var namePanel = new StackPanel { Margin = new Thickness(0, 0, 14, 0) };
        // Add Label("FIELD NAME") to namePanel.Children.
        namePanel.Children.Add(Label("FIELD NAME"));
        // Set _name to a new TextBox object; the entries in braces set its initial contents or properties.
        _name = new TextBox { Text = _draft.Name, MaxLength = 128 };
        // Automation names identify controls to screen readers and UI-testing tools.
        AutomationProperties.SetName(_name, "Field name");
        // Add _name to namePanel.Children.
        namePanel.Children.Add(_name);
        // Add namePanel to identity.Children.
        identity.Children.Add(namePanel);
        // Remember a new StackPanel object as colorPanel.
        var colorPanel = new StackPanel();
        // Add Label("COLOR") to colorPanel.Children.
        colorPanel.Children.Add(Label("COLOR"));
        // Set _color to a new ComboBox object.
        _color = new ComboBox();
        // Call AutomationProperties.SetName(...); the values in parentheses are the inputs.
        AutomationProperties.SetName(_color, "Field color");
        // Remember a new List<ColorChoice> object; the entries in braces set its initial contents or
        // properties as colors.
        var colors = new List<ColorChoice>
        {
            new("Mint", "#54BFA6"), new("Blue", "#6EABFF"),
            new("Amber", "#D9AD68"), new("Lilac", "#CB98EE"),
            new("Coral", "#EE8C98"), new("Sky", "#66C9DD")
        };
        // Retain a valid custom color when editing a project created with a color outside this palette.
        // A ComboBoxItem's visible Content is the swatch/name, while Tag holds the actual saved hex color.
        var selectedColor = colors.FirstOrDefault(c => c.Hex.Equals(_draft.Color, StringComparison.OrdinalIgnoreCase));
        // If both selectedColor equals null and IsColor(_draft.Color) is true, run the following grouped
        // instructions.
        if (selectedColor == null && IsColor(_draft.Color))
        {
            // Set selectedColor to a new ColorChoice object using the inputs in parentheses.
            selectedColor = new ColorChoice("Custom", _draft.Color.ToUpperInvariant());
            // Add selectedColor to colors.
            colors.Add(selectedColor);
        }
        // Take each item from colors in turn, call the current item choice, and run the following grouped
        // instructions.
        foreach (var choice in colors)
        {
            // Remember a new StackPanel object; the entries in braces set its initial contents or
            // properties as content.
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            // Add the expression below to content.Children.
            content.Children.Add(new Border
            {
                // Set this new object's Background entry to the result returned by new
                // BrushConverter().ConvertFromString(...) (! tells the compiler we expect a value here; it
                // does not check at runtime), converted to Brush.
                Background = (Brush)new BrushConverter().ConvertFromString(choice.Hex)!,
                // Set this new object's Width entry to 12.
                Width = 12, Height = 12, CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 0, 9, 0)
            });
            // Add new TextBlock { Text = choice.Name, VerticalAlignment = VerticalAlignment.Center } to
            // content.Children.
            content.Children.Add(new TextBlock { Text = choice.Name, VerticalAlignment = VerticalAlignment.Center });
            // Remember a new ComboBoxItem object; the entries in braces set its initial contents or
            // properties as item.
            var item = new ComboBoxItem { Content = content, Tag = choice.Hex };
            // Add item to _color.Items.
            _color.Items.Add(item);
            // If choice equals selectedColor, set _color.SelectedItem to item.
            if (choice == selectedColor) _color.SelectedItem = item;
        }
        // If _color.SelectedIndex is less than 0, set _color.SelectedIndex to 0.
        if (_color.SelectedIndex < 0) _color.SelectedIndex = 0;
        // Add _color to colorPanel.Children.
        colorPanel.Children.Add(_color);
        // Call Grid.SetColumn(...); the values in parentheses are the inputs.
        Grid.SetColumn(colorPanel, 1);
        // Add colorPanel to identity.Children.
        identity.Children.Add(colorPanel);
        // Add identity to form.Children.
        form.Children.Add(identity);

        // The mapping is ordered, not just a set of selected bits. The first listed source bit becomes
        // the value's most significant bit (MSB, greatest weight); the last becomes its LSB (weight 1).
        // For example, "9, 2, 1" builds a three-bit value in exactly that order, even across source bytes.
        var mappingLabel = Label("SOURCE BITS · VALUE MSB TO LSB");
        // Set mappingLabel.Margin to a new Thickness object using the inputs in parentheses.
        mappingLabel.Margin = new Thickness(0, 20, 0, 6);
        // Add mappingLabel to form.Children.
        form.Children.Add(mappingLabel);
        // Set _mapping to a new TextBox object; the entries in braces set its initial contents or
        // properties.
        _mapping = new TextBox
        {
            // Set this new object's Text entry to the result returned by FormatMapping(...).
            Text = FormatMapping(_draft.OrderedBits),
            // Set this new object's FontFamily entry to a new FontFamily object using the inputs in
            // parentheses.
            FontFamily = new FontFamily("Consolas"),
            // Set this new object's AcceptsReturn entry to true (yes).
            AcceptsReturn = true,
            // Set this new object's TextWrapping entry to TextWrapping.Wrap.
            TextWrapping = TextWrapping.Wrap,
            // Set this new object's VerticalContentAlignment entry to VerticalAlignment.Top.
            VerticalContentAlignment = VerticalAlignment.Top,
            // Set this new object's VerticalScrollBarVisibility entry to ScrollBarVisibility.Auto.
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            // Set this new object's Height entry to 75.
            Height = 75,
            // Set this new object's MaxLength entry to 65536.
            MaxLength = 65536
        };
        // Add _mapping to form.Children.
        form.Children.Add(_mapping);
        // Call AutomationProperties.SetName(...); the values in parentheses are the inputs.
        AutomationProperties.SetName(_mapping, "Source bit mapping");
        // Add the expression below to form.Children.
        form.Children.Add(Muted("Use decimal positions and inclusive ranges: 5-10, 24-27, 20. Descending ranges are allowed. Physical bit 0 is the MS bit of byte 0; these addresses stay the same when ruler numbering changes.", new Thickness(0, 7, 0, 10)));
        // Each lambda (the "bits => ..." function) describes a different mapping transformation.
        // File order sorts physical addresses. Reverse bytes reverses the encountered byte groups
        // while keeping their bits in physical order. Reverse bits reverses every position in the list.
        var transforms = new WrapPanel();
        // Call AddTransform: Adds a mapping-transform button.
        AddTransform(transforms, "File order", "Arrange source bits from the start of the file to the end.", bits => bits.Order().ToList());
        // Call AddTransform: Adds a mapping-transform button.
        AddTransform(transforms, "Reverse bytes", "Reverse byte groups while retaining MS-to-LS physical bit order within each byte.", bits => bits.GroupBy(bit => bit / 8).Reverse().SelectMany(group => group.Order()).ToList());
        // Call AddTransform: Adds a mapping-transform button.
        AddTransform(transforms, "Reverse bits", "Reverse the complete source bit sequence.", bits => bits.AsEnumerable().Reverse().ToList());
        // Add transforms to form.Children.
        form.Children.Add(transforms);
        // Preview the two significance endpoints and source-byte count without changing document data.
        _summary = Muted("", new Thickness(0, 10, 0, 18));
        // Set _summary.FontFamily to a new FontFamily object using the inputs in parentheses.
        _summary.FontFamily = new FontFamily("Consolas");
        // Add _summary to form.Children.
        form.Children.Add(_summary);

        // Add Label("NOTES · OPTIONAL") to form.Children.
        form.Children.Add(Label("NOTES · OPTIONAL"));
        // Set _notes to a new TextBox object; the entries in braces set its initial contents or properties.
        _notes = new TextBox
        {
            // Set this new object's Text entry to _draft.Notes.
            Text = _draft.Notes, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            // Set this new object's VerticalContentAlignment entry to VerticalAlignment.Top.
            VerticalContentAlignment = VerticalAlignment.Top, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            // Set this new object's MinHeight entry to 65.
            MinHeight = 65, MaxHeight = 125, MaxLength = 65536
        };
        // Add _notes to form.Children.
        form.Children.Add(_notes);
        // Call AutomationProperties.SetName(...); the values in parentheses are the inputs.
        AutomationProperties.SetName(_notes, "Field notes");
        // Validation errors stay in the dialog so a mistyped range can be corrected in place.
        _error = new TextBlock
        {
            // Set this new object's Foreground entry to a new SolidColorBrush object using the inputs in
            // parentheses.
            Foreground = new SolidColorBrush(Color.FromRgb(255, 160, 164)),
            // Set this new object's TextWrapping entry to TextWrapping.Wrap.
            TextWrapping = TextWrapping.Wrap,
            // Set this new object's Margin entry to a new Thickness object using the inputs in parentheses.
            Margin = new Thickness(0, 12, 0, 10),
            // Set this new object's MinHeight entry to 20.
            MinHeight = 20
        };
        // Call Grid.SetRow(...); the values in parentheses are the inputs.
        Grid.SetRow(_error, 1);
        // Add _error to layout.Children.
        layout.Children.Add(_error);
        // Remember a new StackPanel object; the entries in braces set its initial contents or properties as
        // actions.
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        // IsCancel provides Escape behavior; IsDefault provides the default confirmation action.
        // Setting DialogResult closes the modal dialog and reports acceptance/cancellation to its caller.
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 88, Margin = new Thickness(0, 0, 10, 0) };
        // Register the operation after => as a listener for cancel.Click; the listener runs when that event
        // is raised.
        cancel.Click += (_, _) => DialogResult = false;
        // Remember a new Button object; the entries in braces set its initial contents or properties as
        // done.
        var done = new Button { Content = "Done", IsDefault = true, MinWidth = 100, Style = (Style)FindResource("AccentButton") };
        // Register the operation after => as a listener for done.Click; the listener runs when that event
        // is raised.
        done.Click += (_, _) => SaveDraft();
        // Add cancel to actions.Children.
        actions.Children.Add(cancel);
        // Add done to actions.Children.
        actions.Children.Add(done);
        // Call Grid.SetRow(...); the values in parentheses are the inputs.
        Grid.SetRow(actions, 2);
        // Add actions to layout.Children.
        layout.Children.Add(actions);
        // Set Content to layout.
        Content = layout;

        // Attach this after construction so all controls needed by validation already exist.
        // Loaded runs when WPF has created the window; focus/select-all then makes the name easy to replace.
        _mapping.TextChanged += (_, _) => UpdateSummary();
        // Register the operation after => as a listener for Loaded; the listener runs when that event is
        // raised.
        Loaded += (_, _) => { _name.Focus(); _name.SelectAll(); UpdateSummary(); };
    }

    /// <summary>Creates a compact form label using the application's shared muted-text styling.</summary>
    private TextBlock Label(string text) => new()
    {
        // Set this new object's Text entry to text.
        Text = text,
        // Set this new object's Foreground entry to the result returned by FindResource(...), converted to
        // Brush.
        Foreground = (Brush)FindResource("MutedBrush"),
        // Set this new object's FontSize entry to 10.
        FontSize = 10,
        // Set this new object's FontWeight entry to FontWeights.SemiBold.
        FontWeight = FontWeights.SemiBold,
        // Set this new object's Margin entry to a new Thickness object using the inputs in parentheses.
        Margin = new Thickness(0, 0, 0, 6)
    };

    /// <summary>Creates wrapping explanatory text with the supplied spacing around it.</summary>
    private TextBlock Muted(string text, Thickness margin) => new()
    {
        // Set this new object's Text entry to text.
        Text = text, Margin = margin, Foreground = (Brush)FindResource("MutedBrush"),
        // Set this new object's TextWrapping entry to TextWrapping.Wrap.
        TextWrapping = TextWrapping.Wrap, FontSize = 12
    };

    /// <summary>
    /// Adds a mapping-transform button. Func is a function passed as data: it accepts a list of source
    /// addresses and returns their new order. Invalid unfinished text is reported rather than transformed.
    /// </summary>
    private void AddTransform(Panel panel, string text, string tooltip, Func<List<long>, List<long>> transform)
    {
        // Remember a new Button object; the entries in braces set its initial contents or properties as
        // button.
        var button = new Button { Content = text, ToolTip = tooltip, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(10, 6, 10, 6) };
        // Register the operation after => as a listener for button.Click; the listener runs when that event
        // is raised.
        button.Click += (_, _) =>
        {
            // If it is not the case that TryReadMapping(out var bits, out var error) is true, run the
            // following grouped instructions.
            if (!TryReadMapping(out var bits, out var error)) { _error.Text = error; return; }
            // Set _mapping.Text to the result returned by FormatMapping(...).
            _mapping.Text = FormatMapping(transform(bits));
            // Call UpdateSummary: Validates the current mapping and previews its size and most/least
            // significant source positions.
            UpdateSummary();
        };
        // Add button to panel.Children.
        panel.Children.Add(button);
    }

    /// <summary>Validates the current mapping and previews its size and most/least significant source positions.</summary>
    private void UpdateSummary()
    {
        // If it is not the case that TryReadMapping(out var bits, out var error) is true, run the following
        // grouped instructions.
        if (!TryReadMapping(out var bits, out var error))
        {
            // Set _summary.Text to the text "Enter a valid source mapping to preview significance.".
            _summary.Text = "Enter a valid source mapping to preview significance.";
            // Set _error.Text to error.
            _error.Text = error;
            // Leave this method immediately.
            return;
        }
        // Index 0 is the first entry; ^1 means one position from the end, or the final entry.
        // Source addresses are physical positions, independent of how the grid's ruler labels each bit.
        long most = bits[0], least = bits[^1];
        // Set _summary.Text to the displayed text below; each {...} inserts a computed value into the text
        // + the displayed text below; each {...} inserts a computed value into the text (addition, or
        // joining text) + the displayed text below; each {...} inserts a computed value into the text
        // (addition, or joining text).
        _summary.Text = $"{bits.Count:N0} bits · {bits.Select(bit => bit / 8).Distinct().Count():N0} source bytes\n" +
            $"Value MSB ← physical {most:N0} (byte {most / 8:N0})\n" +
            $"Value LSB ← physical {least:N0} (byte {least / 8:N0})";
        // Set _error.Text to empty text.
        _error.Text = "";
    }

    /// <summary>
    /// Parses comma/semicolon/newline-separated physical positions and inclusive ranges, preserving
    /// their entered order. The out parameters return either the ordered bit list or a helpful error.
    /// Duplicate positions, out-of-file positions, and fields exceeding the supported size are rejected.
    /// </summary>
    private bool TryReadMapping(out List<long> bits, out string error)
    {
        // Set bits to an empty collection.
        bits = [];
        // Set error to empty text.
        error = "";
        // If _fileBitLength equals 0, run the following grouped instructions.
        if (_fileBitLength == 0) { error = "Open a nonempty file before defining a field."; return false; }
        // If string.IsNullOrWhiteSpace(_mapping.Text) is true, run the following grouped instructions.
        if (string.IsNullOrWhiteSpace(_mapping.Text)) { error = "Select at least one source bit."; return false; }
        // "bits" preserves significance order. "seen" is a separate fast membership check so the same
        // source bit cannot appear twice; using a set alone would lose the user's assembly order.
        var seen = new HashSet<long>();
        // Remember the result returned by _mapping.Text.Split(...) as tokens.
        var tokens = _mapping.Text.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        // If tokens.Length equals 0, run the following grouped instructions.
        if (tokens.Length == 0) { error = "Select at least one source bit."; return false; }
        // Take each item from tokens in turn, call the current item token, and run the following grouped
        // instructions.
        foreach (string token in tokens)
        {
            // This regular expression accepts a nonnegative decimal number, optionally followed by
            // '-' and another number, with surrounding whitespace. ^ and $ require the complete token
            // to match; captured groups 1 and 2 contain the range endpoints.
            var match = Regex.Match(token, @"^\s*(\d+)\s*(?:-\s*(\d+))?\s*$", RegexOptions.CultureInvariant);
            // If it is not the case that match.Success is true, or it is not the case that
            // long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out
            // long first) is true, run the following grouped instructions.
            if (!match.Success || !long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out long first))
            // Give the caller a readable explanation, then answer false to stop
            // parsing this invalid map. No live field has been edited.
            { error = $"Invalid range: {Abbreviate(token)}. Use decimal positions such as 5-10, 20."; return false; }
            // Remember first as last.
            long last = first;
            // If both match.Groups[2].Success is true and it is not the case that
            // long.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out
            // last) is true, run the following grouped instructions.
            if (match.Groups[2].Success && !long.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out last))
            // The end of the range could not fit in a long integer. Return an
            // explanation and a failed result instead of using an incorrect address.
            { error = "A bit position is too large."; return false; }
            // If first is at least _fileBitLength, or last is at least _fileBitLength, run the following
            // grouped instructions.
            if (first >= _fileBitLength || last >= _fileBitLength)
            // Report the actual valid address range and stop. The final valid
            // address is one less than the count, because addresses start at zero.
            { error = $"Source positions must be between 0 and {_fileBitLength - 1:N0}."; return false; }
            // Inclusive ranges contain distance+1 entries: "5-7" adds 5, 6, and 7.
            // Check the size BEFORE expanding the range so an enormous number cannot create a huge list.
            long distance = Math.Abs(last - first);
            // If distance is at least DocumentModel.MaxFieldBits - bits.Count, run the following grouped
            // instructions.
            if (distance >= DocumentModel.MaxFieldBits - bits.Count)
            // Explain the field-size limit and reject the complete proposed map
            // before expanding this range into individual entries.
            { error = $"A field may contain at most {DocumentModel.MaxFieldBits:N0} bits."; return false; }
            // Descending ranges are intentional: "7-5" produces 7, 6, 5 and reverses their significance.
            long direction = last >= first ? 1 : -1;
            // Repeat with long position = first as the starting state, while there is no stopping
            // condition; after each pass, add direction to position and keep the result there (+=). The
            // braces contain one pass.
            for (long position = first; ; position += direction)
            {
                // If it is not the case that seen.Add(position) is true, run the following grouped
                // instructions.
                if (!seen.Add(position)) { error = $"Physical bit {position:N0} appears more than once."; return false; }
                // Add position to bits.
                bits.Add(position);
                // If position equals last, run the following instruction.
                if (position == last) break;
            }
        }
        // Return true (yes) to the caller and leave this method.
        return true;
    }

    /// <summary>
    /// Validates the form, builds a fresh result retaining the original field ID, and accepts the dialog.
    /// No document is edited here; the calling view model commits the result as one undoable change.
    /// </summary>
    private void SaveDraft()
    {
        // Remember _name.Text with spacing removed from both ends as name.
        string name = _name.Text.Trim();
        // If name.Length matches < 1 or > 128, or name.Any(char.IsControl) is true, run the following
        // grouped instructions.
        if (name.Length is < 1 or > 128 || name.Any(char.IsControl))
        // Display the name requirements, move keyboard input back to the name
        // box, and leave SaveDraft without accepting or closing the dialog.
        { _error.Text = "Give the field a name of 1–128 characters, without control characters."; _name.Focus(); return; }
        // If it is not the case that TryReadMapping(out var bits, out var error) is true, run the following
        // grouped instructions.
        if (!TryReadMapping(out var bits, out var error)) { _error.Text = error; _mapping.Focus(); return; }
        // Remember (_color.SelectedItem as ComboBoxItem)?.Tag as string as color.
        string? color = (_color.SelectedItem as ComboBoxItem)?.Tag as string;
        // If it is not the case that IsColor(color) is true, run the following grouped instructions.
        if (!IsColor(color)) { _error.Text = "Choose a valid field color."; return; }
        // Copy only after all validation succeeds, so failed attempts never publish a half-valid result.
        Result = _draft.Clone();
        // Set Result.Name to name.
        Result.Name = name;
        // Set Result.Color to color (! tells the compiler we expect a value here; it does not check at
        // runtime).
        Result.Color = color!;
        // Set Result.Notes to _notes.Text.
        Result.Notes = _notes.Text;
        // Set Result.OrderedBits to bits.
        Result.OrderedBits = bits;
        // Set DialogResult to true (yes).
        DialogResult = true;
    }

    /// <summary>Accepts the saved color form #RRGGBB: a hash followed by six hexadecimal digits.</summary>
    private static bool IsColor(string? color) => color is { Length: 7 } && color[0] == '#' && color.Skip(1).All(char.IsAsciiHexDigit);

    /// <summary>
    /// Compresses consecutive addresses into readable ranges without sorting them. For example,
    /// [5, 6, 7, 12, 11] becomes "5-7, 12-11", retaining the original value-assembly order.
    /// </summary>
    private static string FormatMapping(IEnumerable<long> source)
    {
        // Remember a separate array containing source's items as bits.
        var bits = source.ToArray();
        // Remember a new List<string> object as ranges.
        var ranges = new List<string>();
        // Repeat with int start = 0 as the starting state, while start is less than bits.Length; after each
        // pass, The braces contain one pass.
        for (int start = 0; start < bits.Length;)
        {
            // Remember start as end.
            int end = start;
            // Only runs moving by exactly +1 or -1 can be represented as inclusive ranges.
            // Stop when the direction changes or there is a gap, then begin the next output token.
            long step = start + 1 < bits.Length ? bits[start + 1] - bits[start] : 0;
            // If step matches 1 or -1, run the following instruction.
            if (step is 1 or -1)
                // Keep repeating the following instructions while end + 1 < bits.Length && bits[end + 1] -
                // bits[end] == step is true; check again before each pass.
                while (end + 1 < bits.Length && bits[end + 1] - bits[end] == step) end++;
            // Add the expression below to ranges.
            ranges.Add(end == start ? bits[start].ToString(CultureInfo.InvariantCulture)
                : $"{bits[start].ToString(CultureInfo.InvariantCulture)}-{bits[end].ToString(CultureInfo.InvariantCulture)}");
            // Set start to end + 1 (addition, or joining text).
            start = end + 1;
        }
        // Return the result returned by string.Join(...) to the caller and leave this method.
        return string.Join(", ", ranges);
    }

    /// <summary>Keeps an invalid pasted token short enough to fit a useful validation message.</summary>
    private static string Abbreviate(string text) => text.Length > 32 ? text[..29] + "..." : text;
    /// <summary>A palette entry pairs its user-visible name with the hexadecimal color saved in the field.</summary>
    private sealed record ColorChoice(string Name, string Hex);
}
