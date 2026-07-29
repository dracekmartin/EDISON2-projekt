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

        s agenda = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
        s (linek, smeru, zastavek, legendCelkem, nalezu, i) = 0

        s lin = ""
        f {
            s lin = $o(^[dataDB]JRL(sku,lin))
            q:lin=""
            i '$d(^[dataDB]JRL(sku,lin,"I")) continue
            s linek = linek + 1
            q:linek>limit

            f smerIdx=1:1:2 {
                s smer = $s(smerIdx=1:"T", 1:"Z")
                s smeru = smeru + 1

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
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

}
