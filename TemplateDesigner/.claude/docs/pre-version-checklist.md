# Pre-version checklist — úterní verze (cíl 2026-07-14)

> **Účel:** jedno místo pro validaci před vydáním verze — testovací průchod, doladění dokumentace,
> release kroky a příprava komunikace. Odškrtávej `[x]`, nálezy zapisuj do tabulky dole.
> Po vydání: hotový checklist → smazat / přesunout výsledek do changelogu, nálezy do roadmapy.
>
> Automatika už kryje logiku (.NET 340/340, Caché 33 gold + smoke v pre-push). Ruční průchod
> míří na vizuál, UX a integrační cesty, kam testy nevidí.

---

## 1. Testovací průchod

### A. Kritická cesta — musí projít (~30 min)

- [ ] **Zlatý workflow AD:** distribuční šablona zastávkového JŘ (Ctrl+O, M-line sekce) → 2–3 linky
  vč. gold (0365001, 0320144) → živý náhled → Ctrl+P → tisk s reálnými daty do jednoho PDF.
  Zkontrolovat: minuty, poznámky, legenda, **kroužkové denní číslice ⑥**, bezbariér **piktogram**
  (ne „H"), na-znamení „x" jako text, NZ boxy.
- [ ] **Totéž MHD** (CB_DP 1001) — hlavně **`{POPIS_SMER}` teď tiskne konečnou, ne „T"**
  (kontraktní změna 10.7.!). ⚠️ Projít uložené MHD šablony: pokud některá používala
  `{POPIS_SMER}` jako T/Z, přepnout na `{SMER}`.
- [ ] **Ukládání:** změna → Ctrl+S (tichý update) → zavřít → otevřít → obsah sedí.
  Nová šablona → Ctrl+S založí do DB (Uložit jako).
- [ ] **Spuštění z EDISON shellu / MHD2** — startovní agenda se předvolí, MLRest session funguje
  (nejcitlivější integrace; launch kontrakt se neměnil, ale ověřit).

### B. Nové featury tohoto týdne (~45 min)

- [ ] **Imposice:** Ctrl+P → „A4 na šířku" + Auto → 2×A5 na list sekvenčně; „Každá stránka na celý
  list" → 2× táž vývěska; ořezové značky; **nevalidní mřížka (2×2 na A4) → hláška, žádný soubor**;
  Vypnuto = výstup identický s dřívějškem; export z hlavního okna bere stejné nastavení;
  persistence přes restart aplikace.
- [ ] **Dvouosý model agend:** Ctrl+Shift+A — seskupený dialog (nadpisy agend, varianty pod nimi,
  badge „(aktuální)", dvojklik); chip „Agenda: Zastávkový JŘ · Varianta: AD"; titulek
  `[Zastávkový JŘ · AD]`; Ctrl+O — sloupce **Agenda | Varianta** hned za názvem + filtr funguje;
  obecná šablona → „Agenda: Obecná (bez databáze)".
- [ ] **TR3 nástupiště:** v dialogu sloupců StopListu zapnout sloupec „Nástupiště" →
  CB_CSADAD 0320680 ukáže hodnoty (por. 1/8/12/15/18/20), jinde prázdné.
- [ ] **TR4 fixed-then-shrink:** StopList s ručně nastavenou pevnou výškou řádku + malá Height
  elementu → řádky se zmenší (nepřetečou); default auto beze změny.
- [ ] **Řazení legendy dle typu (nový default!):** legenda = značky → atributy → text;
  stará šablona s explicitním „Výchozí (dle dat)" pořadí NEzmění.
- [ ] **Reset stránky:** Ctrl+Shift+P → změna A4→A5 → potvrzovací dialog → čistý reset;
  jen změna okrajů → žádný reset (undoable změna); Storno → šablona netknutá.
- [ ] **Odsazení zastávek (AD):** linka s odsazenými podzastávkami → odsazení v StopListu
  (`ShowStopIndentation`).
- [ ] **TR2 souhrn na znamení:** opt-in param default VYP → beze změny chování; zapnout na lince,
  kde jsou všechny zastávky „x" → jeden agregátní řádek legendy místo × u každé.
  (SK data zatím netriggerují — čeká na potvrzení kódování, netestovat na SKNI.)

### C. Regresní rychlovky po auditních opravách (~20 min)

- [ ] **B1 fingerprint:** u DepartureGridu měnit v panelu „Výška řádku" / „Oddělovač skupin" /
  „Mezera anotace" → náhled se mění **okamžitě** (dřív mrtvé).
- [ ] **B7 async schéma:** otevřít šablonu agendy nepoužité v session → UI nezamrzne,
  chip „načítám schéma…" → doplní se.
- [ ] **B2 auto-scale:** DepartureGrid s pevnou Height menší než obsah → fonty se zmenší,
  varování nesvítí zbytečně.
- [ ] **B5:** F5 během přepínání instancí → žádná stará data v náhledu.
- [ ] **D3:** AI dialog — API klíč tečkovaný (PasswordBox); PrintDialog tlačítko „Zavřít" funguje.
- [ ] **Chybové cesty:** bez VPN/Caché → TD startuje s GENERAL + srozumitelný stav (ne pád);
  chyba parametrů agendy → hláška, ne tichý prázdný seznam (C-c).

### D. Edge / dle času (~15 min)

- [ ] Import JSON staré šablony (starý kód „MHD" / `KNIZNI_JR_*`) → migrace + remap projde.
- [ ] Export split (per linka / per zastávka×směr) + imposice + název souboru z tokenů dohromady.
- [ ] Multi-varianta časů (0365005, `ShowAllRouteVariants`) — dynamické sloupce OK.
- [ ] PNG export (bez imposice — záměrně) + fit-to-page (Ctrl+0), 1:1 (Ctrl+1), Ctrl+B panel.

### E. Publish build — NEVYNECHAT (~15 min)

- [ ] Zavřít běžící TemplateDesigner.exe → `.\publish.ps1` → `C:\Edison\TemplateDesigner.exe`.
- [ ] Ve **publikovaném** .exe: symbolový font Timetable (piktogramy v legendě/mřížce) —
  embedded fonty jsou glob v csproj, publish je jediné reálné ověření single-file bundle.
- [ ] V publikovaném .exe: rychlý smoke — otevřít šablonu z DB, náhled, 1 PDF export s imposicí.
- [ ] Spuštění publikovaného .exe z EDISON shellu (ne standalone) — session kontrakt.

---

## 2. Dokumentace před verzí

- [ ] **Changelog** — projít záznamy od minulé verze; doplnit chybějící, sjednotit formulace
  (uživatelský jazyk u featur, technický u fixů).
- [ ] **Roadmapa čistá** — jen otevřená práce; nic hotového nevisí (poslední kontrola sekce
  Aktivní/Produkční/Zastávkový JŘ).
- [ ] **CLAUDE.md „Co máme"** — nové featury zapsané (imposice ✅, dvouosý model ✅, TR3/TR4,
  řazení legendy, symbolový font); „Známé problémy" aktuální (dnes prázdné — sedí?).
- [ ] **customer-requirements-zastavkovy-jr.md** — stavy tiketů odpovídají realitě
  (TR2 čeká SK ověření, TR3/TR4 hotové → připravit podklad pro odpovědi na helpdesku).
- [ ] **ux-conventions tabulka zkratek** — žádný nový drift.

---

## 3. Release kroky

- [ ] Vše commitnuto + **pushnuto** (pre-push testy prošly).
- [ ] Publish build (sekce 1E) proběhl z finálního commitu.
- [ ] Označit verzi: git tag / poznámka commit hashe do release záznamu (dle firemní konvence).
- [ ] Distribuční šablony: embed→MLINE seed proběhl (zastávková AD výchozí je v MLINE větvi,
  klienti ji uvidí read-only).
- [ ] Krátký záznam „co je ve verzi" (interní) — vzít z changelogu, období od minulé verze.

---

## 4. Komunikace

### Interní poznámka kolegům (⚠️ důležité)

- [ ] **Breaking pro pilotní MHD šablony:** `{POPIS_SMER}` nově = konečná zastávka (jednotné
  AD≡MHD≡Linkový); T/Z je v novém poli `{SMER}`. Kdo má rozpracovanou MHD šablonu s POPIS_SMER
  jako T/Z, musí přepnout pole. AD šablon se netýká.
- [ ] Nové názvosloví: **Agenda** (Zastávkový JŘ / Linkový JŘ / Faktury) × **Varianta** (AD / MHD)
  — viz kolegův podnět; kombinované kódy se nemění.
- [ ] TR2 (souhrn na znamení) čeká na potvrzení SK kódování na-znamení — kolega.

### Draft e-mailu zákazníkům (doplnit adresáty + tón dle zvyklostí)

- [ ] Zkontrolovat/doladit draft níže a odeslat po vydání.

> **Předmět:** Návrhář tiskových sestav — nová verze (červenec 2026)
>
> Dobrý den,
>
> vydali jsme novou verzi návrháře tiskových sestav. Hlavní novinky:
>
> - **Tisk více vývěsek na jeden list (imposice)** — např. dvě A5 vývěsky na jeden list A4,
>   volitelně tatáž vývěska vícekrát pro řezání, včetně ořezových značek. Nastavíte přímo
>   v tiskovém dialogu, na šablonách se nic nemění.
> - **Dopravní symboly** — bezbariérové spoje, denní značky (①–⑦) a další značky se nyní
>   tisknou standardními piktogramy shodnými s dosavadními jízdními řády.
> - **Nástupiště u zastávek** — nový volitelný sloupec seznamu zastávek (přání TRANSDEV).
> - **Přehlednější výběr typu sestavy** — sestavy jsou nově členěny podle agendy
>   (Zastávkový JŘ, Linkový JŘ, Faktury) a varianty dat (AD / MHD).
> - **Konzistentní řazení legendy poznámek** + řada drobných oprav a zrychlení.
>
> Podrobnosti rádi ukážeme; připomínky posílejte přes helpdesk.
>
> S pozdravem, M-line

---

## 5. Nálezy z průchodu

| # | Kde (bod checklistu) | Popis | Závažnost | Rozhodnutí (fix před verzí / po verzi / won't-fix) |
|---|---|---|---|---|
| 1 | | | | |
| 2 | | | | |
| 3 | | | | |
- Rozumne vychozi nastaveni pro snapovani a mrizku s hlediska UX
- Prehodit QR kdo a dynamickou tabulku v nabidce elementu
- Kdyz zoomuji sipka + kolecko na prave strane stranky, chtel bych se zoomovat tam, ted zustane scrollbar vzdycky vlevo a musim se rucne presnunou
- Vizualni upozorneni / snap pokud budu tahat obdelnik prave kdyz se z nej stane na mrizce ctverec, at nemusi pocitat rucne
- Snapovani na mrizku obcas neni presne - obcas to je trochu dole nebo nad mrizkou - proc to je? - asi kdyz mam vychozi vysku elementu, tak ta se po pridani na canvas nejak nesnapne, takze to musim upravit rucni - co s tim z hledsska UX?
- Pri uzych uzych zastavkach se sipka smeru vytiskne vpravo spatne?
- Projit napovedu a jestli jim pujde pomocnik
- Ve formulari tisku pridat moznost (pouze pokud je otevreno primo z TD), aby se hodnoty paraemtru nacetly podle aktualne otevrene sablony
- Mam A5 sablonu na sirku, jak to nyni vytisknu, aby jedna zastavka byla jedna stranka v pdf? Imposize to ted dava 2 na jednu a4 na vysku
-
