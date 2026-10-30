# Glob: dotnet/**

## Architektonická pravidla (R1-R6) - závazná pro veškerou novou práci

Tato pravidla jsou závazná pro **veškerou novou práci** (vznikla pro Caché migraci, platí dál). Jsou
záměrně obecnější než code style - chrání proti drahým rework cyklům pozdě v projektu.

| # | Pravidlo | Proč |
|---|---|---|
| R1 | **Async Caché volání pro očekávaně pomalé operace** - pro Caché volání kde očekávaná doba >500ms (velké listy, složité queries, batch operace, nahrávání) vždy `async Task` + `await Task.Run(() => mlRest.Query(...))`. Pro typické fast operations (foundational komponenty s 50–300ms loady jako EDList, jednoduché lookupy) sync OK - async by přinesl spíš flicker než benefit. Při neznámé velikosti pesimisticky async. | Legacy BIL/BDG bylo 20 let synchronní a fungovalo - důkaz že je to akceptovatelné pro typické workflow. Naopak sync pro 1s+ operaci zamrzne UI a Windows zobrazí „Not Responding". Pravidlo chrání proti druhému; první je validní výjimka. |
| R2 | **Chyby přes `IPopupDisplayer` z EDControls** - žádný `MessageBox.Show` ani silent catch. Používat existující `EDControls.EDViewModel.IPopupDisplayer` (metody `Error(ex)`, `Confirm`, `Info`, `ConfirmClose`) přes DI nebo `EDViewModel` base. | Bez jednotné cesty se chyby v distribuci ztratí. `IPopupDisplayer` je firemní standard - už ho používá `EDViewModel.Command(...)` automaticky. |
| R3 | **CVar named IN + 4 OUT styly podle obsahu** (wire formát Caché ↔ .NET). | Plný standard, decision tree a příklady žijí v [`cache-api.md §Wire formát pravidlo (R3)`](cache-api.md) + [`EDControls/docs/wire-formaty.md`](../../../EDControls/docs/wire-formaty.md). Tady jen pro úplnost číslování. |
| R4 | **ViewModely bez WPF referencí** - žádný `using System.Windows` ani `MessageBox.Show` v `*ViewModel.cs`. Dialogy přes `IPopupDisplayer` (R2). | MVVM princip, testovatelnost, možnost pozdějšího portu/unit testů. |
| R5 | **Template versioning je prerekvizita persistence** - `TemplateMigrator` + `version` field v JSON musí existovat, než se ukládá do databáze. | Bez migrace se nové šablony nespustí na starším buildu → zničené otevření souboru, nelze retro-fit. **Stav: framework hotový** (5/2026); od 2026-06-30 je DB primární úložiště, prerekvizita splněna. |
| R6 | **Paritu se starým modulem doloží porovnání s NÍM, ne náš vlastní odtisk.** Když reimplementujeme chování legacy (tisk, výpočet, řazení, texty), platí: (1) přečti legacy zdroj a v komentáři u kódu uveď `soubor:řádek`, (2) paritu ověř **oraclem** - spusť legacy a porovnej výstup, nebo alespoň hodnotu po hodnotě proti odtisku ze starého modulu, (3) čeho se oracle netýká, **napiš rovnou jako neověřené** - ne „1:1 ověřeno". Dělá se to **na začátku**, ne až když si zákazník stěžuje. | Zlatý odtisk je hash **našeho** výstupu: hlídá regresi proti včerejšku, ale odchylku od legacy nezachytí ani v principu. U variant jízdních dob jsme se lišili 20 dní (srovnával se celý směr místo úseku od vývěskové zastávky, #12843) a gold testy byly celou dobu zelené - dokonce i pod mutací, která tu chybu vrátila. Dokumentace přitom tvrdila „1:1 ověřeno", protože to platilo pro jinou část featury. |

**Předpoklad:** aplikace běží nad Caché - není cíl fungovat offline, ne každá API potřebuje in-memory fallback. Fallback se řeší jen tam, kde to má konkrétní business smysl (např. poslední otevřené soubory v paměti během session).

