# Architektura systému

> Tato sekce popisuje architekturu systému EDISON — Caché server, .NET klient a MLRest komunikaci.

---

## 2.1 Přehled

```
┌─────────────────────────┐          ┌─────────────────────────────┐
│    .NET WinForms 4.8    │          │     Caché (InterSystems)    │
│    (PC uživatele)       │          │     (vzdálený server)       │
│                         │  MLRest  │                             │
│  BaseForm / IBaseForm   │◄────────►│  ^||ML("IN",...)            │
│  EDComponents           │   HTTP   │  ^||ML("OUT",...)           │
│  EDComponents2          │          │  ^[dataDB]ALVA....          │
│  EDFORMS (BIG, BDG)     │          │                             │
│  C1FlexGrid 4.5.2       │          │  Kód: namespace MLINE       │
│                         │          │  Data: namespace dle IdFa    │
└─────────────────────────┘          └─────────────────────────────┘
```

- **Dvouvrstvá architektura**: .NET klient ↔ Caché server
- Veškerá business logika a data na Caché, UI v .NET
- Komunikace přes MLRest (HTTP)

---

## 2.2 Architektura Caché

### 2.2.1 Namespace a data

- Veškerý **kód** (třídy, rutiny, include soubory) běží v namespace **MLINE**
- **Data** jsou v namespace podle `IdFa` (identifikátor firmy, string, např. `"CB_CSADAD"`)
- Získání datového namespace:

```objectscript
d ##class(ALVA.EDISON.Main).GetApplicationInfo(-1,.loadedInfo,IdFa)
s dataDB=loadedInfo(0)
```

- Přístup k datům vždy přes extended reference:

```objectscript
s hodnota = ^[dataDB]ALVA.MHD.MujGlobal(IdFa,"DTA",id,"Nazev")
```

- **Temp globály**: prefix `^CacheTemp`, BEZ namespace (zůstávají v MLINE), pod GUID uživatele
  - GUID je generováno při přihlášení do EDISON
  - Příklad: `^CacheTempMHD2(GUID,"Linky",idLinky,...)`

### 2.2.2 Process-private globály (^||)

Komunikace .NET ↔ Caché probíhá přes process-private globály, které existují jen po dobu volání a nejsou sdílené mezi uživateli.

| Globál              | Směr          | Popis                                    |
|---------------------|---------------|------------------------------------------|
| `^||ML("IN",...)`   | .NET → Caché  | Vstupní data                             |
| `^||ML("OUT",...)`  | Caché → .NET  | Výstupní data                            |
| `^||ML("SYS",...)`  | systém        | Systémové parametry (IdFa, User, GUID…)  |

### 2.2.3 Parametr sys a ^||ML("SYS")

Každá Caché metoda volaná z .NET má signaturu:

```objectscript
ClassMethod NazevMetody(ByRef sys As %String) As %Status
```

Parametr `sys` obsahuje systémové informace o přihlášeném uživateli a spojení. Data jsou také dostupná v `^||ML("SYS",...)`.

**Struktura sys("U",...) — uživatelská data:**

| Klíč          | Proměnná z FillParams | Popis                                        |
|---------------|----------------------|----------------------------------------------|
| `IdFa`        | `IdFa`               | Identifikátor firmy (string, např. `"CB_CSADAD"`) |
| `delimiter`   | `del`                | Oddělovač pro parsování stringů (`$c(0)`)    |
| `SystemDB`    | `systemDB`           | Systémová databáze (`"MLINE"`)               |
| `DATA`        | `dataDB`             | Datová databáze uživatele                    |
| `MLP`         | `userID`             | ID uživatele (číslo)                         |
| `fullName`    | `userName`           | Celé jméno uživatele                         |
| `loginName`   | `loginName`          | Přihlašovací jméno                           |
| `formId`      | —                    | ID aktuálního formuláře                      |
| `Locks`       | —                    | Zda používat zámky (0/1/2)                   |

**Struktura sys("C",...) — data spojení:**

