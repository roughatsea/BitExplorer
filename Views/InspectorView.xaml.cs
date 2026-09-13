using System.Windows.Controls;

namespace WpfApp1.Views;

/// <summary>
/// Displays interpreted byte/field values using InspectorViewModel bindings.
/// These methods only manipulate visible controls; the view model performs edits.
/// </summary>
public partial class InspectorView : UserControl
{
    /// <summary>Builds the panel from InspectorView.xaml.</summary>
    public InspectorView() => InitializeComponent();

    /// <summary>Reveals the byte input and selects its text, ready for replacement typing.</summary>
    public void FocusByteEditor()
    {
        // The inspector scrolls, so focus alone might land on an offscreen control.
        ByteInput.BringIntoView();
        ByteInput.Focus();
        ByteInput.SelectAll();
    }

    /// <summary>Toggles the collapsible settings section and scrolls it into view on expansion.</summary>
    public void ToggleDisplayOptions()
    {
        DisplayExpander.IsExpanded = !DisplayExpander.IsExpanded;
        if (DisplayExpander.IsExpanded) DisplayExpander.BringIntoView();
    }
}
