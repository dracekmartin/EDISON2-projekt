/// Vlastní sloupce spojů (`B56` S13, `architektonicka-rozhodnuti-agendy.md` R8): definici skládá uživatel
/// v šabloně a posílá se do `GetData` jako `DEFINICE_SLOUPCU`. Sdílí ji vývěska AD i linkový JŘ AD.
/// **Význam definice musí být pro všechny agendy tentýž** - jinak by táž definice udělala
/// ve vývěsce jiné sloupce než v linkovém JŘ. Liší se až to, co s výsledkem prvek udělá:
/// vývěsce je skupina **jeden sloupec**, linkovému JŘ **pás nad několika sloupci spojů**
/// (a spoj patřící do dvou skupin se vypíše dvakrát). Do 30. 10. byla v `BaseAgenda` a podmínky
/// zařazení spoje měla každá agenda opsané.
Class ALVA.TemplateDesigner.Agendy.Jr.VlastniSloupce Extends %RegisteredObject
{

/// Vlastní definice sloupců (parametr DEFINICE_SLOUPCU, JSON ze šablony) → lokální pole.
/// Vrací počet sloupců; 0 = bez definice / nevalidní JSON → režim „automaticky dle linky".
/// defArr(i,"id"|"nadpis"|"nadpis2"|"dny"|"jen"|"bez") + defArr("nevyp",id) pro potlačení značek;
/// seznamy značek normalizované do tvaru ",1,2," (containment test přes [).
ClassMethod ParseDefiniceSloupcu(ByRef defArr) As %Integer
{
    k defArr
    s json = $g(^||ML("IN","DEFINICE_SLOUPCU"))
    i json = "" q 0
    s def = ""
    try { s def = ##class(%DynamicObject).%FromJSON(json) } catch (ex) { ret 0 }
    i ('$isobject(def)) || ('def.%IsA("%Library.DynamicObject")) q 0
    s sloupce = def.%Get("sloupce")
    i ('$isobject(sloupce)) || ('sloupce.%IsA("%Library.DynamicArray")) q 0
    s n = 0
    s iter = sloupce.%GetIterator()
    while iter.%GetNext(.key, .sl) {
        i '$isobject(sl) continue
        s n = n + 1
        s id = sl.%Get("id")
        i id = "" s id = n
        s defArr(n,"id")      = id
        s defArr(n,"nadpis")  = sl.%Get("nadpis")
        s defArr(n,"nadpis2") = sl.%Get("nadpis2")
        s defArr(n,"dny")     = sl.%Get("dny")
        s defArr(n,"jen")     = ..FormatSeznamZnacek(sl.%Get("znackyJen"))
        s defArr(n,"bez")     = ..FormatSeznamZnacek(sl.%Get("znackyBez"))
        // období se drží v původním textovém tvaru - rok se doplňuje až proti platnosti JŘ
        s defArr(n,"obdobi")  = sl.%Get("obdobi")
        s defArr("nevyp",id)  = ..FormatSeznamZnacek(sl.%Get("nevypisovatZnacky"))
    }
    s defArr = n
    q n
}

/// CSV seznam čísel značek → normalizovaný tvar ",1,2," (ořez mezer, +normalizace čísel).
/// Prázdný/bezobsažný vstup → "". Do 30. 10. `BaseAgenda.NormalizujSeznamZnacek`.
ClassMethod FormatSeznamZnacek(seznam As %String) As %String
{
    i $g(seznam) = "" q ""
    s out = ","
    f i=1:1:$l(seznam,",") {
        s cislo = $zstrip($p(seznam,",",i),"<>W")
        i cislo = "" continue
        s out = out_(+cislo)_","
    }
    q $s(out=",":"", 1:out)
}

/// Sedí spoj do vlastního sloupce `i`? Dny (průnik s týdenní platností) → značky „jen s těmito"
/// (CNZ musí být v seznamu; spoj bez CNZ nesedí) → značky „bez těchto" (CNZ v seznamu vyřazuje).
/// Nevyplněná podmínka neomezuje. Osu Období vyhodnocuje jen vývěska (má k ní podklad z ISY).
/// Do 30. 10. měla tyto podmínky vývěska i linkový JŘ AD každá opsané.
ClassMethod JeSpojVeSloupci(ByRef defArr, i As %Integer, dnySpoje As %String, cnz As %String) As %Boolean
{
    i '..SedneDoSloupceDny(dnySpoje, defArr(i,"dny")) q 0
    i (defArr(i,"jen") '= "") && ((cnz = "") || (defArr(i,"jen") '[ (","_(+cnz)_","))) q 0
    i (defArr(i,"bez") '= "") && (cnz '= "") && (defArr(i,"bez") [ (","_(+cnz)_",")) q 0
    q 1
}

/// Sedí spoj do sloupce podle dnů? Sloupec bez dnů ("") neomezuje - sedí každý spoj
/// (kontrakt „nevyplněná podmínka neomezuje"). Spoj bez omezení ("") sedí vždy; "-" nikdy;
/// jinak stačí průnik alespoň jednoho dne.
ClassMethod SedneDoSloupceDny(dnySpoje As %String, sloupecDny As %String) As %Boolean
{
    i sloupecDny = "" q 1
    i dnySpoje = "" q 1
    i dnySpoje = "-" q 0
    // „X" (pracovní dny) se pro **zařazení** chová jako 1-5: spoj jedoucí jen v pondělí do sloupce
    // pracovních dnů patří (jen ho nepokrývá celý, takže dostane značku). Od kalendářního „12345"
    // se „X" liší až na ose Období, kde svátek do pracovních dnů nepadne - `DenPatriDoSloupce`.
    i sloupecDny [ "X" s sloupecDny = sloupecDny_"12345"
    // „+" znamená „neděle a svátky" i u sloupce, ne jen svátky - jinak by sloupec „Neděle a svátky"
    // musel vyjmenovat „7+" a spoj jedoucí jen v neděli by z pouhého „+" vypadl.
    i (sloupecDny [ "+") && (sloupecDny '[ "7") s sloupecDny = sloupecDny_"7"
    f i=1:1:$l(dnySpoje) {
        i sloupecDny [ $e(dnySpoje,i) ret 1
    }
    q 0
}

}
