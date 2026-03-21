# Glob: CACHE/**

## MHD zastávkový JŘ — business logika tisku

### Přehled Caché tříd

| Třída | Dědí z | Zodpovědnost |
|---|---|---|
| `AgendaAPI.cls` | — | Abstraktní základ: vynucuje kontrakt 5 metod (GetSchema/GetTableTemplates/GetTableTemplate/GetData/GetMockData) + GetParameters s default implementací + helpery (AddField, AddBoolParam, AddNumberParam, AddMockValue, WriteTemplate2Col) |
| `ZastavkoveJrMhdAPI.cls` | `AgendaAPI` | Kompletní MHD zastávkový JŘ: 6-fázový tiskový engine + všechny helpery (BuildZastavka, BuildOdjezd, BuildSkupinaDnu, BuildPoznamka, AddAnotace, AddAtribut, AddPoznamkaZastavky, AddZona, ConvertSpecialniZnak) + GetSchema/GetData/GetMockData |
| `TemplateDesignerFAKTURY.cls` | `AgendaAPI` | Fakturační agenda (separátní, nesouvisí s JŘ) |

**Architektura:** Žádné mezivrstvy — každá agenda dědí přímo z `AgendaAPI`. Sdílený kód se extrahuje až když je jasné, co konkrétně sdílejí (ne dopředu).

---

### ZastavkoveJrMhdAPI — fáze zpracování

`GetData` zpracovává data v 6 fázích, každá zapisuje do process-private globálu `^||TiskMHD(...)`:

| Fáze | Metoda | Co dělá |
|---|---|---|
| 1 | `PripravitTrasu` | Načte trasu linky (body, zastávky, slučování), naplní `^||TiskMHD("trasa",SmerID,BodID)` |
| 2 | `NacistPoznamkyAAtributyZastavek` | Atributy zastávek + poznámky z trasy → `atributyZast`, `poznamkyZast` |
| 3 | `AutoPoznamkovatPlatnosti` | Auto-poznámkování týdenních platností → `spojTydenni` |
| 4 | `PromitnutSpoje` | Spoje → odjezdy per zastávka × skupina dnů → `odjezdy`, `spojAtributy`, `spojPoznamky`, `spojNZ` |
| 5 | `VypocitatBitmapyObsluhy` | Bitmapy obsluhy, AllStopBitmap, rozdílové trasy → `obsluha`, `obsluhaVse`, `rozdilTrasy` |
| 6 | `SestavitStranky` | Iteruje zastávky × směry, sestaví JSON stránky (ZASTAVKY, ODJEZDY, POZNAMKY) |

---

### Working global `^||TiskMHD` — klíčové uzly

```
("trasa",SmerID,BodID)                                    = $lb(ZastavkaID, CasJizdy, TarifniCislo)
("trasa",SmerID,"slouceni",BodID,SubBodID)                = "" (sloučené body)
("atributyZast",SmerID,BodID,AtributID)                   = $lb(Popis, 2, SpecialniZnak)
("poznamkyZast",SmerID,BodID,Znak)                        = $lb(PopisPoznamky, 0)
("odjezdy",SmerID,BodID,TypProvozuID,HH,MM,SpojID)       = ""
("spojTydenni",SpojID)                                    = bitstring dnů (1-7)
("spojAtributy",SpojID,"arr",1,AtributID,...)             = kopie z GetAtributySpoj
("spojPoznamky",SpojID,"arr",1,PoznamkaID,...)            = kopie z GetPoznamkySpoj
("spojNZ",SpojID)                                         = znak negativní značky
("spojTrasa",SmerID,SpojID)                               = bitmapa celé trasy spoje
("spojTrasa",SmerID,SpojID,BodID)                         = bitmapa trasy spoje Z daného bodu
("obsluha",SmerID,BodID)                                  = bitmapa obsluhovaných zastávek
("obsluhaKonec",SmerID,BodID,SpojID)                      = konečný BodID spoje z daného bodu
("obsluhaVse",SmerID,BodID)                               = AllStopBitmap (1=všechny, 9=jen některé)
("rozdilTrasy",SmerID,BodID,mytyp,DiffBitmap,SpojID)     = "" (existence = spoj patří do skupiny)
("rozdilTrasyPism",SmerID,BodID,mytyp,DiffBitmap)        = písmeno (A/B/C...)
("nzTexty",NZZnak)                                        = text negativní značky
("nzPrirazen",NZZnak)                                     = přiřazený text NZ
("nazevSkupinyDnu",TypProvozuID)                          = název skupiny dnů
```

---

