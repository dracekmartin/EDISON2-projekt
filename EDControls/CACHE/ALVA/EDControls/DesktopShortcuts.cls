Include GENERAL

/// Zástupci na ploše shellu EDISONMAIN2 - registrační kanál pro legacy agendy.
///
/// Agenda běží jako samostatný proces bez spojení na shell, takže si o zástupce řekne sem;
/// shell tentýž blob čte. Data leží v <class>ALVA.EDControls.UserData</class>
/// (scope "EDISONMAIN2", klíč "Shortcuts") - tahle třída je jen **doménová operace nad ním**,
/// protože přidání jedné položky je read-modify-write a přes generické Save by si souběžné
/// zápisy modulu a shellu přepisovaly celý blob. Tady proběhne v jednom Caché jobu.
///
/// Identita položky = "Kind|ElementId|Id", u MenuModule "MenuModule|ElementId" a "|Input", má-li parametr (mirror DesktopShortcut.Key na klientovi).
/// Pozici ("Col"/"Row") server záměrně **nepřiděluje** - klient chybějící pozici doplní při
/// načtení (viz ModuleShortcutsService.Seed), takže server nemusí znát rozložení mřížky.
Class ALVA.EDControls.DesktopShortcuts Extends %RegisteredObject
{

Parameter SCOPE = "EDISONMAIN2";

Parameter KEY = "Shortcuts";

/// Přidá zástupce, nebo přepíše stávající se stejným klíčem (pozici si ponechá).
/// IN["item"] = JSON objekt jedné položky (Kind, Id, ElementId, Name, Exe, Form, Input, Report)
ClassMethod Add(ByRef sys As %String) As %Status
{
    $$$Init
    try {
        $$$FillParams

        s item = $g(^||ML("IN","item"))
        if item = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "item is required"))

        s obj = ##class(%DynamicObject).%FromJSON(item)
        s key = ..KeyOf(obj)
        if key = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "item has no key"))

        s arr = ..Load(IdFa, userID)
        s replaced = 0
        s out = ##class(%DynamicArray).%New()
        s it = arr.%GetIterator()
        while it.%GetNext(.i, .existing) {
            if ..KeyOf(existing) = key {
                // Pozice dlaždice je uživatelovo nastavení - aktualizace z modulu ji nesmí zahodit.
                if existing.%IsDefined("Col") s obj.Col = existing.Col
                if existing.%IsDefined("Row") s obj.Row = existing.Row
                d out.%Push(obj)
                s replaced = 1
            } else {
                d out.%Push(existing)
            }
        }
        if 'replaced d out.%Push(obj)

        d ..Store(IdFa, userID, out)
        s ^||ML("OUT","count") = out.%Size()
    } catch (ex) { $$$CatchErrRest }
    ret ret
}

/// Odebere zástupce podle klíče. Neexistující položka není chyba.
/// IN["item"] = JSON objekt (stačí Kind, ElementId, Id)
ClassMethod Remove(ByRef sys As %String) As %Status
{
    $$$Init
    try {
        $$$FillParams

        s item = $g(^||ML("IN","item"))
        if item = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "item is required"))

        s key = ..KeyOf(##class(%DynamicObject).%FromJSON(item))
        s arr = ..Load(IdFa, userID)
        s out = ##class(%DynamicArray).%New()
        s it = arr.%GetIterator()
        while it.%GetNext(.i, .existing) {
            if ..KeyOf(existing) '= key d out.%Push(existing)
        }

        d ..Store(IdFa, userID, out)
        s ^||ML("OUT","count") = out.%Size()
    } catch (ex) { $$$CatchErrRest }
    ret ret
}

/// Je zástupce na ploše? OUT["exists"] = 0/1
/// IN["item"] = JSON objekt (stačí Kind, ElementId, Id)
ClassMethod Exists(ByRef sys As %String) As %Status
{
    $$$Init
    try {
        $$$FillParams

        s item = $g(^||ML("IN","item"))
        if item = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "item is required"))

        s key = ..KeyOf(##class(%DynamicObject).%FromJSON(item))
        s found = 0
        s arr = ..Load(IdFa, userID)
        s it = arr.%GetIterator()
        while it.%GetNext(.i, .existing) {
            if ..KeyOf(existing) = key s found = 1 q
        }
        s ^||ML("OUT","exists") = found
    } catch (ex) { $$$CatchErrRest }
    ret ret
}

/// Identita položky - musí sedět s DesktopShortcut.Key na klientovi.
ClassMethod KeyOf(obj As %DynamicObject) As %String
{
    // %Get, ne $g(obj.X) - na chybějící property %DynamicObject hodí <PROPERTY DOES NOT EXIST>.
    if '$isobject(obj) ret ""
    s kind = obj.%Get("Kind")
    if (kind = "") ret ""
    // Varianty modulu menu (Převody AD/MHD) mají shodné ElementId i Id, liší je jen Input.
    if (kind = "MenuModule") {
        s input = obj.%Get("Input")
        ret kind_"|"_obj.%Get("ElementId")_$s(input'="":"|"_input, 1:"")
    }
    ret kind_"|"_obj.%Get("ElementId")_"|"_obj.%Get("Id")
}

/// Uložené položky; chybějící nebo poškozený blob = prázdné pole (plochu nesmí shodit).
ClassMethod Load(pIdFa As %String, pUser As %String) As %DynamicArray
{
    s raw = $g(^ALVA.EDControls.UserData(pIdFa, pUser, ..#SCOPE, ..#KEY))
    if raw = "" ret ##class(%DynamicArray).%New()
    try {
        s arr = ##class(%DynamicArray).%FromJSON(raw)
    } catch {
        s arr = ##class(%DynamicArray).%New()
    }
    ret arr
}

ClassMethod Store(pIdFa As %String, pUser As %String, arr As %DynamicArray)
{
    s ^ALVA.EDControls.UserData(pIdFa, pUser, ..#SCOPE, ..#KEY) = arr.%ToJSON()
}

}
