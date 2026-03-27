Include EDInputForm

Class ALVA.EDControls.EDInputForm.Item Extends %RegisteredObject [ Abstract ]
{

/// Sequential 1-based number assigned automatically by Form.AddGroup / Form.AddField.
Property number As %Integer [ InitialExpression = 0 ];

/// $$$ItemVisibilityVisible (default), $$$ItemVisibilityCollapsed,
/// $$$ItemVisibilityUserHidden, $$$ItemVisibilityProgrammerHidden.
Property visibility As %String [ InitialExpression = {$$$ItemVisibilityVisible} ];

/// Optional help text displayed next to the item. Empty string means no help text.
Property helpText As %String;

/// Serializes this item to a CList ($lb) for MLRest CVar wire format.
Method Serialize() As %List [ Abstract ]
{
}

}
