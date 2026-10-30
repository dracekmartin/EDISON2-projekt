Include %occInclude

/// `B157` - pravidlo „varianta jede jen souvislý úsek jiné", podle kterého se slévají variantní
/// sloupce jízdních dob. Testuje se samotné pravidlo, ne celý GetData: rozhodnutí je v něm
/// a průchod nad linkou by ho zkoušel jen tam, kde zrovna taková dvojice v datech je.
///
/// Signatura varianty nese per zastávku „S" (staví), „&lt;" (projíždí) nebo prázdno (mimo trasu),
/// díly oddělené „~". Pro cestujícího znamená projíždí i mimo trasu totéž („tady nenastoupíš"),
/// takže se rozlišuje jen „S" - rozhodnutí Jan 11. 9.
///
/// ⚠️ **Do 16. 9. tu platila pouhá podmnožina** (`B142`) a tři z těchhle testů měly opačné
/// očekávání. Podmnožinou je totiž i varianta, která uprostřed **zajíždí jinam** a část zastávek
/// vynechá - ta se slila do delší a z vývěsky zmizelo, že tam spoj nejede. Název třídy zůstal,
/// aby na serveru nezůstala viset osiřelá stará.
Class ALVA.TemplateDesigner.Tests.ZastavkoveJrAdPodmnozinyTest Extends ALVA.EDTesting.MLTestCase
{

/// Varianta, která jede souvislý kus trasy od-do, je úsekem té delší. To je ta, kterou
/// zákazník skrýt chce.
Method TestSouvislyUsekSePozna()
{
	set kratsi = "~~S~S~S~"
	set delsi  = "S~S~S~S~S~S"
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).JeUsekemVarianty(kratsi, delsi), 1, "kratší varianta jede souvislý úsek delší")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).JeUsekemVarianty(delsi, kratsi), 0, "delší varianta úsekem kratší není")
}

/// ⚠️ **Jádro `B157`.** Varianta, která uprostřed trasy zajíždí jinam a část zastávek vynechá,
/// je sice podmnožinou delší, ale úsekem ne - a **sloupec si musí nechat**. Bez toho z vývěsky
/// zmizí, že tam spoj nejede (nahlásil TRANSDEV 16. 9.).
Method TestZachazkaUprostredSiSloupecNecha()
{
	set sezachazkou = "S~S~~S~S~S"
	set primy       = "S~S~S~S~S~S"
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).JeUsekemVarianty(sezachazkou, primy), 0, "varianta vynechávající zastávku uprostřed není úsekem")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).JePodmnozinaProSrovnani(sezachazkou, primy), 1, "podmnožinou přitom je - právě proto se pravidlo měnilo")
}

/// „Projíždí" i „mimo trasu" se počítají stejně - cestující v obou případech nenastoupí.
/// Díra uprostřed ale úsek ruší bez ohledu na to, kterým z těch dvou znaků je zapsaná.
Method TestProjizdiIMimoTrasuJsouStejne()
{
	set projizdi = "S~<~S"
	set mimo     = "S~~S"
	set stavi    = "S~S~S"
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).JeUsekemVarianty(projizdi, stavi), 0, "díra uprostřed ruší úsek i když je to projetí")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).JeUsekemVarianty(mimo, stavi), 0, "a stejně tak když je mimo trasu")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).JeUsekemVarianty(projizdi, mimo), 1, "projíždí a mimo trasu se navzájem neliší")
}

/// Varianta, která staví někde navíc, úsekem není - tam se informace ztratit nesmí.
Method TestJinaZastavkaNeniUsek()
{
	set a = "S~S~~S"
	set b = "S~~S~S"
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).JeUsekemVarianty(a, b), 0, "varianta se zastávkou navíc není úsekem")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).JeUsekemVarianty(b, a), 0, "ani obráceně")
}

/// Delší signatura nedá úsek, když je „S" za koncem té druhé - chybějící kousek se čte
/// jako prázdno. Jinak by se slily varianty, které se liší až za koncem kratší z nich.
Method TestZastaveniZaKoncemDruheSignatury()
{
	set delsi  = "S~S~S"
	set kratsi = "S~S"
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).JeUsekemVarianty(delsi, kratsi), 0, "zastavení za koncem druhé signatury úsek ruší")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).JeUsekemVarianty(kratsi, delsi), 1, "kratší jede úsek delší")
}

/// Shodné signatury jsou úsekem navzájem - slévání je proto podmíněné i počtem zastavení,
/// jinak by se dvě stejné varianty slily do sebe a zmizely obě.
Method TestShodneSignatury()
{
	set sig = "S~<~S"
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).JeUsekemVarianty(sig, sig), 1, "shodná signatura je svým úsekem")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).PocetZastaveni(sig), 2, "počítají se jen zastavení, ne projetí")
}

/// Varianta, která nikde nestaví, není úsekem ničeho - slít ji by zahodilo sloupec beze stopy.
Method TestVariantaBezZastaveni()
{
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).JeUsekemVarianty("~<~", "S~S~S"), 0, "varianta bez zastavení se neslévá")
}

/// Velikost varianty = počet zastavení. Na ní stojí výběr NEJVĚTŠÍ nadmnožiny, takže se řetězec
/// A ⊆ B ⊆ C vyřeší jedním průchodem a výsledek nezávisí na pořadí.
Method TestPocetZastaveni()
{
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).PocetZastaveni(""), 0, "prázdná signatura nemá zastavení")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).PocetZastaveni("S~S~S~S"), 4, "čtyři zastavení")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).PocetZastaveni("S~~<~S"), 2, "prázdno ani projetí se nepočítá")
}

/// Řetězec úseků A ⊂ B ⊂ C: A musí být úsekem i té největší, jinak by se slévalo po dvojicích
/// a záleželo by na pořadí průchodu.
Method TestRetezecUseku()
{
	set a = "~~S~S~~"
	set b = "~S~S~S~"
	set c = "S~S~S~S~S"
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).JeUsekemVarianty(a, b), 1, "A jede úsek B")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).JeUsekemVarianty(b, c), 1, "B jede úsek C")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).JeUsekemVarianty(a, c), 1, "A jede úsek i té největší")
}

}
