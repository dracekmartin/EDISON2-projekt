Include GENERAL

/// Měření dat: kolik přípojů má u sebe vyplněné pole „linka pro tisk", kterým starý modul
/// přepisuje číslo linky (u vyčkání 11. položka věty, u navázání 12.). Podklad k rozhodnutí,
/// jestli ten přepis dorovnat. Read-only, nic nemění.
Class ALVA.TemplateDesigner.Tests.LinkaProTiskMereni Extends %RegisteredObject
{

/// OUT: řádky per firma + celkový součet + pár příkladů, kde se čísla liší.
ClassMethod Zmer(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s i = 0
        s (celkem, sPolem, jineCislo, linek) = 0
        f fi=1:1:$l("CB_CSADAD,KV_DP,VS_CSAD,CB_GWBUS", ",") {
            s db = $p("CB_CSADAD,KV_DP,VS_CSAD,CB_GWBUS", ",", fi)
            i '##class(%SYS.Namespace).Exists(db) {
                s ^||ML("OUT",$i(i)) = db_"|NAMESPACE NENI"
                continue
            }
            s (fCelkem, fSPolem, fJine, fLinek) = 0
            s sku = ""
            f {
                s sku = $o(^[db]JRL(sku))
                q:sku=""
                i 'sku continue
                s lin = ""
                f {
                    s lin = $o(^[db]JRL(sku,lin))
                    q:lin=""
                    i '$d(^[db]JRL(sku,lin,"I")) continue
                    s maPole = 0
                    f smer = "T", "Z" {
                        s sp = ""
                        f {
                            s sp = $o(^[db]JRL(sku,lin,smer,sp))
                            q:sp=""
                            i '$isvalidnum(sp) continue
                            f omzId = 31, 32 {
                                s omz = $g(^[db]JRL(sku,lin,smer,sp,omzId))
                                i omz = "" continue
                                f sekceIdx=2:1:$l(omz,"@") {
                                    s sekce = $p(omz,"@",sekceIdx)
                                    i sekce = "" continue
                                    s prvniConn = $s(omzId=31:3, 1:2)
                                    f connIdx=prvniConn:1:$l(sekce,"#") {
                                        s conn = $p(sekce,"#",connIdx)
                                        i conn = "" continue
                                        // Pozice se mezi uzly liší - stejně jako v LinkovyJrAd.
                                        i omzId = 31 {
                                            s linka = $p(conn,":",2), typVystupu = +$p(conn,":",4)
                                            s typZobrazeni = +$p(conn,":",7), proTisk = $p(conn,":",11)
                                        }
                                        else {
                                            s linka = $p(conn,":",1), typVystupu = +$p(conn,":",4)
                                            s typZobrazeni = +$p(conn,":",9), proTisk = $p(conn,":",12)
                                        }
                                        i typVystupu = 2 continue
                                        // Jen přípoje, které dělají řádek - jinde se číslo linky netiskne.
                                        i (typZobrazeni '= 1) && (typZobrazeni '= 2) continue
                                        s fCelkem = fCelkem + 1
                                        i proTisk '= "" {
                                            s fSPolem = fSPolem + 1
                                            s maPole = 1
                                            i proTisk '= linka {
                                                s fJine = fJine + 1
                                                i $g(prikladu(db)) < 3 {
                                                    s prikladu(db) = $g(prikladu(db)) + 1
                                                    s priklady($i(priklady)) = db_" sku="_sku_" lin="_lin
                                                        _" spoj="_sp_" uzel="_omzId_" linka="_linka_" proTisk="_proTisk
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    i maPole s fLinek = fLinek + 1
                }
            }
            s ^||ML("OUT",$i(i)) = db_"|pripoju s radkem="_fCelkem_"|s polem linka pro tisk="_fSPolem
                _"|z toho jine cislo="_fJine_"|linek="_fLinek
            s celkem = celkem + fCelkem, sPolem = sPolem + fSPolem
            s jineCislo = jineCislo + fJine, linek = linek + fLinek
        }
        s ^||ML("OUT",$i(i)) = "CELKEM|pripoju s radkem="_celkem_"|s polem linka pro tisk="_sPolem
            _"|z toho jine cislo="_jineCislo_"|linek="_linek
        s pi = ""
        f {
            s pi = $o(priklady(pi))  q:pi=""
            s ^||ML("OUT",$i(i)) = "PRIKLAD|"_priklady(pi)
        }
    }
    catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

}
