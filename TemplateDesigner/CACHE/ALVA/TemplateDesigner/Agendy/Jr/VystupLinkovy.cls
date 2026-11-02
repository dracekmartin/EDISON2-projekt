/// Stavba výstupu linkového JŘ, společná linkovému JŘ AD i MHD (`B56` S4,
/// `architektonicka-rozhodnuti-agendy.md` R8): poznámky legendy, skupiny dnů a časy ve tvaru, který čte
/// Tabulka spojů a Poznámky (`KontraktDat.cs`). Data dostává parametry, do pracovního globálu agendy
/// nesahá (`cache-api.md` pravidlo 6). Do 30. 10. měla každá linková agenda vlastní kopii.
/// ⚠️ Poznámka linkového JŘ má jiný tvar než poznámka vývěsky (`DRUH` místo kategorie a barev), proto je
/// tu a ne v `Jr.VystupZastavkovy`.
Class ALVA.TemplateDesigner.Agendy.Jr.VystupLinkovy Extends %RegisteredObject
{

/// JSON objekt jedné poznámky legendy.
ClassMethod BuildPoznamka(druh As %Integer, typ As %String, znak As %String, text As %String) As %DynamicObject
{
    s o = ##class(%DynamicObject).%New()
    d o.%Set("DRUH", druh)
    d o.%Set("TYP", typ)
    d o.%Set("ZNAK", znak)
    d o.%Set("TEXT", text)
    q o
}

/// JSON objekt jedné skupiny dnů (blok spojů v Tabulce spojů).
ClassMethod BuildSkupina(id As %Integer, nadpis As %String) As %DynamicObject
{
    s o = ##class(%DynamicObject).%New()
    d o.%Set("ID", id)
    d o.%Set("NADPIS", nadpis)
    q o
}

/// Formát času HH:MM z minut od půlnoci.
ClassMethod FormatCas(minuty As %Integer) As %String
{
    s hod = (minuty\60)#24, min = minuty#60
    q $tr($j(hod,2)," ",0)_":"_$tr($j(min,2)," ",0)
}

}
