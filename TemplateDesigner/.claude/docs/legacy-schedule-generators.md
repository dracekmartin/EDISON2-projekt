# Legacy Caché generátory JŘ — reference pro TD migrace

Mapování typů jízdních řádů na **legacy Caché report generátory** + business kontext per typ. Slouží jako reference před implementací nových agend v TD (Fáze C/D/E, PRIORITA 2 — zastávkový JŘ pro linkové JŘ data).

> **Stav 2026-05-29.** Některé položky jsou open questions (?označené) — doptat se Martina Hubaty.

---

## 1) Zastávkové JŘ — `ALVA.TISKMHD`

**Použití:** vylepuje se na zastávky (zastávková vývěska). **V linkové dopravě se moc nepoužívá** — primárně MHD.

**Vlastnosti:**
- Podporuje **město i linkový formát** (dva módy v jednom generátoru)
- **Typy provozu** v linkové variantě se odvozují na základě **značek + negativních značek** (= „typ provozu" — pattern jiný než MHD kde je `TypProvozuID` přímo entitou)
- Používají **všichni 3 zákazníci** (TRANSDEV / KORID / DPKV)

**Stav v TD:** Implementováno jako agenda `ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd` (MHD datový zdroj). **Linková varianta dosud chybí** — PRIORITA 2 v roadmap.

**MHD specifika:** zákazníci dělají i **knižní** zastávkové JŘ, ale „knižní se už moc nevydávají" — nízká priorita pro knižní MHD.

---

## 2) Linkové JŘ (vývěsní a knižní) — `ALVA.TISKJR`

**Použití:** **celá linka** v jednom tisku. Vylepuje se na zastávky v linkové dopravě (vývěsní) a tiskne se v knižních publikacích.

> ❓ **TODO:** doptat se Martina, jestli `ALVA.TISKJR` umí i MHD nebo je čistě linkový.

**Požadavky (business):**
- **Zóny** — aby jich bylo víc (vícezónový tarif)
- **Km** se tisknou, ale **jezdí se podle zón**, ne podle kilometrů (km = informativní sloupec)
- **Tč** (tarifní číslo)
- **Zastávky** (názvy, řazení)
- **Obousměrný** JŘ nebo **jednosměrný** (config)
- **PostScript trik:** vkládá prázdné sloupce, aby výsledek byl **vždy stejně široký** (fixed layout)
- **Tisk podle typu provozu** — analogie MHD typů provozu
- **Návaznosti** — mohou být:
  - Zapsané **dole** (samostatná sekce pod tabulkou), nebo
  - **Přímo v tabulce** (jako řádek/anotace) — „tisk návazností"

**Stav v TD:** zatím **neimplementováno**. Bude potřeba samostatný element / sada elementů (StopList neuumí celou linku v jednom tisku jako vyžaduje vývěsní linkový JŘ).

---

## 3) Relace (Slováci) — `ALVA.TiskJRRELACE`

**Použití:** slovenský zákazník (CTC?). Něco mezi zastávkovým a linkovým.

**Vlastnosti:**
- Podobné zastávkovému, **ale vypadá to trochu jinak**
- Klíčový obsah: **odjezdy ze zastávky a kam všude to jede** (cílové destinace per odjezd)

**Stav v TD:** zatím **neimplementováno**. Pravděpodobně extension zastávkového JŘ nebo samostatný element.

---

## 4) Vozové JŘ (turnusy)

Dva legacy generátory podle typu:
- **MHD:** `ALVA.MHD.ReportGenerator`
- **Linky:** `AD.ADREPORTGEN`

**Použití:** v linkové dopravě se tiskne **v turnusech** (oběhy vozidel — který spoj jede které vozidlo).

**Stav v TD:** zatím **neimplementováno**. Fáze E v roadmap.

---

## Souhrn priority migrace v TD

Per roadmap PRIORITA 2 (po dokončení Wave 4):

1. **Zastávkové JŘ pro linkové JŘ data** — rozšířit `ZastavkoveJrMhd` o linkový datový zdroj (nebo nová agenda). Zachovat dvojí formát „město × linková" stejně jako legacy `ALVA.TISKMHD`.

Fáze C/D/E v roadmap (později, nižší priorita):

2. **Linkové vývěsní/knižní JŘ** — `ALVA.TISKJR` ekvivalent (Fáze C — meziměstský; ale legacy generuje obojí)
3. **Relace** — `ALVA.TiskJRRELACE` ekvivalent (specifický slovenský use case)
4. **Vozové JŘ** — `ALVA.MHD.ReportGenerator` + `AD.ADREPORTGEN` ekvivalent (Fáze E — vozový)

---

## Open questions (doptat se)

- ❓ `ALVA.TISKJR` — funguje i pro MHD nebo jen linkový?
- ❓ Datové zdroje linkového JŘ v Caché (analogie `ALVA.MHD.LSO/CHR/ZST` pro MHD)?
- ❓ Relace — který zákazník přesně, je to live use case nebo legacy?
- ❓ Vozové JŘ — koho přesně to zajímá, časová priorita?
