Include %occInclude

/// %UnitTest pro rozložené údaje dopravce - skalární pole (NAZEV_DOPRAVCE…) prvního dopravce linky.
///
/// Vznikly proto, že slepený POPIS_DOPRAVCE dával adresu dvakrát: bral sídlo z provozovny a ulici
/// z firmy (#13158). Kolekce dopravců po provozovnách se do dat neposílá (od 30. 10., `B112`).
Class ALVA.TemplateDesigner.Tests.ZastavkoveJrAdDopravciTest Extends ALVA.EDTesting.MLTestCase
{

/// Adresa dopravce musí nést i PSČ a obec.
///
/// ⚠️ **Tohle je ta past, kterou chytilo až ověření voláním.** Číselník má adresu firmy ve dvou
/// polích a NEJSOU vždy totožné: u ČSAD Jindřichův Hradec obě nesou plný tvar, ale u ČSAD AUTOBUSY
/// České Budějovice má první jen „Hálkova 781/9" a plnou adresu s PSČ a obcí nese teprve druhé.
/// Kdo vezme první, tomu na vývěsce zmizí PSČ a obec - a nikdo si toho nevšimne, protože ulice tam
/// je. Test proto porovnává s tím, co nese slepený POPIS_DOPRAVCE, tedy s tvarem, který zákazník
/// dnes reálně vidí.
/// Pravidlo „pole 10, pole 2 jen jako záloha" na záznamu číselníku pod jedinečným klíčem, který test po sobě smaže.
Method TestAdresaZPlnehoPoleCiselniku()
{
	set db = $namespace, fa = "TDTEST"_$translate($system.Util.CreateGUID(), "-")
	try {
		set firma = "", $piece(firma, "~", 1) = "Dopravce", $piece(firma, "~", 2) = "Hálkova 781/9"
		set $piece(firma, "~", 10) = "Hálkova 781/9, 370 04, České Budějovice"
		set ^[db]JRSET("DOP", fa) = firma
		do ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).DopravceUdaje(db, fa, "", .udaje)
		do $$$AssertEquals($get(udaje("ADRESA")), "Hálkova 781/9, 370 04, České Budějovice", "plné pole 10 má přednost")
		set $piece(firma, "~", 10) = ""
		set ^[db]JRSET("DOP", fa) = firma
		do ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).DopravceUdaje(db, fa, "", .udaje)
		do $$$AssertEquals($get(udaje("ADRESA")), "Hálkova 781/9", "bez pole 10 záloha z pole 2")
	} catch ex {
		do $$$AssertTrue(0, "test spadl: "_ex.DisplayString())
	}
	kill ^[db]JRSET("DOP", fa)
}

