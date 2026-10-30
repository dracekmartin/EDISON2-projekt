/// Varianty tras na vývěsce (sloupce jízdních dob v seznamu zastávek), společné vývěsce AD i MHD
/// (`architektonicka-rozhodnuti-agendy.md` R8). Do 30. 10. je počítala jen vývěska AD.
///
/// Varianta = sled zobrazených zastávek **od vývěskové zastávky dál**, kde spoj staví (S), projíždí (<)
/// nebo kudy nejede (prázdno), 1:1 legacy dedup podle `prutmbmp` (`JRapiLinPrtMhd1.mac:266-300`).
/// Spoje stejného sledu s jinými časy se slévají. Hodnoty sloupce jsou průběh **jednoho** spoje varianty
/// (zástupce, nejdelší jízda), nikdy se nemíchají, takže sloupec odpovídá jízdě, kterou opravdu něco jede.
///
/// Data zná agenda, tahle třída je nečte. Postup agendy pro jednu vývěsku:
/// 1. `k v`, pak `AddSpoj` za každý spoj, který z vývěskové zastávky odjíždí,
/// 2. `BuildSloupce` - slití úseků, řazení, ořez a písmena podle parametrů sestavy,
/// 3. `AddZastaveni` za každý sloupec a zastávku podle průběhu zástupce (`ZastupceSloupce`),
/// 4. `BuildTrasaMin` u každého řádku zastávky.
///
/// Struktura `v` (jen pro tuto třídu): v = počet variant; v("sled",sled) = varianta; v("spoj",spoj) = varianta
/// spoje (po slití ta přeživší); v(i,"sled"/"pocet"/"delka"/"zastupce"/"slita"); v(i,"zast",poradi) = $lb(stav, čas);
/// v("sloupcu") = počet tištěných sloupců; v("sloupec",n) = varianta n-tého sloupce.
Class ALVA.TemplateDesigner.Agendy.Jr.VariantyTras Extends %RegisteredObject
{

/// Výchozí sada písmen **variantních sloupců** - vzestupně a disjunktně proti sadě rozdílů tras AD
/// (`ZastavkoveJrAd.SadaRozdiluDefault`) i proti ostatním značkám u minuty.
///
/// ⚠️ Obě řady se do 3. 9. kryly: varianty brala zadrátovaná abeceda `A, B, C, D…`, rozdíly
/// `BCEFGHIJOQRWeijqsvwxz`, takže od druhé varianty výš mělo **jedno písmeno u minuty dva významy**
/// a legenda ho vysvětlila cizím textem (spoj variantou „E" si čtenář spároval s „jede jen do
/// zast. Babice"). Na vývěsce pro cestující je to dezinformace, ne kosmetika.
///
/// Vyškrtané je vše, co u minuty jako značka už je: sada rozdílů AD **včetně malých písmen** (velikost
/// se nerozlišuje, „V" vedle „v" ve výšce písma odjezdů nikdo nerozezná), `R` místenka, `K` kola,
/// `u` pokračující spoj, `H`/`F` bezbariér. `A` v sadě rozdílů není, proto zůstává první -
/// linka se dvěma variantami se tím nezmění a řady se rozejdou až u třetí (rozhodnutí Jan 2. 9.).
///
/// ⚠️ Rozdíly tras MHD mají výchozí sadu „a-z", takže na vývěsce MHD se písmeno varianty s písmenem
/// rozdílu potkat může. Sada pro MHD se teprve rozhodne (Jan 30. 10.); do té doby platí tahle.
Parameter SadaVariantDefault = "ADLMNPTY";

/// Zařadí spoj, který z vývěskové zastávky odjíždí, do varianty podle sledu.
/// `sled` = pro každou zobrazenou zastávku úseku „S", „<" nebo prázdno, oddělené „~" na pozici zastávky.
/// `delka` = jízdní doba z vývěskové zastávky na nejvzdálenější zastávku, kde spoj staví (`DelkaProfilu`).
/// `zastupce` = čím agenda najde průběh spoje (číslo trasy AD, spoj MHD). Zástupcem varianty je spoj
/// s nejdelší jízdou, při shodě nižší `zastupce` kvůli determinismu. Legacy „nechám průběh s delším
/// časem" (`JRapiLinPrtMhd1.mac:290-294`).
ClassMethod AddSpoj(ByRef v, spoj As %String, sled As %String, delka As %Integer, zastupce As %String)
{
    i '$d(v("sled",sled)) s v = $g(v) + 1, v("sled",sled) = v, v(v,"sled") = sled
    s vi = v("sled",sled)
    s v("spoj",spoj) = vi
    s v(vi,"pocet") = $g(v(vi,"pocet")) + 1
    i ('$d(v(vi,"delka"))) || (delka > v(vi,"delka")) || ((delka = v(vi,"delka")) && (zastupce < v(vi,"zastupce"))) {
        s v(vi,"delka") = delka, v(vi,"zastupce") = zastupce
    }
}

/// Seřadí varianty do sloupců a dá jim písmena podle parametrů sestavy (`Jr.Parametry.AddVariantyTras`):
/// slití úseků (`SLUCOVAT_PODMNOZINY`), řazení (`RAZENI_VARIANT`), ořez (`MAX_SLOUPCU_VARIANT`)
/// a první sloupec bez písmene (`PRVNI_BEZ_PISMENE`). Vrátí `VARIANTY_TRAS` (písmeno každého sloupce);
/// `pismenoSpoje(spoj)` dostane písmeno sloupce spoje, pokud ho sloupec má.
ClassMethod BuildSloupce(ByRef v, sada As %String, ByRef pismenoSpoje) As %DynamicArray
{
    s pocetVariant = +$g(v)

    // `B142`: varianta, která staví jen tam, kde staví i jiná, se dá slít do té největší nadmnožiny.
    // ⚠️ Vědomě se tím ZTRÁCÍ informace - u slitých spojů není ve sloupci vidět, které zastávky
    // vynechávají, a časy ukazuje ta delší varianta (vynechaná zastávka posouvá i následující časy).
    // Rozdíly tras to nahrazují jen zčásti: vypínají se u linky a měří se proti zaškrtnuté základní
    // trase, ne proti druhé variantě. Proto jen na vyžádání (rozhodnutí Jan 11. 9.).
    // Cíl je vždy NEJVĚTŠÍ nadmnožina, takže sám nikdy slitý není a řetězce A ⊆ B ⊆ C se vyřeší
    // jedním průchodem - kdyby se slévalo po dvojicích, záleželo by na pořadí.
    i $zcvt($g(^||ML("IN","SLUCOVAT_PODMNOZINY")),"L") = "true" {
        f viA=1:1:pocetVariant {
            s staveniA = ..PocetZastaveni($g(v(viA,"sled")))
            s cil = 0, cilStaveni = 0
            f viB=1:1:pocetVariant {
                i viB = viA continue
                i '..JeUsekemVarianty($g(v(viA,"sled")), $g(v(viB,"sled"))) continue
                s staveniB = ..PocetZastaveni($g(v(viB,"sled")))
                i staveniB '> staveniA continue          // shodný počet = táž varianta, tu slil dedup
                i (cil = 0) || (staveniB > cilStaveni) || ((staveniB = cilStaveni) && (+$g(v(viB,"delka")) > +$g(v(cil,"delka")))) {
                    s cil = viB, cilStaveni = staveniB
                }
            }
            i cil s v(viA,"slita") = cil
        }
        // Četnost přebírá přeživší varianta - na ní visí řazení „Dle četnosti ze zastávky".
        f viA=1:1:pocetVariant {
            i '$d(v(viA,"slita")) continue
            s cil = v(viA,"slita")
            s v(cil,"pocet") = $g(v(cil,"pocet")) + $g(v(viA,"pocet"))
        }
        // Spoje slité varianty musí ukazovat na písmeno té přeživší, jinak by mířily na zmizelý sloupec.
        s spoj = ""
        f {
            s spoj = $o(v("spoj",spoj))
            q:spoj=""
            i $d(v(v("spoj",spoj),"slita")) s v("spoj",spoj) = v(v("spoj",spoj),"slita")
        }
    }

    // Pořadí sloupců: podle četnosti ze zastávky (KOVED default - nejvíc spojů první, shoda četností
    // → pořadí výskytu) / „Stabilní" = pořadí výskytu spojů / podle délky jízdy.
    // `B142`: slitá varianta se do seznamu nedostane - její spoje už ukazují na přeživší sloupec.
    // Nepřečíslovává se, jen se přeskočí, takže díra v pořadí nevznikne (na pořadí visí písmena).
    k v("sloupec")
    s pocetSloupcu = 0
    s razeni = $g(^||ML("IN","RAZENI_VARIANT"))
    i razeni = "Stabilní (pořadí spojů)" {
        f vi=1:1:pocetVariant {
            i $d(v(vi,"slita")) continue
            s pocetSloupcu = pocetSloupcu + 1
            s v("sloupec",pocetSloupcu) = vi
        }
    } else {
        // Nejrychlejší varianta stojí hned vedle sloupce „Čas", nejpomalejší vpravo - cestující
        // pak čte sloupce jako škálu, ne jako nahodilé pořadí. Délka = kumulativní čas nejdál
        // od vývěskové zastávky, tedy táž hodnota, kterou se vybírá zástupce varianty.
        // Shoda → pořadí výskytu, ať výstup nezávisí na pořadí průchodu.
        s podleDelky = (razeni = "Dle délky jízdy (nejkratší první)") || (razeni = "Dle délky jízdy (nejdelší první)")
        s opacne = (razeni = "Dle délky jízdy (nejdelší první)")
        k poradi
        f vi=1:1:pocetVariant {
            i $d(v(vi,"slita")) continue
            i podleDelky { s poradi($s(opacne:-$g(v(vi,"delka")), 1:+$g(v(vi,"delka"))),vi) = "" }
            else { s poradi(-$g(v(vi,"pocet")),vi) = "" }
        }
        s klic = ""
        f {
            s klic = $o(poradi(klic))
            q:klic=""
            s vi = ""
            f {
                s vi = $o(poradi(klic,vi))
                q:vi=""
                s pocetSloupcu = pocetSloupcu + 1
                s v("sloupec",pocetSloupcu) = vi
            }
        }
    }

    // `B181` (TRANSDEV #12843): nejvýš N sloupců - prvních N podle zvoleného řazení, zbytek se
    // netiskne. ⚠️ Vědomá ztráta informace: spoje skrytých variant nemají sloupec, a protože písmena
    // se dávají jen tištěným sloupcům, nemají ani písmeno. Kombinace s písmeny variant tak nedává
    // smysl a ani se neřeší (rozhodnutí Jan 25. 9.). Prázdné (i 0) = bez omezení.
    s maxSloupcu = +$g(^||ML("IN","MAX_SLOUPCU_VARIANT"))
    i (maxSloupcu > 0) && (pocetSloupcu > maxSloupcu) {
        f n=(maxSloupcu+1):1:pocetSloupcu k v("sloupec",n)
        s pocetSloupcu = maxSloupcu
    }
    s v("sloupcu") = pocetSloupcu

    // Písmena variant nejsou volba: bez nich nejde u minuty poznat, který časový sloupec pro ni
    // platí. Volbou zůstává jen „první varianta bez písmene" - ta zároveň drží vývěsku s jedinou
    // variantou bez zbytečného „A" u každé minuty (rozhodnutí Jan 17. 8., naměřeno na lince 0330520).
    // Bool parametr se čte tolerantně malými písmeny - starší klient posílal C# ToString() „True".
    s prvniBez = ($zcvt($g(^||ML("IN","PRVNI_BEZ_PISMENE")),"L") '= "false")
    s pismena = ##class(%DynamicArray).%New()
    k pismenoVarianty
    f n=1:1:pocetSloupcu {
        s pismeno = ..PismenoVarianty(n, prvniBez, sada)
        i pismeno '= "" s pismenoVarianty(v("sloupec",n)) = pismeno
        d pismena.%Push(pismeno)
    }
    s spoj = ""
    f {
        s spoj = $o(v("spoj",spoj))
        q:spoj=""
        i $g(pismenoVarianty(v("spoj",spoj))) '= "" s pismenoSpoje(spoj) = pismenoVarianty(v("spoj",spoj))
    }
    q pismena
}

/// Počet tištěných sloupců variant (po `BuildSloupce`).
ClassMethod PocetSloupcu(ByRef v) As %Integer
{
    q +$g(v("sloupcu"))
}

/// Zástupce varianty ve sloupci `sloupec` (1-based) - podle něj agenda najde průběh pro `AddZastaveni`.
ClassMethod ZastupceSloupce(ByRef v, sloupec As %Integer) As %String
{
    q $g(v(v("sloupec",sloupec),"zastupce"))
}

/// Zapíše, co zástupce varianty ve sloupci `sloupec` dělá na zastávce `poradi`: `stav` „S" (staví,
/// `cas` = jízdní doba od vývěskové zastávky, u projeté zastávky záporná), „<" (projíždí) nebo prázdno.
ClassMethod AddZastaveni(ByRef v, sloupec As %Integer, poradi As %String, stav As %String, cas As %String = "")
{
    i stav = "" q
    s v(v("sloupec",sloupec),"zast",poradi) = $lb(stav, cas)
}

/// Staví varianta ve sloupci `sloupec` na zastávce `poradi`?
ClassMethod MaZastaveni(ByRef v, sloupec As %Integer, poradi As %String) As %Boolean
{
    q $lg($g(v(v("sloupec",sloupec),"zast",poradi)),1) = "S"
}

/// Hodnoty sloupců variant na řádku zastávky `poradi` (`TRASA_MIN`): jízdní doba, kde varianta staví,
/// jinak „<" nebo prázdno.
ClassMethod BuildTrasaMin(ByRef v, poradi As %String) As %DynamicArray
{
    s hodnoty = ##class(%DynamicArray).%New()
    f n=1:1:..PocetSloupcu(.v) {
        s z = $g(v(v("sloupec",n),"zast",poradi))
        i $lg(z,1) = "S" { d hodnoty.%Push(+$lg(z,2)) }
        else { d hodnoty.%Push($lg(z,1)) }
    }
    q hodnoty
}

/// Leží zastávka `poradi` v úseku, podle kterého se rozlišují varianty tras? Tedy od vývěskové
/// zastávky (včetně) dál ve směru jízdy - 1:1 legacy smyčka `f mytc=Tc:Krok:TcDo`
/// (`JRapiLinPrtMhd1.mac:274`).
///
/// ⚠️ Rozdíl trasy PŘED vývěskovou zastávkou nesmí zakládat vlastní variantu: časy se počítají jako
/// rozdíl proti vývěskové zastávce, takže dva spoje se zajížďkou v první polovině linky dají ve druhé
/// polovině dva sloupce s IDENTICKÝMI čísly (hlásil TRANSDEV, #12843). Zpětný směr jde v pořadích
/// sestupně, proto se srovnává obráceně.
ClassMethod JeVUsekuVariant(poradi As %String, currentPoradi As %String, jeTam As %Boolean) As %Boolean
{
    q $s(jeTam:(poradi '< currentPoradi), 1:(poradi '> currentPoradi))
}

/// `B157`: jede varianta `a` jen **souvislý úsek** varianty `b`?
///
/// Sled nese pro každou zastávku „S" (staví), „<" (projíždí) nebo prázdno (mimo trasu) - projíždí
/// i mimo trasu znamená pro cestujícího totéž („tady nenastoupíš"), takže se rozlišuje jen „S".
/// Delší sled než `b` úsek nedá: chybějící kousek se čte jako prázdno, tedy ne „S".
///
/// Dvě podmínky. Kde staví `a`, musí stavět i `b` - a mezi **první a poslední** zastávkou `a`
/// nesmí být žádná, kde staví `b` a `a` ne.
///
/// ⚠️ **Druhá podmínka je celý smysl `B157`** (zadání TRANSDEV 16. 9., opravuje `B142`). Bez ní
/// platí pouhá podmnožina, a tou je i varianta, která uprostřed trasy **zajíždí jinam** a část
/// zastávek vynechá. Ta se slila do delší, takže z vývěsky zmizelo, že tam spoj nejede - zákazník
/// to hlásil jako „prestali sa rozlišovať varianty, kde uprostred trasy je zachádzka". Sám uznal,
/// že požadavek zadal špatně; cílem bylo skrýt varianty jedoucí jen část trasy (od-do).
ClassMethod JeUsekemVarianty(a As %String, b As %String) As %Boolean
{
    s prvni = 0, posledni = 0
    f i=1:1:$l(a,"~") {
        i $p(a,"~",i) '= "S" continue
        i $p(b,"~",i) '= "S" ret 0
        s:'prvni prvni = i
        s posledni = i
    }
    // Varianta, která nikde nestaví, není úsek ničeho - slít ji by zahodilo sloupec beze stopy.
    i 'prvni ret 0
    f i=prvni:1:posledni {
        i ($p(b,"~",i) = "S") && ($p(a,"~",i) '= "S") ret 0
    }
    ret 1
}

/// Je varianta `a` podmnožinou `b`? **Slévání to už neřídí** (od `B157` rozhoduje
/// <method>JeUsekemVarianty</method>) - zůstává jen proto, aby šel v testu doložit rozdíl mezi
/// oběma pravidly: zajížďka uprostřed trasy je podmnožinou, ale úsekem ne.
ClassMethod JePodmnozinaProSrovnani(a As %String, b As %String) As %Boolean
{
    f i=1:1:$l(a,"~") {
        i $p(a,"~",i) '= "S" continue
        i $p(b,"~",i) '= "S" ret 0
    }
    ret 1
}

/// Počet zastávek, kde varianta staví. Velikost varianty pro výběr největší nadmnožiny (`B142`) -
/// na ní stojí to, že se řetězec A ⊆ B ⊆ C vyřeší jedním průchodem.
ClassMethod PocetZastaveni(sled As %String) As %Integer
{
    s n = 0
    f i=1:1:$l(sled,"~") {
        i $p(sled,"~",i) = "S" s n = n + 1
    }
    ret n
}

/// Písmeno variantního sloupce (1-based pořadí sloupce) ze zadané sady; `prvniBez` → první sloupec
/// (hlavní varianta) bez písmene a sada běží od druhého. Přes délku sady se písmena **zdvojují**
/// (`DD`, `LL`) přes <method>ZnakZeSady</method> - stejně jako u rozdílů tras a jako v legacy.
/// Prázdná sada → výchozí sada.
ClassMethod PismenoVarianty(poradiSloupce As %Integer, prvniBez As %Boolean = 1, sada As %String = "") As %String
{
    i sada = "" s sada = ..#SadaVariantDefault
    s idx = poradiSloupce - $s(prvniBez:1, 1:0)
    i idx <= 0 q ""
    q ..ZnakZeSady(sada, idx)
}

/// Značka číslo `poradi` z písmenné sady; když písmena dojdou, zdvojí je (B…z, pak BB…zz).
/// 1:1 `GetZnk^JRapiLinPrtMhd`. Sdílí ji písmena variant i rozdíly tras AD.
ClassMethod ZnakZeSady(sada As %String, poradi As %Integer) As %String
{
    s len = $l(sada), ml = 0
    f { q:poradi'>len  s poradi = poradi-len, ml = ml+1 }
    s znk = $e(sada,poradi), one = znk
    f i=1:1:ml s znk = znk_one
    q znk
}

}
