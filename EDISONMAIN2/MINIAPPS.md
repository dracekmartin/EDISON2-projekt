# Miniaplikace — architektonický návrh

> Stav: **návrh** (nic z toho zatím není implementováno). Tento dokument je živý —
> rozhodnutí označená `[OTEVŘENO]` čekají na doladění, `[ROZHODNUTO]` jsou potvrzená.

Mini-app = malá WPF view, kterou agenda vystaví do shellu. Jde ji **pozicovat v shellu**
(dockovat) i **vyjmout z něj** do plovoucího okna, ale pořád **pod správou shellu**.
Mini-apps mají k dispozici stejné vymoženosti jako normální okna (proto dědí z `EDViewModel`).

---

## 1. Klíčová rozhodnutí

| Téma | Volba | Stav |
| --- | --- | --- |
| Layout / docking | **AvalonDock** (full docking framework) | `[ROZHODNUTO]` |
| MHD PoC + nepředělané agendy | **Nová WPF mini-app nad daty** (žádné WinForms hostování) | `[ROZHODNUTO]` |
| Discovery / registrace | **Compile-time registry** (shell referencuje API projekty agend) | `[ROZHODNUTO]` |
| Párování View ↔ VM | **DataTemplate** (+ lightweight `SimpleMiniAppViewModel` pro obsahové panely) | `[ROZHODNUTO]` |
| Hosting v shellu | **Strom vlevo fixní, dock střed; nástěnka → mini-app, „Přehled" zahozen** | `[ROZHODNUTO]` |
| Persistence layoutu | **Zatím lokálně** (`LocalPrefs`, per IDFa) | `[ROZHODNUTO]` |
| Licenční gating mini-apps | **Prozatím podle licence rodičovské agendy/modulu** | `[ROZHODNUTO]` |
| Instance model | **Multi-instance** (víc panelů téže mini-app, např. tabulka s různým filtrem) | `[ROZHODNUTO]` |
| `MiniAppManager` vs. `WindowNavigator` | **Separátní manager v shellu** (navigator nerozšiřuje) | `[ROZHODNUTO]` |
| Restore per-instance stavu (filtr…) | **Ve v1 ne** — obnoví se panely, ne jejich stav; opt-in pro VM později | `[ROZHODNUTO]` |

---

## 2. Co je miniaplikace

WPF **View (UserControl) + ViewModel dědící z `EDViewModel`**, vystavený API projektem
agendy. Běží **in-process** v shellu, **sdílí shellovu `MLRest` instanci** — tedy stejnou
ACTIVITY session, stejné `Idfa`. Žádný separátní zámek, žádný 10-arg launcher jako u legacy
modulů (`FormToRunGetter`). To je hlavní výhoda proti dnešnímu out-of-process spouštění.

Dědičnost z `EDViewModel` dává zdarma: `MLRest`, `Command(...)`, `PopupDisplayer`,
`Set/OnPropertyChanged`. Mini-app je tak normální WPF view se vším komfortem okna —
může „vystavit téměř co chce".

### Proč ne WinForms hostování (MHD)

MHD (`MHD2PlanDopravy`, ~8,6k LOC WinForms) se nebude přepisovat do WPF. PoC mini-app
z MHD bude **nová malá WPF view, která čte stejná Caché data přes MLRest** — ne embedding
starých forem přes `WindowsFormsHost`. Důvod: WinForms-in-WPF přináší focus/z-order problémy
a tluče se s in-process docking modelem. Mini-apps nepředělaných agend žijí v samostatném
WPF projektu (viz §6).

---

## 3. Kontrakty (žijí v `EDControls`)

`EDControls` je sdílená WPF knihovna referencovaná shellem i API projekty agend → kontrakty
patří sem (vedle `EDViewModel`, `WindowNavigator`).

