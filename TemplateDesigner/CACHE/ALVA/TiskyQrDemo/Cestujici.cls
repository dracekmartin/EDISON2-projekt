Include GENERAL

/// ⚠️ **Jednorázová demo třída** pro `edwcore/apps/tisky-qr` (aplikace pro cestující z QR na výlepu
/// linkového JŘ). Schválně mimo `ALVA.TemplateDesigner`, aby se nepletla s návrhářem a nevydala
/// se s ním; v žádném Caché projektu není. Až se demo rozhodne, buď se smaže, nebo se přepíše
/// do produkční podoby.
///
/// Tenká JSON obálka nad `ALVA.TemplateDesigner.Agendy.LinkovyJrAd.GetData`, volaná přes MLRest
/// `QueryJSON` (signatura `(sys, in, out)`). Stránky agendy vrací beze změny a doplní, co aplikace
/// potřebuje navíc: omezení spojů jako data, polohu zastávek a označníků a vzdálenost mezi
/// označníky obou směrů.
///
/// IN:  {"idfa":"ZL_KOVED","linka":"822995","tc":"14"}
/// OUT: {"success":true,"data":{"skupina":1,"linka":"0822995","stranky":[...],
///        "oznacniky":{"T":{...},"Z":{...}},"vzdalenostOznacnikuM":45}}
Class ALVA.TiskyQrDemo.Cestujici Extends %RegisteredObject
{

/// Technická identita volajícího v kontextu ERP - `$$$FillParams` na prázdném jménu spadne.
Parameter VOLAJICI = "TISKY-QR";

/// Stránky linkového JŘ (oba směry) se zvýrazněnou zastávkou výlepu.
/// Skupina linek se do QR nedává (mění se s novým JŘ), dohledá ji `SkupinaLinky`.
ClassMethod Linka(ByRef sys, ByRef in, ByRef out) As %Status
{
    s err = "", code = ""
    try {
        // QueryJSON předává tělo už rozparsované; text nebo stream jen pro jiné volající.
        i $isobject(in) && in.%IsA("%DynamicAbstractObject") { s req = in }
        else { s req = ##class(%DynamicObject).%FromJSON(in) }
        s idfa = req.%Get("idfa"), linka = req.%Get("linka"), tc = req.%Get("tc")
        i (idfa = "") || (linka = "") s code = "VSTUP", err = "Chybí idfa nebo linka" ztrap
        d ..BuildSys(idfa, .sys)
        s dataDB = sys("U","DATA")
        s lin = $tr($j(linka, 7), " ", "0")

        s skupina = ..SkupinaLinky(dataDB, lin)
        i skupina = "" s code = "NENALEZENO", err = "Linka "_linka_" neexistuje" ztrap

        k ^||ML("IN"), ^||ML("OUT"), ^||ML("ERR")
        s ^||ML("IN","SKUPINA_LINEK_ID") = skupina
        s ^||ML("IN","LINKA_ID") = lin
        s ^||ML("IN","ZVYRAZNIT_TC") = tc
        d ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).GetData(.sys)
        i $g(^||ML("ERR")) '= "" s code = "SYSTEM", err = ^||ML("ERR") ztrap

        s stranky = ##class(%DynamicArray).%New(), oznacniky = {}, key = ""
        f {
            s key = $o(^||ML("OUT", key))
            q:key=""
            continue:key="ERR"
            s stranka = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
            d ..PridejOmezeni(dataDB, skupina, lin, stranka)
            s oznacnik = ..PridejPolohy(dataDB, skupina, lin, stranka)
            s smer = stranka.values.%Get("SMER")
            i $isobject(oznacnik) && (smer '= "") d oznacniky.%Set(smer, oznacnik)
            d stranky.%Push(stranka)
        }
        k ^||ML("IN"), ^||ML("OUT")

        s data = {"skupina":(skupina),"linka":(lin),"stranky":(stranky),"oznacniky":(oznacniky)}
        s platby = ..Platby(dataDB, idfa, lin)
        i $isobject(platby) d data.%Set("platby", platby)
        s tam = oznacniky.%Get("T"), zpet = oznacniky.%Get("Z")
        i $isobject(tam) && $isobject(zpet) && (tam.%Get("lat") '= "") && (zpet.%Get("lat") '= "") {
            d data.%Set("vzdalenostOznacnikuM", ..VzdalenostM(tam.lat, tam.lon, zpet.lat, zpet.lon), "number")
        }
        // Všechny označníky zastávky výlepu pro mapu - cestující má poznat, ke kterému z nich jít.
        s czstVylepu = ""
        i stranky.%Size() > 0 s czstVylepu = stranky.%Get(0).values.%Get("AKTUALNI_ZASTAVKA_CZST")
        i czstVylepu '= "" d data.%Set("oznacnikyZastavky", ..OznacnikyZastavky(dataDB, czstVylepu))
        s out = {"success":true,"data":(data)}
    }
    catch ex {
        i err = "" s err = ex.DisplayString()
        i code = "" s code = "SYSTEM"
        i $isobject($g(%response)) s %response.Status = $s(code = "SYSTEM":500, 1:400)
        s out = {"success":false,"error":{"code":(code),"message":(err)}}
    }
    q $$$OK
}

/// Skupina linek, podle které cestující jede dnes: ta, jejíž platnost linky
/// (`^JRL(sku,lin,"PATCH","Plati")` = `od~do`, `d.m.yyyy`, prázdné „do" = bez konce) zahrnuje dnešek;
/// při víc shodách ta s nejpozdějším začátkem. Nejvyšší číslo skupiny nestačí - firma v nich drží
/// i pracovní a archivní kopie (CB_CSADAD 861 „Označníky - JH" s linkou 340313 z roku 2023).
/// Když dnes neplatí žádná, bere se nejvyšší skupina s linkou (aplikace pak ukáže starý JŘ).
ClassMethod SkupinaLinky(dataDB As %String, lin As %String) As %String
{
    s dnes = +$h, nejlepsi = "", nejlepsiOd = "", zaloha = "", sku = ""
    f {
        s sku = $o(^[dataDB]JRL(sku), -1)
        q:sku=""
        continue:'$isvalidnum(sku)
        continue:'$d(^[dataDB]JRL(sku, lin, "I"))
        i zaloha = "" s zaloha = sku
        s plati = $g(^[dataDB]JRL(sku, lin, "PATCH", "Plati"))
        s od = $zdh($tr($p(plati,"~",1),".","/"), 4, , , , , , , "")
        s do = $zdh($tr($p(plati,"~",2),".","/"), 4, , , , , , , "")
        continue:od=""
        continue:(od > dnes)||((do '= "") && (do < dnes))
        i (nejlepsi = "") || (od > nejlepsiOd) s nejlepsi = sku, nejlepsiOd = od
    }
    q $s(nejlepsi '= "":nejlepsi, 1:zaloha)
}

/// Ke každému spoji stránky přidá `OMEZENI` - datumová omezení jako data, aby aplikace sama
/// poznala, jestli spoj jede dnes. Tisk je má jen jako text (`Jr.DataAd.TextOmezeni`), který se skládá
/// podle nastavení firmy a může být i bez roku, takže se z něj spolehlivě číst nedá.
///   OMEZENI: [{"druh":"jen|jede|take|nejede","useky":[{"od":"2022-12-24","do":"2023-01-01"}]}]
///   TYDNY:   "sude" | "liche" | ""  (dlouhodobá platnost spoje)
ClassMethod PridejOmezeni(dataDB As %String, sku As %String, lin As %String, stranka As %DynamicObject)
{
    s spoje = stranka.rows.%Get("SPOJE")
    q:'$isobject(spoje)
    s it = spoje.%GetIterator()
    while it.%GetNext(.i, .spoj) {
        s smer = spoj.%Get("SMER"), sp = spoj.%Get("CISLO_SPOJE")
        continue:(smer = "")||(sp = "")
        s dlouhodoba = $p($p($g(^[dataDB]JRL(sku,lin,smer,sp)),"#",1),":",8)
        d spoj.%Set("TYDNY", $s(dlouhodoba = 4:"sude", dlouhodoba = 5:"liche", 1:""))
        s omezeni = ##class(%DynamicArray).%New()
        // Druhy omezení podle klíče pod spojem (význam viz `Jr.DataAd.TextOmezeni`).
        f omzId=11,12,13,22 {
            s omz = $g(^[dataDB]JRL(sku,lin,smer,sp,omzId))
            continue:omz=""
            s data = $s(omz["@":$p(omz,"@",2), 1:omz)
            s useky = ##class(%DynamicArray).%New()
            f j = 1:1:$l(data,"#") {
                s polozka = $p(data,"#",j)
                continue:polozka=""
                s od = ..IsoDatum($p(polozka,":",1)), do = ..IsoDatum($p(polozka,":",2))
                continue:od=""
                d useky.%Push({"od":(od),"do":($s(do'="":do, 1:od))})
            }
            continue:useky.%Size()=0
            d omezeni.%Push({"druh":($case(omzId, 11:"jen", 12:"take", 13:"jede", :"nejede")),"useky":(useky)})
        }
        d spoj.%Set("OMEZENI", omezeni)
    }
}

/// Poloha pro mapu. Každému řádku zastávky `LAT`/`LON` z číselníku zastávek (`^JRZ` pole 12 a 13).
/// Řádku zastávky výlepu navíc `OZNACNIK` - označník, ze kterého v tomhle směru linka jede:
/// trasa linky na něj odkazuje polem sloupku (`^JRL(sku,lin,"J",poradi)` "#"-pole 12 =
/// `idTam:idZpet`), záznam je `^JRZ(czst,"Sloupek",id)` = `číslo~lat~lon~popis směru`.
/// Vrací ten označník (nebo "", když trasa žádný neurčuje).
ClassMethod PridejPolohy(dataDB As %String, sku As %String, lin As %String, stranka As %DynamicObject) As %DynamicObject
{
    s vysledek = ""
    s zastavky = stranka.rows.%Get("ZASTAVKY")
    q:'$isobject(zastavky) vysledek
    s jeTam = (stranka.values.%Get("SMER") = "T")
    s it = zastavky.%GetIterator()
    while it.%GetNext(.i, .radek) {
        s czst = radek.%Get("CZST")
        continue:czst=""
        s rec = $g(^[dataDB]JRZ(czst))
        s lat = $p(rec,"$",12), lon = $p(rec,"$",13)
        i $isvalidnum(lat) && $isvalidnum(lon) && (+lat '= 0) {
            d radek.%Set("LAT", +lat, "number"), radek.%Set("LON", +lon, "number")
        }
        continue:radek.%Get("AKTUALNI_ZASTAVKA")'="true"
        s idSloupku = $p($p($g(^[dataDB]JRL(sku,lin,"J",radek.%Get("PORADI"))),"#",12),":",$s(jeTam:1, 1:2))
        continue:idSloupku=""
        s oznacnik = ..Oznacnik(dataDB, czst, idSloupku)
        continue:'$isobject(oznacnik)
        d radek.%Set("OZNACNIK", oznacnik)
        s vysledek = oznacnik
    }
    q vysledek
}

/// Záznam označníku zastávky. Přednost má nejnovější časová varianta, která už platí
/// (`^JRZ(czst,"CasoveVariantyUdaju",horolog,"Sloupek",id)` - tak to čte webová aplikace EDISONu),
/// jinak základní uzel.
ClassMethod Oznacnik(dataDB As %String, czst As %String, id As %String) As %DynamicObject
{
    s zaznam = "", den = $o(^[dataDB]JRZ(czst,"CasoveVariantyUdaju",+$h+1), -1)
    i den '= "" s zaznam = $g(^[dataDB]JRZ(czst,"CasoveVariantyUdaju",den,"Sloupek",id))
    i zaznam = "" s zaznam = $g(^[dataDB]JRZ(czst,"Sloupek",id))
    q:zaznam="" ""
    s oznacnik = {"id":(id),"cislo":($p(zaznam,"~",1)),"popis":($p(zaznam,"~",4))}
    s lat = $p(zaznam,"~",2), lon = $p(zaznam,"~",3)
    i $isvalidnum(lat) && $isvalidnum(lon) && (+lat '= 0) {
        d oznacnik.%Set("lat", +lat, "number"), oznacnik.%Set("lon", +lon, "number")
    }
    q oznacnik
}

/// Jak cestující na lince skutečně platili - podle prodaných jízdenek (modul Tickets). EDISON
/// nikde neeviduje „na lince lze platit kartou", tak se to vyvozuje z prodejů posledních dní.
/// Ven jdou jen počty podle způsobu platby, žádné údaje o jednotlivých jízdenkách.
///
/// Prodeje: `^[db]LYOBD(idfa,rok,mes,den,"ly",lyId)` = denní index, `^[db]LY(lyId)` = `$lb` jízdenky,
/// pole 11 = linka (licenční číslo bez úvodní nuly), pole 22 = způsob platby
/// (`cash` / `bankcard` / `card` = čipová karta dopravce / `mobil` / `eshop` / `coupon`,
/// viz `Tickets/CACHE/ALVATICKETS.inc`). Bere se nejvýš 3 posledních dnů s daty, a jen když
/// nejsou starší než 30 dní - starší údaj by mohl lhát. Bez dat vrací "" (= nevíme).
ClassMethod Platby(dataDB As %String, idfa As %String, lin As %String) As %DynamicObject
{
    s linka = +lin, dnu = 0, nejstarsi = +$h - 30, od = "", do = ""
    k pocty
    s rok = ""
    f {
        s rok = $o(^[dataDB]LYOBD(idfa,rok), -1)
        q:rok=""
        q:dnu>=3
        s mes = ""
        f {
            s mes = $o(^[dataDB]LYOBD(idfa,rok,mes), -1)
            q:mes=""
            q:dnu>=3
            s den = ""
            f {
                s den = $o(^[dataDB]LYOBD(idfa,rok,mes,den), -1)
                q:den=""
                q:dnu>=3
                s datum = $zdh(rok_"-"_$tr($j(mes,2)," ","0")_"-"_$tr($j(den,2)," ","0"), 3, , , , , , , "")
                continue:datum=""
                i datum < nejstarsi s dnu = 3 q
                s dnu = dnu + 1
                s iso = $zd(datum, 3)
                s do = $s(do = "":iso, 1:do), od = iso
                s ly = ""
                f {
                    s ly = $o(^[dataDB]LYOBD(idfa,rok,mes,den,"ly",ly))
                    q:ly=""
                    s rec = $g(^[dataDB]LY(ly))
                    continue:rec=""
                    continue:+$lg(rec,11)'=linka
                    s platba = $lg(rec,22)
                    i platba '= "" s pocty(platba) = $g(pocty(platba)) + 1
                }
            }
        }
    }
    q:'$d(pocty) ""
    s vysledek = {"od":(od),"do":(do)}, zpusob = ""
    f {
        s zpusob = $o(pocty(zpusob))
        q:zpusob=""
        d vysledek.%Set(zpusob, pocty(zpusob), "number")
    }
    q vysledek
}

/// Všechny označníky zastávky s polohou (id ze základního uzlu i z platné časové varianty).
ClassMethod OznacnikyZastavky(dataDB As %String, czst As %String) As %DynamicArray
{
    s vysledek = ##class(%DynamicArray).%New()
    k ids
    s id = ""
    f {
        s id = $o(^[dataDB]JRZ(czst,"Sloupek",id))
        q:id=""
        s ids(id) = ""
    }
    s den = $o(^[dataDB]JRZ(czst,"CasoveVariantyUdaju",+$h+1), -1)
    i den '= "" {
        s id = ""
        f {
            s id = $o(^[dataDB]JRZ(czst,"CasoveVariantyUdaju",den,"Sloupek",id))
            q:id=""
            s ids(id) = ""
        }
    }
    s id = ""
    f {
        s id = $o(ids(id))
        q:id=""
        s oznacnik = ..Oznacnik(dataDB, czst, id)
        continue:'$isobject(oznacnik)
        continue:oznacnik.%Get("lat")=""
        d vysledek.%Push(oznacnik)
    }
    q vysledek
}

/// Export sloupků firmy pro vyhledávač v ISYCloud (zadání 13. 10.): jízdní řády tam jsou z CIS,
/// který sloupky zná jen jako číslo stanoviště u zastavení spoje - polohu a popis směru nemá.
/// Linky se berou ve skupině platné dnes (`SkupinaLinky`), sloupek z trasy linky
/// (J-věta `#12` = `idTam:idZpet`, odkaz do `^JRZ(czst,"Sloupek",id)`).
///
/// IN:  {"idfa":"ZL_KOVED"}
/// OUT: {"success":true,"data":{"firma":..,"vytvoreno":..,
///        "zastavky":[{"cisloCIS":4963,"nazev":..,"sloupky":[{"id","cislo","popis","lat","lon"}]}],
///        "linky":[{"linka":"822995","T":{"<cisloCIS>":"<číslo sloupku>"},"Z":{..}}]}}
ClassMethod ExportSloupku(ByRef sys, ByRef in, ByRef out) As %Status
{
    s err = "", code = ""
    try {
        i $isobject(in) && in.%IsA("%DynamicAbstractObject") { s req = in }
        else { s req = ##class(%DynamicObject).%FromJSON(in) }
        s idfa = req.%Get("idfa")
        i idfa = "" s code = "VSTUP", err = "Chybí idfa" ztrap
        d ..BuildSys(idfa, .sys)
        s dataDB = sys("U","DATA")

        k linky
        s sku = ""
        f {
            s sku = $o(^[dataDB]JRL(sku))
            q:sku=""
            s lin = ""
            f {
                s lin = $o(^[dataDB]JRL(sku,lin))
                q:lin=""
                i $d(^[dataDB]JRL(sku,lin,"I")) s linky(lin) = ""
            }
        }

        k zastavky
        s vystupLinky = ##class(%DynamicArray).%New(), lin = ""
        f {
            s lin = $o(linky(lin))
            q:lin=""
            s skupina = ..SkupinaLinky(dataDB, lin)
            continue:skupina=""
            s tam = {}, zpet = {}, poradi = ""
            f {
                s poradi = $o(^[dataDB]JRL(skupina,lin,"J",poradi))
                q:poradi=""
                s veta = $g(^[dataDB]JRL(skupina,lin,"J",poradi))
                s czst = $p(veta,":",1)
                continue:czst=""
                s cis = +$p($g(^[dataDB]JRZ(czst)),"$",9)
                continue:cis=0
                s sloupky = $p(veta,"#",12)
                f smer = 1, 2 {
                    s id = $p(sloupky,":",smer)
                    continue:id=""
                    s sloupek = ..Oznacnik(dataDB, czst, id)
                    continue:'$isobject(sloupek)
                    continue:sloupek.cislo=""
                    s cilSmer = $s(smer = 1:tam, 1:zpet)
                    d cilSmer.%Set(cis, sloupek.cislo)
                    s zastavky(czst) = cis
                }
            }
            continue:((tam.%Size() = 0) && (zpet.%Size() = 0))
            d vystupLinky.%Push({"linka":(+lin),"T":(tam),"Z":(zpet)})
        }

        s vystupZastavky = ##class(%DynamicArray).%New(), czst = ""
        f {
            s czst = $o(zastavky(czst))
            q:czst=""
            s sloupky = ##class(%DynamicArray).%New()
            k ids
            s id = ""
            f {
                s id = $o(^[dataDB]JRZ(czst,"Sloupek",id))
                q:id=""
                s ids(id) = ""
            }
            s den = $o(^[dataDB]JRZ(czst,"CasoveVariantyUdaju",+$h+1), -1)
            i den '= "" {
                s id = ""
                f {
                    s id = $o(^[dataDB]JRZ(czst,"CasoveVariantyUdaju",den,"Sloupek",id))
                    q:id=""
                    s ids(id) = ""
                }
            }
            s id = ""
            f {
                s id = $o(ids(id))
                q:id=""
                s sloupek = ..Oznacnik(dataDB, czst, id)
                i $isobject(sloupek) && (sloupek.cislo '= "") d sloupky.%Push(sloupek)
            }
            d vystupZastavky.%Push({"cisloCIS":(zastavky(czst)),"czst":(czst),"nazev":($p($g(^[dataDB]JRZ(czst)),"$",1)),"sloupky":(sloupky)})
        }

        s data = {"firma":(idfa),"vytvoreno":($zdt($h,3)),"zastavky":(vystupZastavky),"linky":(vystupLinky)}
        s out = {"success":true,"data":(data)}
    }
    catch ex {
        i err = "" s err = ex.DisplayString()
        i code = "" s code = "SYSTEM"
        i $isobject($g(%response)) s %response.Status = $s(code = "SYSTEM":500, 1:400)
        s out = {"success":false,"error":{"code":(code),"message":(err)}}
    }
    q $$$OK
}

/// Vzdálenost dvou bodů v metrech (haversinova formule), zaokrouhlená na metry.
ClassMethod VzdalenostM(lat1 As %Numeric, lon1 As %Numeric, lat2 As %Numeric, lon2 As %Numeric) As %Integer
{
    s rad = $zpi / 180
    s dLat = (lat2 - lat1) * rad, dLon = (lon2 - lon1) * rad
    s a = ($zsin(dLat / 2) ** 2) + ($zcos(lat1 * rad) * $zcos(lat2 * rad) * ($zsin(dLon / 2) ** 2))
    q $normalize(6371000 * 2 * $zarcsin($zsqr(a)), 0)
}

/// `d.m.yyyy` → `yyyy-mm-dd`; neúplné datum → "".
ClassMethod IsoDatum(datum As %String) As %String
{
    s d = +$p(datum,".",1), m = +$p(datum,".",2), r = $p(datum,".",3)
    i (d = 0) || (m = 0) || ($l(r) '= 4) q ""
    q r_"-"_$tr($j(m,2)," ","0")_"-"_$tr($j(d,2)," ","0")
}

/// Kontext ERP pro `$$$FillParams` agendy (opis `ALWA.EDISONKA.CACHE.Sys.Base.BuildSys`).
ClassMethod BuildSys(idfa As %String, ByRef sys As %String)
{
    k sys("U")
    s sys("U","IdFa") = idfa
    s sys("U","delimiter") = ##class(ALVA.EDISON.Main).GetDelimiter()
    s sys("U","SystemDB") = ##class(ALVA.EDISON.Main).GetSystemDB()
    s sys("U","DATA") = ##class(ALVA.GENERALAPI.DataBase).dataDB(idfa)
    s sys("U","MLP") = 0
    s sys("U","loginName") = ..#VOLAJICI
    s sys("U","fullName") = ..#VOLAJICI
}

}