/// U provozovny bez vlastní adresy je adresa firmy celá i ve slepeném POPIS_DOPRAVCE, jinak by se
/// rozcházelo pole s popisem. Provozovna s vlastní ulicí (CB_CSADAD „prov.Písek, Nádražní 2294") má
/// v popisu svou adresu a firmu tam nečekáme - to by byla zdvojená adresa z #13158.
/// Na linkách sady (testovací namespace linky AD nemá; do 30. 10. tu test na „první lince namespace"
/// hlásil úspěch, aniž co ověřil, a jeho předpoklad na reálných datech neplatil).
Method TestAdresaDopravceNesePlnyTvarJakoStaryPopis()
{
	do ##class(ALVA.TemplateDesigner.Tests.ZastavkoveJrAdSadaTest).LinkyNaServeru(.linky)
	if '$get(linky) { do $$$AssertSkipped("žádná firma sady není na tomto serveru")  quit }
	set overeno = 0
	for i = 1:1:linky {
		set db = $piece(linky(i), "~", 1), sku = $piece(linky(i), "~", 2), lin = $piece(linky(i), "~", 3)
		set dopravci = ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).BuildDopravci(db, sku, lin)
		continue:dopravci.%Size()=0
		set prvni = dopravci.%Get(0)
		do $$$AssertNotEquals(prvni.NAZEV, "", db_" "_lin_": dopravce má název")
		continue:(prvni.ADRESA="")||(prvni."PROVOZOVNA_ADRESA"'="")
		set overeno = overeno + 1
		set popis = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PopisDopravce(db, sku, lin)
		do $$$AssertTrue((popis [ prvni.ADRESA), db_" "_lin_": adresa """_prvni.ADRESA_""" musí být celá v POPIS_DOPRAVCE ("_popis_")")
	}
	do $$$AssertTrue(overeno > 0, "aspoň jedna linka sady má dopravce s adresou firmy v popisu - jinak test nic neověří")
}

/// Skalární pole nesou prvního dopravce linky, toho, kterého vrací `BuildDopravci` jako první.
/// Na linkách sady, stejně jako test výš.
Method TestSkalarniPoleOdpovidajiPrvnimuDopravci()
{
	do ##class(ALVA.TemplateDesigner.Tests.ZastavkoveJrAdSadaTest).LinkyNaServeru(.linky)
	if '$get(linky) { do $$$AssertSkipped("žádná firma sady není na tomto serveru")  quit }
	set sDopravcem = 0
	for i = 1:1:linky {
		set db = $piece(linky(i), "~", 1), sku = $piece(linky(i), "~", 2), lin = $piece(linky(i), "~", 3)
		set vals = ##class(%DynamicObject).%New()
		do ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).DopravceSkalary(db, sku, lin, vals)
		set dopravci = ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).BuildDopravci(db, sku, lin)
		if dopravci.%Size() = 0 {
			// Bez dopravce musí pole existovat a být prázdná, ne chybět.
			do $$$AssertEquals(vals.%Get("NAZEV_DOPRAVCE"), "", db_" "_lin_": bez dopravce je název prázdný, ne chybějící")
			continue
		}
		set sDopravcem = sDopravcem + 1
		set prvni = dopravci.%Get(0)
		do $$$AssertEquals(vals.%Get("NAZEV_DOPRAVCE"), prvni.NAZEV, db_" "_lin_": NAZEV_DOPRAVCE = první dopravce")
		do $$$AssertEquals(vals.%Get("ADRESA_DOPRAVCE"), prvni.ADRESA, db_" "_lin_": ADRESA_DOPRAVCE = první dopravce")
		do $$$AssertEquals(vals.%Get("ICO_DOPRAVCE"), prvni.ICO, db_" "_lin_": ICO_DOPRAVCE = první dopravce")
	}
	do $$$AssertTrue(sDopravcem > 0, "aspoň jedna linka sady má dopravce - jinak test nic neověří")
}

/// Skalární pole musí být ve schématu, kolekce dopravců naopak NESMÍ (v datech ji hlídá
/// `KontraktAgendTest.TestDataOdpovidajiNabidce`).
///
/// ⚠️ Kolekce je **vědomě pryč** (rozhodnutí Jan 1. 9., provozovny vyhozené 4. 9.): řádek na dopravce
/// × provozovnu nutně opakuje údaje firmy, takže tři provozovny daly třikrát týž telefon pod sebou -
/// a tabulka z dat deduplikaci neumí. Kdyby se kolekce vrátila bez vyřešené hierarchie „dopravce →
/// provozovny", spadne tenhle test - a to je záměr.
Method TestSchemaNabiziSkalarniPoleAleNeKolekci()
{
	do ..SetupSys(.sys)
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetSchema(.sys)
	set def = ##class(%DynamicObject).%FromJSON(^||ML("OUT",1))

	set nalezeno = 0
	set it = def.fields.%GetIterator()
	while it.%GetNext(.i, .f) {
		if f.name = "NAZEV_DOPRAVCE" set nalezeno = nalezeno + 1
		if f.name = "ADRESA_DOPRAVCE" set nalezeno = nalezeno + 1
		if f.name = "ICO_DOPRAVCE" set nalezeno = nalezeno + 1
	}
	do $$$AssertEquals(nalezeno, 3, "schéma nabízí skalární pole dopravce")

	set maKolekci = 0
	set it2 = def.fieldCollections.%GetIterator()
	while it2.%GetNext(.j, .c) {
		if c.collectionKey = "DOPRAVCI" set maKolekci = 1
	}
	do $$$AssertEquals(maKolekci, 0, "kolekce DOPRAVCI zůstává skrytá, dokud se neřeší duplicita údajů firmy")
}

}
