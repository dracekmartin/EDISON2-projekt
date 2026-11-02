Include %occInclude

/// `B188`: údaje zastávky z registru se v tisku AD berou z časové varianty platné k „platí od"
/// linky, stejně jako je čte starý tisk (`gZst^JRapiZst` s `linHrlOd`). Zastávka i linka jsou
/// syntetické pod jménem s číslem procesu a po každém testu se smažou - skutečný registr se
/// nečte ani nemění.
Class ALVA.TemplateDesigner.Tests.ZastavkyKDatuTest Extends ALVA.EDTesting.MLTestCase
{

Parameter AGENDA = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd";

/// Hlavní záznam + varianty od horologu 100 (bez konce), 200 (platí do 210) a 300 (bez konce).
/// Název je v `$`-poli 2 jako ve skutečném registru.
ClassMethod Zastavka(hlavni As %String = "HLAVNI") As %String
{
	set czst = "TD-B188-"_$job
	kill ^JRZ(czst)
	set ^JRZ(czst) = "$"_hlavni
	set ^JRZ(czst,"CasoveVariantyUdaju",100) = "$OD100"
	set varianta = "$OD200", $piece(varianta,"$",42) = 210
	set ^JRZ(czst,"CasoveVariantyUdaju",200) = varianta
	set ^JRZ(czst,"CasoveVariantyUdaju",300) = "$OD300"
	quit czst
}

ClassMethod Nazev(czst As %String, hrl As %String, ByRef od As %String) As %String
{
	quit $piece(##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).ZaznamZastavkyKDatu($namespace, czst, hrl, .od), "$", 2)
}

Method OnAfterOneTest(testname As %String) As %Status
{
	kill ^JRZ("TD-B188-"_$job), ^JRL("TD-B188-"_$job)
	kill ^||TiskAd, ^||TiskKnizni
	quit ##super(testname)
}

/// Platí ta varianta, která začala nejpozději k datu a ještě neskončila; skončená se přeskočí
/// a platí předchozí. Před první variantou zůstává hlavní záznam.
Method TestVariantaKDatu()
{
	set czst = ..Zastavka()
	do $$$AssertEquals(..Nazev(czst, 150), "OD100", "mezi 100 a 200 platí varianta od 100")
	do $$$AssertEquals(..Nazev(czst, 205), "OD200", "varianta od 200 platí do 210")
	do $$$AssertEquals(..Nazev(czst, 210), "OD200", "konec platnosti je včetně")
	do $$$AssertEquals(..Nazev(czst, 215), "OD100", "po skončení varianty 200 platí dál ta od 100")
	do $$$AssertEquals(..Nazev(czst, 300), "OD300", "začátek platnosti je včetně")
	do $$$AssertEquals(..Nazev(czst, 50), "HLAVNI", "před první variantou hlavní záznam")
}

/// Zastávka, která má data jen ve variantách: před první variantou se vezme první varianta
/// (legacy fallback `JRapiZst.mac:141`), jinak by tisk vyšel bez názvu.
Method TestPrazdnyHlavniZaznam()
{
	set czst = ..Zastavka("")
	set ^JRZ(czst) = ""
	do $$$AssertEquals(..Nazev(czst, 50), "OD100", "prázdný hlavní záznam → první varianta")
	do $$$AssertEquals(..Nazev(czst, 150), "OD100", "k datu platná varianta beze změny")
}

/// Zastávka bez časových variant se chová jako dosud - tisk čte hlavní záznam.
Method TestBezVariant()
{
	set czst = ..Zastavka()
	kill ^JRZ(czst,"CasoveVariantyUdaju")
	do $$$AssertEquals(..Nazev(czst, 150), "HLAVNI", "bez variant hlavní záznam")
	do $$$AssertEquals(..Nazev(czst, ""), "HLAVNI", "bez data hlavní záznam")
}

/// Začátek vybrané varianty říká, odkud se berou sloupky zastávky (mají vlastní podstrom
/// u každé varianty); hlavní záznam = prázdný začátek.
Method TestZacatekVybraneVarianty()
{
	set czst = ..Zastavka()
	do ..Nazev(czst, 205, .od)
	do $$$AssertEquals(od, 200, "vybraná varianta od 200")
	do ..Nazev(czst, 50, .od)
	do $$$AssertEquals(od, "", "hlavní záznam nemá začátek varianty")
}

/// Popis sloupku se bere z vybrané varianty, a nemá-li varianta sloupky, z hlavního záznamu.
Method TestPopisSloupkuZVarianty()
{
	set czst = ..Zastavka()
	set ^JRZ(czst,"Sloupek",1) = "1~~~smer hlavni"
	set ^JRZ(czst,"CasoveVariantyUdaju",200,"Sloupek",1) = "1~~~smer 200"
	set ^||TiskAd("zastavky","hrl") = 205
	do $$$AssertEquals($classmethod(..#AGENDA, "PopisSloupku", $namespace, czst, 1), "smer 200", "sloupek z varianty")
	set ^||TiskAd("zastavky","hrl") = 150
	do $$$AssertEquals($classmethod(..#AGENDA, "PopisSloupku", $namespace, czst, 1), "smer hlavni", "varianta bez sloupků → hlavní záznam")
}

/// Datum, ke kterému se zastávky čtou = „platí od" linky (legacy `gLinHrlOd^JRapiLinUtl`).
Method TestPlatnostOdLinky()
{
	set sku = "TD-B188-"_$job
	set ^JRL(sku,"0000001","PATCH","Plati") = "11.12.2022~09.12.2023"
	set ocekavano = ##class(ALVA.GENERALAPI.Date).DateToHor("11.12.2022")
	set ad = "ALVA.TemplateDesigner.Agendy.Jr.DataAd"
	do $$$AssertEquals($lg($classmethod(ad, "HorologPlatnostiLinky", $namespace, sku, "0000001"), 1), ocekavano, "platí od linky")
	do $$$AssertEquals($lg($classmethod(ad, "HorologPlatnostiLinky", $namespace, sku, "0000002"), 1), "", "linka bez platnosti → prázdné (tisk pak bere dnešek)")
	// Přípona za dvojtečkou nepatří do data „do" (`B56` S8) - do 30. 10. ji tam čtyři místa ze šesti nechala.
	set ^JRL(sku,"0000003","PATCH","Plati") = "11.12.2022~09.12.2023:ruční poznámka"
	do $$$AssertEquals($listtostring($classmethod(ad, "PlatnostLinky", $namespace, sku, "0000003"), "~"), "11.12.2022~09.12.2023", "přípona se odřízne")
	do $$$AssertEquals($lg($classmethod(ad, "HorologPlatnostiLinky", $namespace, sku, "0000003"), 2), ##class(ALVA.GENERALAPI.Date).DateToHor("09.12.2023"), "platí do bez přípony")
}

/// Obě AD agendy čtou zastávku k datu, které si nastavily pro tištěnou linku.
Method TestAgendyCtouKDatuLinky()
{
	set czst = ..Zastavka()
	set ^||TiskAd("zastavky","hrl") = 205
	do $$$AssertEquals($piece(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).ZaznamZastavky($namespace, czst), "$", 2), "OD200", "zastávkový JŘ")
	set ^||TiskKnizni("zastavky","hrl") = 150
	do $$$AssertEquals($piece(##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).ZaznamZastavky($namespace, czst), "$", 2), "OD100", "linkový JŘ")
}

}
