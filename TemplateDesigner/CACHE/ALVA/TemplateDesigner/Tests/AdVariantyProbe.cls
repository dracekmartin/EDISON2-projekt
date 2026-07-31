Include (%occInclude, GENERAL)

/// Read-only sonda variant tras (KOVED #13048 bod 5): per zastávka × směr porovná počet variant
/// dle ČASOVÉHO PROFILU (stará granularita TD) vs dle TRASY (nová = legacy `prutmbmp`; `|` = nestaví)
/// + diagnosticky trasa VČETNĚ nástupišť (kolik sloupců by přidalo rozlišení sloupků - vědomě
/// nepoužité, viz BuildStrankaZastavky). Ukáže, o kolik se linka „zeštíhlí" (0330510: 33 → 2).
///
/// IN:  SKUPINA_LINEK_ID (default první v ^JRL), LINKA_ID (povinné), SMER (T/Z, default T),
///      LIMIT (max řádků, default 40)
/// OUT[n] = "poradi=..~zastavka=..~spoju=..~profiluStare=..~trasNove=..~trasVcNastupist=..~cetnosti=.."
/// Nic nezapisuje (kromě úklidu vlastní ^||TiskAd cache).
Class ALVA.TemplateDesigner.Tests.AdVariantyProbe Extends %RegisteredObject
{

ClassMethod Scan(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        k ^||TiskAd

        s sku = $g(^||ML("IN","SKUPINA_LINEK_ID"))
        i sku = "" s sku = $o(^[dataDB]JRL(""))
        s lin = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).NormalizLinku($g(^||ML("IN","LINKA_ID")))
        i lin = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "LINKA_ID is required"))
        s smer = $g(^||ML("IN","SMER"))
        i smer = "" s smer = "T"
        s limit = +$g(^||ML("IN","LIMIT"))
        i 'limit s limit = 40
        s jeTam = (smer = "T")

        k served
        d ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).ObsluhovanePoradi(dataDB, sku, lin, smer, .served)

        s radku = 0, cur = ""
        f {
            s cur = $o(served(cur))
            q:cur=""
            i '$isvalidnum(cur) continue

            k profSig, trasSig, nastSig, poctyTras, jNast
            s nSp = 0, nProf = 0, nTras = 0, nTrasNast = 0
            s sp = ""
            f {
                s sp = $o(^[dataDB]JRL(sku,lin,smer,sp))
                q:sp=""
                i '$isvalidnum(sp) continue
                s ct = $p($p($g(^[dataDB]JRL(sku,lin,smer,sp)),"#",1),":",3)
                i ct = "" continue
                d ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).WMapa(dataDB, sku, lin, ct, .wMap)
                i ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).CasHodnota($g(wMap(cur))) = "" continue
                s nSp = nSp + 1
                d ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).WNastupisteMapa(dataDB, sku, lin, ct, .nMap)
                s sigProf = "", sigTras = "", sigNast = "", pp = ""
                f {
                    s pp = $o(served(pp))  q:pp=""
                    i '$isvalidnum(pp) continue
                    s vv = $g(wMap(pp))
                    i (vv = "") || (vv = "<") || (vv = "|") {
                        s znNestavi = $s(vv="":"", 1:"<")
                        s $p(sigProf,"~",pp) = znNestavi
                        s $p(sigTras,"~",pp) = znNestavi
                        s $p(sigNast,"~",pp) = znNestavi
                        continue
                    }
                    s $p(sigProf,"~",pp) = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).CasJizdyZMapy(.wMap, cur, pp)
                    s $p(sigTras,"~",pp) = "S"
                    s nast = $g(nMap(pp))
                    i nast = "" {
                        i '$d(jNast(pp)) s jNast(pp) = $p($p($g(^[dataDB]JRL(sku,lin,"J",pp)),"#",3),":",$s(jeTam:1,1:2))
                        s nast = $g(jNast(pp))
                    }
                    s $p(sigNast,"~",pp) = "S"_nast
                }
                i '$d(profSig(sigProf)) { s nProf = nProf + 1  s profSig(sigProf) = "" }
                i '$d(nastSig(sigNast)) { s nTrasNast = nTrasNast + 1  s nastSig(sigNast) = "" }
                i '$d(trasSig(sigTras)) { s nTras = nTras + 1  s trasSig(sigTras) = nTras }
                s poctyTras(trasSig(sigTras)) = $g(poctyTras(trasSig(sigTras))) + 1
            }
            i 'nSp continue

            s cetnosti = ""
            s ti = ""
            f {
                s ti = $o(poctyTras(ti))  q:ti=""
                s cetnosti = cetnosti_$s(cetnosti="":"",1:",")_poctyTras(ti)
            }
            s nazev = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).NazevZastavky(dataDB, $p($g(^[dataDB]JRL(sku,lin,"J",cur)),":",1))
            s ^||ML("OUT",$i(^||ML("OUT"))) = "poradi="_cur_"~zastavka="_nazev_"~spoju="_nSp_"~profiluStare="_nProf_"~trasNove="_nTras_"~trasVcNastupist="_nTrasNast_"~cetnosti="_cetnosti
            s radku = radku + 1
            q:radku>=limit
        }
    } catch (ex) { $$$CatchErrRest }
    k ^||TiskAd
    ret $$$OK
}