### Systém poznámek — typy a datový tok

#### Anotace u minut (horní index v DepartureGrid)

Generují se ve fázi 6 (`SestavitStranky`) per odjezd:

| # | Typ | Zdroj | Příklad | Typ anotace |
|---|---|---|---|---|
| 1 | Týdenní platnost | `spojTydenni(SpojID)` | Po, So, Ne | `specialni` |
| 2 | Speciální znaky spoje | `spojPoznamky` kde `TypPoznamkyID=1` | ikonové znaky | `specialni` |
| 3 | NZ (negativní značka) | `spojNZ(SpojID)` — auto z bitmap obsluhy | ×, ⊘ | `nz` |
| 4 | Atributy spoje | `spojAtributy` → SpecZnak z ENUMS → `$char()` | F | `specialni` |
| 5 | Rozdíl trasy (alg. 1002) | `rozdilTrasy` bitmapy → písmena | A, B | `text` |
| 6 | Rozdíl trasy (alg. 1001) | volá legacy `MakeOmezTrasaTxMH` | a, b, c | `text` |
| 7 | Textové poznámky spoje | `spojPoznamky` kde `TypPoznamkyID=0` | libovolný text | `text` |

#### Atributy u zastávek (StopListElement — vedle názvu)

| Zdroj | Příklad | Filtr |
|---|---|---|
| `atributyZast(SmerID,BodID,AtributID)` → SpecZnak → `$char()` | x, { | Jen neprojete; aktuální jen ID ≤ 1003 |
| `poznamkyZast(SmerID,BodID,Znak)` | textové znaky | Jen neprojete zastávky |

#### Legenda POZNAMKY (pod mřížkou)

Tři zdroje v tomto pořadí:

1. **Poznámky zastávky** — `poznamkyZast(SmerID,aktualniZastBodID,...)`
2. **Stránkové poznámky** — `strankovePoznamky(znak)` nasbírané z odjezdů (F, A, B, dny...)
3. **Atributy zastávek** — ze všech zobrazených zastávek, deduplikované per SpecZnak

---

### SpecZnak — konverze interního čísla na zobrazovaný znak

`ALVA.MHD.ENUMS(IdFa,"Atribut","DTA",AtributID,"SpecZnak")` obsahuje **ASCII kód** znaku
ze speciálního fontu (ne zobrazovaný znak přímo).

Konverze v `ZastavkoveJrMhdAPI.ConvertSpecialniZnak()`:

```
SpecZnak 1-7   → "Po"/"Út"/"St"/"Čt"/"Pá"/"So"/"Ne" (dny v týdnu)
SpecZnak > 31  → $char(SpecZnak) — ASCII znak (70→F, 120→x, 123→{)
jinak          → beze změny
```

Příklady z číselníku CB_DP:

| AtributID | Popis | SpecZnak | `$char()` | ProSpoj | ProZastavku |
|---|---|---|---|---|---|
| 1003 | na znamení | 68 | D | 0 | 1 |
| 1005 | přestup na železniční dopravu | 120 | x | 0 | 1 |
| 1006 | jízdenkový automat | 123 | { | 0 | 1 |
| 1007 | bezbariérově přístupné | 72 | H | 1 | 1 |
| 1008 | částečně bezbariérově, nutná dopomoc | 70 | F | 1 | 0 |

---

### Algoritmus 1002 — rozdíly tras (A/B/C písmena)

Fáze 5 (`VypocitatBitmapyObsluhy`) pro každou zastávku (BodID) × směr:

1. **ObsluhaBitmap** — sjednocení všech zastávek kam jedou spoje z tohoto bodu
2. **TrasaSpojZBodu** — per-spoj bitmapa (které zastávky tento spoj obsluhuje od BodID, **včetně** konečné)
3. **AllStopBitmap** — 1 = všechny spoje staví, 9 = jen některé
4. **DiffBitmap** (mytyp=0) — kde spoj staví navíc (pozice s AllStopBitmap=9)
5. **EndDiffBitmap** (mytyp=1) — spoj končí dříve než ostatní
6. Písmena A/B/C... se přiřadí sekvenčně přes mytyp × DiffBitmap

Fáze 6 čte `rozdilTrasy` per odjezd a přidá písmeno jako anotaci + text do legendy:
- mytyp=0 → "zastávky obsluhují pouze shodně označené spoje"
- mytyp=1 → "končí v zast. {název}"

**Klíčový detail:** TrasaSpojZBodu musí zahrnovat konečný bod (jako legacy) — bez toho jsou bitmapy prázdné a A/B se nevygenerují.
