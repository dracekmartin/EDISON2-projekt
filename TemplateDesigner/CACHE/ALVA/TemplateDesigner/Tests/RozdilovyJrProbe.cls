Include GENERAL

/// Sonda (`B192`): hledá dvojice verzí téže linky, na kterých je rozdílový linkový JŘ vidět -
/// stejná trasa a nějaké změněné, nové nebo zrušené spoje. Jen čte; spouští se ručně přes MLRest.
Class ALVA.TemplateDesigner.Tests.RozdilovyJrProbe Extends %RegisteredObject
{

/// IN: NOVE = skupiny nových verzí („306,307"), PUVODNI = skupiny, kde hledat původní verzi.
/// OUT(n) = JSON { nova, puvodni, linka, zmenene, nove, zrusene } za dvojici se stejnou trasou
/// a aspoň jedním rozdílem; dvojice s jinou trasou se přeskočí.
ClassMethod NajdiDvojice(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s skupinyNove = $g(^||ML("IN","NOVE")), skupinyPuvodni = $g(^||ML("IN","PUVODNI"))
        k vysledky
        f i=1:1:$l(skupinyNove, ",") {
            s skuN = $p(skupinyNove, ",", i)
            s lin = ""
            f {
                s lin = $o(^[dataDB]JRL(skuN, lin))
                q:lin=""
                continue:'$d(^[dataDB]JRL(skuN, lin, "I"))
                f j=1:1:$l(skupinyPuvodni, ",") {
                    s skuP = $p(skupinyPuvodni, ",", j)
                    continue:(skuP = skuN)||('$d(^[dataDB]JRL(skuP, lin, "I")))
                    s radek = ..Porovnej(.sys, skuN, skuP, lin)
                    i radek '= "" s vysledky($i(vysledky)) = radek
                }
            }
        }
        k ^||ML("OUT")
        f i=1:1:$g(vysledky) s ^||ML("OUT", i) = vysledky(i)
    } catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Porovná jednu dvojici; "" = jiná trasa, chyba nebo žádný rozdíl.
ClassMethod Porovnej(ByRef sys As %String, skuN As %String, skuP As %String, lin As %String) As %String
{
    s agenda = "ALVA.TemplateDesigner.Agendy.LinkovyJrAdRozdilovy"
    s vstup("SKUPINA_LINEK_ID") = skuN, vstup("LINKA_ID") = lin, vstup("DELENI_SLOUPCU") = "Bez rozlišení"
    s (zmenene, nove, zrusene) = 0
    try {
        d $classmethod(agenda, "NactiStranky", .sys, .vstup, "", "", .stranky)
        d $classmethod(agenda, "NactiStranky", .sys, .vstup, skuP, lin, .puvodni)
        f i=1:1:$g(stranky) {
            s protejsek = $classmethod(agenda, "ProtejsiStranka", stranky(i), i, .puvodni)
            continue:protejsek=""
            s stranka = $classmethod(agenda, "SlucStranky", stranky(i), protejsek)
            s it = stranka.%Get("rows").%Get("SPOJE").%GetIterator()
            while it.%GetNext(.k, .spoj) {
                s verze = spoj.%Get("VERZE")
                i verze = "puvodni" s zmenene = zmenene + 1
                i verze = "nova" s nove = nove + 1
            }
            s seznam = stranka.%Get("values").%Get("ZRUSENE_SPOJE")
            i seznam '= "" s zrusene = zrusene + $l(seznam, ",")
        }
    } catch (ex) {
        ret ""
    }
    s nove = nove - zmenene
    q:(zmenene + nove + zrusene)=0 ""
    s o = ##class(%DynamicObject).%New()
    d o.%Set("nova", skuN), o.%Set("puvodni", skuP), o.%Set("linka", lin)
    d o.%Set("zmenene", zmenene, "number"), o.%Set("nove", nove, "number"), o.%Set("zrusene", zrusene, "number")
    q o.%ToJSON()
}

}
