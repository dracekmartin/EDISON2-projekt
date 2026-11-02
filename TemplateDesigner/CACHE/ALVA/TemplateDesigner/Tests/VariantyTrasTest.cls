Include %occInclude

/// Sloupce variant tras (`Jr.VariantyTras`), které počítají vývěsky AD i MHD: výběr zástupce varianty,
/// řazení, ořez, slití úseků, písmena a hodnoty řádku. Na vymyšlených spojích, ne na datech linky -
/// na lince by se každé pravidlo ukázalo jen tam, kde se v datech zrovna potká.
///
/// Tři varianty, v pořadí, jak přibyly: A (sled S~S~S, 1 spoj, jízda 15 min, zástupce 10),
/// B (S~S~, 3 spoje, 10 min, zástupce 20) a C (S~&lt;~S, 2 spoje, 30 min, zástupce 30).
/// B jede souvislý úsek A, C zajíždí jinam (vynechá zastávku, kde A staví).
Class ALVA.TemplateDesigner.Tests.VariantyTrasTest Extends ALVA.EDTesting.MLTestCase
{

Method TestZastupceJeSpojSNejdelsiJizdou()
{
	kill ^||ML("IN")
	do ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).AddSpoj(.v, "s1", "S~S", 10, 5)
	do ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).AddSpoj(.v, "s2", "S~S", 12, 7)
	do ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).AddSpoj(.v, "s3", "S~S", 12, 3)
	set pismena = ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).BuildSloupce(.v, "", .pismenoSpoje)
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).PocetSloupcu(.v), 1, "spoje téhož sledu jsou jedna varianta")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).ZastupceSloupce(.v, 1), 3, "zástupce = nejdelší jízda, při shodě nižší")
	do $$$AssertEquals(pismena.%ToJSON(), "[""""]", "jediná varianta je bez písmene")
	do $$$AssertNotTrue($data(pismenoSpoje), "spoj varianty bez písmene písmeno nemá")
}

Method TestPoradiPodleCetnosti()
{
	kill ^||ML("IN")
	do ..TriVarianty(.v)
	set pismena = ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).BuildSloupce(.v, "", .pismenoSpoje)
	do $$$AssertEquals(..Zastupci(.v), "20,30,10", "nejčastější varianta první (B, C, A)")
	do $$$AssertEquals(pismena.%ToJSON(), "["""",""A"",""D""]", "první bez písmene, pak výchozí sada")
	do $$$AssertEquals($get(pismenoSpoje("b2")), "", "spoj první varianty bez písmene")
	do $$$AssertEquals($get(pismenoSpoje("c1")), "A", "spoj druhé varianty")
	do $$$AssertEquals($get(pismenoSpoje("a1")), "D", "spoj třetí varianty")
}

Method TestPoradiStabilniADelky()
{
	kill ^||ML("IN")
	set ^||ML("IN","RAZENI_VARIANT") = "Stabilní (pořadí spojů)"
	do ..TriVarianty(.v)
	do ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).BuildSloupce(.v, "", .p)
	do $$$AssertEquals(..Zastupci(.v), "10,20,30", "stabilní = pořadí, jak varianty přibyly")

	set ^||ML("IN","RAZENI_VARIANT") = "Dle délky jízdy (nejkratší první)"
	kill v, p
	do ..TriVarianty(.v)
	do ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).BuildSloupce(.v, "", .p)
	do $$$AssertEquals(..Zastupci(.v), "20,10,30", "nejkratší jízda první (B 10, A 15, C 30)")

	set ^||ML("IN","RAZENI_VARIANT") = "Dle délky jízdy (nejdelší první)"
	kill v, p
	do ..TriVarianty(.v)
	do ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).BuildSloupce(.v, "", .p)
	do $$$AssertEquals(..Zastupci(.v), "30,10,20", "nejdelší jízda první")
	kill ^||ML("IN")
}

