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
Method TestAdresaDopravceNesePlnyTvarJakoStaryPopis()
{
	do ..SetupSys(.sys)
	do ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).PrvniLinka($namespace, .sku, .lin)
	if lin = "" {
		do $$$AssertTrue(1, "Namespace nemá žádnou linku v ^JRL - test přeskočen")
		quit
	}

	kill udaje
	set dopravci = ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).BuildDopravci($namespace, sku, lin)
	if dopravci.%Size() = 0 {
		do $$$AssertTrue(1, "Linka "_sku_"/"_lin_" nemá v datech dopravce - test přeskočen")
		quit
	}

	set prvni = dopravci.%Get(0)
	set popis = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PopisDopravce($namespace, sku, lin)

	do $$$AssertNotEquals(prvni.NAZEV, "", "dopravce má název")
	// Adresa ze samostatného pole musí být v tom starém slepeném bloku obsažená celá - jinak jsme
	// vzali kratší variantu z číselníku a zákazníkovi zmizelo PSČ a obec.
	if prvni.ADRESA '= "" {
		do $$$AssertTrue((popis [ prvni.ADRESA), "adresa """_prvni.ADRESA_""" musí být celá v POPIS_DOPRAVCE ("_popis_")")
	}
}

/// Skalární pole nesou prvního dopravce linky, toho, kterého vrací `BuildDopravci` jako první.
Method TestSkalarniPoleOdpovidajiPrvnimuDopravci()
{
	do ..SetupSys(.sys)
	do ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).PrvniLinka($namespace, .sku, .lin)
	if lin = "" {
		do $$$AssertTrue(1, "Namespace nemá žádnou linku v ^JRL - test přeskočen")
		quit
	}

	set vals = ##class(%DynamicObject).%New()
	do ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).DopravceSkalary($namespace, sku, lin, vals)

	set dopravci = ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).BuildDopravci($namespace, sku, lin)
	if dopravci.%Size() = 0 {
		// Bez dopravce musí pole existovat a být prázdná, ne chybět.
		do $$$AssertEquals(vals.%Get("NAZEV_DOPRAVCE"), "", "bez dopravce je název prázdný, ne chybějící")
		quit
	}

	set prvni = dopravci.%Get(0)
	do $$$AssertEquals(vals.%Get("NAZEV_DOPRAVCE"), prvni.NAZEV, "NAZEV_DOPRAVCE = první dopravce")
	do $$$AssertEquals(vals.%Get("ADRESA_DOPRAVCE"), prvni.ADRESA, "ADRESA_DOPRAVCE = první dopravce")
	do $$$AssertEquals(vals.%Get("ICO_DOPRAVCE"), prvni.ICO, "ICO_DOPRAVCE = první dopravce")
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
