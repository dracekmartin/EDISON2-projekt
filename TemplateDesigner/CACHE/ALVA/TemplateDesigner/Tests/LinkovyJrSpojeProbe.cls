Include (%occInclude, GENERAL)

/// Read-only sonda: **co GetData linkového JŘ reálně pošle za spoje** - číslo, směr a skupinu dnů,
/// v pořadí, v jakém to klient dostane.
///
/// **K čemu to je.** Uživateli chyběly v tabulce spoje 501-509 (`B120`, `CB_GWBUS 2 / 0320144`),
/// zatímco server je posílá. Rozhodnout, kde se ztrácejí, nejde bez toho, aby bylo vidět **směr
/// a skupinu** každého spoje: klient spoje dělí na hlavní a zrcadlený směr a na bloky sloupců,
/// takže spoj s cizí hodnotou v těchto polích může vypadnout na jiném místě než v datech.
///
/// Sonda **nic nemění**, jen zavolá `GetData` a vypíše z odpovědi tři pole per spoj.
Class ALVA.TemplateDesigner.Tests.LinkovyJrSpojeProbe
{

/// Vypíše spoje, které `GetData` poslal pro jednu linku.
///
/// IN: `SKUPINA_LINEK_ID`, `LINKA_ID` (+ volitelně kterýkoli parametr tisku - předá se dál).
/// OUT: řádek `strana~<n>` se souhrnem stránky a pak `spoj~<n>~<i>` s `číslo:směr:skupina`.
ClassMethod Spoje(ByRef sys As %String) As %Status
{
	s err = ""
	try {
		$$$FillParams
		s sku = $g(^||ML("IN","SKUPINA_LINEK_ID"))
		s lin = $g(^||ML("IN","LINKA_ID"))

		// Vstup si odložíme - GetData zapisuje do téhož ^||ML a IN by si přepsalo samo sebe.
		m vstup = ^||ML("IN")
		k ^||ML("OUT")
		d ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).GetData(.sys)

		m odpoved = ^||ML("OUT")
		k ^||ML("OUT")
		s ^||ML("OUT","linka") = dataDB_" "_sku_" "_lin
		s ^||ML("OUT","chyba") = $g(odpoved("ERR"))

		s stranek = 0, spojuCelkem = 0, key = ""
		f {
			s key = $o(odpoved(key))
			q:key=""
			continue:key="ERR"
			s stranek = stranek + 1
			s page = ##class(%DynamicObject).%FromJSON(odpoved(key))
			s spoje = page.rows.%Get("SPOJE")
			s zast = page.rows.%Get("ZASTAVKY")
			s pocetSpoju = $s($isobject(spoje):spoje.%Size(), 1:0)
			s pocetZast = $s($isobject(zast):zast.%Size(), 1:0)
			s spojuCelkem = spojuCelkem + pocetSpoju
			s ^||ML("OUT","strana~"_key) = "smer="_page.values.%Get("SMER")
				_" spoju="_pocetSpoju_" zastavek="_pocetZast
			// Číslo, směr a skupina per spoj - v pořadí odpovědi, ať jde srovnat s tím, co uživatel vidí.
			f si=0:1:pocetSpoju-1 {
				s sp = spoje.%Get(si)
				// Kolik časů spoj reálně nese: sloupec bez jediného času se sice nakreslí, ale je
				// prázdný - a uživatel prázdný sloupec nahlásí jako chybějící spoj.
				s casy = sp.%Get("CASY"), casu = 0, neprazdnych = 0
				i $isobject(casy) {
					s casu = casy.%Size()
					f ci=0:1:casu-1 i casy.%Get(ci) '= "" s neprazdnych = neprazdnych + 1
				}
				s ^||ML("OUT","spoj~"_key_"~"_$tr($j(si+1,3)," ","0")) =
					sp.%Get("CISLO_SPOJE")_":"_sp.%Get("SMER")_":"_sp.%Get("SKUPINA")
					_" casy="_neprazdnych_"/"_casu
			}
		}
		s ^||ML("OUT","souhrn") = "stranek="_stranek_" spoju="_spojuCelkem
		k ^||ML("IN")
		m ^||ML("IN") = vstup
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Vypíše **persistované parametry tisku** uložené uživatelům pro danou agendu.
///
/// Proč: náhled i tisk skládají IN z uložených hodnot, takže volání „naprázdno" může vracet jiná
/// data než aplikace u uživatele. Bez tohoto výpisu se hádá, s čím se to má přehrát
/// (TemplateDesigner/CLAUDE.md, diagnostika „UI ukazuje jiné nastavení, než se tiskne").
///
/// IN: `AGENDA` (default `LINKOVY_JR_AD`). OUT: řádek `<uživatel>` s uloženým blobem.
ClassMethod Parametry(ByRef sys As %String) As %Status
{
	s err = ""
	try {
		$$$FillParams
		s agenda = $g(^||ML("IN","AGENDA"))
		s:agenda="" agenda = "LINKOVY_JR_AD"
		k ^||ML("OUT")
		s ^||ML("OUT","agenda") = agenda_" IdFa="_IdFa_" dataDB="_dataDB
		// Globál hledáme v obou namespace - kde UserData reálně leží, se z kódu nepozná.
		s nalezeno = 0
		f ns=1:1:2 {
			s fa = ""
			f {
				s fa = $s(ns=1:$o(^ALVA.EDControls.UserData(fa)), 1:$o(^[dataDB]ALVA.EDControls.UserData(fa)))
				q:fa=""
				s user = ""
				f {
					s user = $s(ns=1:$o(^ALVA.EDControls.UserData(fa, user)), 1:$o(^[dataDB]ALVA.EDControls.UserData(fa, user)))
					q:user=""
					s klic = ""
					f {
						s klic = $s(ns=1:$o(^ALVA.EDControls.UserData(fa, user, klic)), 1:$o(^[dataDB]ALVA.EDControls.UserData(fa, user, klic)))
						q:klic=""
						continue:klic'["PrintParams"
						s ag = ""
						f {
							s ag = $s(ns=1:$o(^ALVA.EDControls.UserData(fa, user, klic, ag)), 1:$o(^[dataDB]ALVA.EDControls.UserData(fa, user, klic, ag)))
							q:ag=""
							s blob = $s(ns=1:$g(^ALVA.EDControls.UserData(fa, user, klic, ag)), 1:$g(^[dataDB]ALVA.EDControls.UserData(fa, user, klic, ag)))
							continue:blob=""
							s nalezeno = nalezeno + 1
							s ^||ML("OUT","ns"_ns_"~"_fa_"~"_user_"~"_ag) = $e(blob,1,900)
						}
					}
				}
			}
		}
		s ^||ML("OUT","souhrn") = "zaznamu s ulozenymi parametry="_nalezeno
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

}
