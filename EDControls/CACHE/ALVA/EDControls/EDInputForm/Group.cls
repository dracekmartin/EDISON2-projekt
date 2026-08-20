Class ALVA.EDControls.EDInputForm.Group Extends ALVA.EDControls.EDInputForm.Item
{

/// Stabilní identifikátor pro lookup AddFieldToGroup / GetGroup.
Property key As %String;

Property groupName As %String;

Method Serialize() As %List
{
	q $lb("GROUP",..number,..key,..groupName,..visibility,..helpText)
}

}