| Klíč            | Popis                                          |
|-----------------|------------------------------------------------|
| `User`          | Systémový uživatel                             |
| `App`           | Název aplikace (`"ALWA.SYS.MLRest"`)           |
| `GUID`          | Unikátní GUID session (generováno při přihlášení) |
| `IP`            | IP adresa klienta                              |
| `EdisonVersion` | Verze EDISON                                   |

### 2.2.4 Standardní makra (GENERAL.inc)

**`$$$Init`** — inicializace metody:
```objectscript
s ret = 0       // návratová hodnota (0 = OK)
s err = ""      // chybová zpráva
```

**`$$$FillParams`** — naplnění proměnných ze sys:
```objectscript
k output
s IdFa = $g(sys("U","IdFa"))           // firma
s del = $g(sys("U","delimiter"))        // oddělovač
s systemDB = $g(sys("U","SystemDB"))    // systémová DB
s systemUser = $g(sys("C","User"))      // systémový uživatel
s dataDB = $g(sys("U","DATA"))          // datová DB
s userID = $g(sys("U","MLP"))           // ID uživatele
s userName = $g(sys("U","fullName"))    // jméno uživatele
s loginName = $g(sys("U","loginName"))  // login
s GUID = $g(sys("C","GUID"))            // session GUID
// + validace povinných parametrů
```

**`$$$GetFiltr`** — načtení filtru z vstupu:
```objectscript
// Parsuje ^||ML("IN",...) kde hodnota začíná "Filter"
// Výstup: filtr(controlName) = value
//         filtr(controlName, value) = ""
// Také načte rowCount z "RowCount" položky
```

**`$$$CatchErrRest`** — standardní catch blok:
```objectscript
// Pro MLRest: nastaví ret a uloží chybu do ^||ML("ERR")
// Pro MLServer: uloží do ^CacheTempMLSrv($j,"OUT",0)
```

**`$$$InitializeMethodEx("ALVAEDFORMS")`** — pro BIG/BDG metody:
- Používá se místo `$$$Init` + `$$$FillParams` v metodách `Init`, `Validate`, `Save`
- Inicializuje BIG/BDG framework

**Kdy použít které makro:**
- `$$$Init` + `$$$FillParams` — pro FLG metody (`InitFg`, `LoadFg`, `Delete`) a obecné metody
- `$$$InitializeMethodEx("ALVAEDFORMS")` — pro BIG/BDG metody (`Init`, `Validate`, `Save`, `Load`)

**Návratová hodnota `ret`:**
- `ret = 0` nebo `ret = $$$OK` — úspěch
- `ret = "1~chybová zpráva"` — chyba
- Na konci metody vždy `ret ret` (vrátí hodnotu `ret`)

Standardní kostra Caché metody:

```objectscript
Include GENERAL

ClassMethod MojeMetoda(ByRef sys As %String) As %Status
{
    $$$Init
    try
    {
        $$$FillParams

        // Vstup
        s vstup = ^||ML("IN",1)

        // Přístup k datům (vždy s extended reference!)
        s hodnota = ^[dataDB]ALVA.MujGlobal(IdFa,"DTA",id)

        // Výstup
        s ^||ML("OUT",1) = "klic"_del_"hodnota"
    }
    catch (ex) { $$$CatchErrRest }
    ret ret
}
```

### 2.2.5 Include soubory

| Include soubor   | Obsah                                                        |
|------------------|--------------------------------------------------------------|
| `GENERAL`        | Základní makra: `$$$Init`, `$$$FillParams`, `$$$CatchErrRest`, `$$$GetFiltr` |
| `ALVAEDFORMS`    | BIG/BDG makra: `$$$BigItemDataType*`, `$$$BigStateType*`, `$$$FormMode*`, `$$$BDGDataType*`, `$$$BDGState*`, `$$$InitializeMethodEx` |
| `ALVAEDREPORTS`  | Kódy číselníků: `$$$CodebookType*`                           |
| `ALVALIBRARY`    | Systémové utility, stack trace (`$$$GetStack`)               |
| `ALWASYSRest`    | Systémová makra: `$$$SYSBinary`, `$$$Idfa`, `$$$Mlp`, `$$$GUID`, `$$$Db` |

---

## 2.3 Architektura .NET

