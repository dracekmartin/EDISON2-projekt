Include GENERAL

/// Sonda (`B192`): hledá dvojice verzí téže linky, na kterých je rozdílový linkový JŘ vidět -
/// změněné, nové nebo zrušené spoje, jiné značky, dopravci nebo trasa. Jen čte; spouští se ručně
/// přes MLRest.
Class ALVA.TemplateDesigner.Tests.RozdilovyJrProbe Extends %RegisteredObject
{

/// IN: NOVE = skupiny nových verzí („306,307"), PUVODNI = skupiny, kde hledat původní verzi.
/// OUT(n) = JSON { nova, puvodni, linka, zmenene, nove, zrusene, znacky, dopravci, zastavky } za
/// dvojici s aspoň jedním rozdílem. `znacky` = spoje se stejnými časy a jinými značkami, `dopravci` = spoje z obou verzí, které jede v každé jiný dopravce,
/// `zastavky` = řádky zastávek jen v jedné verzi (změna trasy, `B192d`).
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

/// Pořadí spojů ve sloučené tabulce jedné dvojice - na ověření kroku `B192` na skutečných datech.
/// IN: NOVA, PUVODNI = skupiny JŘ, LINKA. OUT(n) = stránka jako „směr: číslo:verze, …"
/// (verze p = původní, n = nová, z = jen jiné značky, bez písmene beze změny), nebo chyba porovnání.
ClassMethod PrehledDvojice(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s agenda = "ALVA.TemplateDesigner.Agendy.LinkovyJrAdRozdilovy"
        s lin = $g(^||ML("IN","LINKA")), skuP = $g(^||ML("IN","PUVODNI"))
        s vstup("SKUPINA_LINEK_ID") = $g(^||ML("IN","NOVA")), vstup("LINKA_ID") = lin
        s vstup("DELENI_SLOUPCU") = "Bez rozlišení"
        d $classmethod(agenda, "NactiStranky", .sys, .vstup, "", "", .stranky)
        d $classmethod(agenda, "NactiStranky", .sys, .vstup, skuP, lin, .puvodni)
        k ^||ML("OUT")
        f i=1:1:$g(stranky) {
            s protejsek = $classmethod(agenda, "ProtejsiStranka", stranky(i), i, .puvodni)
            i protejsek = "" { s ^||ML("OUT", i) = "bez protějšku" continue }
            s dopravci = " | dopravci nová: "_..JmenaDopravcu(stranky(i))_" | původní: "_..JmenaDopravcu(protejsek)
            try {
                s stranka = $classmethod(agenda, "SlucStranky", stranky(i), protejsek)
            } catch exSlouceni {
                s ^||ML("OUT", i) = "neporovnáno: "_exSlouceni.DisplayString()
                continue
            }
            s radek = "", it = stranka.%Get("rows").%Get("SPOJE").%GetIterator()
            while it.%GetNext(.k, .spoj) {
                s verze = $s(spoj.%Get("VERZE") = "puvodni": "p", spoj.%Get("VERZE") = "nova": "n", ..JenZnacky(spoj): "z", 1: "")
                s radek = radek_$s(radek = "": "", 1: ", ")_spoj.%Get("CISLO_SPOJE")_":"_verze
            }
            s trasa = "", it = stranka.%Get("rows").%Get("ZASTAVKY").%GetIterator()
            while it.%GetNext(.k, .z) {
                continue:z.%Get("VERZE")=""
                s trasa = trasa_" "_$s(z.%Get("VERZE") = "nova": "+", 1: "-")_z.%Get("NAZEV_ZASTAVKY")
            }
            s ^||ML("OUT", i) = stranka.%Get("values").%Get("SMER")_": "_radek_" | zrušené "_stranka.%Get("values").%Get("ZRUSENE_SPOJE")_" | trasa"_trasa_dopravci
        }
    } catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Porovná jednu dvojici; "" = chyba nebo žádný rozdíl.
ClassMethod Porovnej(ByRef sys As %String, skuN As %String, skuP As %String, lin As %String) As %String
{
    s agenda = "ALVA.TemplateDesigner.Agendy.LinkovyJrAdRozdilovy"
    s vstup("SKUPINA_LINEK_ID") = skuN, vstup("LINKA_ID") = lin, vstup("DELENI_SLOUPCU") = "Bez rozlišení"
    s (zmenene, nove, zrusene, znacky, dopravci, zastavky) = 0
    try {
        d $classmethod(agenda, "NactiStranky", .sys, .vstup, "", "", .stranky)
        d $classmethod(agenda, "NactiStranky", .sys, .vstup, skuP, lin, .puvodni)
        f i=1:1:$g(stranky) {
            s protejsek = $classmethod(agenda, "ProtejsiStranka", stranky(i), i, .puvodni)
            continue:protejsek=""
            k dopNove, dopPuvodni
            d ..DopravciSpoju(stranky(i), .dopNove), ..DopravciSpoju(protejsek, .dopPuvodni)
            s cislo = ""
            f {
                s cislo = $o(dopNove(cislo), 1, text)
                q:cislo=""
                i $d(dopPuvodni(cislo)) && (dopPuvodni(cislo) '= text) { s dopravci = dopravci + 1 }
            }
            s stranka = $classmethod(agenda, "SlucStranky", stranky(i), protejsek)
            s it = stranka.%Get("rows").%Get("ZASTAVKY").%GetIterator()
            while it.%GetNext(.k, .z) { s:z.%Get("VERZE")'="" zastavky = zastavky + 1 }
            // Změněný spoj = původní hned před novou podobou téhož čísla. Samotná původní verze je
            // od `B192a` i zrušený spoj, ten se počítá ze ZRUSENE_SPOJE.
            s it = stranka.%Get("rows").%Get("SPOJE").%GetIterator(), predchozi = ""
            while it.%GetNext(.k, .spoj) {
                s:..JenZnacky(spoj) znacky = znacky + 1
                i spoj.%Get("VERZE") = "nova" {
                    i $isobject(predchozi) && (predchozi.%Get("VERZE") = "puvodni") && (predchozi.%Get("CISLO_SPOJE") = spoj.%Get("CISLO_SPOJE")) {
                        s zmenene = zmenene + 1
                    } else {
                        s nove = nove + 1
                    }
                }
                s predchozi = spoj
            }
            s seznam = stranka.%Get("values").%Get("ZRUSENE_SPOJE")
            i seznam '= "" s zrusene = zrusene + $l(seznam, ",")
        }
    } catch (ex) {
        ret ""
    }
    q:(zmenene + nove + zrusene + znacky + dopravci + zastavky)=0 ""
    s o = ##class(%DynamicObject).%New()
    d o.%Set("nova", skuN), o.%Set("puvodni", skuP), o.%Set("linka", lin)
    d o.%Set("zmenene", zmenene, "number"), o.%Set("nove", nove, "number"), o.%Set("zrusene", zrusene, "number")
    d o.%Set("znacky", znacky, "number"), o.%Set("dopravci", dopravci, "number"), o.%Set("zastavky", zastavky, "number")
    q o.%ToJSON()
}

/// Různá jména dopravců spojů stránky, oddělená „; ".
ClassMethod JmenaDopravcu(stranka As %DynamicObject) As %String
{
    d ..DopravciSpoju(stranka, .poSpojich)
    s cislo = ""
    f {
        s cislo = $o(poSpojich(cislo), 1, text)
        q:cislo=""
        s:text'="" jmena(text) = ""
    }
    s out = "", text = ""
    f {
        s text = $o(jmena(text))
        q:text=""
        s out = out_$s(out = "": "", 1: "; ")_$e(text, 2, $l(text))
    }
    q out
}

/// Dopravci stránky po spojích: poSpojich(číslo spoje) = jména dopravců. Liší-li se u spoje, který
/// je v obou verzích, je co porovnat v hlavičce (`B192f`).
ClassMethod DopravciSpoju(stranka As %DynamicObject, ByRef poSpojich)
{
    s it = stranka.%Get("rows").%Get("SPOJE").%GetIterator()
    while it.%GetNext(.k, .spoj) {
        s dopravci = spoj.%Get("DOPRAVCI"), text = ""
        i $isobject(dopravci) {
            s it2 = dopravci.%GetIterator()
            while it2.%GetNext(.k2, .nazev) { s text = text_"/"_nazev }
        }
        s poSpojich(+spoj.%Get("CISLO_SPOJE")) = text
    }
}

/// Spoj se stejnými časy, kterému se změnily jen značky (`B192c`)?
ClassMethod JenZnacky(spoj As %DynamicObject) As %Boolean
{
    q $isobject(spoj.%Get("PLATNOST_VERZE")) || (spoj.%Get("NZ_VERZE") '= "") || (spoj.%Get("NZ_PUVODNI") '= "")
}

}
