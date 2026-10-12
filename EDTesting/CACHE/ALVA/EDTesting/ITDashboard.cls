Include (GENERAL, ALVAMINIAPLIKACE)

/// Testovací Přehled (legacy miniaplikace) pro integrační test ALVA.EDISON.MiniAplikaceAPI.LoadData:
/// zapíše ho tam, odkud ho LoadData čte, a zase smaže. Pracuje jen pod testovací firmou.
Class ALVA.EDTesting.ITDashboard Extends %RegisteredObject
{

Parameter TestIdFa = "TEST_EDISON2_IT";

/// IN["appType"] = typ miniaplikace. Zapíše label, tabulku se dvěma řádky (druhý se stylem) a label za tabulkou.
ClassMethod Seed(ByRef sys As %String) As %Status
{
    $$$Init
    try {
        $$$FillParams
        s appType = ..AppType(IdFa)

        // LoadData čte uživatele 0, nebo přihlášeného, když má server zapnuté funkční omezení: zapisuje se oběma.
        for appUserID = 0, userID {
            s glb = $na(^[##class(ALVA.EDISON.MiniAplikaceAPI).GetDatabase()]ALVA.MINIAPLIKACE(IdFa,"data",appType,appUserID))
            k @glb
            s @glb@("param",$$$ParamTimeStamp) = $h
            s @glb@("data",1,$$$IndexType) = $$$ObjectLabel
            s @glb@("data",1,$$$IndexData) = "IT nadpis"
            s @glb@("data",2,$$$IndexType) = $$$ObjectTable
            s @glb@("data",2,$$$IndexData) = "T:N2#"
            s @glb@("data",2,$$$IndexData,"th") = "Nazev~Hodnota"
            s @glb@("data",2,$$$IndexData,"tr",1) = "prvni~1"
            s @glb@("data",2,$$$IndexData,"tr",2) = "druhy~2"
            s @glb@("data",2,$$$IndexData,"tr",2,"style") = $lb(1,"#FF0000")
            s @glb@("data",3,$$$IndexType) = $$$ObjectLabel
            s @glb@("data",3,$$$IndexData) = "IT poznamka"
        }
    } catch (ex) { $$$CatchErrRest }
    ret ret
}

/// IN["appType"] = typ miniaplikace, jehož Přehled se smaže.
ClassMethod Clean(ByRef sys As %String) As %Status
{
    $$$Init
    try {
        $$$FillParams
        s appType = ..AppType(IdFa)
        k ^[##class(ALVA.EDISON.MiniAplikaceAPI).GetDatabase()]ALVA.MINIAPLIKACE(IdFa,"data",appType)
    } catch (ex) { $$$CatchErrRest }
    ret ret
}

/// Jiná firma = chyba, ať test nepřepíše ani nesmaže Přehledy skutečné firmy.
ClassMethod AppType(IdFa As %String) As %String [ Private ]
{
    if IdFa '= ..#TestIdFa $$$ThrowStatus($$$ERROR($$$GeneralError, "ITDashboard pracuje jen pod firmou "_..#TestIdFa))
    s appType = $g(^||ML("IN","appType"))
    if appType = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "appType is required"))
    quit appType
}

}