### 2.3.1 Životní cyklus formuláře (IBaseForm)

```
FillParameters(parent, input)
    │
    ▼
GetInput()              → vrací BaseInput, uloží do ThisInput
    │
    ▼
InitializeForm()        → inicializace komponent, sloupců gridu
    │
    ▼
LoadData()              → volá RefreshData (pokud není zakázáno filtrem)
    │
    ▼
RefreshData()           → načtení dat z Caché
    │
    ▼
[uživatel pracuje s formulářem]
    │
    ▼
GetOutput()             → vrací BaseOutput s výsledky (při DialogResult.OK)
```

### 2.3.2 Klíčové knihovny a reference

| Knihovna         | Obsah                                                |
|------------------|------------------------------------------------------|
| `EDComponents`   | EDFlexGrid2 (FLG), EDFilter                          |
| `EDComponents2`  | EDStavovyRadek, EDAkceProbiha, EDMessageBox          |
| `EDFORMS`        | BaseInputGrid (BIG), BaseDataGrid (BDG), BaseItemList (BIL), PackageAdaptor |
| `EDCOREMAIN`     | BaseInput, BaseOutput, BaseItem, BaseEnum             |
| `EDCore`         | EDISFormEnum, EDPackageEnum                           |
| `EDLibrary`      | EDForm, BaseForm (legacy — nepoužívat v nových projektech) |
| `EDREPORTS`      | Codebook API, CodebookTypeEnum                        |
| `GENERAL`        | EDCacheButtonTisk, FormFilterBig, společné funkce     |
| `GENERALAPI`     | **BaseForm2** (pro nové projekty), společné funkce    |
| `MLRest`         | Komunikace s Caché                                    |

### 2.3.3 Vzorový formulář a reference

- **Vzorový projekt pro nové formuláře:** `EDVZOR` (BaseForm2 + package "EDFORMS.GENERAL")
- Legacy vzor: `formMHD2Sablona` v projektu `MHD2Linky` (BaseForm + IBaseForm)
- Kopírujeme vzorový formulář do svého projektu, upravíme namespace a název třídy

---

## 2.4 Komunikace .NET ↔ Caché (MLRest)

### 2.4.1 Obecný princip

```
.NET                              Caché
─────                             ─────
MLRest.Query(třída, metoda, vstup)
    │
    ├─ serializace vstupu
    │       │
    │       ▼  HTTP
    │   ^||ML("IN",...) ← deserializace
    │       │
    │       ▼
    │   Caché metoda zpracuje
    │       │
    │       ▼
    │   ^||ML("OUT",...) → serializace
    │       │
    ▼       ▼  HTTP
výstup ← deserializace
    │
    ▼
MLRest.RestStatus.ThrowIfError()   ← VŽDY volat!
```

### 2.4.2 Formát A: object[] (pole)

Použití: jednoduché dotazy, Delete, jednorázové operace.

**Pravidla:**
- .NET indexy od **0**, Caché od **1**
- Výstup na pozici **0** = chyba (pokud nastala), jinak prázdno
- Oddělovač hodnot: `MLRest.Delimiter` (.NET) = `del` (Caché, z `$$$FillParams`)

```csharp
// .NET — odeslání a čtení
var input = new List<object>();
input.Add("Hodnota 1");
input.Add($"klic{MLRest.Delimiter}hodnota");

object[] output = MLRest.Query(Cache.getCachePackage2(this), "Metoda", input.ToArray());
MLRest.RestStatus.ThrowIfError();

for (int i = 1; i < output.Length; i++)
{
    string[] arr = output[i].ToString().Split(MLRest.Delimiter);
    // arr[0] = klíč, arr[1] = hodnota
}
```

```objectscript
// Caché — čtení a odpověď
ClassMethod Metoda(ByRef sys As %String) As %Status
{
    $$$Init
    try
    {
        $$$FillParams
        s hodnota1 = ^||ML("IN",1)
        s klic = $p(^||ML("IN",2),del,1)

        s ^||ML("OUT",1) = "pozdrav"_del_"cus"
    }
    catch (ex) { $$$CatchErrRest }
    ret ret
}
```

### 2.4.3 Formát B: CVar (strukturovaná proměnná)

