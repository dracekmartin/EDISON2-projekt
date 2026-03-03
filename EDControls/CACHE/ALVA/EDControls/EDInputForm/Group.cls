Class ALVA.EDControls.EDInputForm.Group Extends ALVA.EDControls.EDInputForm.Item
{

Property groupName As %String;

Method Serialize(del As %String) As %String
{
	q "GROUP"_del_..groupName
}

}