/// Read-only sken NOČNÍCH linek (#9036 - časová osa přes půlnoc): projde všechny linky namespace
/// a spočítá spoje s výchozím odjezdem 22:00–04:59. OUT = top řádky „sku~lin~nocnich=N~ukazky=HH:MM,…"
/// seřazené dle počtu nočních spojů. Pro výběr testovací linky, nic nemění.
ClassMethod ScanNocni(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s limit = +$g(^||ML("IN","LIMIT"))
        i 'limit s limit = 10

        k vysledky
        s sku = ""
        f {
            s sku = $o(^[dataDB]JRL(sku))
            q:sku=""
            i 'sku continue
            s lin = ""
            f {
                s lin = $o(^[dataDB]JRL(sku,lin))
                q:lin=""
                i '$d(^[dataDB]JRL(sku,lin,"I")) continue
                s nocnich = 0, ukazky = ""
                f smer = "T", "Z" {
                    s sp = ""
                    f {
                        s sp = $o(^[dataDB]JRL(sku,lin,smer,sp))
                        q:sp=""
                        i '$isvalidnum(sp) continue
                        s odjezd = +$p($p($g(^[dataDB]JRL(sku,lin,smer,sp)),"#",1),":",4)
                        s hodina = (odjezd\60)#24
                        i (hodina >= 22) || (hodina < 5) {
                            s nocnich = nocnich + 1
                            i $l(ukazky,",") <= 4 s ukazky = ukazky_$s(ukazky="":"",1:",")_hodina_":"_$e("0"_(odjezd#60),*-1,*)
                        }
                    }
                }
                i nocnich s vysledky(-nocnich, sku_"~"_lin) = ukazky
            }
        }
        s radku = 0, nKey = ""
        f {
            s nKey = $o(vysledky(nKey))
            q:nKey=""
            s linKey = ""
            f {
                s linKey = $o(vysledky(nKey,linKey))
                q:linKey=""
                s ^||ML("OUT",$i(^||ML("OUT"))) = linKey_"~nocnich="_(-nKey)_"~ukazky="_vysledky(nKey,linKey)
                s radku = radku + 1
                q:radku>=limit
            }
            q:radku>=limit
        }
        i 'radku s ^||ML("OUT","1") = "zadna nocni linka v namespace"
    } catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Zavolá OSTROU BuildStrankaZastavky pro jednu zastávku a shrne variantní výstup: hlavičky
/// sloupců (VARIANTY_TRAS), TRASA_MIN vývěskové zastávky a rozpad anotací u minut dle kategorie.
/// Parametry tisku (PISMENA_VARIANT / PRVNI_BEZ_PISMENE / RAZENI_VARIANT) čte engine přímo
/// z ^||ML("IN") - sonda je jen průhledně předává.
/// IN: SKUPINA_LINEK_ID, LINKA_ID (povinné), SMER (default T), PORADI (default první).
ClassMethod Stranka(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        k ^||TiskAd

        s sku = $g(^||ML("IN","SKUPINA_LINEK_ID"))
        i sku = "" s sku = $o(^[dataDB]JRL(""))
        s lin = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).NormalizLinku($g(^||ML("IN","LINKA_ID")))
        i lin = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "LINKA_ID is required"))
        s smer = $g(^||ML("IN","SMER"))
        i smer = "" s smer = "T"
        s poradi = $g(^||ML("IN","PORADI"))

        s page = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).BuildStrankaZastavky(dataDB, sku, lin, poradi, smer)
        i page = "" {
            s ^||ML("OUT","1") = "prazdna stranka (konecna / bez odjezdu)"
        } else {
            s ^||ML("OUT","VARIANTY_TRAS") = page.values.%Get("VARIANTY_TRAS").%ToJSON()
            // TRASA_MIN vývěskové (aktuální) zastávky
            s zast = page.rows.%Get("ZASTAVKY")
            f iz=0:1:zast.%Size()-1 {
                s zz = zast.%Get(iz)
                i zz.%Get("AKTUALNI_ZASTAVKA") = "true" s ^||ML("OUT","TRASA_MIN_AKTUALNI") = zz.%Get("TRASA_MIN").%ToJSON()
            }
            // anotace u minut dle kategorie (počty) + ukázka písmen variant
            k katPocty
            s pismenaUkazka = ""
            s odj = page.rows.%Get("ODJEZDY")
            f io=0:1:odj.%Size()-1 {
                s anot = odj.%Get(io).%Get("ANOTACE")
                f ia=0:1:anot.%Size()-1 {
                    s an = anot.%Get(ia)
                    s katPocty($s(an.%Get("KATEGORIE")'="":an.%Get("KATEGORIE"),1:"(bez)")) = $g(katPocty($s(an.%Get("KATEGORIE")'="":an.%Get("KATEGORIE"),1:"(bez)"))) + 1
                    i (an.%Get("KATEGORIE") = "varianta-trasy") && ($l(pismenaUkazka) < 20) s pismenaUkazka = pismenaUkazka_an.%Get("ZNAK")
                }
            }
            s katCsv = "", kat = ""
            f {
                s kat = $o(katPocty(kat))  q:kat=""
                s katCsv = katCsv_$s(katCsv="":"",1:", ")_kat_"="_katPocty(kat)
            }
            s ^||ML("OUT","ANOTACE_KATEGORIE") = katCsv
            s ^||ML("OUT","PISMENA_UKAZKA") = pismenaUkazka
            s ^||ML("OUT","POCET_ODJEZDU") = odj.%Size()
            // časy odjezdů stránky (h:mm, v pořadí emise) - diagnostika časové osy (#9036 noční spoje)
            s casyCsv = ""
            f io=0:1:odj.%Size()-1 {
                s oOdj = odj.%Get(io)
                i $l(casyCsv) < 400 s casyCsv = casyCsv_$s(casyCsv="":"",1:",")_oOdj.%Get("HODINA")_":"_$e("0"_oOdj.%Get("MINUTA"),*-1,*)
            }
            s ^||ML("OUT","ODJEZDY_CASY") = casyCsv
            // rozpad per sloupec dnů (seřazeno dle času) - skutečný provozní obraz stránky
            s skup = page.rows.%Get("SKUPINY_DNU")
            f isk=0:1:skup.%Size()-1 {
                s skId = skup.%Get(isk).%Get("ID")
                k dleCasu
                f io=0:1:odj.%Size()-1 {
                    s oOdj = odj.%Get(io)
                    i oOdj.%Get("SKUPINA_DNU_ID") '= skId continue
                    s klicMin = (oOdj.%Get("HODINA")*60) + oOdj.%Get("MINUTA")
                    s dleCasu(klicMin, io) = oOdj.%Get("HODINA")_":"_$e("0"_oOdj.%Get("MINUTA"),*-1,*)
                }
                s radek = "", km = ""
                f {
                    s km = $o(dleCasu(km))  q:km=""
                    s io2 = ""
                    f {
                        s io2 = $o(dleCasu(km,io2))  q:io2=""
                        i $l(radek) < 400 s radek = radek_$s(radek="":"",1:",")_dleCasu(km,io2)
                    }
                }
                s ^||ML("OUT","SLOUPEC_"_skId) = skup.%Get(isk).%Get("NADPIS")_": "_radek
            }
            s ^||ML("OUT","POCET_POZNAMEK") = page.rows.%Get("POZNAMKY").%Size()
            // Kandidáti duplikace řádku: zastávky s ≥2 různými označníky mezi variantami stránky
            // (přesně podmínka klientského ExpandPlatformRows). Prázdné = na stránce se nic nerozpadne.
            s dupl = ""
            f iz=0:1:zast.%Size()-1 {
                s zz = zast.%Get(iz)
                s nvArr = zz.%Get("NASTUPISTE_VARIANT")
                i '$isobject(nvArr) continue
                k distinctOzn
                f in=0:1:nvArr.%Size()-1 {
                    s ov = nvArr.%Get(in)
                    i ov '= "" s distinctOzn(ov) = ""
                }
                s pocetOzn = 0, oo = ""
                f { s oo = $o(distinctOzn(oo))  q:oo=""  s pocetOzn = pocetOzn + 1 }
                i pocetOzn >= 2 s dupl = dupl_$s(dupl="":"",1:"; ")_zz.%Get("NAZEV_ZASTAVKY")_" ("_pocetOzn_" ozn.)"
            }
            s ^||ML("OUT","DUPLIKACE_KANDIDATI") = $s(dupl'="":dupl, 1:"(žádná zastávka na stránce)")
        }
    } catch (ex) { $$$CatchErrRest }
    k ^||TiskAd
    ret $$$OK
}

