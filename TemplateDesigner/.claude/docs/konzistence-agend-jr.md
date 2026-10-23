# Konzistence agend jízdních řádů (`B193`)

> Jednorázová analýza 23. 10. 2026: review zastávkového a linkového JŘ (AD i MHD) před vydáním
> linkového JŘ MHD. **Smazat po dokončení `B193`** - co má platit trvale, patří do `cache-api.md`,
> kandidáti sloučení kódu do [`legacy-nahrada.md §4`](legacy-nahrada.md).
> Rozdílový linkový JŘ je mimo rozsah: nebude šablona pro uživatele, ale nástroj modulu JŘ.

**Co je zamčené:** jen popisky voleb parametrů sestavy (ukládá se zobrazený text, `ux-conventions §8`).
Název parametru, skupina a popis se mění volně - návrhář je při načtení bere ze serveru a uložený
popis v šabloně přepíše (`TemplateDesignerViewModel.cs:2305`). U linkového MHD je do vydání volné
i pole v nabídce dat; po vydání platí „jen přidávat" (`cache-api.md §Evoluce kontraktu`).

**Co hlídají testy:** `AgendaSchemaDriftTests.cs:101-135` jen „stejné pole = stejný popisek a typ".
Parametry sestavy, tvar hodnot a pole, která agenda vždy pošle prázdná, nehlídá nic.

## B193a Dopravce a poznámka nad linku v linkovém MHD

- `LinkovyJrMhd.cls:429-430` posílá `POZNAMKA_NAD_LINKU` a `POPIS_DOPRAVCE` vždy prázdné,
  nabídka dat je přitom nabízí (`:59-60`).
- Vývěska MHD dopravce téže linky skládá (`ZastavkoveJrMhd.cls:1114`, `PopisDopravce` `:1840`).
- Starý knižní tisk řádek dopravce posílá prázdný ([`§18.8`](linkovy-knizni-jr-implementace-analyza.md)),
  doplnění je tedy zlepšení proti němu.
- Poznámku nad linku MHD nemá odkud vzít (`ZastavkoveJrMhd.cls:1123`), ale zkušební šablona na ni
  má prvek (`templates/linkovy-jr-mhd-postskript-a5.json:46`).

## B193b Číslo linky v šablonách MHD

- Vývěska MHD tiskne `{CISLO_LINKY}` = licenční číslo (`SystemTemplates/zastavkovy-jr-mhd.json:234`),
  u CB_DP 325018 místo 18.
- Zkušební linkový MHD tiskne `{LOKALNI_OZNACENI_LINKY}` = veřejné (`templates/linkovy-jr-mhd-postskript-a5.json:86`).
- Výchozí název PDF v okně Tisk je `{CISLO_LINKY}_…` (`PrintDialogViewModel.cs:30`).

## B193c Drobné rozdíly linkového MHD proti vývěsce MHD

- **Typ JŘ** při výluce i návrhu: vývěska „VÝLUKOVÝ" (`ZastavkoveJrMhd.cls:323-324`, výluka se
  nastaví až po návrhu), linkový „NÁVRH" (`LinkovyJrMhd.cls:424-425`). Komentář tvrdí „1:1 zastávkový vzor".
- **Výchozí skupina linek:** linkový čte příznak `Zablokovano` (`LinkovyJrMhd.cls:274`), všude jinde
  `Blocked` (`:212`, `ZastavkoveJrMhd.cls:2011`). Výchozí tak může být zablokovaná skupina, kterou
  nabídka skupin neukazuje. Neověřeno na datech.
- **Výběr linek:** vývěska ukazuje platnost a bez veřejného čísla licenční (`ZastavkoveJrMhd.cls:189-211`),
  linkový platnost ne a bez čísla interní id (`LinkovyJrMhd.cls:245-248`).
- **Popis Směru pro tisk** odkazuje na obousměrný tisk (`LinkovyJrAd.cls:441`), který MHD nemá.
  Zvýraznit zastávku už popis podle agendy mění (`:424`).

