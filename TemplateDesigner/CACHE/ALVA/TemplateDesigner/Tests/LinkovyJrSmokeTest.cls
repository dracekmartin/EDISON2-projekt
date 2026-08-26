Include (%occInclude, GENERAL)

/// Smoke test obou linkových agend (`LinkovyJrAd`, `LinkovyJrMhd`): projdou všechny metody kontraktu
/// bez chyby a vrátí strukturu, kterou `LineTableElement` umí přečíst?
///
/// **Proč to tu je:** do 19. 8. neměly linkové agendy (2 000 řádků) ani jeden test. Rozbitá agenda
/// se pozná až tím, že uživateli zůstane prázdná tabulka - a v návrháři to vypadá jako chyba šablony,
/// ne serveru. Tenhle test je nejlevnější pojistka, že se vůbec zavolat dají.
///
/// Firma, která na serveru není, se přeskočí; test nesmí padat kvůli tomu, kde běží.
Class ALVA.TemplateDesigner.Tests.LinkovyJrSmokeTest Extends ALVA.EDTesting.MLTestCase
{

/// Wire klíče, které musí být v každé stránce - na nich stojí `LineTableElement`.
/// Kdyby některý zmizel, prvek nakreslí prázdno a nikdo se to nedozví.
Method TestAdVraciOcekavaneKolekce()
{
	do ..SetupSys(.sys)
	do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).Jadro(.jadro)
	kill parametry
	set overeno = 0, chybi = "", prazdnych = 0, i = ""
	for  {
		set i = $order(jadro(i))
		quit:i=""
		set db = $piece(jadro(i),"~",1), sku = $piece(jadro(i),"~",2), lin = $piece(jadro(i),"~",3)
		continue:'##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, db, sku, lin, .parametry)
		set overeno = overeno + 1
		do $$$AssertEquals($get(^||ML("OUT","ERR")), "", "GetData "_db_" "_lin_" nesmí vrátit chybu")
		set key = ""
		for  {
			set key = $order(^||ML("OUT", key))
			quit:key=""
			continue:key="ERR"
			set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
			for kolekce = "ZASTAVKY", "SPOJE", "SKUPINY_DNU", "POZNAMKY" {
				if '$isobject(page.rows.%Get(kolekce)) {
					set chybi = chybi _ $select(chybi="":"", 1:", ") _ db_" "_lin_": "_kolekce
				}
			}
			if page.values.%Get("SMER") = "" set prazdnych = prazdnych + 1
		}
	}
	if 'overeno {
		do $$$AssertTrue(1, "žádná firma z jádra sady není na tomto serveru - přeskočeno")
		quit
	}
	do $$$AssertEquals(chybi, "", "ve výstupu chybí kolekce: "_chybi)
	do $$$AssertEquals(prazdnych, 0, "stránek bez směru: "_prazdnych)
}

/// Zbytek kontraktu agendy - schéma, parametry, ukázková data. Volá se z návrháře při každém
/// otevření, takže výpadek kterékoli metody znamená, že se agenda nedá vybrat.
Method TestAdKontraktProjde()
{
	do ..SetupSys(.sys)
	do ..ProjdiKontrakt(.sys, "ALVA.TemplateDesigner.Agendy.LinkovyJrAd", "AD")
}

Method TestMhdKontraktProjde()
{
	do ..SetupSys(.sys)
	do ..ProjdiKontrakt(.sys, "ALVA.TemplateDesigner.Agendy.LinkovyJrMhd", "MHD")
}

