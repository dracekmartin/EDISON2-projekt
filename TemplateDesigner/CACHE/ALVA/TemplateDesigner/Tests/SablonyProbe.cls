Include (%occInclude, GENERAL)

/// Read-only sonda: **které uložené šablony obsahují daný text** - napříč všemi firmami.
///
/// **K čemu to je.** Než se z nabídky odebere volba (hodnota výčtu v JSON šablony), je potřeba
/// vědět, jestli ji nějaká uložená šablona používá - jinak by se jí po odebrání zobrazilo prázdné
/// pole. Šablony leží v jednom globálu po kusech, takže se hledá i přes hranici dvou kusů.
///
/// Sonda **nic nemění**, jen čte `^ALVA.TemplateDesigner.Templates`.
Class ALVA.TemplateDesigner.Tests.SablonyProbe
{

/// IN: `TEXT` (hledaný text, např. `"OnEveryPageExceptFirst"`), `MAX` (kolik nálezů, výchozí 50).
/// OUT: `nalez~<n>` = `IdFa / TemplateID / název šablony`, `souhrn` = počet prošlých šablon a nálezů.
ClassMethod Najdi(ByRef sys As %String) As %Status
{
	s err = ""
	try {
		$$$FillParams
		s hledany = $g(^||ML("IN","TEXT"))
		s max = +$g(^||ML("IN","MAX"))
		s:max<1 max = 50
		if hledany = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "TEXT is required"))
		s presah = $l(hledany)
		s nalezu = 0, sablon = 0, firma = ""
		f {
			s firma = $o(^ALVA.TemplateDesigner.Templates(firma))
			q:firma=""
			s sablona = ""
			f {
				s sablona = $o(^ALVA.TemplateDesigner.Templates(firma, "DTA", sablona))
				q:sablona=""
				s sablon = sablon + 1
				s nalezeno = 0
				if $d(^ALVA.TemplateDesigner.Templates(firma, "DTA", sablona, "Data", 1)) {
					s predchozi = "", kusu = +$g(^ALVA.TemplateDesigner.Templates(firma, "DTA", sablona, "Data"))
					f n=1:1:kusu {
						s kus = $g(^ALVA.TemplateDesigner.Templates(firma, "DTA", sablona, "Data", n))
						if $f($e(predchozi, *-presah, *)_kus, hledany) {
							s nalezeno = 1
							q
						}
						s predchozi = kus
					}
				} else {
					s:$f($g(^ALVA.TemplateDesigner.Templates(firma, "DTA", sablona, "Data")), hledany) nalezeno = 1
				}
				continue:'nalezeno
				s nalezu = nalezu + 1
				s nazev = $g(^ALVA.TemplateDesigner.Templates(firma, "DTA", sablona, "Metadata", "Name"))
				s ^||ML("OUT", "nalez~"_nalezu) = firma_" / "_sablona_" / "_nazev
				if nalezu '< max {
					s ^||ML("OUT", "souhrn") = "sablon="_sablon_" nalezu="_nalezu_" (zastaveno na MAX)"
					ret $$$OK
				}
			}
		}
		s ^||ML("OUT", "souhrn") = "sablon="_sablon_" nalezu="_nalezu
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

}
