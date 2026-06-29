namespace EDControls;

/// <summary>
/// Generická volba „klíč + popisek" sdílená napříč EDControls (EDInputForm List/ComboBox pole,
/// <c>EDFilterComboBox.Suggestions</c>, …). Záměrně v root namespace — není vázaná na konkrétní control.
/// </summary>
public record ListItem(string Key, string Label);
