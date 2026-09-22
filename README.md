# EDISON2

Dvouvrstvá WPF aplikace: .NET UI (`net10.0-windows`) ↔ Caché (InterSystems) back-end
přes HTTP knihovnu MLRest. Postupně nahrazuje legacy WinForms EDISON ze sesterského repa
`C:\MLineRepos\EDISON`, se kterým běží paralelně proti téže Caché instanci.

## Projekty

| Projekt | Co to je |
|---|---|
| `EDISONMAIN2/` | Shell nového Edisonu - login, výběr firmy, stromy modulů, miniaplikace, spouštění legacy agend |
| `EDControls/` | Sdílená WPF knihovna (EDList, EDInputForm, EDViewModel, theming) + její Caché strana |
| `TemplateDesigner/` | Editor tiskových šablon, samostatná aplikace nad EDControls |
| `EDUPDATE2/` | Klientský updater nového shellu (vědomě bez EDControls - vyměňuje je za běhu) |
| `EDISONMAIN2.LegacyMiniApps/` | Miniaplikace nepřepsaných agend nad Caché daty |
| `ServiceGtfsRt/` | Služba pro GTFS Realtime |
| `EDTesting/` | Caché test framework (`MLTestCase` + `TestRunner`) |

## Build a spuštění

Předpoklad: **sbuildit legacy repo `EDISON` do `C:\Edison\`**. Odsud se referencují
`MLRest.dll`, `EDISONAPI` a `EDISONMAIN2API`, bez nich se EDISON2 nepřeloží.

```
dotnet build EDISON2.sln
dotnet test  EDISON2.sln                          # xUnit + NSubstitute
pwsh -File tools/run-cache-tests.ps1              # Caché testy (potřebují dev server přes VPN)
```

Do `C:\Edison\` se aplikace dostávají jen přes `dotnet publish` s profilem
„SingleFileCEdison" (`Properties/PublishProfiles/`); `C:\Edison\` je staging, do produkce
jde až explicitní klik v interním Compileru.

## Dokumentace

`CLAUDE.md` v kořeni a v každém projektu drží invarianty, wire kontrakty a pasti - je to
hlavní vývojářská dokumentace. Delší reference jsou v `docs/`:
[architektura Caché a MLRest](docs/mlrest.md), [seznam binárek k vydání](docs/deploy.md).
