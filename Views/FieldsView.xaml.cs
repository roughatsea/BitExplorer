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
// Make names from System.Windows.Controls available here without writing their full prefix each time. This
// does not run that library's code.
using System.Windows.Controls;
// Make names from System.Windows.Input available here without writing their full prefix each time. This
// does not run that library's code.
using System.Windows.Input;
// Make names from WpfApp1.ViewModels available here without writing their full prefix each time. This does
// not run that library's code.
using WpfApp1.ViewModels;

// Place this file's definitions in the WpfApp1.Views naming group, which prevents clashes with names in
// other groups.
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
            // If no owning list row is found, stop instead of editing a different
            // field merely because it happened to be selected earlier.
            ItemsControl.ContainerFromElement(FieldList, source) is not ListBoxItem item) return;
        // Select before executing: the command reads the session's selected field.
        item.IsSelected = true;
        // Ask WPF to give this control keyboard focus, so keyboard input is directed here.
        item.Focus();
        // If DataContext matches FieldsViewModel model, set e.Handled to the result returned by
        // Execute(...).
        if (DataContext is FieldsViewModel model) e.Handled = Execute(model.EditLabelCommand);
    }

    /// <summary>Offers F2 and Delete only while keyboard focus is inside this list.</summary>
    private void FieldList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // If it is not the case that FieldList.IsKeyboardFocusWithin is true, or Keyboard.Modifiers differs
        // from ModifierKeys.None, or DataContext matches not FieldsViewModel model, leave this method
        // immediately.
        if (!FieldList.IsKeyboardFocusWithin || Keyboard.Modifiers != ModifierKeys.None ||
            // Require the expected view model too. Any failed requirement ends
            // this handler so an unrelated key press is not treated as a label edit.
            DataContext is not FieldsViewModel model) return;
        // If e.Key equals Key.F2, set e.Handled to the result returned by Execute(...).
        if (e.Key == Key.F2) e.Handled = Execute(model.EditLabelCommand);
        // Otherwise, try this next condition: e.Key == Key.Delete.
        else if (e.Key == Key.Delete) e.Handled = Execute(model.DeleteLabelCommand);
    }

    /// <summary>Selects a right-clicked row before its context menu opens.</summary>
    private void FieldList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // If both e.OriginalSource matches DependencyObject source and
        // ItemsControl.ContainerFromElement(FieldList, source) matches ListBoxItem item, run the following
        // grouped instructions.
        if (e.OriginalSource is DependencyObject source &&
            ItemsControl.ContainerFromElement(FieldList, source) is ListBoxItem item)
        {
            // Set item.IsSelected to true (yes).
            item.IsSelected = true;
            // Ask WPF to give this control keyboard focus, so keyboard input is directed here.
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
        // If FieldList.SelectedItem matches null, or both e.CursorLeft is at least 0 and it is not the case
        // that overItem is true, set e.Handled to true (yes).
        if (FieldList.SelectedItem is null || e.CursorLeft >= 0 && !overItem) e.Handled = true;
    }

    /// <summary>Runs an available command and tells the event handler whether to consume the gesture.</summary>
    private static bool Execute(ICommand command)
    {
        // If it is not the case that command.CanExecute(null) is true, return false (no) to the caller and
        // leave this method.
        if (!command.CanExecute(null)) return false;
        // Invoke command.Execute to perform the action stored in that command; what changes depends on
        // which command it is.
        command.Execute(null);
        // Return true (yes) to the caller and leave this method.
        return true;
    }
}
