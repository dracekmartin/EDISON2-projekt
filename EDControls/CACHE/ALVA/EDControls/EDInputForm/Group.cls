Class ALVA.EDControls.EDInputForm.Group Extends ALVA.EDControls.EDInputForm.Item
{

Property groupName As %String;

Method Serialize() As %List
{
	q $lb("GROUP",..groupName)
}

}
