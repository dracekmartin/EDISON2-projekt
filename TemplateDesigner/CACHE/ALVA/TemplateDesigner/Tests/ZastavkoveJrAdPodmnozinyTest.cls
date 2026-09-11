Include %occInclude

/// `B142` - pravidlo „varianta je podmnožinou jiné", podle kterého se slévají variantní sloupce
/// jízdních dob. Testuje se samotné pravidlo, ne celý GetData: rozhodnutí je v něm a průchod nad
/// linkou by ho zkoušel jen tam, kde zrovna taková dvojice v datech je.
///
/// Signatura varianty nese per zastávku „S" (staví), „&lt;" (projíždí) nebo prázdno (mimo trasu),
/// pieces oddělené „~". Pro cestujícího znamená projíždí i mimo trasu totéž („tady nenastoupíš"),
/// takže se rozlišuje jen „S" - rozhodnutí Jan 11. 9.
Class ALVA.TemplateDesigner.Tests.ZastavkoveJrAdPodmnozinyTest Extends ALVA.EDTesting.MLTestCase
{

/// Varianta, která staví jen tam, kde staví i ta druhá, je její podmnožinou.
Method TestPodmnozinaSePozna()
{
	set mensi = "S~~S~S"
	set vetsi = "S~S~S~S"
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).JePodmnozinaVarianty(mensi, vetsi), 1, "menší varianta má být podmnožinou větší")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).JePodmnozinaVarianty(vetsi, mensi), 0, "větší varianta podmnožinou menší není")
}

/// „Projíždí" i „mimo trasu" se počítají stejně - cestující v obou případech nenastoupí.
Method TestProjizdiIMimoTrasuJsouMene()
{
	set projizdi = "S~<~S"
	set mimo     = "S~~S"
	set stavi    = "S~S~S"
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).JePodmnozinaVarianty(projizdi, stavi), 1, "projíždějící varianta je podmnožinou té, co tam staví")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).JePodmnozinaVarianty(mimo, stavi), 1, "varianta mimo trasu je podmnožinou té, co tam staví")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).JePodmnozinaVarianty(projizdi, mimo), 1, "projíždí a mimo trasu se navzájem neliší")
}

/// Varianta, která staví někde navíc, podmnožinou není - tam se informace ztratit nesmí.
Method TestJinaZastavkaNeniPodmnozina()
{
	set a = "S~S~~S"
	set b = "S~~S~S"
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).JePodmnozinaVarianty(a, b), 0, "varianta se zastávkou navíc není podmnožinou")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).JePodmnozinaVarianty(b, a), 0, "ani obráceně")
}

/// Delší signatura nedá podmnožinu, když je „S" za koncem té druhé - chybějící kousek se čte
/// jako prázdno. Jinak by se slily varianty, které se liší až za koncem kratší z nich.
Method TestZastaveniZaKoncemDruheSignatury()
{
	set delsi  = "S~S~S"
	set kratsi = "S~S"
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).JePodmnozinaVarianty(delsi, kratsi), 0, "zastavení za koncem druhé signatury podmnožinu ruší")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).JePodmnozinaVarianty(kratsi, delsi), 1, "kratší je podmnožinou delší")
}

/// Shodné signatury jsou podmnožinou navzájem - slévání je proto podmíněné i počtem zastavení,
/// jinak by se dvě stejné varianty slily do sebe a zmizely obě.
Method TestShodneSignatury()
{
	set sig = "S~<~S"
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).JePodmnozinaVarianty(sig, sig), 1, "shodná signatura je svou podmnožinou")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PocetZastaveni(sig), 2, "počítají se jen zastavení, ne projetí")
}

/// Velikost varianty = počet zastavení. Na ní stojí výběr NEJVĚTŠÍ nadmnožiny, takže se řetězec
/// A ⊆ B ⊆ C vyřeší jedním průchodem a výsledek nezávisí na pořadí.
Method TestPocetZastaveni()
{
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PocetZastaveni(""), 0, "prázdná signatura nemá zastavení")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PocetZastaveni("S~S~S~S"), 4, "čtyři zastavení")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PocetZastaveni("S~~<~S"), 2, "prázdno ani projetí se nepočítá")
}

/// Řetězec A ⊆ B ⊆ C: A musí být podmnožinou i té největší, jinak by se slévalo po dvojicích
/// a záleželo by na pořadí průchodu.
Method TestRetezecPodmnozin()
{
	set a = "S~~~S"
	set b = "S~S~~S"
	set c = "S~S~S~S"
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).JePodmnozinaVarianty(a, b), 1, "A je podmnožinou B")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).JePodmnozinaVarianty(b, c), 1, "B je podmnožinou C")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).JePodmnozinaVarianty(a, c), 1, "A je podmnožinou i té největší")
}

}