Použití: složitější strukturovaná data, parametry.

Mapuje se na `^||ML("IN",key1,key2,...)` v Caché. Podporuje `CList` ($lb() v Caché) a `CHorolog` (datum).

```csharp
// .NET
CVar cVarIn = new CVar();
cVarIn["Lide", "Pepa"] = new CList("Pepa Novak", new CHorolog(new DateTime(1996, 4, 6)), 24500.50);

CVar cVarOut = MLRest.Query(Cache.getCachePackage2(this), "Metoda2", cVarIn);
MLRest.RestStatus.ThrowIfError();

foreach (var kvp in cVarOut["Lide"])
{
    string clovek = kvp.Key;
    CList cList = kvp.Value; // implicitní konverze z CVar na CList
}
```

```objectscript
// Caché — struktura vstupu:
// ^||ML("IN","Lide","Pepa") = $lb("Pepa Novak","64325",24500.5)

ClassMethod Metoda2(ByRef sys As %String) As %Status
{
    $$$Init
    try
    {
        $$$FillParams
        m ^||ML("OUT") = ^||ML("IN")
    }
    catch (ex) { $$$CatchErrRest }
    ret ret
}
```

### 2.4.4 Formát C: Binární data (byte[])

Použití: přenos souborů (dokumenty, obrázky).

**Odeslání .NET → Caché:**
```csharp
byte[] bytes = File.ReadAllBytes(soubor);
string guid = MLRest.BinarySave(bytes);
MLRest.RestStatus.ThrowIfError();

CVar cIn = new CVar();
cIn["souborGuid"] = guid;
MLRest.Query("ALVA.MojeTrida", "MojeMetoda", cIn);
```

```objectscript
// Caché — data v $$$SYSBinary(GUID), DOČASNÉ!
s souborGuid = ^||ML("IN","souborGuid")
m ^||soubor = $$$SYSBinary(souborGuid)
// Uložit do vlastního globálu a uklidit:
k $$$SYSBinary(souborGuid)
```

**Načtení Caché → .NET:**
```objectscript
// Caché — připravit data
s souborGuid = $system.Util.CreateGUID()
k $$$SYSBinary(souborGuid)
m $$$SYSBinary(souborGuid) = ^||soubor
s ^||ML("OUT","souborGuid") = souborGuid
```

```csharp
// .NET — stáhnout
byte[] bytes = MLRest.BinaryLoad(souborGuid);
MLRest.RestStatus.ThrowIfError();
```

### 2.4.5 Lokalizace

- Caché: desetinná čísla **vždy s tečkou**
- .NET: při konverzi číslo ↔ string **vždy** `CultureInfo.InvariantCulture`

### 2.4.6 Adresování Caché třídy z .NET

Caché třídy pro formuláře jsou organizovány do složek podle typu komponenty:

| Složka               | Komponenta | Metody (nové projekty)              | Metody (legacy) | Příklad                                      |
|----------------------|------------|-------------------------------------|-----------------|----------------------------------------------|
| `Forms`              | FLG        | `InitFg`, `LoadFg`, `Delete`        | —               | `ALVA.MHD2.Linky.Forms.formMHD2LinkyMain`    |
| `BigForms`           | BIG        | `LoadItems`, `ValidateItems`, `SaveItems` | `Init`, `Validate`, `Save` | `ALVA.MHD2.Linky.BigForms.formMHD2LinkyObehyEdit` |
| `BilForms`           | BIL        | `LoadItems`                         | —               | `ALVA.MHD2.Linky.BilForms.formMHD2LinkyLinky` |
| `BdgForms`           | BDG        | `Load`, `Validate`, `Save`          | —               | `ALVA.MHD2.Linky.BdgForms.formMHD2LinkySpojeEdit` |
| `BigForms.TEMPLATES` | BIG (PackageAdaptor) | `LoadItems`, `ValidateItems`, `SaveItems` | `Init`, `Validate`, `Save` | `ALVA.MHD2.Linky.BigForms.TEMPLATES.Export`  |
| `BilForms.TEMPLATES` | BIL (PackageAdaptor) | `LoadItems`                 | —               | `ALVA.MHD2.Linky.BilForms.TEMPLATES.PrejezdLinky` |

