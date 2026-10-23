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

## B193i Název pro MHD

- Vývěska AD: `NAZEV_PRO_MHD` zapnuté = název pro MHD (pole 7 registru) vždy, když ho zastávka má
  (`ZastavkoveJrAd.cls:3276`, použití `:3135`).
- Linkový AD: jen u městské linky (typ MEST/MESP) s nastavením linky `PATCH,nazMHD`
  (`LinkovyJrAd.cls:2430-2448`, použití `ZdrojNazvuZastavky` `:2415`) - parita se starým knižním tiskem.
- Popisy parametru to říkají každý jinak (`ZastavkoveJrAd.cls:3497`, `LinkovyJrAd.cls:307-312`).
  Zda je chování vývěsky shodné se starým vývěskovým modulem, neověřeno.
