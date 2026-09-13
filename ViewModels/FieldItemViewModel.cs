using BitExplorer.Core;
using WpfApp1.Infrastructure;

namespace WpfApp1.ViewModels;

public sealed class FieldItemViewModel : ObservableObject
{
    private string _name = "", _color = "", _details = "", _value = "";
    public FieldItemViewModel(Guid id) => Id = id;
    public Guid Id { get; }
    public string Name { get => _name; private set => SetProperty(ref _name, value); }
    public string Color { get => _color; private set => SetProperty(ref _color, value); }
    public string Details { get => _details; private set => SetProperty(ref _details, value); }
    public string Value { get => _value; private set => SetProperty(ref _value, value); }

    internal void Update(NamedField field, DocumentModel document)
    {
        Name = field.Name;
        Color = field.Color;
        Details = $"{field.OrderedBits.Count} bits · {NumericText.DescribeSource(field.OrderedBits)}";
        var value = document.ReadField(field);
        Value = $"{NumericText.ShortHex(value)} · {NumericText.ShortNumber(value)}";
    }
}
