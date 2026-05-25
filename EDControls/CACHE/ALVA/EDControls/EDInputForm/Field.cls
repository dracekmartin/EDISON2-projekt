Class ALVA.EDControls.EDInputForm.Field Extends ALVA.EDControls.EDInputForm.Item
{

Property key As %String;

Property label As %String;

/// $$$FieldTypeText or $$$FieldTypeNumber (Include EDInputForm).
Property type As %String [ InitialExpression = "TEXT" ];

Property value As %String;

Property validateOnChange As %Boolean [ InitialExpression = 0 ];

Property required As %Boolean [ InitialExpression = 0 ];

/// Empty string means no error.
Property validationError As %String;

Property editable As %Boolean [ InitialExpression = 1 ];

/// Options for List field type. Each element is $lb(key, label).
Property listItems As list Of %List;

/// Dialog filter for File field type. "$FOLDER" for folder selection,
/// otherwise interpreted as OpenFileDialog filter string.
Property fileFilter As %String;

/// Minimum allowed value for Number / Decimal field types.
/// Empty string = no lower bound. Validated client-side and server-side.
Property min As %String;

/// Maximum allowed value for Number / Decimal field types.
/// Empty string = no upper bound.
Property max As %String;

/// Display format for Decimal field values (e.g. "N2", "0.##").
/// Applied in UI on focus loss. Empty = no formatting.
Property format As %String;

/// Unit shown as suffix next to the input (e.g. "mm", "pt"). Visual only.
/// Empty = no unit suffix.
Property unit As %String;

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
	q $lb("FIELD",..number,..key,..label,..type,..value,..validateOnChange,..required,..validationError,..editable,..visibility,..helpText,listStr,..fileFilter,..min,..max,..format,..unit)
}

}
