using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EDControls.EDList;

/// <summary>
/// Jeden řádek v <see cref="EDList"/>. Drží Id + sloupcová data + checked state.
/// <para>
/// Indexer <c>row[col.Key]</c> propustí binding <c>"[col.Key]"</c> jako dříve s Dictionary —
/// ne breaking change pro stávající column bindings.
/// </para>
/// <para>
/// <see cref="IsChecked"/> má <see cref="INotifyPropertyChanged"/>, takže CheckBox v Check
/// módu se aktualizuje i při programmatic změnách (CheckAll, header toggle).
/// </para>
/// </summary>
public sealed class EDListRow : INotifyPropertyChanged
{
    public string Id { get; }

    /// <summary>Sloupcová data, klíč = column.Key.</summary>
    public IReadOnlyDictionary<string, string?> Values => _values;
    private readonly Dictionary<string, string?> _values;

    public string? this[string key] => _values.TryGetValue(key, out var v) ? v : null;

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            OnPropertyChanged();
        }
    }

    public EDListRow(string id, Dictionary<string, string?> values)
    {
        Id = id;
        _values = values;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
