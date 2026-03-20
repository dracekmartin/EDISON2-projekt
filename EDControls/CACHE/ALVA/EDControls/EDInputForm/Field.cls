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

Method Serialize() As %List
{
	q $lb("FIELD",..number,..key,..label,..type,..value,..validateOnChange,..required,..validationError,..editable,..visibility)
}

}
