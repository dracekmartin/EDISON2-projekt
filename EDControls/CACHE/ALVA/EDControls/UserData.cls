Include GENERAL

/// Generic per-user persist API pro EDControls komponenty.
///
/// Storage: ^ALVA.EDControls.UserData(IdFa, userID, scope, key) = <JSON blob>
///
/// (IdFa, userID) se získají automaticky z $$$FillParams (MLRest sys context).
/// (scope, key) předává klient. Hodnota je opaque JSON blob — server nezná strukturu.
///
/// Použití:
///   scope = "EDControls.EDList", key = "TemplateDesigner.TemplatePicker"
///   scope = "EDControls.EDInputForm", key = "TemplateDesigner.SaveDialog"
Class ALVA.EDControls.UserData Extends %RegisteredObject
{

/// Uloží JSON blob pod (IdFa, userID, scope, key). Přepíše existující.
/// IN["scope"] = "EDControls.EDList"
/// IN["key"]   = "TemplateDesigner.TemplatePicker"
/// IN["data"]  = JSON string (libovolná struktura podle komponenty)
ClassMethod Save(ByRef sys As %String) As %Status
{
    try {
        $$$FillParams

        s scope = $g(^||ML("IN","scope"))
        s key   = $g(^||ML("IN","key"))
        s data  = $g(^||ML("IN","data"))

        if scope = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "scope is required"))
        if key   = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "key is required"))

        s ^ALVA.EDControls.UserData(IdFa, userID, scope, key) = data
    } catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Načte JSON blob. Neexistující klíč → prázdný OUT (caller dostane null v .NET).
/// IN["scope"] = "EDControls.EDList"
/// IN["key"]   = "TemplateDesigner.TemplatePicker"
/// OUT["data"] = JSON string (nebo neexistuje pokud klíč není uložený)
ClassMethod Load(ByRef sys As %String) As %Status
{
    try {
        $$$FillParams

        s scope = $g(^||ML("IN","scope"))
        s key   = $g(^||ML("IN","key"))

        if scope = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "scope is required"))
        if key   = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "key is required"))

        if $d(^ALVA.EDControls.UserData(IdFa, userID, scope, key)) {
            s ^||ML("OUT","data") = ^ALVA.EDControls.UserData(IdFa, userID, scope, key)
        }
    } catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Smaže klíč. No-op pokud neexistoval.
ClassMethod Delete(ByRef sys As %String) As %Status
{
    try {
        $$$FillParams

        s scope = $g(^||ML("IN","scope"))
        s key   = $g(^||ML("IN","key"))

        if scope = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "scope is required"))
        if key   = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "key is required"))

        k ^ALVA.EDControls.UserData(IdFa, userID, scope, key)
    } catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

}
