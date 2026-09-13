using BitExplorer.Core;
using WpfApp1.Infrastructure;

namespace WpfApp1.ViewModels;

/// <summary>
/// A stable row in the named-fields list. The field ID stays fixed while properties
/// are refreshed after edits; property notifications update WPF without rebuilding the row.
/// </summary>
public sealed class FieldItemViewModel : ObservableObject
{
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
        Name = field.Name;
        Color = field.Color;
        Details = $"{field.OrderedBits.Count} bits · {NumericText.DescribeSource(field.OrderedBits)}";
        var value = document.ReadField(field);
        Value = $"{NumericText.ShortHex(value)} · {NumericText.ShortNumber(value)}";
    }
}
