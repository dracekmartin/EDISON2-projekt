Class ALVA.EDControls.EDInputForm.Group Extends ALVA.EDControls.EDInputForm.Item
{

Property groupName As %String;

/// When 0 the entire group (header + fields) is hidden on the .NET side.
Property isVisible As %Boolean [ InitialExpression = 1 ];

Method Serialize() As %List
{
	q $lb("GROUP",..groupName,$s(..isVisible:"1",1:"0"))
}

}