```csharp
// Báze pro mini-app VM. Záměrně NEpřebírá window-bound chování EDViewModel
// (geometrii řeší AvalonDock, ne WindowStatePersistence).
public abstract class EDMiniAppViewModel : EDViewModel
{
    protected override string? PersistedFormId => null;   // layout drží AvalonDock
    public abstract string Title { get; }
    public virtual ImageSource? Icon => null;
    protected void CloseSelf() => CloseDialog();           // reuse base RequestClose (Action<bool?>)
    public virtual void OnOpened() { }                     // MiniAppManager volá po namountování
    public virtual void OnClosed() { }                     // MiniAppManager volá při zavření / switchi firmy
}

public sealed record MiniAppDescriptor(
    string Id,                              // stabilní typový identifikátor mini-app (ne ContentId)
    Func<EDMiniAppViewModel> CreateViewModel);

// Každý API projekt agendy implementuje a vrací své mini-apps.
public interface IMiniAppProvider { IEnumerable<MiniAppDescriptor> GetMiniApps(); }
```

### Multi-instance a ContentId

Mini-app jde otevřít **vícekrát** (např. tabulka se dvěma různými filtry vedle sebe). `Descriptor.Id`
je proto **typový** identifikátor, ne identita panelu. AvalonDock vyžaduje **unikátní `ContentId`
per panel**, takže `MiniAppManager` skládá `ContentId = "{Id}#{instance}"` (suffix opaque — counter
nebo GUID). Při obnově layoutu manager z `ContentId` odřízne suffix, podle prefixu najde descriptor
v katalogu a vyrobí **novou** instanci VM.

> Dopad na persistenci: AvalonDock uloží jen geometrii + `ContentId`, **ne stav instance** (filtr,
> výběr…). Po restartu se tedy obnoví panely (počet + pozice), ale každý začne s **výchozím stavem**.
> Restore per-instance stavu je opt-in rozšíření pro VM (serializovatelný state blob keyovaný
> `ContentId`) — ve v1 se nedělá (viz §9).

### Párování View ↔ VM — přes DataTemplate

Každá agenda dodá `ResourceDictionary`:

```xml
<DataTemplate DataType="{x:Type vm:VozidlaMiniAppViewModel}">
    <v:VozidlaMiniAppView/>
</DataTemplate>
```

…mergnutý do `App.Resources` při startu (stejný princip jako `EDIcons.xaml` / `EDStrings.xaml`,
viz povinné nastavení App.xaml v `CLAUDE.md`). AvalonDock hostuje **VM**, WPF si View vyrenderuje
sám přes DataTemplate, `Title`/`Icon`/zavírání se bindují z VM. Deskriptor je tak minimální
(jen `CreateViewModel`) a celé to zůstává čistý MVVM.

`CreateViewModel` používá `new VozidlaMiniAppViewModel()` → bezparametrický ctor `EDViewModel`
čte z `EDServices.Current` (konzistentní se stávajícím window patternem).

### Lightweight cesta — `SimpleMiniAppViewModel` (obsahové panely)

Pro čistě obsahové panely (nástěnka = WebView2, statický přehled, …) je psát vlastní VM + View +
DataTemplate zbytečný boilerplate. `EDControls` proto poskytne hotový **`SimpleMiniAppViewModel`**
a jednu sdílenou **`SimpleMiniAppView`** (= `ContentPresenter`) s jednou registrovanou
DataTemplate:

```csharp
public sealed class SimpleMiniAppViewModel : EDMiniAppViewModel
{
    public SimpleMiniAppViewModel(string title, Func<FrameworkElement> contentFactory) { ... }
    public override string Title { get; }
    public Func<FrameworkElement> ContentFactory { get; }   // ne živý element — kvůli re-parentingu při dock/float
}
```

Mini-app pak vznikne bez jediné nové VM/View/template třídy:

```csharp
new MiniAppDescriptor("Core.Nastenka",
    () => new SimpleMiniAppViewModel("Nástěnka",
        () => new WebView2 { Source = new Uri("https://www.m-line.cz/Nastenka1.html") }));
```

Všechny `SimpleMiniAppViewModel` instance se renderují stejnou `SimpleMiniAppView` (liší se jen
daty), takže DataTemplate pairing zůstává nedotčený. Je to **opt-in zkratka** — plné mini-apps
jdou dál přes vlastní VM+View+DataTemplate.

---

## 4. Hostování v shellu

