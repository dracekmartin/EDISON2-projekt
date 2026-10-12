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
