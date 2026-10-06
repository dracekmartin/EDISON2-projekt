Class ALVA.EDControls.EDInputForm.Field Extends ALVA.EDControls.EDInputForm.Item
{

Property key As %String;

Property label As %String;

/// $$$FieldTypeText / $$$FieldTypeNumber ... (Include EDInputForm).
Property type As %String [ InitialExpression = "TEXT" ];

Property value As %String;

Property validateOnChange As %Boolean [ InitialExpression = 0 ];

Property required As %Boolean [ InitialExpression = 0 ];

/// Prázdný řetězec = bez chyby.
Property validationError As %String;

Property editable As %Boolean [ InitialExpression = 1 ];

/// Položky pro typ List. Každý prvek je $lb(key, label).
Property listItems As list Of %List;

/// Filtr dialogu pro typ File. "$FOLDER" = výběr složky, jinak filtr OpenFileDialogu.
Property fileFilter As %String;

/// Dolní mez pro typ Number / Decimal. Prázdný řetězec = bez meze (validuje klient i server).
Property min As %String;

/// Horní mez pro typ Number / Decimal. Prázdný řetězec = bez meze.
Property max As %String;

/// Rezervováno: klient ho přenáší (pozice 17), ale hodnotu nijak neformátuje - nenastavovat.
/// Implementace až pro skutečného konzumenta.
Property format As %String;

/// Jednotka jako suffix za polem (např. "mm", "pt"), jen vizuální. Prázdné = bez jednotky.
Property unit As %String;

/// Textové pole přijímá datové placeholdery {POLE} - klient u něj ukáže „vložit pole".
/// SEZNAM polí server neposílá (nezná agendu šablony), plní ho klient do listItems.
Property placeholders As %Boolean [ InitialExpression = 0 ];

Method AddListItem(key As %String, label As %String)
{
	d ..listItems.Insert($lb(key, label))
}

Method ClearListItems()
{
	d ..listItems.Clear()
}

Method Serialize() As %List
{
	s listStr = ""
	i ..listItems.Count()>0 {
		s cnt = ..listItems.Count()
		f idx=1:1:cnt {
			s pair = ..listItems.GetAt(idx)
			s key = $li(pair,1), label = $li(pair,2)
			s:listStr'="" listStr = listStr _ $$$ListItemSep
			s listStr = listStr _ key _ $$$ListKeyValSep _ label
		}
	}
	q $lb("FIELD",..number,..key,..label,..type,..value,..validateOnChange,..required,..validationError,..editable,..visibility,..helpText,listStr,..fileFilter,..min,..max,..format,..unit,..placeholders)
}

}
