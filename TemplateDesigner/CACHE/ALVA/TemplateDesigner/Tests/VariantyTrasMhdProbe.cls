Include GENERAL

/// Sonda: jak by vypadaly varianty tras na vývěsce MHD. Jen čte - trasu linky připraví fáze 1
/// zastávkového JŘ MHD (`ZastavkoveJrMhd.PripravitTrasu`) do `^||TiskMHD`, spoje rozloží
/// `ALVA.MHD.SpojeFce.MakeTableSpoj` stejně jako fáze 4. Spouští se ručně přes MLRest.
///
/// Varianta = sled bodů trasy od vývěskové zastávky dál, kde spoj staví (S), projíždí (<) nebo
/// kudy nejede (.), jako u vývěsky AD. Počítají se jen spoje, které z vývěskové zastávky odjíždějí
/// a mají sloupec minut (typ provozu se SMO1-4). Body s příznakem „netisknout" se do varianty
/// počítají zvlášť (`variantVcSkrytych`), protože je vývěska neukazuje.
Class ALVA.TemplateDesigner.Tests.VariantyTrasMhdProbe Extends %RegisteredObject
{

/// Souhrn po linkách. IN: LINKY = LinkaID čárkou, prázdné = linky se spoji; MAX = nejvýš tolik
/// linek (výchozí 40). OUT(n) = JSON za linku.
ClassMethod Prehled(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        k ^||ML("OUT")
        s seznam = $g(^||ML("IN","LINKY")), max = +$g(^||ML("IN","MAX"))
        i 'max s max = 40
        k linky
        i seznam '= "" {
            f i=1:1:$l(seznam,",") s linky($i(linky)) = $p(seznam,",",i)
        } else {
            s lin = ""
            f {
                s lin = $o(^[dataDB]ALVA.MHD.LSO(IdFa,2,"IDX",1,1,1,lin)) q:(lin="")||($g(linky)>=max)
                s linky($i(linky)) = lin
            }
        }
        f i=1:1:$g(linky) {
            k vysl
            s start = $zh
            d ..Rozbor(.sys, linky(i), .vysl)
            s o = ..Souhrn(IdFa, dataDB, linky(i), .vysl)
            d o.%Set("ms", +$j(($zh - start) * 1000, 0, 0), "number")
            s ^||ML("OUT",$i(^||ML("OUT"))) = o.%ToJSON()
        }
    } catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Vývěsky jedné linky po variantách. IN: LINKA_ID; MAX = nejvýš tolik vývěsek (výchozí 30);