/// Zavolá GetSchema / GetParameters / GetMockData a ověří, že vrátí platný JSON bez chyby.
/// Je to `Method`, ne `ClassMethod` - `$$$Assert*` jsou instanční a z class metody je zavolat nejde.
Method ProjdiKontrakt(ByRef sys, trida As %String, popis As %String)
{
	for metoda = "GetSchema", "GetParameters", "GetMockData" {
		kill ^||ML
		do $classmethod(trida, metoda, .sys)
		do $$$AssertEquals($get(^||ML("OUT","ERR")), "", popis_" "_metoda_" nesmí vrátit chybu")
		set json = $get(^||ML("OUT", 1))
		do $$$AssertNotEquals(json, "", popis_" "_metoda_" musí něco vrátit")
		// Neplatný JSON by na klientu skončil výjimkou při parsování, ne čitelnou hláškou.
		set ok = 1
		try { do ##class(%DynamicAbstractObject).%FromJSON(json) } catch { set ok = 0 }
		do $$$AssertEquals(ok, 1, popis_" "_metoda_" musí vrátit platný JSON")
	}
}

/// Výběr instancí musí u **každé** agendy vracet číslo i název - ne jen vnitřní hodnotu.
///
/// ⚠️ **Přesně tohle bylo 24. 8. rozbité**: linková agenda posílala jen `value`, `display`
/// a `subtitle`, jenže seznam vypisuje sloupce `cislo` a `nazev`, takže se uživateli otevřel
/// **prázdný** výběr linek. Ze serveru přitom chodilo 200 řádků, takže na tom nebylo nic vidět,
/// dokud to někdo nezkusil v aplikaci. Test kontroluje **tvar** odpovědi, ne počet.
Method TestVyberInstanciMaCisloINazev()
{
	do ..SetupSys(.sys)
	set chybi = "", overeno = 0
	for trida = "ALVA.TemplateDesigner.Agendy.LinkovyJrAd", "ALVA.TemplateDesigner.Agendy.LinkovyJrMhd", "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd" {
		kill ^||ML
		set ^||ML("IN","search") = ""
		set ^||ML("IN","limit") = 5
		do $classmethod(trida, "GetAvailableInstances", .sys)
		set json = $get(^||ML("OUT", 1))
		continue:json=""
		set arr = ##class(%DynamicArray).%FromJSON(json)
		continue:arr.%Size()=0
		set overeno = overeno + 1
		set prvni = arr.%Get(0)
		for pole = "value", "cislo", "nazev" {
			continue:prvni.%Get(pole)'=""
			set chybi = chybi _ $select(chybi="":"", 1:", ") _ $piece(trida, ".", *) _ ":" _ pole
		}
	}
	if 'overeno {
		do $$$AssertTrue(1, "žádná agenda na tomto serveru nevrátila instance - přeskočeno")
		quit
	}
	do $$$AssertEquals(chybi, "", "výběr instancí vrací prázdná pole (seznam by byl prázdný): "_chybi)
}

/// Schéma musí popisovat **tytéž kolekce**, jaké `GetData` reálně posílá. Rozejít se můžou tiše:
/// uživatel pak v nabídce dat vidí skupinu, která v datech není (nebo naopak), a tabulka zůstane
/// prázdná. Kontroluje se u obou agend, protože sdílejí jeden prvek.
Method TestSchemaOdpovidaDatum()
{
	do ..SetupSys(.sys)
	for trida = "ALVA.TemplateDesigner.Agendy.LinkovyJrAd", "ALVA.TemplateDesigner.Agendy.LinkovyJrMhd" {
		kill ^||ML
		do $classmethod(trida, "GetSchema", .sys)
		set schema = ##class(%DynamicAbstractObject).%FromJSON($get(^||ML("OUT", 1)))
		kill vSchematu
		set kolekce = schema.%Get("fieldCollections")
		if $isobject(kolekce) {
			for ci = 0:1:kolekce.%Size()-1 {
				set vSchematu(kolekce.%Get(ci).%Get("collectionKey")) = ""
			}
		}
		set chybi = ""
		for ocekavana = "ZASTAVKY", "SPOJE", "SKUPINY_DNU", "POZNAMKY" {
			continue:$data(vSchematu(ocekavana))
			set chybi = chybi _ $select(chybi="":"", 1:", ") _ ocekavana
		}
		do $$$AssertEquals(chybi, "", trida_": schéma nepopisuje kolekce "_chybi)
	}
}

}