/// Ořezaná varianta nemá sloupec, a tedy ani písmeno (`B181`, rozhodnutí Jan 25. 9.).
Method TestOrezNaPocetSloupcu()
{
	kill ^||ML("IN")
	set ^||ML("IN","MAX_SLOUPCU_VARIANT") = 2
	do ..TriVarianty(.v)
	set pismena = ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).BuildSloupce(.v, "", .pismenoSpoje)
	do $$$AssertEquals(..Zastupci(.v), "20,30", "zůstanou první dvě podle řazení")
	do $$$AssertEquals(pismena.%Size(), 2, "písmeno jen pro tištěné sloupce")
	do $$$AssertNotTrue($data(pismenoSpoje("a1")), "spoj ořezané varianty nemá písmeno")
	kill ^||ML("IN")
}

/// B jede souvislý úsek A, takže se slije do ní a A převezme její spoje; C zajíždí jinam a zůstane (`B157`).
Method TestSlitiUseku()
{
	kill ^||ML("IN")
	set ^||ML("IN","SLUCOVAT_PODMNOZINY") = "true"
	set ^||ML("IN","PRVNI_BEZ_PISMENE") = "false"     // přeživší sloupec musí mít písmeno, jinak by test nic nedokázal
	do ..TriVarianty(.v)
	set pismena = ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).BuildSloupce(.v, "", .pismenoSpoje)
	do $$$AssertEquals(..Zastupci(.v), "10,30", "B slitá do A; A má teď 4 spoje a jde první")
	do $$$AssertEquals($get(pismenoSpoje("b1")), "A", "spoj slité varianty ukazuje na přeživší sloupec")
	do $$$AssertEquals($get(pismenoSpoje("c1")), "D", "zajížďka si sloupec nechá")
	kill ^||ML("IN")
}

Method TestPismenaOdPrvniVarianty()
{
	kill ^||ML("IN")
	set ^||ML("IN","PRVNI_BEZ_PISMENE") = "False"
	do ..TriVarianty(.v)
	set pismena = ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).BuildSloupce(.v, "", .pismenoSpoje)
	do $$$AssertEquals(pismena.%ToJSON(), "[""A"",""D"",""L""]", "písmena od prvního sloupce (hodnota čtená bez ohledu na velikost)")
	do $$$AssertEquals($get(pismenoSpoje("b1")), "A", "spoj první varianty má písmeno")
	kill ^||ML("IN")
}

/// Řádek zastávky nese za každý sloupec jízdní dobu zástupce, „<", nebo prázdno.
Method TestHodnotyRadku()
{
	kill ^||ML("IN")
	do ..TriVarianty(.v)
	do ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).BuildSloupce(.v, "", .p)
	do ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).AddZastaveni(.v, 1, 5, "S", 7)
	do ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).AddZastaveni(.v, 2, 5, "<")
	do ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).AddZastaveni(.v, 3, 5, "")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).BuildTrasaMin(.v, 5).%ToJSON(), "[7,""<"",""""]", "staví / projíždí / nejede")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).BuildTrasaMin(.v, 4).%ToJSON(), "["""","""",""""]", "zastávka, kudy nejede žádná varianta")
	do $$$AssertTrue(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).MaZastaveni(.v, 1, 5), "první sloupec na zastávce staví")
	do $$$AssertNotTrue(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).MaZastaveni(.v, 2, 5), "projíždění není zastavení")
}

/// Varianty A, B, C z popisu třídy.
Method TriVarianty(ByRef v)
{
	do ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).AddSpoj(.v, "a1", "S~S~S", 15, 10)
	do ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).AddSpoj(.v, "b1", "S~S~", 10, 20)
	do ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).AddSpoj(.v, "c1", "S~<~S", 30, 30)
	do ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).AddSpoj(.v, "b2", "S~S~", 10, 21)
	do ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).AddSpoj(.v, "c2", "S~<~S", 30, 31)
	do ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).AddSpoj(.v, "b3", "S~S~", 10, 22)
}

/// Zástupci sloupců v pořadí sloupců, čárkou.
Method Zastupci(ByRef v) As %String
{
	set s = ""
	for n=1:1:##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).PocetSloupcu(.v) {
		set s = s_$select(s="":"", 1:",")_##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).ZastupceSloupce(.v, n)
	}
	quit s
}

}