V `EdisonMainWindow.xaml` se prázdný střední region (dnes sloupec 2 + okolí prázdného
7-sloupcového Gridu) nahradí jedním **`DockingManager`** (AvalonDock).

### Konzervativní hosting

- **Strom modulů vlevo zůstává fixní** — je to launcher, ne mini-app.
- `DockingManager` vyplní zbytek centrální plochy. Mini-apps jsou defaultně **anchorable**
  panely (dockovatelné / plovoucí / auto-hide); výchozí umístění řeší `MiniAppManager` napevno
  (bez per-mini-app konfigurace).
- **Nástěnka** (dnes WebView2 v sloupci 4, řízená `LoadBoards`) se **předělá na mini-app**
  přes `SimpleMiniAppViewModel` (viz §3). Tím odpadá zvláštní sloupec 4 i toggle `LoadBoards`.
- **Stub „Přehled"** (sloupec 6) se **zahodí**.

Alternativa (plně IDE-like): dockovat i strom modulů. Větší změna shellu — zatím odloženo,
dokud nebude konkrétní potřeba.

### Výchozí layout (first-run) `[OTEVŘENO]`

Až nástěnka přejde na mini-app, dnešní „nástěnka vždy viditelná přes `LoadBoards`" se změní na
„panel v docku jako každý jiný" — uživatel si ji může zavřít / oddockovat. To je žádoucí kvůli
konzistenci, ale je to **změna UX oproti dnešku**. Pokud má být nástěnka pro nové uživatele
defaultně otevřená, řeší to **výchozí layout v `MiniAppManager`u** (použije se, když uživatel
nemá uložený layout pro danou IDFa). Otevřená otázka: které mini-apps patří do výchozího layoutu.

### Toggle `ShowMiniApps`