**Adresování z .NET:**

```csharp
// FLG — automaticky přes getCachePackage2 (vrací [Namespace].Forms.[Formulář])
EDFGrid2.initFG2(this, fg, mode, Name, "Titulek", 0,
    Cache.getCachePackage2(this) + ".InitFg", false);

// BIG — explicitní string (složka BigForms)
args.PackageActionId = "ALVA.MHD2.Linky.BigForms.formMHD2LinkyObehyEdit";

// BIL — explicitní string (složka BilForms)
args.PackageActionId = "ALVA.MHD2.Linky.BilForms.formMHD2LinkyLinky";

// BDG — explicitní string (složka BdgForms)
bdg.ActivateComponent(new BaseDataGridActivationInput {
    CacheClass = "ALVA.MHD2.Linky.BdgForms.formMHD2LinkySpojeEdit", ...
});

// PackageAdaptor BIG — složka BigForms.TEMPLATES
inputBig.GridArgs.PackageActionId = "ALVA.MHD2.Linky.BigForms.TEMPLATES.Export";
```

**Konvence pojmenování:**
- Namespace: `ALVA.[Projekt]` nebo `ALVA.[Oblast].[Projekt]` (např. `ALVA.MHD2.Linky`, `ALVA.ISY.ISYTurnusy`)
- Třída: `form[Projekt][NázevFormulare]` (např. `formMHD2LinkyObehyEdit`)

### 2.4.7 Společný rozbočovač TEMPLATES (pro nové projekty)

**DŮLEŽITÉ:** Nové projekty používají společný rozbočovač `ALVA.EDFORMS.GENERAL.TEMPLATES` místo vlastních TEMPLATES tříd.

**.NET — aktivace BIG/BIL se společným rozbočovačem:**

```csharp
// BIG — Package = "EDFORMS.GENERAL"
var args = new EDFORMS.CONTROLS.BaseInputGrid.ActivateComponentArgs();
args.PackageActionId = "ALVA.MujProjekt.BigForms.formMujProjektEdit";
big.ActivateComponent(ref errState, "EDFORMS.GENERAL", args);
//                                   ^^^^^^^^^^^^^^^^ společný rozbočovač

// BIL — Package = "EDFORMS.GENERAL"
var bilArgs = new EDFORMS.CONTROLS.BaseItemList.ActivateComponentArgs();
bilArgs.PackageActionId = "ALVA.MujProjekt.BilForms.formMujProjektSeznam";
bil.ActivateComponent(ref errState, "EDFORMS.GENERAL", bilArgs);
```

**Caché — cílové metody musí mít tyto názvy:**

| Komponenta | Metoda rozbočovače | Cílová metoda na BigForms/BilForms |
|------------|--------------------|------------------------------------|
| BIG        | `LoadItems`        | `LoadItems`                        |
| BIG        | `ValidateItems`    | `ValidateItems`                    |
| BIG        | `SaveItems`        | `SaveItems`                        |
| BIG        | `LoadTemplate`     | `LoadTemplate`                     |
| BIL        | `LoadItems`        | `LoadItems`                        |

**Jak rozbočovač funguje:**

```
.NET: big.ActivateComponent("EDFORMS.GENERAL", args)
        │
        ▼
ALVA.EDFORMS.CONTROLS.BIG.Base.LoadItems
        │
        │ GetClass("EDFORMS.GENERAL")
        ▼
ALVA.EDFORMS.GENERAL.TEMPLATES.Big.LoadItems    ← společný rozbočovač
        │
        │ $classmethod(actionId, "LoadItems", ...)
        ▼
ALVA.MujProjekt.BigForms.formXxx.LoadItems      ← cílová třída z PackageActionId
```

**Legacy projekty (MHD2, ISY...)** používají vlastní `ALVA.[Projekt].TEMPLATES.Big/Bil` s metodami `Init`, `Save`, `Validate`.

### 2.4.8 Chybové stavy a MLError

Systém pro zachycení a předání chyb mezi Caché a .NET.

