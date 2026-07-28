# Glob: dotnet/**

## Architektonická pravidla (R1-R5) - závazná pro veškerou novou práci

Tato pravidla jsou závazná pro **veškerou novou práci** (vznikla pro Caché migraci, platí dál). Jsou
záměrně obecnější než code style - chrání proti drahým rework cyklům pozdě v projektu.

| # | Pravidlo | Proč |
|---|---|---|
| R1 | **Async Caché volání pro očekávaně pomalé operace** - pro Caché volání kde očekávaná doba >500ms (velké listy, složité queries, batch operace, nahrávání) vždy `async Task` + `await Task.Run(() => mlRest.Query(...))`. Pro typické fast operations (foundational komponenty s 50–300ms loady jako EDList, jednoduché lookupy) sync OK - async by přinesl spíš flicker než benefit. Při neznámé velikosti pesimisticky async. | Legacy BIL/BDG bylo 20 let synchronní a fungovalo - důkaz že je to akceptovatelné pro typické workflow. Naopak sync pro 1s+ operaci zamrzne UI a Windows zobrazí „Not Responding". Pravidlo chrání proti druhému; první je validní výjimka. |
| R2 | **Chyby přes `IPopupDisplayer` z EDControls** - žádný `MessageBox.Show` ani silent catch. Používat existující `EDControls.EDViewModel.IPopupDisplayer` (metody `Error(ex)`, `Confirm`, `Info`, `ConfirmClose`) přes DI nebo `EDViewModel` base. | Bez jednotné cesty se chyby v distribuci ztratí. `IPopupDisplayer` je firemní standard - už ho používá `EDViewModel.Command(...)` automaticky. |
| R3 | **CVar named IN + 4 OUT styly podle obsahu** (wire formát Caché ↔ .NET). | Plný standard, decision tree a příklady žijí v [`cache-api.md §Wire formát pravidlo (R3)`](cache-api.md) + [`EDControls/DOCS/01-wire-formats.md`](../../../EDControls/DOCS/01-wire-formats.md). Tady jen pro úplnost číslování. |
| R4 | **ViewModely bez WPF referencí** - žádný `using System.Windows` ani `MessageBox.Show` v `*ViewModel.cs`. Dialogy přes `IPopupDisplayer` (R2). | MVVM princip, testovatelnost, možnost pozdějšího portu/unit testů. |
| R5 | **Template versioning je prerekvizita persistence** - `TemplateMigrator` + `version` field v JSON musí existovat, než se ukládá do databáze. | Bez migrace se nové šablony nespustí na starším buildu → zničené otevření souboru, nelze retro-fit. **Stav: framework hotový** (Wave 3 W3-4); od 2026-06-30 je DB primární úložiště, prerekvizita splněna. |

**Předpoklad:** aplikace běží nad Caché - není cíl fungovat offline, ne každá API potřebuje in-memory fallback. Fallback se řeší jen tam, kde to má konkrétní business smysl (např. poslední otevřené soubory v paměti během session).

**Kde jsou pravidla ověřitelná:**
- R1: review PR → `grep "MLRest\." *ViewModel*.cs` najde synchronní volání. U každého ověřit očekávanou dobu - fast operace OK, slow musí být async.
- R2/R4: `grep "MessageBox\." **/*ViewModel.cs` musí vracet 0 hitů.
- R3: nové API metody: IN `$g(^||ML("IN","klic"))`, OUT podle obsahu (decision tree) - viz `cache-api.md` + `EDControls/DOCS/01-wire-formats.md`.
- R5: před produkčním nasazením s reálnými šablonami musí být migrator schopný zpracovat každou breaking change.

---

## 🚫 „Must not happen" scénáře

Konkrétní situace, které integrace s Caché musí předejít. Kontrolní seznam při code review.

1. **Synchronní Caché volání v UI thread** → zamrznutí aplikace. Kontrola: `grep "MLRest\." *ViewModel*.cs` hledá synchronní volání (R1).
2. **Silent fail při Caché chybě** → uživatel neví proč se data nenačetla. Kontrola: chyba jde přes `IPopupDisplayer.Error(ex)` (R2).
3. **Breaking JSON change bez migrátoru** → staré šablony nejdou otevřít. Kontrola: každá breaking změna JSON formátu musí mít migraci v `TemplateMigrator` (R5).
4. **Všechno naráz** (big bang migration) → nelze vrátit. Kontrola: per-element migrace, paralelní běh starého a nového systému (vzor: PropertyDescriptor + EDInputForm paralelně, Wave 4).
