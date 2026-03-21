# Glob: CACHE/**

## Caché databáze — mentální model pro AI

Caché ukládá data do hierarchických globálů (stromových struktur). Každý globál je multi-tenant — první subscript je vždy `IdFa` (identifikátor firmy/dopravce, např. `"CB_DP"`).

Přístup k datům v jiném namespace: `^[dataDB]Global(...)` kde `dataDB` je cílový namespace.

### Konvence subscriptů

```
^[dataDB]ALVA.MHD.<TABULKA>(IdFa, <TypZáznamu>, "DTA", <ID>, "DTA", 1, "<PropertyName>")
                                   │                   │              │
                                   │                   │              └── vždy 1 (verze záznamu)
                                   │                   └── ID konkrétního záznamu
                                   └── typ záznamu (1=linka, 2=spoj, 4=trasa, ...)
```

- `"DTA"` = datový uzel (obsahuje vlastnosti záznamu)
- `"IDX"` = indexový uzel (sekundární přístup k záznamům)
- `"Val"` = vnořené hodnoty (sub-properties)
- `"Itm"` = hodnota položky v poli (array item)

---

## Persistentní globály

### ALVA.MHD.LSO — Linky, Spoje, Organizace trasy

Hlavní storage pro MHD data. Obsahuje linky (typ 1), spoje (typ 2) a trasy (typ 4).

#### Linka (typ záznamu = 1)

```
^[dataDB]ALVA.MHD.LSO(IdFa, 1, "DTA", LinkaID, "DTA", 1, <property>)
```

| Property | Typ | Popis |
|---|---|---|
| `PlatiOd` | datum | Platnost linky od (formát YYYY-MM-DD) |
| `PlatiDo` | datum | Platnost linky do |
| `VariantaChronometrazeID` | ID | Odkaz na variantu chronometráže (doba jízdy mezi zastávkami) |
| `CisloLinkyDleLicence` | string | Číslo linky podle licence (interní) |
| `CisloVerejne` | string | Veřejné číslo linky (co vidí cestující) |
| `NazevLinky` | string | Název linky (např. "Praha–Brno") |
| `QRCode` | string | QR kód pro JŘ |
| `Navrh` | bool | 1 = linka je návrh (zobrazí se "NÁVRH" na JŘ) |
| `Vyluka` | bool | 1 = výlukový JŘ (zobrazí se "VÝLUKOVÝ") |
| `AutomatickyPoznamkovatRozdilyVTraseSpoju` | bool | Zapnout auto-poznámkování rozdílů v trase |
| `AutomatickyPoznamkovatRozdilyVTraseSpojuAlgoritmus` | int | Algoritmus: 1001 (textový) nebo 1002 (písmena A/B/C) |
| `AutomatickyPoznamkovatRozdilyVTraseSpojuPismena` | string | Písmena pro alg. 1001/1002 (default "abcdefghijklmnopqrstuvwxyz") |
| `AutomatickyPoznamkovatOdlisnostiVPlatnostechSpoju` | bool | Zapnout auto-poznámkování odlišností v platnostech |

#### Spoj (typ záznamu = 2)

```
^[dataDB]ALVA.MHD.LSO(IdFa, 2, "DTA", SpojID, "DTA", 1, <property>)
```

| Property | Typ | Popis |
|---|---|---|
| `SmerID` | int | Směr: 0 = tam, 1 = zpět |
| `TypProvozuID` | ID | Skupina dnů provozu (pracovní den, sobota, neděle...) |
| `ProvozBitMapa` | bitstring | Bitmapa platnosti — které dny spoj jede |
| `NegativniZnacka` | ID | Přiřazená negativní značka (NZ) — omezení trasy |
| `RucniZadani` | bool | 1 = ručně zadaný spoj (ne z optimalizace) |

**Index spojů per linka:**
```
^[dataDB]ALVA.MHD.LSO(IdFa, 2, "IDX", 1, 1, 1, LinkaID, SpojID)
```

#### Trasa / Model trasy (typ záznamu = 4)

```
^[dataDB]ALVA.MHD.LSO(IdFa, 4, "DTA", TrasaID, "DTA", 1, "ModelTrasaBodDetail", BodID, ...)
```

Trasa je sekvence bodů (BodID). Každý bod odpovídá jedné zastávce na trase. Iterace přes body: `$o(^...(..."ModelTrasaBodDetail", BodID))`.

