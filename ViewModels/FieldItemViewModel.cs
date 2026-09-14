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

// Make names from BitExplorer.Core available here without writing their full prefix each time. This does
// not run that library's code.
using BitExplorer.Core;
// Make names from WpfApp1.Infrastructure available here without writing their full prefix each time. This
// does not run that library's code.
using WpfApp1.Infrastructure;

// Place this file's definitions in the WpfApp1.ViewModels naming group, which prevents clashes with names
// in other groups.
namespace WpfApp1.ViewModels;

/// <summary>
/// A stable row in the named-fields list. The field ID stays fixed while properties
/// are refreshed after edits; property notifications update WPF without rebuilding the row.
/// </summary>
public sealed class FieldItemViewModel : ObservableObject
{
    // Remember empty text as _name. Remember empty text as _color. Remember empty text as _details.
    // Remember empty text as _value.
    private string _name = "", _color = "", _details = "", _value = "";
    /// <summary>Associates this row with a field's permanent identifier, independent of its editable name.</summary>
    public FieldItemViewModel(Guid id) => Id = id;
    /// <summary>The field identity used for row reuse, selection and command targeting.</summary>
    public Guid Id { get; }
    /// <summary>The user-assigned label shown as the row heading.</summary>
    public string Name { get => _name; private set => SetProperty(ref _name, value); }
    /// <summary>A #RRGGBB color string; the XAML binding converts it to a brush for the small color marker.</summary>
    public string Color { get => _color; private set => SetProperty(ref _color, value); }
    /// <summary>A compact summary of the field's bit count and source-byte span.</summary>
    public string Details { get => _details; private set => SetProperty(ref _details, value); }
    /// <summary>The current interpreted value in abbreviated hexadecimal and decimal notation.</summary>
    public string Value { get => _value; private set => SetProperty(ref _value, value); }

    /// <summary>Reads the latest field metadata and value; setters notify only properties whose text changed.</summary>
    internal void Update(NamedField field, DocumentModel document)
    {
        // Set Name to field.Name.
        Name = field.Name;
        // Set Color to field.Color.
        Color = field.Color;
        // Set Details to the displayed text below; each {...} inserts a computed value into the text.
        Details = $"{field.OrderedBits.Count} bits · {NumericText.DescribeSource(field.OrderedBits)}";
        // Remember the result returned by document.ReadField(...) as value.
        var value = document.ReadField(field);
        // Set Value to the displayed text below; each {...} inserts a computed value into the text.
        Value = $"{NumericText.ShortHex(value)} · {NumericText.ShortNumber(value)}";
    }
}
