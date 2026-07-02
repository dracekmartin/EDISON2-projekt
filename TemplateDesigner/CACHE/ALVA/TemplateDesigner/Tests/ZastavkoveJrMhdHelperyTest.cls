Include %occInclude

/// %UnitTest pro deterministické helpery ZastavkoveJrMhd — čisté funkce bez čtení globálů
/// (syntetické vstupy → přesné asserty, stabilní nezávisle na živých datech).
/// AD helpery (SestavTextOmezeni, ZarazeniSpoje, ...) doplnit až po dokončení
/// rozpracované práce na ZastavkoveJrAd — netestovat WIP stav.
Class ALVA.TemplateDesigner.Tests.ZastavkoveJrMhdHelperyTest Extends ALVA.EDTesting.MLTestCase
{

/// SpecZnak konverze: 1-7 = dny v týdnu, >31 = ASCII znak ($char), jinak beze změny.
/// Reference: cache-business-logic.md §SpecZnak + číselník CB_DP (72→H bezbariér,
/// 70→F částečně bezbariérové, 120→x přestup na železnici, 123→{ automat).
Method TestConvertSpecialniZnakDnyVTydnu()
{
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).ConvertSpecialniZnak(1), "Po", "1 → Po")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).ConvertSpecialniZnak(5), "Pá", "5 → Pá")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).ConvertSpecialniZnak(7), "Ne", "7 → Ne")
}

Method TestConvertSpecialniZnakAsciiZnaky()
{
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).ConvertSpecialniZnak(72), "H", "72 → H (bezbariérově přístupné)")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).ConvertSpecialniZnak(70), "F", "70 → F (částečně bezbariérové)")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).ConvertSpecialniZnak(120), "x", "120 → x (přestup na železnici)")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).ConvertSpecialniZnak(123), "{", "123 → { (jízdenkový automat)")
}

Method TestConvertSpecialniZnakOstatniBezeZmeny()
{
	// 8-31 není den ani ASCII rozsah → vrací se beze změny
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).ConvertSpecialniZnak(30), 30, "30 (≤31, ne den) → beze změny")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).ConvertSpecialniZnak(""), "", "prázdný vstup → prázdný výstup")
}

}
