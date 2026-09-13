using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp1.ViewModels;

namespace WpfApp1.Views;

/// <summary>
/// The named-fields list. XAML binds its contents and buttons to FieldsViewModel.
/// This small adapter handles gestures that depend on which real control was hit.
/// It never edits the document itself: all actions go through view-model commands.
/// </summary>
public partial class FieldsView : UserControl
{
    /// <summary>Builds the controls from FieldsView.xaml.</summary>
    public FieldsView() => InitializeComponent();

    /// <summary>Edits the field under a left-button double-click, ignoring empty list space.</summary>
    private void FieldList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // OriginalSource may be a child TextBlock or colored shape inside a row.
        // ContainerFromElement walks back to the ListBoxItem that owns that child.
        if (e.ChangedButton != MouseButton.Left || e.OriginalSource is not DependencyObject source ||
            ItemsControl.ContainerFromElement(FieldList, source) is not ListBoxItem item) return;
        // Select before executing: the command reads the session's selected field.
        item.IsSelected = true;
        item.Focus();
        if (DataContext is FieldsViewModel model) e.Handled = Execute(model.EditLabelCommand);
    }

    /// <summary>Offers F2 and Delete only while keyboard focus is inside this list.</summary>
    private void FieldList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!FieldList.IsKeyboardFocusWithin || Keyboard.Modifiers != ModifierKeys.None ||
            DataContext is not FieldsViewModel model) return;
        if (e.Key == Key.F2) e.Handled = Execute(model.EditLabelCommand);
        else if (e.Key == Key.Delete) e.Handled = Execute(model.DeleteLabelCommand);
    }

    /// <summary>Selects a right-clicked row before its context menu opens.</summary>
    private void FieldList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source &&
            ItemsControl.ContainerFromElement(FieldList, source) is ListBoxItem item)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    /// <summary>Suppresses a mouse-opened menu over empty space instead of targeting an unrelated row.</summary>
    private void FieldList_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        // CursorLeft is negative for keyboard-invoked menus, which may use the
        // current selected row without requiring the mouse to be over that row.
        bool overItem = Mouse.DirectlyOver is DependencyObject source &&
            ItemsControl.ContainerFromElement(FieldList, source) is ListBoxItem;
        if (FieldList.SelectedItem is null || e.CursorLeft >= 0 && !overItem) e.Handled = true;
    }

    /// <summary>Runs an available command and tells the event handler whether to consume the gesture.</summary>
    private static bool Execute(ICommand command)
    {
        if (!command.CanExecute(null)) return false;
        command.Execute(null);
        return true;
    }
}
