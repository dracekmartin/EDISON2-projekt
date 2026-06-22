Include (GENERAL, EDInputForm)

/// ⚠️ AKTUÁLNĚ NEPOUŽITÉ — stylový pod-formulář jede přes C# CellStyleEditor (StyleEditDialog),
/// který umí tri-state „dědí" placeholdery (EDInputform ploché pole ne). Ponecháno jako server-side
/// varianta, kdyby byla někdy potřeba (StyleFields helper je sdílený a dál se používá pro inline zóny).
///
/// Generické EDInputForm schéma pro samostatnou editaci JEDNOHO TextStyle (pod-formulář stylu).
/// IN["prefix"] = property-path prefix na elementu (HeaderStyle,
/// MinuteStyle, AnotaceSymbolStyle, …), IN["title"] = nadpis. Sadu polí emituje sdílený StyleFields
/// (jediný zdroj balíčku) → každá zóna/element má identický styl-formulář.
Class ALVA.TemplateDesigner.EDInputForm.StyleEdit Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
    try {
        $$$FillParams
        s prefix = $g(^||ML("IN","prefix"))
        s title = $g(^||ML("IN","title"))
        i title = "" s title = "Styl"
        s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()
        d ##class(ALVA.TemplateDesigner.EDInputForm.StyleFields).Add(form, prefix, "style", title)
        d form.WriteToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

ClassMethod Validate(ByRef sys As %String) As %Status
{
    try {
        $$$FillParams
        s form = ##class(ALVA.EDControls.EDInputForm.Form).ReadFromIn()
        d form.WriteToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

ClassMethod Save(ByRef sys As %String) As %Status
{
    q $$$OK
}

}
