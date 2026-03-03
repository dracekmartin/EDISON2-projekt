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

Method Serialize(del As %String) As %String
{
	q "FIELD"_del_..key_del_..label_del_..type_del_..value_del_..validateOnChange_del_..required_del_..validationError
}

}