**Tok chyby:**
```
Caché (chyba)                          .NET (zpracování)
─────────────                          ─────────────────
try { ... }                            object[] output = MLRest.Query(...)
catch (ex) {                                   │
  $$$CatchErrRest                              ▼
}                                      if (output[0] != "") → chyba
    │                                          │
    ▼                                          ▼
^||ML("ERR") = chyba              RestStatus.State = Error
^||ML("OUT",0) = chyba            RestStatus.Message = output[0]
    │                                          │
    ▼                                          ▼
ret ret (= "1~zpráva")            ThrowIfError() → Exception
```

**Caché — zachycení chyby:**

```objectscript
ClassMethod MojeMetoda(ByRef sys As %String) As %Status
{
    $$$Init          // s ret = 0, err = ""
    try
    {
        $$$FillParams

        // Vlastní chyba (validační)
        i necoJeŠpatně {
            s ret = "1~Popis chyby pro uživatele"
            ret ret
        }

        // ... logika metody ...
    }
    catch (ex) {
        $$$CatchErrRest   // Zachytí výjimku, nastaví ret a ^||ML("ERR")
    }
    ret ret
}
```

**Makro `$$$CatchErrRest` (z GENERAL.inc):**
```objectscript
// Nastaví ret = "1~chybová zpráva"
// Uloží stack trace do ^||ML("ERR")
// Pro MLServer používá ^CacheTempMLSrv($j,"OUT",0)
```

**.NET — kontrola chyby:**

```csharp
// 1. Standardní způsob — ThrowIfError
object[] output = MLRest.Query(cacheClass, "Metoda", input);
MLRest.RestStatus.ThrowIfError();  // Vyhodí Exception pokud je chyba

// 2. Ruční kontrola
if (!string.IsNullOrEmpty(output[0]?.ToString()))
{
    string chyba = output[0].ToString();
    // Zpracovat chybu...
}

// 3. Kontrola přes RestStatus
if (MLRest.RestStatus.State == MLRest.MLRStatus.EState.Error)
{
    string zprava = MLRest.RestStatus.Message;
    EDMessageBox.Show(zprava, "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Error);
}
```

**Třída MLRStatus:**
```csharp
public class MLRStatus
{
    public enum EState { Ok, Error }

    public EState State { get; }
    public string Message { get; }
    public string Title { get; }

    // Vyhodí Exception pokud State == Error
    public void ThrowIfError();

    // Zobrazí chybovou hlášku pokud State == Error
    public void ShowIfError(string title);
}
```

**MLError — zobrazení chyb v .NET:**

```csharp
// Získání stack trace z výjimky
string stackTrace = this.MLError.GetStack(ex);

// Zobrazení chyby uživateli
this.MLError.ShowError("Název operace", chybovaZprava);

// V BaseForm2 — helper metoda
protected bool ShowMLErrorIfNotEmpty(string errState)
{
    if (!string.IsNullOrEmpty(errState))
    {
        this.MLError.ShowError(Text, errState);
        return true;
    }
    return false;
}
```

**Vzor použití v BaseForm2:**

```csharp
protected override void HandleToolStripItemClick(object sender, EventArgs e)
{
    string errState = string.Empty;

    if (sender == tsbAdd) AddItem(ref errState, ThisInput);
    else if (sender == tsbEdit) EditItem(ref errState, ThisInput, SelectedItem);
    else if (sender == tsbDel) DeleteItems(ref errState, ThisInput, SelectedItems);

    ShowMLErrorIfNotEmpty(errState);  // Zobrazí chybu pokud nastala
}

public override void RefreshData(ref string errState, BaseInput input, out BaseStatus status)
{
    status = null;
    try
    {
        EDAkceProbiha.Show();
        EDFGrid2.refresh2(fgMain, edFilter, MLRest,
            Cache.getCachePackage2(this) + ".LoadFg", ThisInput);
    }
    catch (Exception ex)
    {
        errState = this.MLError.GetStack(ex);  // Zachytí celý stack trace
    }
    finally { EDAkceProbiha.Hide(); }
}
```

