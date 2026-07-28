Include GENERAL

/// Obecné nastavení TemplateDesigneru uložené v Caché (per firma).
///
/// Globál: ^ALVA.TemplateDesigner.Config(IdFa, sekce, klíč) = hodnota.
/// Pattern: vestavěné defaulty + override z globálu - accessor vrátí hodnotu z globálu,
/// a když chybí, vestavěný default. Zero-config tedy funguje bez seed migrace a firma
/// může přepsat. První konzument = formáty stránky (PageEdit); další TD config reusne.
Class ALVA.TemplateDesigner.API.Settings Extends %RegisteredObject
{

/// Vestavěné výchozí formáty stránky (fallback). $lb($lb(code, šířka, výška, popis), ...), rozměry portrait v mm.
ClassMethod DefaultPrintFormats() As %List
{
    q $lb($lb("A3","297","420","A3"),$lb("A4","210","297","A4"),$lb("A5","148","210","A5"),$lb("A6","105","148","A6"),$lb("Letter","216","279","Letter"),$lb("Legal","216","356","Legal"))
}

/// Formáty stránky pro firmu: per-IdFa override z globálu, jinak vestavěné defaulty.
/// Vrací $lb($lb(code, šířka, výška, popis), ...). Caché-internal (PageEdit volá přímo).
ClassMethod PrintFormats(idFa As %String) As %List
{
    s out = ""
    s code = ""
    f  {
        s code = $o(^ALVA.TemplateDesigner.Config(idFa,"PrintFormats",code))
        q:code=""
        s rec = ^ALVA.TemplateDesigner.Config(idFa,"PrintFormats",code)
        s out = out_$lb($lb(code,$lg(rec,1),$lg(rec,2),$lg(rec,3)))
    }
    i out'="" q out
    q ..DefaultPrintFormats()
}

/// Rozměry konkrétního formátu jako "šířka~výška" (portrait), nebo "" když neznámý.
ClassMethod FormatDims(idFa As %String, code As %String) As %String
{
    s list = ..PrintFormats(idFa)
    f i=1:1:$ll(list) {
        s rec = $li(list,i)
        i $lg(rec,1)=code RETURN $lg(rec,2)_"~"_$lg(rec,3)
    }
    q ""
}

/// Generický getter skalárního nastavení (sekce/klíč), s defaultem.
ClassMethod GetValue(idFa As %String, section As %String, key As %String, default As %String = "") As %String
{
    q $g(^ALVA.TemplateDesigner.Config(idFa,section,key),default)
}

/// Generický setter skalárního nastavení.
ClassMethod SetValue(idFa As %String, section As %String, key As %String, value As %String) As %Status
{
    s ^ALVA.TemplateDesigner.Config(idFa,section,key) = value
    q $$$OK
}

}
