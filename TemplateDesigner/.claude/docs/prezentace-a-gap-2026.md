# Prezentace zákazníkům 2026 — shrnutí + gap vs legacy (reference)

> **Trvalé shrnutí.** Prezentace proběhly (statická ~19.6., živá seminář 26.6.2026). Tento dokument
> konsoliduje to, co má hodnotu i po akci: **produktové sdělení** (pro příští prezentace/rozhodování),
> **co se zjistilo** (zpětná vazba), a **gap vs legacy** jako referenci. Přípravné artefakty (osnova
> 60 min, podrobná režie demo, ranní gap-checklist) byly smazány — vznikly pro konkrétní akci a doslova
> se „proklikaly". Historie v gitu.

---

## 1. Produktové sdělení a filozofie (reusable)

**Klíčová věta:** *„Stejná data, stejný výsledný tisk — ale návrh šablony je teď vizuální, okamžitý a
ve vašich rukou."*

**Co prodáváme proti legacy** (starý nástroj = formuláře + čísla, bez náhledu, výsledek až po tisku):
- vizuální WYSIWYG editace místo „naslepo do globálů",
- okamžitý náhled, vše vratné (undo/redo),
- šablona reaguje na data (placeholdery, podmíněná viditelnost, agregace),
- centrální úložiště v databázi per firma (ne roztroušené konfigurace po stanicích),
- nová aplikace = prostor přidávat funkce (u starého nástroje prakticky nešlo).

**⭐ Produktová filozofie „nedávat do zásoby" (důležité pro rozhodování o featurách):**
Záměrně stavíme nástroj **odlehčeně**. Starý nástroj měl tolik voleb, že se v něm uživatelé sami
těžko vyznali. Cíl = **srozumitelnost** → přidávat jen to, co zákazník **reálně používá**, ne co by
*teoreticky* šlo mít. Při sběru požadavků aktivně odrazovat od „požadavků do zásoby". (Promítá se do
`feature-design-principles.md` — klasifikace typu variace 1/2/3 a per-customer divergence.)

**Otázky na zákazníky** (sběr po prezentaci — co reálně potřebují):
1. Která nastavení/přepínače ve starém AD reálně používáte?
2. Bez čeho se neobejdete (must-have)?
3. Máte vzorové JŘ výstupy na ověření shody 1:1?
4. Liší se něco mezi vašimi linkami/provozy, co musíme podchytit?

**Připravené odpovědi na typické dotazy (FAQ):**
- *„Můžeme si šablonu upravit sami?"* → Ano, to je celý smysl — vizuálně + uložení do databáze.
- *„Co se starými šablonami?"* → V1 neimportuje 1:1; nové se navrhnou znovu (rychle, WYSIWYG).
- *„Bude to vypadat stejně?"* → U AD JŘ datově ověřeno 1:1; vizuál je nový engine (SkiaSharp místo GDI+),
  obsah a rozložení sedí, pixelová shoda se negarantuje (a ani neusiluje).
- *„Jak se to k nám dostane?"* → Integrované v EDISONu, tisk stejným enginem jako náhled.
- *„A umí to ještě…?"* → většina nastavení je v demu; co nepoužíváte, schválně nezahlcujeme.

---

## 2. Co se zjistilo (zpětná vazba z prezentací)

**Nové požadavky** zaznamenané z živých prezentací (neanalyzováno, před implementací založit ticket) —
detail viz [`customer-requirements-zastavkovy-jr.md §P`](customer-requirements-zastavkovy-jr.md),
backlog v [`roadmap.md`](roadmap.md):
- **P1** — ruční poznámky do patičky JŘ *(vyřazeno 2026-07-10 — nejasné zadání)*
- **P2** — sloučení více linek se stejným posledním trojčíslím (i různá Tč / více dopravců) do 1 výstupu *(řeší se mimo tuto agendu)*
- **P3** — logická imposice 2×A5 na A4 *(✅ hotovo 2026-07-12, v1 bez brožury/duplexu)*

**Poctivé výhrady prezentované zákazníkům** (NEpodávat jako 100% hotové):
- Helpdesk tickety vyřešené převážně v **AD agendě** (TRANSDEV jezdí linkově); MHD modul může mít
  drobné odlišnosti.
- #7959 (filtr neobsluhovaných) funguje, ale „ověřit na reálných TRANSDEV datech" → „připraveno k ověření".
- #7955 pod-položka „výběr spojů jen jednociferně" = bug v **EDISON Studiu JŘ**, ne v Návrháři.

> Mapování vyřešených helpdesk tiketů → konkrétní featura: [`customer-requirements-zastavkovy-jr.md §D`](customer-requirements-zastavkovy-jr.md)
> + roadmap tabulka A.2.D.

---

## 3. Gap vs legacy → samostatná reference

Kompletní porovnání **legacy TiskMHD vs. nový zastávkový JŘ** (verdikt, element-po-elementu mapovací
matice StopList/DepartureGrid/NotesLegend, zbývající mezery P2–P4, vědomé rozdíly „kde jsme bohatší",
nuance vizuální vs datové 1:1) žije v samostatném dokumentu — **analýza udělaná jednou, nedělat znovu**:

➡️ [`gap-analyza-legacy-vs-novy-jr.md`](gap-analyza-legacy-vs-novy-jr.md)

Aktuální stav otevřených mezer se trackuje v [`roadmap.md`](roadmap.md) (AD-N tabulka). Přehled „co
umíme navíc oproti legacy" pro komunikaci se zákazníky: [`customer-requirements-zastavkovy-jr.md §F`](customer-requirements-zastavkovy-jr.md).
