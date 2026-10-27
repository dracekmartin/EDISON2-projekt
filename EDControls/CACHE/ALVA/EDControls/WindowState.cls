Include GENERAL

/// Reset uložené geometrie oken aplikací na EDControls (shell EDISON2, TD).
///
/// Geometrii ukládá .NET CacheWindowStateStore přes legacy ALVA.LIBRARY.Form.SaveParams do číselníku
/// LIBRARY/Form pod formID~state~userID. Legacy ResetSettings maže jen "props" (pozice WinForms agend).
/// Celý "state" smazat nejde, drží i uložené parametry legacy formulářů (EDSaveParams).
Class ALVA.EDControls.WindowState Extends %RegisteredObject
{

/// Smaže uloženou geometrii oken aktuálního uživatele v aktuální IdFa, jejichž formID začíná "<prefix>.".
/// IN["prefixes"] = $lb("EDISON2","TD") (prefixy FormIdPrefix aplikací)
/// OUT["deleted"] = počet formID, kterým se geometrie smazala
ClassMethod ResetUserStates(ByRef sys As %String) As %Status
{
    $$$Init
    try {
        $$$FillParams

        s prefixes = $g(^||ML("IN","prefixes"))
        if '$listvalid(prefixes) || ($ll(prefixes) = 0) $$$ThrowStatus($$$ERROR($$$GeneralError, "prefixes is required"))
        for i = 1:1:$ll(prefixes) {
            if $lg(prefixes, i) = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "prefix must not be empty"))
        }

        k ^||EDControlsWindowState
        k aIn, aOut
        s aIn("IdFa") = IdFa
        s aIn("oblast") = "LIBRARY"
        s aIn("ciselnik") = "Form"
        s aIn("global") = "^||EDControlsWindowState"
        d ##class(ISML.SYS.MLFirma.API.Ciselnik).Load(.aIn, .aOut)
        if $g(aOut("err")) '= "" $$$ThrowStatus($$$ERROR($$$GeneralError, aOut("err")))

        s deleted = 0, formID = ""
        for {
            s formID = $o(^||EDControlsWindowState("Dta", formID)) q:formID=""
            // "~" ve formID by posunul indexovou cestu a Delete by smazal cizí uzel.
            continue:((formID [ "~") || '..HasPrefix(formID, prefixes))
            continue:'$d(^||EDControlsWindowState("Dta", formID, "state", userID))

            k aIn, aOut
            s aIn("IdFa") = IdFa
            s aIn("oblast") = "LIBRARY"
            s aIn("ciselnik") = "Form"
            s aIn("indexPath") = formID_"~state~"_userID
            s aIn("pathDelimiter") = "~"
            d ##class(ISML.SYS.MLFirma.API.Ciselnik).Delete(.aIn, .aOut)
            if $g(aOut("err")) '= "" $$$ThrowStatus($$$ERROR($$$GeneralError, aOut("err")))
            s deleted = deleted + 1
        }
        s ^||ML("OUT","deleted") = deleted
    } catch (ex) { $$$CatchErrRest }
    k ^||EDControlsWindowState
    ret ret
}

ClassMethod HasPrefix(formID As %String, prefixes As %List) As %Boolean [ Private ]
{
    for i = 1:1:$ll(prefixes) {
        s prefix = $lg(prefixes, i)_"."
        ret:($e(formID, 1, $l(prefix)) = prefix) 1
    }
    ret 0
}

}