| Subscript za BodID | Popis |
|---|---|
| `"Val","ZastavkaTamID"` | ID zastávky ve směru tam |
| `"Val","ZastavkaZpetID"` | ID zastávky ve směru zpět |
| `"Val","JizdniDobaTam"` | Ruční doba jízdy směr tam (minuty) |
| `"Val","JizdniDobaz5"` | Ruční doba jízdy směr zpět (minuty) |
| `"Val","PoznamkyTam",N` / `"Val","PoznamkyZpet",N` | Poznámky per směr (N = index) |
| `"Val","PoznamkyTam",N,"Itm"` | → PoznamkaSpojID (odkaz do ENUMS) |
| `"Val","AtributyTam",N` / `"Val","AtributyZpet",N` | Atributy per směr |
| `"Val","AtributyTam",N,"Itm"` | → AtributID (odkaz do ENUMS) |

---

### ALVA.MHD.CHR — Chronometráže (doby jízdy)

Doba jízdy mezi dvěma zastávkami. Verze platnosti se vybírá podle data.

```
// Index: najdi ChronometrazID pro dvojici zastávek
^[dataDB]ALVA.MHD.CHR(IdFa, 2, "IDX", 1, 1, 1, PlatnostChronometrazeID, PredchoziZastavkaID, ZastavkaID)

// Data: doba jízdy v minutách
^[dataDB]ALVA.MHD.CHR(IdFa, 2, "DTA", ChronometrazID, "DTA", 1, "ZakladniDobaJizdy")
```

---

### ALVA.MHD.ZST — Zastávky

```
^[dataDB]ALVA.MHD.ZST(IdFa, 1, "DTA", ZastavkaID, "DTA", 1, <property>)
```

| Property | Typ | Popis |
|---|---|---|
| `KategorieZastavekID` | ID | Kategorie: 1001 = reálná zastávka (filtruje se — jiné kategorie se přeskakují) |
| `Cislo` | string | Číslo zastávky (tarifní číslo) |
| `Oznacnik` | string | Označník zastávky (symbol/kód) |
| `ZonyID`,N,`"Itm"` | ID | Zóny přiřazené zastávce (pole ZonaID) |
| `Atributy`,N,`"Itm"` | ID | Atributy zastávky (pole AtributID → ENUMS) |

---

### ALVA.MHD.ENUMS — Číselníky (enumeration tables)

Centrální storage pro všechny číselníky MHD modulu. Klíčem je typ číselníku.

```
^[dataDB]ALVA.MHD.ENUMS(IdFa, <TypCiselniku>, "DTA", <ItemID>, <property>)
```

#### Atribut — vlastnosti zastávek a spojů

```
^[dataDB]ALVA.MHD.ENUMS(IdFa, "Atribut", "DTA", AtributID, <property>)
```