## B193d Popisky a skupiny parametrů

- Skupina **„Tabulka linky"** u linkových agend (`LinkovyJrAd.cls:197, 221, 240, 256, 337, 400, 422`).
  Prvek se jmenuje „Tabulka spojů" (`ElementNames.cs:78`) a `ux-conventions §8` tvar „Tabulka linky"
  zakazuje. Zastávkové agendy pojmenovávají skupiny podle prvků (Seznam zastávek, Minuty odjezdů).
- **Tvar názvu zastávky:** jiné pořadí voleb (`ZastavkoveJrAd.cls:3507-3510` × `LinkovyJrAd.cls:316-319`).
  Jiný výchozí tvar je vědomý (`LinkovyJrAd.cls:302-306`).

## B193e Skupiny spojů jedním názvem

- Zastávkový AD „Rozdělení sloupců (typy provozu)" ve skupině Minuty odjezdů (`ZastavkoveJrAd.cls:3626`),
  linkový AD „Dělení spojů dle platnosti" (`LinkovyJrAd.cls:195`), linkový MHD skupina „Typy provozu
  pro tisk" (`LinkovyJrMhd.cls:174`).
- Zamčené volby obou AD se liší: „Pracovní / Sobota / Neděle+svátky" × „Pracovní dny / Soboty /
  Neděle+svátky", „Pracovní / víkend (So+Ne+svátky)" × „Pracovní dny / víkend+svátky". Linkový AD
  nemá „Automaticky (dle linky)". Návrh v [`§18.5`](linkovy-knizni-jr-implementace-analyza.md).

## B193f Popis platnosti a typ JŘ

| Agenda | `POPIS_PLATNOSTI` | Místo |
|---|---|---|
| zastávkový AD | „Platí od X" (bez „do") | `ZastavkoveJrAd.cls:359` |
| linkový AD | „Platí od X do Y" | `LinkovyJrAd.cls:787-789` |
| zastávkový MHD | „X - Y" | `ZastavkoveJrMhd.cls:1115` |
| linkový MHD | „Platí od X do Y" | `LinkovyJrMhd.cls:418-419` |

- Návrh jízdního řádu: AD „návrh jízdního řádu" (`ZastavkoveJrAd.cls:365`, `LinkovyJrAd.cls:791`),
  MHD „NÁVRH" (`ZastavkoveJrMhd.cls:323`, `LinkovyJrMhd.cls:425`).
- Změna u vývěsek pohne vytištěnými vývěskami zákazníků i zlatými odtisky.

## B193g Linka bez dat

- Linkový MHD pošle důvod a radu (`DUVOD_PRAZDNE`, `LinkovyJrMhd.cls:307`).
- Linkový AD vrátí prázdno bez vysvětlení (`LinkovyJrAd.cls:595`, `:756-765`).
- Vývěska MHD u linky bez trasy spadne na chybu (`ZastavkoveJrMhd.cls:303`) - při tisku víc linek
  zastaví i ostatní, proti čemuž je `DUVOD_PRAZDNE` zavedený (`cache-api.md`).
- Vývěska AD stránky bez odjezdů tiše vynechá (`ZastavkoveJrAd.cls:778`).

## B193h Pole napříč agendami

- Linkový AD nemá veřejné označení linky; vývěska AD ho posílá (`ZastavkoveJrAd.cls:334-344`, ze `SeT_MhD`).
- Číslo zastávky pro adresu v QR: vývěsky `{CISLO_ZASTAVKA}` (AD číslo zastávky, MHD „číslo-označník",
  `ZastavkoveJrMhd.cls:1122`), linkové `{AKTUALNI_ZASTAVKA_CZST}`. Popisek „Číslo zastávky" nese ve
  vývěskách skalár `CISLO_ZASTAVKA` a v linkových pole kolekce `CZST`.
