using System.Windows.Controls;

namespace WpfApp1.Views;

/// <summary>
/// Reusable display-options panel. Its XAML bindings send values and commands
/// directly to DisplaySettingsViewModel, so no event-handling code is needed here.
/// UserControl means this is a piece of a window, not a separate desktop window.
/// </summary>
public partial class DisplayOptionsView : UserControl
{
    /// <summary>Creates the controls declared in the matching XAML file.</summary>
    public DisplayOptionsView() => InitializeComponent();
}
