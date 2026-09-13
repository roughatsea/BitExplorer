using System.Windows.Controls;

namespace WpfApp1.Views;

public partial class InspectorView : UserControl
{
    public InspectorView() => InitializeComponent();

    public void FocusByteEditor()
    {
        ByteInput.BringIntoView();
        ByteInput.Focus();
        ByteInput.SelectAll();
    }

    public void ToggleDisplayOptions()
    {
        DisplayExpander.IsExpanded = !DisplayExpander.IsExpanded;
        if (DisplayExpander.IsExpanded) DisplayExpander.BringIntoView();
    }
}
