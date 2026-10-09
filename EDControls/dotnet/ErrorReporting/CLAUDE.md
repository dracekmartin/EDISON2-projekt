# ErrorReporting - poznámky pro vývojáře

Hlášení chyb na `podpora.m-line.cz/api/telemetry/ingest` - **přímý HTTPS POST mimo Caché**
(usage telemetrie `EDUsageTelemetry` je jiný kanál, jde přes MLRest; společného nemají nic
než slovo „telemetrie"). Náhrada legacy `frmMLErrorEx` (`EDISON/EDLIBRARY/`), ne doslovná
parita - odchylky jsou vědomé a jsou níž.

Vstup je `ErrorReports.Report*` (volá je `PopupDisplayer.Error` a globální handlery v obou
`App.xaml.cs`): statická fasáda nad výchozí `ErrorReportingPipeline`, která drží throttle, HTTP
klienta, záložní log i zdroj kontextu. Testy skládají vlastní instanci a statiku nemění.

## Kontrakt serveru (ověřený v `edwcore/apps/mlweb/backend`)

- Povinné `source`, `type`, `module`, `name`; `type` ∈ `info` | `warning` | `error`.
- Volitelné `partner` (= IdFa), `description`, `data`, `attachments[{filename,mimeType,data}]`.
- Pole až 100 záznamů = jedna dávka; víc server odmítne 400 (`MAX_BATCH_SIZE`, klient `MaxBatchSize`).
- Jediný klíč pro celý systém (nastavení `telemetry_api_key`), **sdílený s legacy EDISONem** -
  server nerozlišuje podle zdroje. Vlastní klíč by byl zásah do mlwebu.

**Past: `applyEdisonVersions`.** Server přepíše firmě „poslední hlášenou verzi EDISONu"
podle `data.version`, když je záznam instalační událost (`module="EDISON"` **a**
`name="Instalace verze"`) nebo nese klíč `pocty` / `ad_pocetRZ` / `mhd_pocetRZ` /
`zam_pocetZam`. `data.version` posíláme vždy (viz Identita níž), takže se těm dvěma
vzorům musíme vyhýbat - jinak by každá chyba z EDISON2 přepsala zákazníkovi verzi legacy
EDISONu. Hlídají to `ErrorReportTests.Never_LooksLikeInstallEvent` / `Never_CarriesUnitMetricKeys`.

## Vědomé odchylky od legacy

- **Hybrid místo „až na tlačítko".** Jádro odejde automaticky (`stage=auto`), dobrovolný popis
  okolností posílá uživatel z dialogu jako druhý záznam `type=info` se stejným `refId`
  (`stage=user`). Párování přes `refId` v `description` - server se kvůli tomu neměnil.
- **Deduplikace je povinná, ne vylepšení.** Suppression list vědomě **nemáme** (rozhodnutí
  2026-08-04), takže projde i běžný provozní `Lock exist failed!`, který legacy zahazoval.
  Jediná pojistka proti chybě v `OnPaint` / timeru je `ErrorThrottle` (klíč = typ výjimky +
  5 rámců stacku + form, okno 5 min, pak souhrn s `repeatCount`, strop 50 na běh).
- **Snímek jen vlastních oken** (`RenderTargetBitmap`), ne obrazovky - cizí aplikace se do
  přílohy nemají jak dostat. Legacy sešívá všechny monitory. Důsledek: **HWND obsah se
  nevykreslí**, takže WebView2 miniaplikace shellu zůstanou v snímku prázdné a snímek nikdy
  není přesně to, co uživatel viděl.
- **Interní běh se označí, nezahodí** - `source` dostane suffix `-internal` (`MlineEnvironment`,
  zrcadlí legacy `IsDomainMline` včetně registru `HKCU\Software\Edison\MlineDomain` čteného jako VB
  `CBool`, týmž pravidlem skrývá strom shellu mlineOnly uzly). Legacy z m-line domény neposílá nic a přichází
  o vlastní dogfooding; zahodit se dá vždy na serveru, vrátit nikdy. Suffix ve `source` (ne
  příznak v `data`) proto, že Přehled telemetrie filtruje podle sloupce Zdroj a volby bere
  z dat - nová hodnota se ve filtru objeví sama. Pozor: `source` jde i do předmětu tiketu.

## Drobné

- **Identita** (`source`, `data.version`) z `AppIdentity` (`EDViewModel/EDServices.cs`): `EDServices` → entry assembly
  (`1.0.0+<8 znaků hashe>`) → `nezjištěna`. Pád před `Initialize` jde se `source` = jméno exe, ne `FormIdPrefix`.
- **Neodeslaná hlášení** (offline, VPN, 500) zachytí `ErrorFallbackLog`:
  `%APPDATA%\m-line\common\error-reports.log`, NDJSON **bez příloh**, rotace nad ~1 MB
  do `.old`. Nic se zpětně neodesílá - podpora si soubor vyžádá ručně.
- `ReportLegacyExit` (pád hostovaného legacy modulu podle exit kódu, volá `LegacyModuleHost`):
  kontext okna = forma/exe modulu a **stabilní throttle klíč** `LegacyExit|exe|forma|kód` - syntetická
  výjimka nemá stack, `BuildKey` by slepil pády všech modulů do jednoho záznamu. Výjimku zná jen host:
  před pádem ji zapíše do `%TEMP%\m-line\legacy-crash\<pid>.txt` (`LegacyCrashDetail`, cestu zrcadlí
  `HostedErrorReporter` v repu EDISON) a shell ji po konci procesu přiloží (`data.stack`, příčina v popisu).
- `ReportFatal` posílá **synchronně** s 5s timeoutem: po `AppDomain.UnhandledException`
  runtime proces vzápětí ukončí a fire-and-forget by se nestihl.
- `TryCapture` bere `FrameworkElement`, ne `Window` - nezobrazené okno má nulovou
  `ActualWidth`, takže by šlo testovat jen s reálně otevřeným oknem.
