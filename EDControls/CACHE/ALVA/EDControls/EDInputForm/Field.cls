Class ALVA.EDControls.EDInputForm.Field Extends ALVA.EDControls.EDInputForm.Item
{

Property key As %String;

Property label As %String;

/// "TEXT" or "NUMBER"
Property type As %String [ InitialExpression = "TEXT" ];

Property value As %String;

Property validateOnChange As %Boolean [ InitialExpression = 0 ];

Property required As %Boolean [ InitialExpression = 0 ];

/// Empty string means no error.
Property validationError As %String;

Method Serialize() As %List
{
	q $lb("FIELD",..number,..key,..label,..type,..value,..validateOnChange,..required,..validationError)
}

}
