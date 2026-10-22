Include GENERAL

/// Sonda (`B191a`): hledá linky MHD, na kterých je linkový JŘ MHD co porovnávat se starým modulem -
/// negativní značky, poznámky určené pro knižní tisk, zóny, příjezd a odjezd. Jen čte globály
/// modulu MHD; spouští se ručně přes MLRest.
Class ALVA.TemplateDesigner.Tests.LinkovyJrMhdProbe Extends %RegisteredObject
{

/// Firmy s daty modulu MHD na tomto serveru. OUT(n) = JSON { namespace, idfa, linky }.
ClassMethod Firmy(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        k ^||ML("OUT")
        d ##class(%SYS.Namespace).ListAll(.seznam)
        s ns = ""
        f {
            s ns = $o(seznam(ns))
            q:ns=""
            continue:ns["^"
            s firma = ""
            try {
                f {
                    s firma = $o(^[ns]ALVA.MHD.LSO(firma))
                    q:firma=""
                    s o = ##class(%DynamicObject).%New()
                    d o.%Set("namespace", ns), o.%Set("idfa", firma)
                    d o.%Set("linky", +$g(^[ns]ALVA.MHD.LSO(firma,1,"DTA")), "number")
                    s ^||ML("OUT", $i(^||ML("OUT"))) = o.%ToJSON()
                }
            } catch (exNs) { }
        }
    } catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Linky aktuální firmy s ukazateli, co je na nich vidět. IN: SKUPINY = skupiny linek („1001,1002"),
/// prázdné = všechny linky; MAX = nejvýš tolik linek (výchozí 300). OUT(n) = JSON za linku se spoji.
ClassMethod Linky(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        k ^||ML("OUT")
        s skupiny = $g(^||ML("IN","SKUPINY")), max = +$g(^||ML("IN","MAX"))
        i 'max s max = 300
        k linky
        i skupiny = "" {
            s lin = ""
            f {
                s lin = $o(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",lin), -1)
                q:(lin="")||($g(linky)>=max)
                s linky($i(linky)) = lin
            }
        } else {
            f i=1:1:$l(skupiny, ",") {
                s sku = $p(skupiny, ",", i), lin = ""
                f {
                    s lin = $o(^[dataDB]ALVA.MHD.LSO(IdFa,1,"IDX",1,1,1,sku,lin))
                    q:(lin="")||($g(linky)>=max)
                    s linky($i(linky)) = lin
                }
            }
        }
        f i=1:1:$g(linky) {
            s radek = ..Linka(IdFa, dataDB, linky(i))
            i radek '= "" s ^||ML("OUT", $i(^||ML("OUT"))) = radek
        }
    } catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Zastávky s víc zónami jednoho integrovaného systému (hraniční zastávky). IN: MAX = nejvýš tolik
/// (výchozí 50). OUT(n) = "ZastavkaID|název|IDSID|čísla zón čárkou".
ClassMethod ViceZon(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        k ^||ML("OUT")
        s max = +$g(^||ML("IN","MAX"))
        i 'max s max = 50
        s zast = ""
        f {
            s zast = $o(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",zast))
            q:(zast="")||($g(^||ML("OUT"))>=max)
            k poIds
            s c = ""
            f {
                s c = $o(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",zast,"DTA",1,"ZonyID",c))
                q:c=""
                s zona = $g(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",zast,"DTA",1,"ZonyID",c,"Itm"))
                continue:zona=""
                s ids = +$g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Zony","DTA",zona,"ZonaIDSID"))
                continue:'ids
                continue:$g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Zony","DTA",zona,"Cislo"))=""
                s poIds(ids) = $g(poIds(ids))_","_$g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Zony","DTA",zona,"Cislo"))
                s poIds(ids,"n") = $g(poIds(ids,"n")) + 1
            }
            s ids = ""
            f {
                s ids = $o(poIds(ids))
                q:ids=""
                continue:poIds(ids,"n")<2
                s ^||ML("OUT", $i(^||ML("OUT"))) = zast_"|"_$g(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",zast,"DTA",1,"Nazev"))_"|"_ids_"|"_$e(poIds(ids),2,*)
            }
        }
    } catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Proč linka nedá linkový JŘ. IN: LINKA. OUT("trasa"), OUT("bodu") = bodů trasy se zastávkou
/// kategorie 1001 (směr tam), OUT("spoju"), OUT("sTabulkou") = spojů, kterým MakeTableSpoj spočítá
/// řádky, OUT("legacy") = výsledek starého tisku (1 = vytiskl, jinak chyba).
ClassMethod ProcPrazdna(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        k ^||ML("OUT")
        s lin = $g(^||ML("IN","LINKA"))
        s trasa = ##class(ALVA.MHD.LinkyFce).GetTrasaId(IdFa, dataDB, lin)
        s ^||ML("OUT","trasa") = trasa
        s bodu = 0, bod = ""
        f {
            q:trasa=""
            s bod = $o(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",trasa,"DTA",1,"ModelTrasaBodDetail",bod))
            q:bod=""
            s zast = +$g(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",trasa,"DTA",1,"ModelTrasaBodDetail",bod,"Val","ZastavkaTamID"))
            i zast, +$g(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",zast,"DTA",1,"KategorieZastavekID")) = 1001 s bodu = bodu + 1
        }
        s ^||ML("OUT","bodu") = bodu
        s (spoju, sTabulkou) = 0, spoj = ""
        f {
            s spoj = $o(^[dataDB]ALVA.MHD.LSO(IdFa,2,"IDX",1,1,1,lin,spoj))
            q:spoj=""
            s spoju = spoju + 1
            k arr
            d ##class(ALVA.MHD.SpojeFce).MakeTableSpoj(.sys, spoj, "", .arr)
            i $o(arr("arr",1,"")) '= "" s sTabulkou = sTabulkou + 1
        }
        s ^||ML("OUT","spoju") = spoju, ^||ML("OUT","sTabulkou") = sTabulkou
        s legacy = ""
        try {
            k in, out
            s n = 0, tp = ""
            f {
                s tp = $o(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",tp))
                q:tp=""
                s in("A",2,"arr",11,$i(n)) = "TypProvozuID#"_tp_"~1"
            }
            s in("A",2,"arr",11,$i(n)) = "SmerID#0~1", in("A",2,"arr",11,$i(n)) = "SmerID#1~1"
            s in("A",2,"dta",1) = 1000000, in("A",2,"dta",2) = 1000000
            s legacy = ##class(ALVA.MHD.LinkyPrintFormatJR).MakeData(IdFa, dataDB, lin, "^||TdProcPrazdna", .in, .out)
            k ^||TdProcPrazdna
        } catch exL { s legacy = exL.DisplayString() }
        s ^||ML("OUT","legacy") = legacy
    } catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Ukazatele jedné linky; "" = linka bez spojů.
ClassMethod Linka(IdFa As %String, dataDB As %String, lin As %String) As %String
{
    s (spoje, rucni, nzPole, poznKnizni, poznNz, atrBC) = 0
    k typy
    s spoj = ""
    f {
        s spoj = $o(^[dataDB]ALVA.MHD.LSO(IdFa,2,"IDX",1,1,1,lin,spoj))
        q:spoj=""
        s spoje = spoje + 1
        s typy(+$g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",spoj,"DTA",1,"TypProvozuID"))) = ""
        i +$g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",spoj,"DTA",1,"RucniZadani")) s rucni = rucni + 1
        i $g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",spoj,"DTA",1,"NegativniZnacka")) '= "" s nzPole = nzPole + 1
        s (knizni, nz) = 0, idx = ""
        f {
            s idx = $o(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",spoj,"DTA",1,"PoznamkySpoj",idx))
            q:idx=""
            s pozn = $g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",spoj,"DTA",1,"PoznamkySpoj",idx,"Itm","PoznamkaSpojID"))
            continue:pozn=""
            continue:'$g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",pozn,"TiskJRMHDKnizni"))
            s knizni = 1
            i (+$g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",pozn,"TypPoznamkyID")) = 2) && ($g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",pozn,"NZ")) '= "") s nz = 1
        }
        s poznKnizni = poznKnizni + knizni, poznNz = poznNz + nz
        s idx = "", bc = 0
        f {
            s idx = $o(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",spoj,"DTA",1,"Atributy",idx))
            q:idx=""
            s atr = $g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",spoj,"DTA",1,"Atributy",idx,"Itm"))
            continue:atr=""
            i ",B,C," [ (","_$g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Atribut","DTA",atr,"Znak"))_",") s bc = 1
        }
        s atrBC = atrBC + bc
    }
    q:spoje=0 ""

    s pocetTypu = 0, t = ""
    f {
        s t = $o(typy(t))
        q:t=""
        s pocetTypu = pocetTypu + 1
    }
    s (odjPri, poznTrasy) = 0
    s trasa = ""
    try { s trasa = ##class(ALVA.MHD.LinkyFce).GetTrasaId(IdFa, dataDB, lin) } catch (exTr) { }
    i trasa '= "" {
        s bod = ""
        f {
            s bod = $o(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",trasa,"DTA",1,"ModelTrasaBodDetail",bod))
            q:bod=""
            f klic = "AtributyTam", "AtributyZpet" {
                s c = ""
                f {
                    s c = $o(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",trasa,"DTA",1,"ModelTrasaBodDetail",bod,"Val",klic,c))
                    q:c=""
                    i $g(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",trasa,"DTA",1,"ModelTrasaBodDetail",bod,"Val",klic,c,"Itm")) = 1011 s odjPri = odjPri + 1
                }
            }
            f klic = "PoznamkyTam", "PoznamkyZpet" {
                s c = ""
                f {
                    s c = $o(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",trasa,"DTA",1,"ModelTrasaBodDetail",bod,"Val",klic,c))
                    q:c=""
                    s pozn = $g(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",trasa,"DTA",1,"ModelTrasaBodDetail",bod,"Val",klic,c,"Itm"))
                    continue:pozn=""
                    i $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",pozn,"TiskJRMHDKnizni")) s poznTrasy = poznTrasy + 1
                }
            }
        }
    }

    s o = ##class(%DynamicObject).%New()
    d o.%Set("linka", lin)
    d o.%Set("cislo", $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",lin,"DTA",1,"CisloVerejne")))
    d o.%Set("platiDo", $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",lin,"DTA",1,"PlatiDo")))
    d o.%Set("ids", $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",lin,"DTA",1,"IDSID")))
    d o.%Set("auto", +$g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",lin,"DTA",1,"AutomatickyPoznamkovatOdlisnostiVPlatnostechSpoju")), "number")
    d o.%Set("spoje", spoje, "number"), o.%Set("typy", pocetTypu, "number")
    d o.%Set("rucni", rucni, "number"), o.%Set("nzPole", nzPole, "number")
    d o.%Set("poznKnizni", poznKnizni, "number"), o.%Set("poznNz", poznNz, "number")
    d o.%Set("atrBC", atrBC, "number"), o.%Set("odjPri", odjPri, "number"), o.%Set("poznTrasy", poznTrasy, "number")
    q o.%ToJSON()
}

}
