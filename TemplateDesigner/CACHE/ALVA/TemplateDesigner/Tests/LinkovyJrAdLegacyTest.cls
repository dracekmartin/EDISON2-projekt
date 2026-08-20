Include (%occInclude, GENERAL)

/// **Brána proti starému modulu** pro linkový JŘ: pro každou linku jádra sady se spustí legacy
/// `mkPLinWin` i naše `LinkovyJrAd.GetData` a porovnají se pole po poli (pořadí spojů, názvy
/// zastávek, km, zóny, sloupec MHD, časy včetně značek, legenda).
///
/// **Proč ne zlatý odtisk:** digest je hash **našeho** výstupu - hlídá regresi proti včerejšku, ale
/// odchylku od starého modulu nezachytí ani v principu (pravidlo R6). Odtisk má vedle toho smysl
/// a žije v `LinkovyJrGoldTest`; tenhle test je ta druhá polovina.
///
/// **Co je pád a co ne** (mechanika v `LinkovyJrLegacyOracle.ZnameOdchylky`):
/// - `rozdil` = nikdo o něm nerozhodl → **pád**
/// - `odchylka` = chceme se lišit, s důvodem → projde
/// - `dluh` = nechceme se lišit, ale zatím se lišíme; má přiřazenou vlnu → projde, ale je vidět
///
/// Rozdělení je záměr, ne měkkost: síť má chytat, že jsme se rozešli **omylem**, a nesmí bránit
/// tomu, abychom se rozešli **záměrně** (požadavky z helpdesku, analýza §14).
///
/// ⚠️ Vázané na per-firma namespace (CB_CSADAD / KV_DP / VS_CSAD) a na přítomnost starého modulu.
/// Co na serveru není, se přeskočí - test nesmí padat kvůli tomu, kde běží.
/// Diagnostika mimo runner: `cache_query_cvar` na `LinkovyJrLegacyOracle.Souhrn`.
Class ALVA.TemplateDesigner.Tests.LinkovyJrAdLegacyTest Extends ALVA.EDTesting.MLTestCase
{

/// Kategorie, které se porovnávají. Pojmenované česky, ať je z hlášky poznat, o co jde.
ClassMethod Kategorie(ByRef kat)
{
	kill kat
	set kat("spoje") = "pořadí spojů"
	set kat("zast") = "názvy zastávek"
	set kat("km") = "kilometry"
	set kat("zona") = "zóny"
	set kat("mhd") = "sloupec MHD"
	set kat("cas") = "časy v buňkách"
	set kat("pozn") = "legenda poznámek"
}

/// Hlavní brána: na jádru sady nesmí zůstat **žádný nedeklarovaný rozdíl**.
Method TestParitaSeStarymModulem()
{
	do ..SetupSys(.sys)
	do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).Jadro(.jadro)
	do ..Kategorie(.kat)

	set overeno = 0, nalezy = "", souhrn = "", porovnano = 0, odchylek = 0, dluhu = 0
	set i = ""
	for  {
		set i = $order(jadro(i))
		quit:i=""
		set db = $piece(jadro(i),"~",1), sku = $piece(jadro(i),"~",2), lin = $piece(jadro(i),"~",3)
		continue:'##class(ALVA.TemplateDesigner.Tests.LinkovyJrLegacyOracle).Porovnej(.sys, db, sku, lin, .res)
		set overeno = overeno + 1
		set k = ""
		for  {
			set k = $order(kat(k))
			quit:k=""
			continue:'$data(res(k))
			set porovnano = porovnano + +$get(res(k, "leg"))
			set odchylek = odchylek + +$get(res(k, "odchylka"))
			set dluhu = dluhu + +$get(res(k, "dluh"))
			continue:'+$get(res(k, "rozdil"))
			set nalezy = nalezy _ $select(nalezy="":"", 1:" | ") _ db_" "_lin_" "_kat(k)_" ("_res(k,"rozdil")_"): "_$get(res(k, "prvni"))
		}
	}

	if 'overeno {
		do $$$AssertTrue(1, "starý modul ani jedna firma jádra sady na tomto serveru není - přeskočeno")
		quit
	}
	set souhrn = "linek "_overeno_", porovnáno hodnot "_porovnano_", vědomých odchylek "_odchylek_", dluhů "_dluhu
	do $$$AssertEquals(nalezy, "", "nedeklarovaný rozdíl proti starému modulu ["_souhrn_"]: "_nalezy)
	// Doklad, že brána reálně něco měřila - prázdné porovnání by prošlo taky.
	do $$$AssertEquals(porovnano > 1000, 1, "porovnáno podezřele málo hodnot ["_souhrn_"]")
}

/// Deklarovaná odchylka ani dluh nesmí být **mrtvý záznam**. Kdyby se rozdíl přestal objevovat
/// (opravili jsme ho, nebo zmizel z dat), zůstane v registru výjimka, která už nic nekryje - a příště
/// pod ní projde skutečná chyba. Běží jen na serveru s celým jádrem, jinak by hlásil mrtvý záznam
/// kvůli chybějící firmě.
Method TestRegistrNemaMrtveZaznamy()
{
	do ..SetupSys(.sys)
	do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).Jadro(.jadro)
	do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrLegacyOracle).ZnameOdchylky(.odch)

	set overeno = 0, pocetLinek = 0, i = ""
	kill uplatneno
	for  {
		set i = $order(jadro(i))
		quit:i=""
		set pocetLinek = pocetLinek + 1
		set db = $piece(jadro(i),"~",1), sku = $piece(jadro(i),"~",2), lin = $piece(jadro(i),"~",3)
		continue:'##class(ALVA.TemplateDesigner.Tests.LinkovyJrLegacyOracle).Porovnej(.sys, db, sku, lin, .res)
		set overeno = overeno + 1
		set k = ""
		for  {
			set k = $order(res(k))
			quit:k=""
			continue:k="duvod"
			if +$get(res(k, "odchylka")) set uplatneno(k) = ""
			if +$get(res(k, "dluh")) set uplatneno(k) = ""
		}
	}
	if overeno < pocetLinek {
		do $$$AssertTrue(1, "část jádra sady na serveru chybí - mrtvé záznamy registru se nekontrolují")
		quit
	}

	// Registr je vedený po kategoriích; mrtvá je ta, u které se na celém jádru neuplatnil ani jeden rozdíl.
	set mrtve = "", k = ""
	for  {
		set k = $order(odch(k))
		quit:k=""
		continue:$data(uplatneno(k))
		set mrtve = mrtve _ $select(mrtve="":"", 1:", ") _ k
	}
	do $$$AssertEquals(mrtve, "", "registr má záznam, který už žádný rozdíl nekryje (smaž ho): "_mrtve)
}

}