/// Read-only hledání ukázkové linky se ZACHÁZKAMI (#13070 BBSK): městská linka, kde část spojů
/// zajíždí mimo hlavní trasu. Skóruje kandidáty tak, aby šel zákazníkovi ukázat výstup, který
/// odpovídá jeho zadání - tedy zapnuté poznámkování rozdílů tras + nedegenerovaná vzorová trasa
/// (ta rozhoduje, jestli poznámka řekne „jede přes zast. X", nebo jen „neobsluhuje zast. X").
/// IN:  SKUPINA_LINEK_ID ("" / "*" = všechny), LIMIT (kolik linek projít, default 150),
///      TOP (kolik řádků vypsat, default 12), MIN_SPOJU (default 8)
/// OUT[n] = "sku~lin~smer~skore=..~spoju=..~zastavek=..~variant=..~zachazek=..~vzor=..~autoPozn=..
///           ~vety=..~zachazky=.."
ClassMethod ScanZachazky(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s zadanaSku = $g(^||ML("IN","SKUPINA_LINEK_ID"))
        s limit = +$g(^||ML("IN","LIMIT"))         i 'limit s limit = 150
        s top = +$g(^||ML("IN","TOP"))             i 'top s top = 12
        s minSpoju = +$g(^||ML("IN","MIN_SPOJU"))  i 'minSpoju s minSpoju = 8
        s jenAutoPozn = +$g(^||ML("IN","JEN_AUTOPOZN"))   // 1 = jen linky se zapnutým poznámkováním
        s agenda = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"

        k skupiny
        i (zadanaSku '= "") && (zadanaSku '= "*") {
            s skupiny(zadanaSku) = ""
        } else {
            s s1 = ""
            f { s s1 = $o(^[dataDB]JRL(s1))  q:s1=""  i +s1 s skupiny(s1) = "" }
        }

        k vysledky
        s (linek, kandidatu) = 0
        s sku = ""
        f {
            s sku = $o(skupiny(sku))
            q:sku=""
            q:linek>=limit
            s lin = ""
            f {
                s lin = $o(^[dataDB]JRL(sku,lin))
                q:lin=""
                q:linek>=limit
                i '$d(^[dataDB]JRL(sku,lin,"I")) continue
                s linek = linek + 1
                k ^||TiskAd                        // cache je per linka (jinak si linky přebijí W-mapu)

                // počet zastávek v trase linky
                s zastavek = 0, p = ""
                f { s p = $o(^[dataDB]JRL(sku,lin,"J",p))  q:p=""  i $isvalidnum(p) s zastavek = zastavek + 1 }
                i zastavek < 3 continue

                s autoPozn = +$g(^[dataDB]JRL(sku,lin,"PATCH","mhd","not"))
                continue:jenAutoPozn&&'autoPozn

                f smer = "T", "Z" {
                    // varianty tras + kolikrát je která zastávka obsloužena
                    k obsluha, bitmapy
                    s (spoju, variant) = 0, sp = ""
                    f {
                        s sp = $o(^[dataDB]JRL(sku,lin,smer,sp))
                        q:sp=""
                        i '$isvalidnum(sp) continue
                        s cisloTrasy = $p($p($g(^[dataDB]JRL(sku,lin,smer,sp)),"#",1),":",3)
                        s bmp = $classmethod(agenda,"SpojBitmapa", dataDB, sku, lin, cisloTrasy)
                        continue:bmp=""
                        s spoju = spoju + 1
                        i '$d(bitmapy(bmp)) s bitmapy(bmp) = "", variant = variant + 1
                        f poz=1:1:$l(bmp) i +$e(bmp,poz) s obsluha(poz) = $g(obsluha(poz)) + 1
                    }
                    continue:spoju<minSpoju
                    continue:variant<2

                    // zacházka = zastávka, kterou obsluhuje menšina spojů (ne všechny, ale aspoň jeden)
                    s zachazek = 0, zachazky = "", poz = ""
                    f {
                        s poz = $o(obsluha(poz))
                        q:poz=""
                        s pocet = obsluha(poz)
                        continue:pocet=spoju
                        continue:(pocet*2)>=spoju
                        s zachazek = zachazek + 1
                        i $l(zachazky,";") <= 2 {
                            s zachazky = zachazky_$s(zachazky="":"",1:"; ")
                                _$classmethod(agenda,"NazZPoradi", dataDB, sku, lin, poz)_" ("_pocet_"/"_spoju_")"
                        }
                    }

                    // vzorová trasa: základní (#10) → tištěná (#7) → sjednocení obsluhy
                    s vzor10 = $classmethod(agenda,"TrasaBitmapaZPole", dataDB, sku, lin, smer, 10)
                    s vzor7  = $classmethod(agenda,"TrasaBitmapaZPole", dataDB, sku, lin, smer, 7)
                    s bitu10 = $l(vzor10,"1") - 1
                    s bitu7  = $l(vzor7,"1") - 1
                    s vzorTyp = $s(vzor10'="":"zakladni("_bitu10_"/"_zastavek_")", vzor7'="":"tisteny("_bitu7_"/"_zastavek_")", 1:"fallbackUnion")
                    s bituVzor = $s(vzor10'="":bitu10, vzor7'="":bitu7, 1:0)

                    // Jaké věty na lince reálně vzniknou. Vzorkuje se víc zastávek - na první
                    // zastávce směru poznámky často nejsou a linka by vypadala prázdná.
                    k served  d $classmethod(agenda,"ObsluhovanePoradi", dataDB, sku, lin, smer, .served)
                    s vety = "", legendPolozek = 0, vzorku = 0, poz = ""
                    f {
                        s poz = $o(served(poz))
                        q:poz=""
                        i '$isvalidnum(poz) continue
                        s vzorku = vzorku + 1
                        q:vzorku>5
                        k pismenoSpoj, legenda
                        d $classmethod(agenda,"RozdilyTras", dataDB, sku, lin, smer, poz, .pismenoSpoj, .legenda)
                        s key = ""
                        f {
                            s key = $o(legenda(key))
                            q:key=""
                            s txt = $g(legenda(key))
                            continue:txt=""
                            s legendPolozek = legendPolozek + 1
                            i txt["jede přes"    s vety = $s(vety["pres":vety, 1:vety_$s(vety="":"",1:",")_"pres")
                            i txt["neobsluhuje"  s vety = $s(vety["neobsluhuje":vety, 1:vety_$s(vety="":"",1:",")_"neobsluhuje")
                            i txt["jede jen do"  s vety = $s(vety["jenDo":vety, 1:vety_$s(vety="":"",1:",")_"jenDo")
                            i txt["jede až do"   s vety = $s(vety["azDo":vety, 1:vety_$s(vety="":"",1:",")_"azDo")
                        }
                    }
                    i vety = "" s vety = "(zadne)"

                    // skóre: chceme zapnuté poznámkování, zacházku a vzorovou trasu, která je
                    // opravdu trasa (degenerovaná vzorová trasa dělá „jede až do" u všeho)
                    s skore = 0
                    i autoPozn s skore = skore + 50
                    i zachazek >= 1 s skore = skore + 30
                    i variant >= 3 s skore = skore + 20
                    i spoju >= 15 s skore = skore + 10
                    i (bituVzor >= (zastavek*0.6)) s skore = skore + 25
                    i (vzorTyp '[ "fallback") && (bituVzor <= 2) s skore = skore - 20
                    i vety["pres" s skore = skore + 15

                    s kandidatu = kandidatu + 1
                    s vysledky(-skore, sku_"~"_lin_"~"_smer) = "skore="_skore_"~spoju="_spoju_"~zastavek="_zastavek
                        _"~variant="_variant_"~zachazek="_zachazek_"~vzor="_vzorTyp_"~autoPozn="_autoPozn
                        _"~vety="_vety_"~legendPolozek="_legendPolozek
                        _"~zachazky="_$s(zachazky'="":zachazky, 1:"(zadna)")
                }
            }
        }
        k ^||TiskAd

        s radku = 0, sKey = ""
        f {
            s sKey = $o(vysledky(sKey))
            q:sKey=""
            s linKey = ""
            f {
                s linKey = $o(vysledky(sKey,linKey))
                q:linKey=""
                s ^||ML("OUT",$i(^||ML("OUT"))) = linKey_"~"_vysledky(sKey,linKey)
                s radku = radku + 1
                q:radku>=top
            }
            q:radku>=top
        }
        s ^||ML("OUT","SOUHRN") = "linekProslo="_linek_"~kandidatu="_kandidatu_"~vypsano="_radku
            _"~minSpoju="_minSpoju
    } catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

}