**Kde jsou pravidla ověřitelná:**
- R1: review PR → `grep "MLRest\." *ViewModel*.cs` najde synchronní volání. U každého ověřit očekávanou dobu - fast operace OK, slow musí být async.
- R2/R4: `grep "MessageBox\." **/*ViewModel.cs` musí vracet 0 hitů.
- R3: nové API metody: IN `$g(^||ML("IN","klic"))`, OUT podle obsahu (decision tree) - viz `cache-api.md` + `EDControls/docs/wire-formaty.md`.
- R5: před produkčním nasazením s reálnými šablonami musí být migrator schopný zpracovat každou breaking change.
- R6: grep po `1:1` a `paritn` v docs a komentářích - u každého tvrzení musí být vidět, **čím** je doloženo
  (oracle test / porovnaný odtisk), jinak je to dohad. Zlatý digest jako doklad parity neplatí.

---

## 🚫 „Must not happen" scénáře

Konkrétní situace, které integrace s Caché musí předejít. Kontrolní seznam při code review.

1. **Synchronní Caché volání v UI thread** → zamrznutí aplikace. Kontrola: `grep "MLRest\." *ViewModel*.cs` hledá synchronní volání (R1).
2. **Silent fail při Caché chybě** → uživatel neví proč se data nenačetla. Kontrola: chyba jde přes `IPopupDisplayer.Error(ex)` (R2).
3. **Breaking JSON change bez migrátoru** → staré šablony nejdou otevřít. Kontrola: každá breaking změna JSON formátu musí mít migraci v `TemplateMigrator` (R5).
4. **Všechno naráz** (big bang migration) → nelze vrátit. Kontrola: per-element migrace, paralelní běh starého a nového systému (vzor: PropertyDescriptor + EDInputForm paralelně).
5. **„Ověřeno 1:1" doložené jen naším odtiskem** → tiše se lišíme od legacy, dokud si nestěžuje zákazník;
   navíc tomu tvrzení pak nikdo nevěří ani tam, kde platí. Kontrola: u parity musí existovat oracle nebo
   ruční porovnání se starým modulem, a rozsah tvrzení musí odpovídat tomu, co oracle skutečně kryje (R6).
6. **Test závislý na stroji, kde běží** → padá kolegům na věcech, které nezavinili. Test smí záviset
   jen na obsahu repa a na tom, co si sám v běhu vytvoří **pod unikátním jménem** (`Guid.NewGuid()`);
   cokoli lokálního (sejmutá baseline, soubor v `%TEMP%` se stálým jménem, uživatelský profil) musí
   být **vypnuté defaultně** a zapínat se proměnnou prostředí. Autor to sám nepozná - `pre-push` běží
   na jeho stroji, kde artefakt existuje a je svěží, a CI, které by sadu pustilo na čistém stroji,
   nemáme. Kontrola: `MachineLocalStateGuardTests` (projede zdrojáky všech `*.Tests` projektů;
   vědomé výjimky u sebe nesou důvod).
7. **Caché test mlčí, když mu z dat zmizí linka** → přeskočí ji jako chybějící firmu a dál hlásí úspěch,
   ačkoli nic nekontroluje (30. 10. dvě linky CB_GWBUS, 268 testů zelených). Přeskočit smí jen firma,
   která na serveru není. Kontrola: `TestovaciLinkyTest` hledá linky ve zdrojácích testů v tvarech
   z `Tvary` - nový tvar zápisu linky tam přidej.
8. **Test bez dat hlásí úspěch** → `$$$AssertTrue(1, "… přeskočeno")` vypadá v souhrnu jako ověřený test.
   Do 30. 10. takhle psalo 99 míst a pět testů na dev serveru léta neověřovalo nic, protože hledaly linku
   v testovacím namespace, kde linky nejsou; dvě jejich očekávání mezitím zastarala. Bez dat
   `$$$AssertSkipped("důvod")`, informace k testu, který něco ověřil, `do ..LogMessage(…)`; linku AD bere
   test ze sady (`ZastavkoveJrAdSadaTest.LinkyNaServeru`). Kontrola: `PreskoceniTestuCacheTests`.
