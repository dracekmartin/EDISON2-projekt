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

## B193h Číslo zastávky pro QR

- Číslo zastávky pro adresu v QR: vývěsky `{CISLO_ZASTAVKA}` (AD číslo zastávky, MHD „číslo-označník",
  `ZastavkoveJrMhd.cls:1122`), linkové `{AKTUALNI_ZASTAVKA_CZST}`. Popisek „Číslo zastávky" nese ve
  vývěskách skalár `CISLO_ZASTAVKA` a v linkových pole kolekce `CZST`.

## B193i Název pro MHD

- Vývěska AD: `NAZEV_PRO_MHD` zapnuté = název pro MHD (pole 7 registru) vždy, když ho zastávka má
  (`ZastavkoveJrAd.cls:3276`, použití `:3135`).
- Linkový AD: jen u městské linky (typ MEST/MESP) s nastavením linky `PATCH,nazMHD`
  (`LinkovyJrAd.cls:2430-2448`, použití `ZdrojNazvuZastavky` `:2415`) - parita se starým knižním tiskem.
- Popisy parametru to říkají každý jinak (`ZastavkoveJrAd.cls:3497`, `LinkovyJrAd.cls:307-312`).
  Zda je chování vývěsky shodné se starým vývěskovým modulem, neověřeno.