Dnes existuje (`LocalPrefs.ShowMiniApps` + menu „Zobrazit miniaplikace"), ale **ukládá se
bez konzumenta**. Touto featurou konečně dostane konzumenta — řídí viditelnost dock regionu.

---

## 5. Lifecycle a „správa shellem"

`MiniAppManager` je **samostatný** v `EDISONMAIN2` (vlastní `DockingManager` + AvalonDock).
Záměrně **nerozšiřuje `WindowNavigator`** — ten zůstává v `EDControls` zaměřený na WPF okna +
legacy procesy, bez docking dependency. Koordinace se shellem jde **explicitními voláními**:

- **Open / Close / track** otevřených instancí mini-apps (multi-instance přes `ContentId`, §3).
- `CloseAll()` — shell ho volá při zavření → zavolá `OnClosed()` na všech instancích.
- `OnCompanySwitched()` — `EdisonMainViewModel.RebuildSessionState` ho volá při přepnutí firmy
  (mění se `MLRest.Idfa`): mini-apps drží stará data → manager **zavře vše a znovuotevře z lokálně
  uloženého layoutu nové firmy** (§7). Deterministické a čisté.

Mini-apps **se nezařazují do `WindowNavigator.GetActiveModules()`** (menu „Spuštěné moduly") —
jsou vidět přímo v docku jako taby/panely, duplikovat je do menu by byl šum. Kdyby to někdy bylo
potřeba, `EdisonMainViewModel` si seznam z manageru vyzvedne explicitně.

Plovoucí okno mini-appky vytváří **AvalonDock dynamicky** (`LayoutFloatingWindowControl`) →
je pořád pod správou docku, ne samostatné top-level okno. Tím je naplněno „vyjmout z shellu,
ale pořád pod jeho správou".

---

## 6. Struktura projektů (compile-time refs)

| Projekt | Role |
| --- | --- |
| `EDControls` | **Kontrakty** (`EDMiniAppViewModel`, `MiniAppDescriptor`, `IMiniAppProvider`, `MiniAppCatalog`) + lightweight `SimpleMiniAppViewModel`/`SimpleMiniAppView`. |
| `EDISONMAIN2` | `DockingManager` host + `MiniAppManager` + katalog/registrace + AvalonDock NuGet ref. |
| API projekt přepsané agendy | Vlastní mini-apps + `IMiniAppProvider` + DataTemplate dictionary. |
| **`EDISONMAIN2.LegacyMiniApps`** (nový, WPF) | Mini-apps **nepředělaných** agend (MHD…) — čistě WPF nad Caché daty přes MLRest. |
| `EDISONMAIN2` (Dev složka) | První **testovací mini-app** (sample, à la `EDComponentAndViewModelTestWindow`). |

### Registrace = explicitní seznam v composition root

V `AppBootstrapper` (žádná reflexe → bezpečné pro single-file publish, sedí na stávající styl
ručního wiringu):

```csharp
var providers = new List<IMiniAppProvider>
{
    new DevSampleMiniApps(),        // testovací mini-app
    new LegacyMiniApps.MhdMiniApps(),
    // new VozidlaMiniApps(),       // každá přepsaná agenda = jeden řádek
};
var catalog = MiniAppCatalog.Build(providers);
```

**Licenční gating prozatím podle rodičovské agendy/modulu** — mini-app se nabídne, pokud je
uživateli dostupný její modul (provider mini-apps registruje jen tehdy, když je modul agendy
licencovaný/přístupný). Vlastní per-mini-app gating (samostatný `ElementId` v `EDISFormEnum`/
`AccessTable`, jako u module tree přes `EdisonMainViewModel.TryFilter`) je odložený — doplní se,
až bude potřeba jemnější granularita.

**Refresh při přepnutí firmy:** dostupnost mini-apps závisí na licenci/modulu, což se mění při
přepnutí firmy (`RebuildSessionState` re-filtruje stromy). Katalog (resp. jeho filtrovaná podmnožina
nabízená v launcheru) se proto musí **re-vyhodnotit stejně jako stromy modulů**, jinak launcher po
přepnutí ukazuje mini-apps nedostupné firmy.

---

## 7. Persistence layoutu — zatím lokálně

AvalonDock `XmlLayoutSerializer` serializuje rozložení do XML; obnova páruje obsah přes
`ContentId` (`= "{Id}#{instance}"`, §3) → při loginu / přepnutí firmy `MiniAppManager` z `ContentId`
odřízne suffix, podle prefixu najde descriptor v katalogu a vyrobí novou instanci VM
(s **výchozím stavem** — per-instance stav se neukládá, §3).

**Uložení zatím lokálně** v `LocalPrefs` (JSON v `%APPDATA%\m-line\EDISONMAIN2\local.json`).
Layout držíme **per IDFa** (slovník `idfa → layoutXml`), aby přepnutí firmy mělo vlastní rozložení
a sedělo na close/reopen chování z §5. Přidá se jako nullable property (chybějící klíč =
prázdný layout = výchozí docking), kompatibilní upgrade dle vzoru v `CLAUDE.md`.

> Trade-off: lokální = per-stanice, nepřenáší se mezi počítači. Pokud to bude vadit, pozdější
> migrace na server-side (per-user × IDFa, dedikovaný Caché endpoint nebo `MLRest.BinarySave`)
> je přímočará — `MiniAppManager` čte/zapisuje layout přes jedno rozhraní.
>
> Pozn.: EDISONMAIN2 zatím nikde neběží → formát persistence lze volit volně, žádné migrace
> starých dat (viz memory `project_predeployment_no_migrations`).

---

## 8. Plán práce po commitech

Každý commit je samostatně reviewovatelný a **zelený pro pre-push hook** (`dotnet test` +
`run-cache-tests.ps1`) — builduje a testy procházejí. Pořadí respektuje závislosti a
**front-loaduje rizika** (AvalonDock + single-file publish hned, ne na konci).

| # | Commit | Obsah | Test |
| --- | --- | --- | --- |
| 1 | **Kontrakty + katalog** (EDControls) | `EDMiniAppViewModel`, `MiniAppDescriptor`, `IMiniAppProvider`, `MiniAppCatalog.Build`. Případný drobný refactor `EDViewModel` (oddělit popup-owner / keybindings / geometrii). Čistě knihovna, žádný shell wiring. | xUnit: agregace providerů → katalog |
| 2 | **Lightweight host** (EDControls) | `SimpleMiniAppViewModel` + `SimpleMiniAppView` + DataTemplate. | xUnit: title / content factory |
| 3 | **AvalonDock skeleton + ověření publish** | NuGet AvalonDock do `EDISONMAIN2`; prázdný střední region → `DockingManager` (strom vlevo zůstává); dočasný hardcoded panel pro ověření dock/float. **Ověřit `SingleFileCEdison` publish.** | manuálně: běh + publish |
| 4 | **`MiniAppManager`: open/close/track** | Otevření descriptoru do docku, tracking instancí, `OnOpened`/`OnClosed`, `RequestClose` → zavření. Dočasný dev launcher (menu) z katalogu — jako throwaway entry použije `SimpleMiniApp`. | xUnit: open/close/track (mock) |
| 5 | **Integrace do shell lifecycle** | Explicitní volání z shellu na `MiniAppManager`: `CloseAll` → `OnClosed`; `OnCompanySwitched` (close/reopen + refresh katalogu); `ShowMiniApps` toggle řídí viditelnost docku. | xUnit kde dává smysl |
| 6 | **Persistence layoutu (lokálně)** | `LocalPrefs` layout per IDFa (XmlLayoutSerializer string); save on close/change; restore on login + switch; re-instancuje z katalogu přes `ContentId`. | xUnit: round-trip serializace |
| 7 | **Theming AvalonDock** | Sladění docku s `EDTheme.*` `DynamicResource`. | manuálně |
| 8 | **První testovací mini-app** (dev sample) | Reálná `EDMiniAppViewModel` + View + provider — end-to-end ověření full MVVM cesty. | xUnit |
| 9 | **Nástěnka jako mini-app** | Nástěnka přes `SimpleMiniAppViewModel`; odstranit starý sloupec 4 (WebView2) + `LoadBoards`; zahodit „Přehled" (sloupec 6). | manuálně |
| 10 | **(později)** `EDISONMAIN2.LegacyMiniApps` + **MHD PoC** | Nový WPF projekt + první mini-app nad MHD daty (přes MLRest). | dle agendy |

Commity 1–2 jdou případně spojit. Commit 3 musí předcházet 4–9 (zakládá dock). Commit 8
(dev sample) lze prohodit dřív, pokud chceme reálnou mini-app pro testování 5–6 místo throwaway.

---

## 9. Otevřené body / rizika k pohlídání

- **AvalonDock + single-file publish** — knihovna je managed-only (žádné nativní DLL), mělo by
  projít, ale **ověřit hned na začátku** (publish profil „SingleFileCEdison"), ne až na konci.
- **Theming** — AvalonDock má vlastní témata; sladění s `EDTheme.*` `DynamicResource` je
  samostatná položka práce (ne kosmetika na pět minut).
- **Klávesové zkratky docked mini-app** — `EDViewModel.AttachToWindow` cílí na `Window`
  (InputBindings + geometrie persistence). Pro mini-appku se obejde: zkratky řeší `InputBindings`
  na vlastním `UserControl` (focus-scoped), geometrii AvalonDock, popupy vlastní shell window
  jako owner. `EDMiniAppViewModel` tedy `AttachToWindow` **nevolá** — možná drobný refactor
  `EDViewModel`, aby šly ty tři věci (popup owner / keybindings / geometrie) použít odděleně.
- **Licenční gating** `[ROZHODNUTO, prozatím]` — mini-app dědí dostupnost z rodičovské agendy/
  modulu (registruje se jen pro dostupný modul). Vlastní per-mini-app `ElementId` se zatím
  nedělá; přidat, až bude třeba jemnější granularita.
- **Popup owner při floatingu** — popupy mini-appky by ideálně měly mít owner aktuální hostující
  okno (shell vs. floating). Ve v1 návrh: owner = shell main window (default `DefaultOwner`).
- **Restore per-instance stavu** `[odloženo]` — po restartu se obnoví panely, ne jejich stav
  (filtr, výběr). Opt-in rozšíření: VM vystaví serializovatelný state blob keyovaný `ContentId`,
  `MiniAppManager` ho uloží vedle layoutu a nakrmí zpět při obnově. Ve v1 se nedělá.

---

## 10. Poznámky

> _(sekce pro poznámky během vývoje — doplňovat průběžně)_
