Include GENERAL

/// Sdílení šablon mezi firmami přes sdílení dat ISYCloudu (`B189`, rozhodnutí R9 v `architektonicka-rozhodnuti-agendy.md`).
/// Spojení s ISYCloudem drží obecný `Sdileni.Klient`; tady je jen to, co je o šablonách.
Class ALVA.TemplateDesigner.API.SdileniSablon Extends %RegisteredObject
{

/// Stav sdílení firmy: OUT["1"] = {aktivni, nazev, duvod}. Nevyhazuje chybu - firma bez povoleného sdílení
/// dostane důvod pro uživatele (akce zůstane vidět a řekne, kdo sdílení povoluje).
ClassMethod GetStav(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s ^||ML("OUT","1") = ##class(ALVA.TemplateDesigner.Sdileni.Klient).Stav(IdFa).%ToJSON()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

}
