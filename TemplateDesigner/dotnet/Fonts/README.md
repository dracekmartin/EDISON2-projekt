# Embedded fonty

Sem hoď `.ttf` / `.otf`. Glob v `TemplateDesigner.csproj` je zabalí jako `<EmbeddedResource>`
do assembly (a tím i do single-file `.exe` z `publish.ps1`). `Models/FontResolver` je **automaticky
načte** podle názvu rodiny — objeví se v renderu i v UI výběru fontu. **Žádná úprava kódu ani .csproj
není potřeba** — stačí soubor sem přidat.

## Priorita při renderu
`embedded rodina → OS-nainstalovaná → Arial → Skia default`. Embedded má přednost = deterministický
výstup nezávislý na tom, co je na daném stroji nainstalované (důležité pro headless/serverový tisk).

## Řezy fontu
Jeden soubor = jedna váha/sklon. Pro tučné/kurzívu přidej samostatné soubory (Regular + Bold + Italic
+ BoldItalic) — `FontResolver` vybere nejbližší variantu podle stylu. Bez varianty Skia nesyntetizuje
(faux-bold přes `SKFont` se tu nepoužívá) → text zůstane v Regular.

## ⚠️ Licence — číst před přidáním
Font se **redistribuuje** (je zabalený v .exe a šíří se). Přidávej **jen** fonty s licencí, která
redistribuci/bundling výslovně povoluje:

- **Arimo** (Apache-2.0) — metricky kompatibilní s Arial/Helvetica
- **Liberation Sans** (OFL) — metricky kompatibilní s Arial
- jiné OFL / Apache-2.0 fonty

**NEpřidávej** komerční fonty (Helvetica, Arial, …) — na ty licence Windows ani „volné stažení"
nestačí; jejich bundling vyžaduje placenou redistribuční licenci. Základní text tyto fonty nepotřebuje
embednout — na Windows se spoléhá na OS-nainstalovaný Arial.

Speciální JŘ symbolový font (Timetable) doplnit až po vyjasnění jeho zdroje a licence k redistribuci.
