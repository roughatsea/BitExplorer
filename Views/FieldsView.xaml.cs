using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp1.ViewModels;

namespace WpfApp1.Views;

public partial class FieldsView : UserControl
{
    public FieldsView() => InitializeComponent();

    private void FieldList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.OriginalSource is not DependencyObject source ||
            ItemsControl.ContainerFromElement(FieldList, source) is not ListBoxItem item) return;
        item.IsSelected = true;
        item.Focus();
        if (DataContext is FieldsViewModel model) e.Handled = Execute(model.EditLabelCommand);
    }

    private void FieldList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!FieldList.IsKeyboardFocusWithin || Keyboard.Modifiers != ModifierKeys.None ||
            DataContext is not FieldsViewModel model) return;
        if (e.Key == Key.F2) e.Handled = Execute(model.EditLabelCommand);
        else if (e.Key == Key.Delete) e.Handled = Execute(model.DeleteLabelCommand);
    }

    private void FieldList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source &&
            ItemsControl.ContainerFromElement(FieldList, source) is ListBoxItem item)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    private void FieldList_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        bool overItem = Mouse.DirectlyOver is DependencyObject source &&
            ItemsControl.ContainerFromElement(FieldList, source) is ListBoxItem;
        if (FieldList.SelectedItem is null || e.CursorLeft >= 0 && !overItem) e.Handled = true;
    }

    private static bool Execute(ICommand command)
    {
        if (!command.CanExecute(null)) return false;
        command.Execute(null);
        return true;
    }
}
