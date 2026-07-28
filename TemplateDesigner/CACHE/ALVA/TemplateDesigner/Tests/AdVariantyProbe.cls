Include (%occInclude, GENERAL)

/// Read-only sonda variant tras (KOVED #13048 bod 5): per zastávka × směr porovná počet variant
/// dle ČASOVÉHO PROFILU (stará granularita TD) vs dle TRASY (nová = legacy `prutmbmp`; `|` = nestaví)
/// + diagnosticky trasa VČETNĚ nástupišť (kolik sloupců by přidalo rozlišení sloupků — vědomě
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

/// Zavolá OSTROU BuildStrankaZastavky pro jednu zastávku a shrne variantní výstup: hlavičky
/// sloupců (VARIANTY_TRAS), TRASA_MIN vývěskové zastávky a rozpad anotací u minut dle kategorie.
/// Parametry tisku (PISMENA_VARIANT / PRVNI_BEZ_PISMENE / RAZENI_VARIANT) čte engine přímo
/// z ^||ML("IN") — sonda je jen průhledně předává.
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
            s ^||ML("OUT","POCET_POZNAMEK") = page.rows.%Get("POZNAMKY").%Size()
        }
    } catch (ex) { $$$CatchErrRest }
    k ^||TiskAd
    ret $$$OK
}

}
