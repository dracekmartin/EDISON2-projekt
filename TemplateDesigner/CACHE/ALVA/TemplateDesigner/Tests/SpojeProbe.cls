Include GENERAL

/// Diagnostika: které spoje agenda vrátí a v jaké skupině dnů. Read-only.
Class ALVA.TemplateDesigner.Tests.SpojeProbe Extends %RegisteredObject
{

/// `GetData` s přesnými parametry z panelu (Jan 4. 9.) - kolik spojů dorazí.
ClassMethod SJehoParametry(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        k ^||ML("OUT")
        s ^||ML("IN","SKUPINA_LINEK_ID") = 2
        s ^||ML("IN","LINKA_ID") = "0320144"
        s ^||ML("IN","TVAR_NAZVU_ZASTAVKY") = "Plný CIS"
        s ^||ML("IN","NAZEV_PRO_MHD") = "true"
        s ^||ML("IN","OBOUSMERNY_TISK") = "false"
        s ^||ML("IN","DELENI_SLOUPCU") = "Pracovní dny / Soboty / Neděle+svátky"
        s ^||ML("IN","SLOUPEC_TC") = "Automaticky (dle linky)"
        s ^||ML("IN","ZASTAVKY_BEZ_ZASTAVENI") = "Automaticky (dle linky)"
        s ^||ML("IN","BARVY_SPOJU") = "Netisknout"
        d ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).GetData(.sys)

        k souhrn
        s key = ""
        f {
            s key = $o(^||ML("OUT", key))
            q:key=""
            continue:key="ERR"
            s page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
            s spoje = page.rows.%Get("SPOJE"), zast = page.rows.%Get("ZASTAVKY")
            s cisla = ""
            i $isobject(spoje) {
                f si=0:1:spoje.%Size()-1 { s cisla = cisla_$s(cisla="":"", 1:",")_spoje.%Get(si).%Get("CISLO_SPOJE") }
            }
            s souhrn($i(souhrn)) = "stranka "_key_" smer="_page.values.%Get("SMER")
                _" zastavek="_$s($isobject(zast):zast.%Size(), 1:0)
                _" spoju="_$s($isobject(spoje):spoje.%Size(), 1:0)_" | "_$e(cisla,1,220)
        }
        k ^||ML("OUT"), ^||ML("IN")
        s i = 0, s2 = ""
        f { s s2 = $o(souhrn(s2))  q:s2=""  s ^||ML("OUT",$i(i)) = souhrn(s2) }
    }
    catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Celé `GetData` tak, jak ho volá návrhář (jen LINKA_ID) - kolik spojů dorazí na klienta.
ClassMethod PresGetData(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        k ^||ML("OUT")
        s ^||ML("IN","SKUPINA_LINEK_ID") = 2
        s ^||ML("IN","LINKA_ID") = "0320144"
        d ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).GetData(.sys)

        k souhrn
        s key = ""
        f {
            s key = $o(^||ML("OUT", key))
            q:key=""
            continue:key="ERR"
            s page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
            s spoje = page.rows.%Get("SPOJE")
            s cisla = ""
            i $isobject(spoje) {
                f si=0:1:spoje.%Size()-1 {
                    s cisla = cisla_$s(cisla="":"", 1:",")_spoje.%Get(si).%Get("CISLO_SPOJE")
                }
            }
            s souhrn($i(souhrn)) = "stranka "_key_" smer="_page.values.%Get("SMER")
                _" spoju="_$s($isobject(spoje):spoje.%Size(), 1:0)_" | "_$e(cisla,1,240)
        }
        k ^||ML("OUT"), ^||ML("IN")
        s i = 0, s2 = ""
        f { s s2 = $o(souhrn(s2))  q:s2=""  s ^||ML("OUT",$i(i)) = souhrn(s2) }
    }
    catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Všechny režimy dělení sloupců - hledá se, jestli v některém spoje vypadnou.
ClassMethod VsechnaDeleni(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s i = 0
        f deleni = 0, 1, 2, 3 {
            s page = ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).BuildStrankaLinky(dataDB, 2, "0320144", "T", deleni)
            i page = "" {
                s ^||ML("OUT",$i(i)) = "deleni="_deleni_"|STRANKA PRAZDNA"
                continue
            }
            s spoje = page.%Get("rows").%Get("SPOJE")
            k videno
            s pocet = 0
            f si=0:1:spoje.%Size()-1 {
                s cislo = spoje.%Get(si).%Get("CISLO_SPOJE")
                s videno(cislo) = ""
                s pocet = pocet + 1
            }
            s unikatu = 0, c = ""
            f { s c = $o(videno(c))  q:c=""  s unikatu = unikatu + 1 }
            s ^||ML("OUT",$i(i)) = "deleni="_deleni_"|radku="_pocet_"|unikatnich spoju="_unikatu
                _"|ma 501="_$s($d(videno(501)):"ano", 1:"NE")
                _"|ma 509="_$s($d(videno(509)):"ano", 1:"NE")
        }
    }
    catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Pevně CB_GWBUS 2 / 0320144, směr Tam - tam chybí řada spojů 501+.
ClassMethod Vypis(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s page = ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).BuildStrankaLinky(dataDB, 2, "0320144", "T", 2)
        i page = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "stranka se nesestavila"))

        s i = 0
        s skupiny = page.%Get("rows").%Get("SKUPINY_DNU")
        s txt = ""
        i $isobject(skupiny) {
            f gi=0:1:skupiny.%Size()-1 {
                s g = skupiny.%Get(gi)
                s txt = txt_$s(txt="":"", 1:" | ")_g.%Get("ID")_"="_g.%Get("NADPIS")
            }
        }
        s ^||ML("OUT",$i(i)) = "SKUPINY|"_txt

        s spoje = page.%Get("rows").%Get("SPOJE")
        s radek = "", pocet = 0
        f si=0:1:spoje.%Size()-1 {
            s sp = spoje.%Get(si)
            s pocet = pocet + 1
            s radek = radek_$s(radek="":"", 1:",")_sp.%Get("CISLO_SPOJE")_"/sk"_sp.%Get("SKUPINA")
                _"/sm"_sp.%Get("SMER")_"/cas"_$s($isobject(sp.%Get("CASY")):sp.%Get("CASY").%Size(), 1:"-")
            i $l(radek) > 300 { s ^||ML("OUT",$i(i)) = "SPOJE|"_radek  s radek = "" }
        }
        i radek '= "" s ^||ML("OUT",$i(i)) = "SPOJE|"_radek
        s ^||ML("OUT",$i(i)) = "POCET|"_pocet
    }
    catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

}
