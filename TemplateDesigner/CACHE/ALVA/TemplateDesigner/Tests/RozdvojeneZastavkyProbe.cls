Include (%occInclude, GENERAL)

/// Read-only sonda: **na kterých linkách se zastávka tiskne s příjezdem i odjezdem.**
///
/// **K čemu to je.** Volba sloučeného popisu (`B140`) se dá vyzkoušet jen na lince, kde je aspoň
/// jedna taková zastávka - jinak vypadá zapnutá i vypnutá stejně. Rozhoduje o tom atribut trasy
/// `P` nebo `J` u zastávky, a ten z výpisu linek vidět není.
///
/// Sonda **nic nemění** a nevolá `GetData` - jen čte `^JRL`, takže projde všechny linky rychle.
Class ALVA.TemplateDesigner.Tests.RozdvojeneZastavkyProbe
{

/// Firmy k prohledání, když se nezadá jiný seznam.
Parameter FIRMY = "CB_CSADAD,CB_GWBUS,KV_DP,VS_CSAD,ZL_KOVED,SKNI_MHDNITRA";

/// Linky s aspoň jednou rozdvojenou zastávkou, seřazené od nejvíc rozdvojených.
///
/// IN: `FIRMY` (volitelně, CSV - jinak <see cref="FIRMY"/>), `MAX` (kolik linek vypsat, výchozí 15).
/// OUT: `souhrn~<firma>` s počtem linek a řádky `linka~<n>` ve tvaru
/// `firma sku / lin  rozdvojenych=<n> zastavek=<n> spoju=<n>`.
ClassMethod Linky(ByRef sys As %String) As %Status
{
	s err = ""
	try {
		$$$FillParams
		s firmy = $g(^||ML("IN","FIRMY"))
		s:firmy="" firmy = ..#FIRMY
		s max = +$g(^||ML("IN","MAX"))
		s:max<1 max = 15

		// nalez(rozdvojenych, firma, sku, lin) = popis - $order sestupně dá rovnou pořadí výpisu
		k nalez
		f fi = 1:1:$l(firmy, ",") {
			s firma = $p(firmy, ",", fi)
			continue:'##class(%SYS.Namespace).Exists(firma)
			s linekSNalezem = 0, s1 = ""
			f {
				s s1 = $o(^[firma]JRL(s1))
				q:s1=""
				continue:'$isvalidnum(s1)
				s l1 = ""
				f {
					s l1 = $o(^[firma]JRL(s1, l1))
					q:l1=""
					continue:'$d(^[firma]JRL(s1, l1, "I"))
					// Linka bez spojů se nedá vytisknout, takže by v nabídce byla k ničemu.
					s spoju = +$g(^[firma]JRL(s1, l1, "T"))
					continue:'spoju
					s rozdvojenych = 0, zastavek = 0, poradi = ""
					f {
						s poradi = $o(^[firma]JRL(s1, l1, "J", poradi))
						q:poradi=""
						continue:'$isvalidnum(poradi)
						s zastavek = zastavek + 1
						// Atributy trasy stojí ve druhém úseku věty za dvojtečkou, před „#".
						s atrZnaky = $p($p($g(^[firma]JRL(s1, l1, "J", poradi)), ":", 2), "#", 1)
						i (atrZnaky [ "P") || (atrZnaky [ "J") s rozdvojenych = rozdvojenych + 1
					}
					continue:'rozdvojenych
					s linekSNalezem = linekSNalezem + 1
					s nalez(rozdvojenych, firma, s1, l1) = firma_" "_s1_" / "_l1
						_"  rozdvojenych="_rozdvojenych_" zastavek="_zastavek_" spoju="_spoju
				}
			}
			s ^||ML("OUT","souhrn~"_firma) = "linek s rozdvojenou zastavkou: "_linekSNalezem
		}

		s vypsano = 0, poc = ""
		f {
			s poc = $o(nalez(poc), -1)
			q:poc=""
			s f2 = ""
			f {
				s f2 = $o(nalez(poc, f2))
				q:f2=""
				s s2 = ""
				f {
					s s2 = $o(nalez(poc, f2, s2))
					q:s2=""
					s l2 = ""
					f {
						s l2 = $o(nalez(poc, f2, s2, l2))
						q:l2=""
						s vypsano = vypsano + 1
						s ^||ML("OUT","linka~"_vypsano) = nalez(poc, f2, s2, l2)
						i vypsano '< max ret $$$OK
					}
				}
			}
		}
	} catch (ex) { $$$CatchErrRest }
	q $$$OK
}

}
