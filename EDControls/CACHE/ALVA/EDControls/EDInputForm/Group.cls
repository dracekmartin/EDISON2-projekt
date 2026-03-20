Class ALVA.EDControls.EDInputForm.Group Extends ALVA.EDControls.EDInputForm.Item
{

/// Stable identifier used by AddFieldToGroup / GetGroup lookups.
Property key As %String;

/// Display text shown in the group header on the .NET side.
Property groupName As %String;

Method Serialize() As %List
{
	q $lb("GROUP",..number,..key,..groupName,..visibility)
}

}