**Chybové kódy v návratové hodnotě:**
| Formát | Popis |
|--------|-------|
| `0` nebo `$$$OK` | Úspěch |
| `"1~zpráva"` | Chyba s uživatelskou zprávou |
| `$$$ERROR(...)` | Caché %Status chyba |

---

## 2.5 Komunikační vzory podle komponenty

> **DŮLEŽITÉ:** `BaseForm` (EDFORMS) je **legacy**. Všechny nové formuláře musí dědit z `BaseForm2` (GENERALAPI), která poskytuje standardní implementaci životního cyklu, klávesových zkratek, dvojkliku a událostí.

### FLG (EDFlexGrid2 — seznam/grid)

```
.NET                                    Caché
─────                                   ─────
EDFGrid2.initFG2(...)              →    InitFg()      → definice sloupců
EDFGrid2.refresh2(...)             →    LoadFg()      → data řádků (+ filtr)
MLRest.Query(..., "Delete", ids)   →    Delete()      → smazání položek
```

### BIG (BaseInputGrid — editační formulář)

**Nové projekty (Package = `"EDFORMS.GENERAL"`):**
```
.NET                                    Caché
─────                                   ─────
bigMain.ActivateComponent(args)    →    LoadItems()      → položky + hodnoty
  (automaticky po LoadItems)       →    ValidateItems()  → validace
  (automaticky po editaci)         →    ValidateItems()  → validace editované položky
bigMain.SaveComponent()            →    SaveItems()      → uložení dat
```

**Legacy projekty (Package = `"MHD2"`, `"ISY"` apod.):**
```
.NET                                    Caché
─────                                   ─────
bigMain.ActivateComponent(args)    →    Init()        → položky + hodnoty
  (automaticky po Init)            →    Validate()    → validace
bigMain.SaveComponent()            →    Save()        → uložení dat
```

### BIL (BaseItemList — seznam položek s akcemi)

```
.NET                                    Caché
─────                                   ─────
bil.ActivateComponent(args)        →    LoadItems()      → seznam položek
bil.RefreshItems()                 →    LoadItems()      → refresh
  (klik na akci)                   →    RunAction()      → spuštění akce
  (klik na šablonu)                →    LoadTemplate()   → načtení šablony
```

### BDG (BaseDataGrid — editační tabulka)

```
.NET                                    Caché
─────                                   ─────
bdg.ActivateComponent(input)       →    Load()        → sloupce + buňky
  (automaticky po Load)            →    Validate()    → validace
bdg.ValidateComponent(...)         →    Validate()    → manuální validace
bdg.SaveComponent(...)             →    Validate() + Save()
```

### EDStavovyRadek

```
.NET                                    Caché
─────                                   ─────
edStavovyRadek.Update(params)      →    EDStavovyRadekUpdate()
```

### EDCacheButtonTisk

```
.NET                                    Caché
─────                                   ─────
btnTisk.ActivateComponent(...)     →    LoadMenuItems()   → definice menu
  (klik na sestavu)                →    LoadTable()       → data pro tisk
```

### Filtr

```
.NET: EDFGrid2.refresh2 předá filtr automaticky
Caché: $$$GetFiltr → filtr(controlName, value) = ""
```

---

## 2.6 Zámky

Zámky zamezují souběžné práci více uživatelů s jedním objektem. Jsou vázány na GUID (instance EDISON).

```csharp
// Nastavení zámku
EditLock = MLRest.LockSet(new MLRest.Lock(
    new MLRest.Lock.DefObj(MLRest.Idfa, "název zámku", "id objektu"),
    new MLRest.Lock.DefInfo(MLRest), null));
MLRest.RestStatus.ThrowIfError();

// Kontrola
if (EditLock.Count == 0) // 0 = zamčeno někým jiným
{
    EDMessageBox.Show(MLRest.Lock.DefWarning(EditLock), Text,
        MessageBoxButtons.OK, MessageBoxIcon.Information);
    return;
}

// Zrušení zámku
MLRest.LockDel(EditLock);
MLRest.RestStatus.ThrowIfError();
```

Správa zámků:
- *Správa systému / Edison / Uživatelé / Nástroje / Zámky*
- *Horní menu / Nástroje / Správa zámků*
