Include (%occInclude, GENERAL)

/// Read-only sonda k #7947: hledá linky, kde generátor poznámek reálně vypíše
/// **„jede až do zast. <konečná>"** - text pro spoj jedoucí ZA vzorovou trasu. Bez takové linky
/// se tiket nedá ověřit a nesmí se zavřít.
///
/// ⚠️ **Nereimplementuje podmínku** - volá přímo `ZastavkoveJrAd.RozdilyTras` (týž kód, který text
/// tiskne) pro každou linku × směr × obsluhovanou zastávku a hledá tu frázi v legendě. Dřívější
/// verze si podmínku odvozovala sama a vyráběla artefakty (délka bitmapy místo konečné zastávky).
///
/// IN:  SKUPINA_LINEK_ID (default první v ^JRL), LIMIT linek (default 500), FRAZE (default „jede až do")
/// OUT[n] = "linka=..~smer=..~poradi=..~text=.."  (jen nálezy, max 30 řádků)
/// OUT["SOUHRN"] = "linek=..~smeru=..~zastavek=..~legendCelkem=..~nalezu=.."
/// Nic nezapisuje (kromě úklidu vlastní ^||TiskAd cache).
Class ALVA.TemplateDesigner.Tests.AdProdlouzeniProbe Extends %RegisteredObject
{

/// Autoritativní kontrola „dostane se poznámka na stránku?": nechá `GetData` vygenerovat stránky
/// a vypíše **kolekci POZNAMKY** té stránky (zastávka × směr). Rozliší „v datech není" od
/// „v datech je, ale nevykreslí se".
///
/// IN:  SKUPINA_LINEK_ID, LINKA_ID (povinné), ZASTAVKA (podřetězec názvu, default vše), SMER (default T)
/// OUT[n] = "stranka=..~zastavka=..~smer=..~typ=..~znak=..~text=.."
/// OUT["SOUHRN"] = "stranek=..~stranekVybranych=..~poznamekCelkem=.."
ClassMethod StrankaPoznamky(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s filtrZast = $g(^||ML("IN","ZASTAVKA"))
        s filtrSmer = $g(^||ML("IN","SMER"))
        i filtrSmer = "" s filtrSmer = "T"

        // GetData čte tytéž IN klíče (SKUPINA_LINEK_ID / LINKA_ID) - necháme je být a zavoláme ji
        d ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)

        // OUT si přebereme do lokálu a vyčistíme, ať můžeme psát vlastní výstup
        k stranky
        s n = "", pocetStranek = 0
        f {
            s n = $o(^||ML("OUT",n))
            q:n=""
            i '$isvalidnum(n) continue
            s pocetStranek = pocetStranek + 1
            s stranky(pocetStranek) = ^||ML("OUT",n)
        }
        k ^||ML("OUT")

        s (i, vybranych, poznamek) = 0
        f s=1:1:pocetStranek {
            s obj = ##class(%DynamicObject).%FromJSON(stranky(s))
            s vals = obj.values
            continue:'$isobject(vals)
            s naz = vals.%Get("POPIS_ZASTAVKA")
            s smer = vals.%Get("POPIS_SMER")
            s smerKod = vals.%Get("SMER")

            i (filtrZast '= "") && (naz '[ filtrZast) continue
            i (filtrSmer '= "") && (smerKod '= "") && (smerKod '= filtrSmer) continue
            s vybranych = vybranych + 1

            s rows = obj.rows
            continue:'$isobject(rows)
            s poz = rows.%Get("POZNAMKY")
            continue:'$isobject(poz)

            s it = poz.%GetIterator()
            while it.%GetNext(.k, .v) {
                continue:'$isobject(v)
                s poznamek = poznamek + 1
                i i < 40 {
                    s ^||ML("OUT",$i(i)) = "stranka="_s_"~zastavka="_naz_"~smer="_smer
                        _"~typ="_v.%Get("TYP")_"~znak="_v.%Get("ZNAK")_"~text="_v.%Get("TEXT")
                }
            }
        }

        s ^||ML("OUT","SOUHRN") = "stranek="_pocetStranek_"~stranekVybranych="_vybranych
            _"~poznamekCelkem="_poznamek
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Doplněk inventury: kolik linek má vůbec **zdrojová data** pro jednotlivé druhy poznámek. Rozliší
/// „kategorie nedorazila, protože data jsou prázdná" od „kategorie nedorazila, protože ji kód nečte".
/// IN: SKUPINA_LINEK_ID, LIMIT.  OUT[n] = "zdroj=..~linek=..~zaznamu=.."
ClassMethod ZdrojeDat(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s sku = $g(^||ML("IN","SKUPINA_LINEK_ID"))
        i sku = "" s sku = $o(^[dataDB]JRL(""))
        s limit = +$g(^||ML("IN","LIMIT"))
        i 'limit s limit = 500

        k pocetLinek, pocetZaznamu
        s linek = 0, lin = ""
        f {
            s lin = $o(^[dataDB]JRL(sku,lin))
            q:lin=""
            i '$d(^[dataDB]JRL(sku,lin,"I")) continue
            s linek = linek + 1
            q:linek>limit

            // ruční komentáře spoje (číselník SeT_SpeC + přiřazení MhD_SpeC u spoje)
            k maZdroj
            s zn = ""
            f {
                s zn = $o(^[dataDB]JRL(sku,lin,"MhD","SeT_SpeC",zn))
                q:zn=""
                s pocetZaznamu("ciselnik komentaru (SeT_SpeC)") = $g(pocetZaznamu("ciselnik komentaru (SeT_SpeC)")) + 1
                s maZdroj("ciselnik komentaru (SeT_SpeC)") = ""
            }
            f smerIdx=1:1:2 {
                s smer = $s(smerIdx=1:"T", 1:"Z")
                s sp = ""
                f {
                    s sp = $o(^[dataDB]JRL(sku,lin,smer,sp))
                    q:sp=""
                    i '$isvalidnum(sp) continue
                    i $g(^[dataDB]JRL(sku,lin,smer,sp,"MhD_SpeC")) '= "" {
                        s pocetZaznamu("komentar u spoje (MhD_SpeC)") = $g(pocetZaznamu("komentar u spoje (MhD_SpeC)")) + 1
                        s maZdroj("komentar u spoje (MhD_SpeC)") = ""
                    }
                }
            }
            // směrové poznámky linky (SeT_MhD ">" / "<" / "<>")
            f klicIdx=1:1:3 {
                s smerovyKlic = $p(">,<,<>", ",", klicIdx)
                s poz = ""
                f {
                    s poz = $o(^[dataDB]JRL(sku,lin,"MhD","SeT_MhD",smerovyKlic,poz))
                    q:poz=""
                    i $g(^[dataDB]JRL(sku,lin,"MhD","SeT_MhD",smerovyKlic,poz)) '= "" {
                        s pocetZaznamu("smerove poznamky (SeT_MhD)") = $g(pocetZaznamu("smerove poznamky (SeT_MhD)")) + 1
                        s maZdroj("smerove poznamky (SeT_MhD)") = ""
                    }
                }
            }
            // auto-poznámkování rozdílů tras + „Spoje, nepoznámkovat" rozsahy
            i +$g(^[dataDB]JRL(sku,lin,"PATCH","mhd","not")) s maZdroj("auto-poznamkovani zapnuto (PATCH mhd not)") = ""
            i $d(^[dataDB]JRL(sku,lin,"PATCH","MHDSPNPZN")) s maZdroj("rozsah Spoje-nepoznamkovat (MHDSPNPZN)") = ""
            i $g(^[dataDB]JRL(sku,lin,"PATCH","mhd","notchar")) '= "" s maZdroj("vlastni sada pismen (notchar)") = ""

            s zdroj = ""
            f { s zdroj = $o(maZdroj(zdroj))  q:zdroj=""  s pocetLinek(zdroj) = $g(pocetLinek(zdroj)) + 1 }
        }

        s i = 0
        s zdroj = ""
        f {
            s zdroj = $o(pocetLinek(zdroj))
            q:zdroj=""
            s ^||ML("OUT",$i(i)) = "zdroj="_zdroj_"~linek="_pocetLinek(zdroj)_"~zaznamu="_$g(pocetZaznamu(zdroj),"-")
        }
        s ^||ML("OUT","SOUHRN") = "linekCelkem="_$s(linek>limit:limit,1:linek)
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Inventura zdrojů poznámek: přes všechny stránky (zastávka × směr) N linek posbírá anotace u minut
/// i řádky legendy **po kategoriích** a ověří tři věci, na kterých úplnost poznámek stojí:
///   1. která kategorie vůbec dorazí (počty > 0 = zdroj je živý)
///   2. „symbol u minuty bez řádku legendy" (cestující vidí značku, kterou nic nevysvětluje)
///   3. „kolize značky" - tentýž znak nese v legendě dva různé významy
///
/// IN:  SKUPINA_LINEK_ID, LIMIT linek (default 500), OD_LINKY (pokračovat za danou linkou - chunkování)
/// OUT[n] = "KATEGORIE=..~anotaci=..~stranekSAnotaci=..~legendPolozek=..~stranekSLegendou=.."
///          "PROBLEM=..~..." (nálezy 2 a 3, max 20 řádků)
/// OUT["SOUHRN"] = "linek=..~stranek=..~bezLegendy=..~kolizi=..~posledniLinka=.."
ClassMethod Inventura(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s sku = $g(^||ML("IN","SKUPINA_LINEK_ID"))
        i sku = "" s sku = $o(^[dataDB]JRL(""))
        s limit = +$g(^||ML("IN","LIMIT"))
        i 'limit s limit = 500
        s agenda = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"

        k statAn, statLeg, statAnStr, statLegStr, problemy
        s (linek, stranek, bezLegendy, kolizi, problemu) = 0
        s lin = $g(^||ML("IN","OD_LINKY"))
        f {
            s lin = $o(^[dataDB]JRL(sku,lin))
            q:lin=""
            i '$d(^[dataDB]JRL(sku,lin,"I")) continue
            s linek = linek + 1
            q:linek>limit
            k ^||TiskAd

            f smerIdx=1:1:2 {
                s smer = $s(smerIdx=1:"T", 1:"Z")
                s poradi = ""
                f {
                    s poradi = $o(^[dataDB]JRL(sku,lin,"J",poradi))
                    q:poradi=""
                    i '$isvalidnum(poradi) continue
                    i $p($g(^[dataDB]JRL(sku,lin,"J",poradi)),":",1) = "" continue

                    s page = $classmethod(agenda,"BuildStrankaZastavky", dataDB, sku, lin, poradi, smer, "")
                    continue:'$isobject(page)
                    s stranek = stranek + 1

                    // anotace u minut (znak × kategorie) napříč odjezdy stránky
                    k anZnak, anKat, legZnak, legKat
                    s odj = page.rows.%Get("ODJEZDY")
                    s itO = odj.%GetIterator()
                    while itO.%GetNext(.ok, .ov) {
                        continue:'$isobject(ov)
                        s itA = ov.%Get("ANOTACE").%GetIterator()
                        while itA.%GetNext(.ak, .av) {
                            continue:'$isobject(av)
                            s kat = av.%Get("KATEGORIE"), zn = av.%Get("ZNAK")
                            i kat = "" s kat = "(bez kategorie)"
                            s anKat(kat) = $g(anKat(kat)) + 1
                            i zn '= "" s anZnak(zn, kat) = ""
                        }
                    }
                    // řádky legendy
                    s itP = page.rows.%Get("POZNAMKY").%GetIterator()
                    while itP.%GetNext(.pk, .pv) {
                        continue:'$isobject(pv)
                        s kat = pv.%Get("KATEGORIE"), zn = pv.%Get("ZNAK")
                        i kat = "" s kat = "(bez kategorie)"
                        s legKat(kat) = $g(legKat(kat)) + 1
                        i zn '= "" s legZnak(zn, kat) = pv.%Get("TEXT")
                    }

                    // agregace per kategorie
                    s kat = ""
                    f { s kat = $o(anKat(kat))  q:kat=""  s statAn(kat) = $g(statAn(kat)) + anKat(kat), statAnStr(kat) = $g(statAnStr(kat)) + 1 }
                    s kat = ""
                    f { s kat = $o(legKat(kat))  q:kat=""  s statLeg(kat) = $g(statLeg(kat)) + legKat(kat), statLegStr(kat) = $g(statLegStr(kat)) + 1 }

                    // nález 2: symbol u minuty, který legenda nevysvětluje (kategorie-agnosticky)
                    s zn = ""
                    f {
                        s zn = $o(anZnak(zn))
                        q:zn=""
                        continue:$d(legZnak(zn))
                        s bezLegendy = bezLegendy + 1
                        s katA = $o(anZnak(zn,""))
                        i problemu < 20 {
                            s problemu = problemu + 1
                            s problemy(problemu) = "PROBLEM=anotaceBezLegendy~linka="_lin_"~smer="_smer_"~poradi="_poradi
                                _"~znak="_zn_"~kategorie="_katA
                        }
                    }
                    // nález 3: tentýž znak ve legendě ve dvou různých kategoriích = dva významy
                    s zn = ""
                    f {
                        s zn = $o(legZnak(zn))
                        q:zn=""
                        s k1 = $o(legZnak(zn,""))
                        s k2 = $o(legZnak(zn,k1))
                        continue:k2=""
                        s kolizi = kolizi + 1
                        i problemu < 20 {
                            s problemu = problemu + 1
                            s problemy(problemu) = "PROBLEM=kolizeZnaku~linka="_lin_"~smer="_smer_"~poradi="_poradi
                                _"~znak="_zn_"~kategorie1="_k1_"~text1="_$g(legZnak(zn,k1))
                                _"~kategorie2="_k2_"~text2="_$g(legZnak(zn,k2))
                        }
                    }
                }
            }
        }
        k ^||TiskAd

        s i = 0
        s kat = ""
        f {
            s kat = $o(statAn(kat))
            q:kat=""
            s ^||ML("OUT",$i(i)) = "KATEGORIE="_kat_"~anotaci="_$g(statAn(kat))_"~stranekSAnotaci="_$g(statAnStr(kat))
                _"~legendPolozek="_$g(statLeg(kat))_"~stranekSLegendou="_$g(statLegStr(kat))
        }
        s kat = ""
        f {
            s kat = $o(statLeg(kat))
            q:kat=""
            continue:$d(statAn(kat))                  // už vypsané výše
            s ^||ML("OUT",$i(i)) = "KATEGORIE="_kat_"~anotaci=0~stranekSAnotaci=0"
                _"~legendPolozek="_$g(statLeg(kat))_"~stranekSLegendou="_$g(statLegStr(kat))
        }
        f pi=1:1:problemu s ^||ML("OUT",$i(i)) = problemy(pi)

        s ^||ML("OUT","SOUHRN") = "linek="_$s(linek>limit:limit,1:linek)_"~stranek="_stranek
            _"~bezLegendy="_bezLegendy_"~kolizi="_kolizi_"~posledniLinka="_lin
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Diagnostika „vady 1" (#7947): rozliší, zda `RozdilyTras` legendu vůbec nevyrobí, nebo se ztrácí
/// dál. Pro každou pozici zastávky (filtr dle názvu) zavolá **v jednom procesu** RozdilyTras i
/// `BuildStrankaZastavky` a vypíše obojí - takže je vidět i vliv stavu cache `^||TiskAd`.
///
/// IN:  SKUPINA_LINEK_ID, LINKA_ID (povinné), ZASTAVKA (podřetězec názvu), SMER (default T)
/// OUT[n] = diagnostické řádky (gate, vzorové trasy, legenda RozdilyTras, poznámky stránky)
ClassMethod RozdilyDiag(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        k ^||TiskAd

        s sku = $g(^||ML("IN","SKUPINA_LINEK_ID"))
        i sku = "" s sku = $o(^[dataDB]JRL(""))
        s lin = $g(^||ML("IN","LINKA_ID"))
        s filtrZast = $g(^||ML("IN","ZASTAVKA"))
        s smer = $g(^||ML("IN","SMER"))
        i smer = "" s smer = "T"
        s agenda = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"

        s i = 0
        s ^||ML("OUT",$i(i)) = "gateAutoPozn="_+$g(^[dataDB]JRL(sku,lin,"PATCH","mhd","not"))
            _"~notchar="_$g(^[dataDB]JRL(sku,lin,"PATCH","mhd","notchar"))
        s ^||ML("OUT",$i(i)) = "vzor10="_$classmethod(agenda,"TrasaBitmapaZPole", dataDB, sku, lin, smer, 10)
            _"~vzor7="_$classmethod(agenda,"TrasaBitmapaZPole", dataDB, sku, lin, smer, 7)

        k served
        d $classmethod(agenda,"ObsluhovanePoradi", dataDB, sku, lin, smer, .served)
        s poradi = ""
        f {
            s poradi = $o(served(poradi))
            q:poradi=""
            i '$isvalidnum(poradi) continue
            s naz = $classmethod(agenda,"NazZPoradi", dataDB, sku, lin, poradi)
            i (filtrZast '= "") && (naz '[ filtrZast) continue

            k pismenoSpoj, legenda
            d $classmethod(agenda,"RozdilyTras", dataDB, sku, lin, smer, poradi, .pismenoSpoj, .legenda)
            s pocetPism = 0, sp = ""
            f { s sp = $o(pismenoSpoj(sp))  q:sp=""  s pocetPism = pocetPism + 1 }
            s key = "", pocetLeg = 0
            f {
                s key = $o(legenda(key))
                q:key=""
                s pocetLeg = pocetLeg + 1
                s ^||ML("OUT",$i(i)) = "poradi="_poradi_"~nazev="_naz_"~RT_znak="_key_"~RT_text="_legenda(key)
            }
            s ^||ML("OUT",$i(i)) = "poradi="_poradi_"~nazev="_naz_"~RT_legendPolozek="_pocetLeg
                _"~RT_spojuSPismenem="_pocetPism

            // táž pozice přes generátor stránky (co reálně dorazí do POZNAMKY)
            s page = $classmethod(agenda,"BuildStrankaZastavky", dataDB, sku, lin, poradi, smer, "")
            i '$isobject(page) {
                s ^||ML("OUT",$i(i)) = "poradi="_poradi_"~nazev="_naz_"~STRANKA=negenerovana"
                continue
            }
            s poz = page.rows.%Get("POZNAMKY")
            s pocetPozn = 0, textovych = 0
            s it = poz.%GetIterator()
            while it.%GetNext(.pk, .pv) {
                continue:'$isobject(pv)
                s pocetPozn = pocetPozn + 1
                i pv.%Get("TYP") = "text" {
                    s textovych = textovych + 1
                    s ^||ML("OUT",$i(i)) = "poradi="_poradi_"~STRANKA_znak="_pv.%Get("ZNAK")_"~STRANKA_text="_pv.%Get("TEXT")
                }
            }
            s ^||ML("OUT",$i(i)) = "poradi="_poradi_"~nazev="_naz_"~STRANKA_poznamek="_pocetPozn
                _"~STRANKA_textovych="_textovych_"~STRANKA_odjezdu="_page.rows.%Get("ODJEZDY").%Size()
        }
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

ClassMethod Scan(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        k ^||TiskAd

        s sku = $g(^||ML("IN","SKUPINA_LINEK_ID"))
        i sku = "" s sku = $o(^[dataDB]JRL(""))
        s limit = +$g(^||ML("IN","LIMIT"))
        i 'limit s limit = 500
        s fraze = $g(^||ML("IN","FRAZE"))
        i fraze = "" s fraze = "jede až do"
        s jenLinka = $g(^||ML("IN","LINKA_ID"))     // volitelně jedna linka (čistá cache = bez kontaminace mezi linkami)

        s agenda = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
        s (linek, smeru, zastavek, legendCelkem, nalezu, i) = 0
        // vzorová trasa (J-věta #10 základní / #7 tištěná): když je prázdná, RozdilyTras padá na fallback
        // „union obsluhy" - a pak žádný spoj nemůže jet ZA vzor → věta „jede až do" je nedosažitelná
        s (vzorZakl, vzorTisk, vzorFallback, autoPoznVyp) = 0

        s lin = ""
        f {
            s lin = $o(^[dataDB]JRL(sku,lin))
            q:lin=""
            i '$d(^[dataDB]JRL(sku,lin,"I")) continue
            i (jenLinka '= "") && (lin '= jenLinka) continue
            s linek = linek + 1
            q:linek>limit

            f smerIdx=1:1:2 {
                s smer = $s(smerIdx=1:"T", 1:"Z")
                s smeru = smeru + 1

                i '+$g(^[dataDB]JRL(sku,lin,"PATCH","mhd","not")) s autoPoznVyp = autoPoznVyp + 1
                i $classmethod(agenda,"TrasaBitmapaZPole", dataDB, sku, lin, smer, 10) '= "" {
                    s vzorZakl = vzorZakl + 1
                } elseif $classmethod(agenda,"TrasaBitmapaZPole", dataDB, sku, lin, smer, 7) '= "" {
                    s vzorTisk = vzorTisk + 1
                } else {
                    s vzorFallback = vzorFallback + 1
                }

                // obsluhované pozice = kandidáti na "aktuální zastávku" stránky
                k served
                d $classmethod(agenda,"ObsluhovanePoradi", dataDB, sku, lin, smer, .served)

                s poradi = ""
                f {
                    s poradi = $o(served(poradi))
                    q:poradi=""
                    i '$isvalidnum(poradi) continue
                    s zastavek = zastavek + 1

                    k pismenoSpoj, legenda
                    d $classmethod(agenda,"RozdilyTras", dataDB, sku, lin, smer, poradi, .pismenoSpoj, .legenda)

                    s key = ""
                    f {
                        s key = $o(legenda(key))
                        q:key=""
                        s txt = $g(legenda(key))
                        i txt = "" continue
                        s legendCelkem = legendCelkem + 1
                        continue:txt'[fraze

                        s nalezu = nalezu + 1
                        i i < 30 {
                            // název aktuální zastávky = stránka vývěsky, na které se poznámka objeví
                            s nazAkt = $classmethod(agenda,"NazZPoradi", dataDB, sku, lin, poradi)
                            s ^||ML("OUT",$i(i)) = "linka="_lin_"~smer="_smer_"~poradi="_poradi
                                _"~zastavkaStranky="_nazAkt_"~text="_txt
                        }
                    }
                }
            }
        }

        s ^||ML("OUT","SOUHRN") = "linek="_linek_"~smeru="_smeru_"~zastavek="_zastavek
            _"~legendCelkem="_legendCelkem_"~nalezu="_nalezu
            _"~vzorZakladni="_vzorZakl_"~vzorTisteny="_vzorTisk_"~vzorFallbackUnion="_vzorFallback
            _"~autoPoznVypnuto="_autoPoznVyp
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

}