/// JEN_VARIANTY = 1 vypíše jen vývěsky s víc než jednou variantou. OUT(n) = řádek textu.
ClassMethod Linka(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        k ^||ML("OUT")
        s LinkaID = $g(^||ML("IN","LINKA_ID")), max = +$g(^||ML("IN","MAX")), jenVarianty = +$g(^||ML("IN","JEN_VARIANTY"))
        i 'max s max = 30
        k vysl
        d ..Rozbor(.sys, LinkaID, .vysl)
        i $g(vysl("chyba")) '= "" s ^||ML("OUT",1) = vysl("chyba") q
        s vypsano = 0
        f SmerID = 0,1 {
            s P = ""
            f {
                s P = $o(vysl("vyvesky",SmerID,P)) q:(P="")||(vypsano>=max)
                s s = vysl("vyvesky",SmerID,P)
                i jenVarianty && ($lg(s,2) < 2) continue
                s vypsano = vypsano + 1
                s zastID = $lg($g(^||TiskMHD("trasa",SmerID,P)),1)
                s nazev = $lg(##class(ALVA.MHD.ZastavkyFce).GetNazevHrl(IdFa, dataDB, zastID, vysl("platnostOdHrl")),1)
                s ^||ML("OUT",$i(^||ML("OUT"))) = $s(SmerID:"Z",1:"T")_" bod "_P_" "_nazev_": odjíždí "_$lg(s,1)_", variant "_$lg(s,2)_" (se skrytými body "_$lg(s,3)_"), časy jako model "_$lg(s,6)_"/"_$lg(s,1)_", nejvíc o "_$lg(s,7)_" min"
                s vi = ""
                f {
                    s vi = $o(vysl("varianty",SmerID,P,vi), 1, d) q:vi=""
                    s ^||ML("OUT",$i(^||ML("OUT"))) = "    "_$c(64 + vi)_": "_$lg(d,1)_" spojů, staví "_$lg(d,2)_", průběhů časů "_$lg(d,4)_", rozptyl "_$lg(d,3)_" min, sled "_$lg(d,5)
                }
            }
        }
    } catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Pole hlavičky spoje (co spoj o své trase ví). IN: SPOJ_ID, nebo LINKA_ID = první spoj linky.
/// OUT(n) = „pole = hodnota", u pole s podřízenými uzly „pole (uzly)".
ClassMethod PoleSpoje(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        k ^||ML("OUT")
        s SpojID = $g(^||ML("IN","SPOJ_ID"))
        i SpojID = "" s SpojID = $o(^[dataDB]ALVA.MHD.LSO(IdFa,2,"IDX",1,1,1,$g(^||ML("IN","LINKA_ID")),""))
        s ^||ML("OUT",$i(^||ML("OUT"))) = "SpojID = "_SpojID
        s pole = ""
        f {
            s pole = $o(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",SpojID,"DTA",1,pole)) q:pole=""
            s d = $d(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",SpojID,"DTA",1,pole))
            s ^||ML("OUT",$i(^||ML("OUT"))) = pole_$s(d#2:" = "_$g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",SpojID,"DTA",1,pole)), 1:"")_$s(d>1:" (uzly)", 1:"")
        }
    } catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Rozbor jedné linky do `v`:
/// v("vyvesky",SmerID,BodID) = $lb(odjíždí, variant, variant se skrytými body, největší rozptyl,
///     nejvíc průběhů časů v jedné variantě, spojů s časy jako model, největší odchylka od modelu)
/// v("varianty",SmerID,BodID,i) = $lb(spojů, staví, rozptyl, průběhů časů, sled)
/// v("spoju"), v("pulnoc",SpojID,BodID) = odjezd v 0:00, v("bezCasu") = zastavení bez času.
ClassMethod Rozbor(ByRef sys As %String, LinkaID As %String, ByRef v)
{
    $$$FillParams
    k ^||TiskMHD, spoj
    s TrasaID = ##class(ALVA.MHD.LinkyFce).GetTrasaId(IdFa, dataDB, LinkaID)
    i TrasaID = "" s v("chyba") = "linka nemá trasu" q
    s platnostOdHrl = $zdh($g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"PlatiOd")),3,,,,,,,"")
    s VariantaChronometrazeID = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"VariantaChronometrazeID"))
    s v("platnostOdHrl") = platnostOdHrl
    s ^||TiskMHD("config","LinkaID") = LinkaID
    s ^||TiskMHD("config","TrasaID") = TrasaID
    s ^||TiskMHD("config","PlatnostChronometrazeID") = ##class(ALVA.MHD.ChronometrazeFce).GetPlatnostChronometrazeID(IdFa, dataDB, VariantaChronometrazeID, platnostOdHrl)
    d ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).PripravitTrasu(.sys)

    // Spoje rozložené do bodů trasy stejně jako fáze 4: sloučené body, průjezd, čas = max(příjezd, odjezd).
    s SpojID = ""
    f {
        s SpojID = $o(^[dataDB]ALVA.MHD.LSO(IdFa,2,"IDX",1,1,1,LinkaID,SpojID)) q:SpojID=""
        s SmerID = $g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",SpojID,"DTA",1,"SmerID"))
        s TypProvozuID = $g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",SpojID,"DTA",1,"TypProvozuID"))
        i (TypProvozuID = "") || (SmerID = "") continue
        i ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).SmoSloupec(dataDB, IdFa, TypProvozuID) = "" continue
        s smerKrok = $s(SmerID:-1, 1:1)
        k tab
        i '##class(ALVA.MHD.SpojeFce).MakeTableSpoj(.sys, SpojID, "", .tab) continue
        s konecnyBod = $s(SmerID:$o(tab("arr",1,"")), 1:$o(tab("arr",1,""),-1))
        s v("spoju") = $g(v("spoju")) + 1
        s BodID = ""
        f {
            s BodID = $o(^||TiskMHD("trasa",SmerID,BodID)) q:BodID=""
            i 'BodID continue
            s stav = "", cas = "", jeKonecny = (BodID = konecnyBod), sub = "", nula = 0
            f {
                s sub = $o(^||TiskMHD("trasa",SmerID,"slouceni",BodID,sub), smerKrok) q:sub=""
                i sub = konecnyBod s jeKonecny = 1
                i '$d(tab("arr",1,sub)) continue
                s bodVal = tab("arr",1,sub)
                i $lg(bodVal,5) {
                    s:stav="" stav = "<"
                    continue
                }
                s c = $lg(bodVal,3)
                i $lg(bodVal,4) > c s c = $lg(bodVal,4)
                i c = "" s v("bezCasu") = $g(v("bezCasu")) + 1 continue
                i +c = 0 s nula = 1
                i (cas = "") || (c > cas) s cas = c
                s stav = "S"
            }
            i stav '= "" s spoj(SmerID,SpojID,BodID) = $lb(stav, cas, jeKonecny)
            // Fáze 4 bere čas přes `cas > casOdjezdu` s prázdným startem, takže odjezd v 0:00 vynechá.
            i nula && (+cas = 0) && 'jeKonecny s v("pulnoc",SpojID,BodID) = SmerID
        }
    }

    f SmerID = 0,1 {
        s smerKrok = $s(SmerID:-1, 1:1)
        s P = ""
        f {
            s P = $o(^||TiskMHD("trasa",SmerID,P)) q:P=""
            i 'P continue
            s modelP = $lg($g(^||TiskMHD("trasa",SmerID,P)),2)
            k varianty, vsechny, pocet, prubehy, minRel, maxRel, sled
            s odjizdi = 0, jakoModel = 0, maxOdchylka = 0
            s t = ""
            f {
                s t = $o(spoj(SmerID,t)) q:t=""
                s vp = $g(spoj(SmerID,t,P))
                i ($lg(vp,1) '= "S") || $lg(vp,3) continue
                s odjizdi = odjizdi + 1, casP = $lg(vp,2)
                s sigVse = "", sigVid = "", prubeh = "", odchylka = 0, k = 0, Q = ""
                f {
                    s Q = $s(Q="":P, 1:$o(^||TiskMHD("trasa",SmerID,Q), smerKrok)) q:(Q="")||('Q)
                    s x = $g(spoj(SmerID,t,Q)), stav = $lg(x,1)
                    s znak = $s(stav="":".", 1:stav)
                    s sigVse = sigVse_znak
                    i (Q = P) || '$d(^||TiskMHD("trasa",SmerID,"netisknout",Q)) s sigVid = sigVid_znak
                    s k = k + 1
                    i stav '= "S" s $p(prubeh,"~",k) = "" continue
                    s rel = $lg(x,2) - casP
                    i rel < 0 s rel = rel + 1440
                    s $p(prubeh,"~",k) = rel
                    s rozdil = rel - ($lg($g(^||TiskMHD("trasa",SmerID,Q)),2) - modelP)
                    i rozdil < 0 s rozdil = -rozdil
                    i rozdil > odchylka s odchylka = rozdil
                }
                i '$d(varianty(sigVid)) s varianty(sigVid) = $i(varianty), sled(varianty) = sigVid
                s vi = varianty(sigVid)
                s vsechny(sigVse) = ""
                s pocet(vi) = $g(pocet(vi)) + 1
                s prubehy(vi,prubeh) = ""
                i 'odchylka s jakoModel = jakoModel + 1
                i odchylka > maxOdchylka s maxOdchylka = odchylka
                f j=1:1:$l(prubeh,"~") {
                    s r = $p(prubeh,"~",j)
                    i r = "" continue
                    i '$d(minRel(vi,j)) || (r < minRel(vi,j)) s minRel(vi,j) = r
                    i '$d(maxRel(vi,j)) || (r > maxRel(vi,j)) s maxRel(vi,j) = r
                }
            }
            i 'odjizdi continue
            s pocetVse = 0, s = ""
            f {
                s s = $o(vsechny(s)) q:s=""
                s pocetVse = pocetVse + 1
            }
            s maxRozptyl = 0, maxPrubehu = 0
            f vi=1:1:$g(varianty) {
                s rozptyl = 0, j = ""
                f {
                    s j = $o(minRel(vi,j)) q:j=""
                    i maxRel(vi,j) - minRel(vi,j) > rozptyl s rozptyl = maxRel(vi,j) - minRel(vi,j)
                }
                s prubehu = 0, pr = ""
                f {
                    s pr = $o(prubehy(vi,pr)) q:pr=""
                    s prubehu = prubehu + 1
                }
                i rozptyl > maxRozptyl s maxRozptyl = rozptyl
                i prubehu > maxPrubehu s maxPrubehu = prubehu
                s v("varianty",SmerID,P,vi) = $lb(pocet(vi), $l(sled(vi),"S") - 1, rozptyl, prubehu, sled(vi))
            }
            s v("vyvesky",SmerID,P) = $lb(odjizdi, $g(varianty), pocetVse, maxRozptyl, maxPrubehu, jakoModel, maxOdchylka)
        }
    }
}

/// Souhrn rozboru linky pro `Prehled`.
ClassMethod Souhrn(IdFa As %String, dataDB As %String, LinkaID As %String, ByRef v) As %DynamicObject
{
    s o = ##class(%DynamicObject).%New()
    d o.%Set("linkaId", LinkaID)
    d o.%Set("cislo", $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"CisloVerejne")))
    i $g(v("chyba")) '= "" d o.%Set("chyba", v("chyba")) q o
    d o.%Set("spoju", +$g(v("spoju")), "number")
    s vyvesek = 0, sVariantami = 0, maxVariant = 0, maxVcSkrytych = 0, maxRozptyl = 0, sRozptylem = 0
    s maxPrubehu = 0, odjezdu = 0, jakoModel = 0, maxOdchylka = 0, sOdchylkou = 0
    k cetnost
    f SmerID = 0,1 {
        s P = ""
        f {
            s P = $o(v("vyvesky",SmerID,P), 1, s) q:P=""
            s vyvesek = vyvesek + 1
            s cetnost($lg(s,2)) = $g(cetnost($lg(s,2))) + 1
            i $lg(s,2) > 1 s sVariantami = sVariantami + 1
            i $lg(s,2) > maxVariant s maxVariant = $lg(s,2)
            i $lg(s,3) > maxVcSkrytych s maxVcSkrytych = $lg(s,3)
            i $lg(s,4) > maxRozptyl s maxRozptyl = $lg(s,4)
            i $lg(s,4) s sRozptylem = sRozptylem + 1
            i $lg(s,5) > maxPrubehu s maxPrubehu = $lg(s,5)
            s odjezdu = odjezdu + $lg(s,1), jakoModel = jakoModel + $lg(s,6)
            i $lg(s,7) > maxOdchylka s maxOdchylka = $lg(s,7)
            i $lg(s,7) s sOdchylkou = sOdchylkou + 1
        }
    }
    s text = "", n = ""
    f {
        s n = $o(cetnost(n)) q:n=""
        s text = text_$s(text="":"", 1:" ")_n_"x"_cetnost(n)
    }
    d o.%Set("vyvesek", vyvesek, "number")
    d o.%Set("variantNaVyvesku", text)
    d o.%Set("vyvesekSVariantami", sVariantami, "number")
    d o.%Set("maxVariant", maxVariant, "number")
    d o.%Set("maxVariantVcSkrytych", maxVcSkrytych, "number")
    d o.%Set("vyvesekSRozptylem", sRozptylem, "number")
    d o.%Set("maxRozptylMin", maxRozptyl, "number")
    d o.%Set("maxPrubehuVeVariante", maxPrubehu, "number")
    d o.%Set("odjezduJakoModel", jakoModel_"/"_odjezdu)
    d o.%Set("vyvesekOdchylkaOdModelu", sOdchylkou, "number")
    d o.%Set("maxOdchylkaOdModeluMin", maxOdchylka, "number")
    s odjezdV0000 = "", sp = ""
    f {
        s sp = $o(v("pulnoc",sp)) q:sp=""
        s b = ""
        f {
            s b = $o(v("pulnoc",sp,b)) q:b=""
            s odjezdV0000 = odjezdV0000_$s(odjezdV0000="":"", 1:" ")_sp_"/"_b
        }
    }
    d o.%Set("odjezdyV0000", odjezdV0000)
    d o.%Set("zastaveniBezCasu", +$g(v("bezCasu")), "number")
    q o
}

}
