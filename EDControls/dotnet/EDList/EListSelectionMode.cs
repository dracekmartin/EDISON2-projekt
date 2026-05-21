namespace EDControls.EDList;

/// <summary>Mode výběru řádků v <see cref="EDList"/>.</summary>
public enum EListSelectionMode
{
    /// <summary>Klasický single-select přes <see cref="System.Windows.Controls.DataGrid.SelectedItem"/>.</summary>
    Single,
    /// <summary>Multi-select přes checkbox sloupec prepended před data sloupci. Header checkbox = toggle all.</summary>
    Check
}