| Property | Popis | Příklad |
|---|---|---|
| `Popis` | Textový popis atributu | "na znamení", "bezbariérově přístupné" |
| `SpecZnak` | ASCII kód znaku ze speciálního fontu | 68→D, 72→H, 120→x, 123→{ |

**Konverze SpecZnak → zobrazení** (metoda `ConvertSpecialniZnak`):
- 1–7 → "Po","Út","St","Čt","Pá","So","Ne" (dny v týdnu)
- \>31 → `$char(SpecZnak)` (ASCII znak)

Známé AtributID (z CB_DP):

| AtributID | Popis | SpecZnak → znak | ProSpoj | ProZastavku |
|---|---|---|---|---|
| 1003 | na znamení | 68 → D | 0 | 1 |
| 1005 | přestup na železnici | 120 → x | 0 | 1 |
| 1006 | jízdenkový automat | 123 → { | 0 | 1 |
| 1007 | bezbariérově přístupné | 72 → H | 1 | 1 |
| 1008 | částečně bezbariérové | 70 → F | 1 | 0 |

#### PoznamkaSpoj — poznámky přiřaditelné ke spojům

```
^[dataDB]ALVA.MHD.ENUMS(IdFa, "PoznamkaSpoj", "DTA", PoznamkaSpojID, <property>)
```

| Property | Popis |
|---|---|
| `TiskJRMHDZastavkove` | bool: tisknout v zastávkovém JŘ? |
| `TiskJRMHDZastavkovePouzeZnacku` | bool: tisknout jen značku (bez popisu)? |
| `TypPoznamkyID` | 0 = text, 1 = speciální symbol |
| `Text` | Zobrazovaný znak/symbol |
| `PopisPoznamky` | Popis pro legendu |

#### TypyProvozu — skupiny dnů (pracovní den, sobota, neděle...)

```
^[dataDB]ALVA.MHD.ENUMS(IdFa, "TypyProvozu", "DTA", TypProvozuID, <property>)
```

| Property | Popis |
|---|---|
| `NadpisSMOPrvni` | První řádek nadpisu skupiny dnů (např. "Pracovní") |
| `NadpisSMODruhy` | Druhý řádek nadpisu (např. "den") |
| `Poradi` | Pořadí zobrazení (1, 2, 3...) |
| `NegativniZnacka` | NZ přiřazená tomuto typu provozu (pro kontrolu kolizí) |

#### Zony — tarifní zóny

```
^[dataDB]ALVA.MHD.ENUMS(IdFa, "Zony", "DTA", ZonaID, "Cislo") → číslo zóny
```

---

### ALVA.MHD.SETTINGS — Globální nastavení MHD modulu

```
^[dataDB]ALVA.MHD.SETTINGS(IdFa, "Obecne", "DTA", 1001, <property>)
```

| Property | Popis |
|---|---|
| `RozsahNegativnichZnacekOd` | První dostupný znak pro NZ (negativní značky) |
| `RozsahNegativnichZnacekDo` | Poslední dostupný znak pro NZ |

---

## Helper třídy (##class volání)

Caché helper třídy zapouzdřují složitější operace nad daty.

| Třída | Metoda | Co dělá | Vstup → Výstup |
|---|---|---|---|
| `ALVA.MHD.LinkyFce` | `GetTrasaId` | Vrátí ID trasy pro linku | (IdFa, dataDB, LinkaID) → TrasaID |
| `ALVA.MHD.ChronometrazeFce` | `GetPlatnostChronometrazeID` | Vrátí ID platnosti chronometráže | (IdFa, dataDB, VariantaChronometrazeID, platnostOdHrl) → ID |
| `ALVA.MHD.KalendarFce` | `GetJede` | Vypočítá bitmapu dnů provozu | (IdFa, dataDB, od, do, TypProvozuID, ...) → .arr |
| `ALVA.MHD.KalendarFce` | `MakeRozpisTplProTypProvozuID` | Vygeneruje textový rozpis týdenní platnosti | (IdFa, dataDB, TypProvozuID) → .arr |
| `ALVA.MHD.KalendarFce` | `MakeOmezeniTXT` | Vygeneruje text omezení platnosti (pro NZ legendu) | (.arri) → .arr |
| `ALVA.MHD.SpojeFce` | `MakeTableSpoj` | Rozloží spoj na tabulku odjezdů per zastávka | (.sys, SpojID) → .tabulkaSpoje |
| `ALVA.MHD.SpojeFce` | `GetPoznamkySpoj` | Vrátí poznámky spoje | (.sys, SpojID) → .myarr |
| `ALVA.MHD.SpojeFce` | `GetAtributySpoj` | Vrátí atributy spoje | (.sys, SpojID) → .myarr |
| `ALVA.MHD.ZastavkyFce` | `GetNazevHrl` | Vrátí název zastávky (v aktuálním jazyce) | (IdFa, dataDB, ZastavkaID, platnostOd) → $lb(nazev,...) |
| `ALVA.MHD.LinkyPrintFormatMHD` | `MakeOmezTrasaTxMH29052025` | Vygeneruje text o rozdílu trasy (alg. 1001) | (IdFa, dataDB, .arri) → .arro |

---

## Business koncepty

### Linka → Trasa → Body → Zastávky

Linka má jednu trasu (`TrasaID`). Trasa je sekvence bodů (`BodID`). Každý bod odkazuje na dvě zastávky — jednu pro směr tam (`ZastavkaTamID`) a jednu pro směr zpět (`ZastavkaZpetID`). Bod může mít `KategorieZastavekID '= 1001` — to jsou technické body (ne reálné zastávky), které se ve výstupu přeskakují.

### Spoj

Konkrétní jízda vozidla po trase. Má směr (`SmerID`: 0=tam, 1=zpět), skupinu dnů (`TypProvozuID`), bitmapu platnosti, a odjezdové časy per zastávka (získané přes `MakeTableSpoj`).

### TypProvozuID (dříve SMOID)

Skupina dnů provozu — pracovní den, sobota, neděle, prázdniny... Každý typ má nadpis (např. "Pracovní den"), pořadí zobrazení, a bitmapu kalendáře. V DepartureGrid tvoří sloupce.

### Negativní značky (NZ)

Systém pro označení spojů s omezenou trasou. Když spoj nestaví na všech zastávkách, dostane NZ symbol (×, ⊘, ...). Symbol se zobrazí jako horní index u minuty v DepartureGrid, a jeho význam je v legendě POZNAMKY.

### Chronometráž

Doba jízdy mezi dvěma po sobě jdoucími zastávkami. Verzovaná — platnost se vybírá podle data. Kumulativní čas od počáteční zastávky se počítá sčítáním.
