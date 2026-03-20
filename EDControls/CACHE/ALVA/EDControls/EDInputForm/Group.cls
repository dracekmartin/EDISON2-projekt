Class ALVA.EDControls.EDInputForm.Group Extends ALVA.EDControls.EDInputForm.Item
{

/// Stable identifier used by AddFieldToGroup / GetGroup lookups.
Property key As %String;

/// Display text shown in the group header on the .NET side.
Property groupName As %String;

/// When 0 the entire group (header + fields) is hidden on the .NET side.
Property isVisible As %Boolean [ InitialExpression = 1 ];

Method Serialize() As %List
{
	q $lb("GROUP",..number,..key,..groupName,$s(..isVisible:"1",1:"0"))
}

}
