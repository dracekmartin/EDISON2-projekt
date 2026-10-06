Include EDInputForm

Class ALVA.EDControls.EDInputForm.Item Extends %RegisteredObject [ Abstract ]
{

/// Pořadové 1-based číslo, přiděluje automaticky Form.AddGroup / Form.AddField.
Property number As %Integer [ InitialExpression = 0 ];

/// $$$ItemVisibilityVisible (default), $$$ItemVisibilityCollapsed, $$$ItemVisibilityProgrammerHidden;
/// $$$ItemVisibilityUserHidden je rezervované (žádné UI ho nenastavuje).
Property visibility As %String [ InitialExpression = {$$$ItemVisibilityVisible} ];

/// Volitelná nápověda vedle položky. Prázdný řetězec = bez nápovědy.
Property helpText As %String;

/// Serializuje položku do CListu ($lb) pro wire formát MLRest CVar.
Method Serialize() As %List [ Abstract ]
{
}

}
