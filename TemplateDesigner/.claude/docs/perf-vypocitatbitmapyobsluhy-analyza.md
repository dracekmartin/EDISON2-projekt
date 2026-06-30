# Perf analýza: `VypocitatBitmapyObsluhy` (MHD agenda) — podklad pro implementátora

> **Účel:** odpovědět „je to skutečně O(bodů³) a rizikové?", popsat reálný problém a připravit
> implementátorovi hotový podklad (složitost, hot spoty, riziko, verifikace, měřicí recipe), ať
> nemusí znova procházet kód. **Read-only analýza, žádná změna kódu.**
>
> **Zdroj:** `CACHE/ALVA/TemplateDesigner/Agendy/ZastavkoveJrMhd.cls`, metoda `VypocitatBitmapyObsluhy`
> (ř. 713–826), volaná z fáze 5 `GetData` (ř. 315). Vstupní globály plní fáze 4 `PromitnutSpoje` (ř. ~560–605).

---

## TL;DR / doporučení

- **Skutečná složitost = O(S · B²)** per směr (S = počet spojů, B = počet bodů trasy), **NE B³** jak
  uvádí roadmap. „³" je horní odhad za předpokladu S ≈ B.
- Na reálných MHD linkách (B ≈ 30–60 zastávek, S ≈ 100–300 spojů) → ~10⁵–10⁶ levných `$extract` operací
  → odhadem **jednotky až desítky ms**. Pravděpodobně **NENÍ reálný bottleneck.**
- ⚠️ **Měř první** (recipe v §6). Pokud měření na největší reálné lince potvrdí < ~stovky ms, **zavřít
  L2 jako YAGNI** — refaktor ověřeného enginu se nevyplatí.
- Refaktor je **rizikový** (byte-identický gold výstup, subtilní bitmap sémantika), proto ho dělat
  jen když měření ukáže reálný problém.

---

## 1. Co metoda počítá

Pro každou zastávku (BodID) × směr sestaví bitmapy obsluhy potřebné pro mřížku odjezdů a auto-poznámky:
- `obsluha` / `obsluhaVse` (AllStopBitmap: 1 = staví všechny spoje, 9 = jen některé),
- `spojTrasa[…,BodID]` (trasa spoje z daného bodu, **vč. konečného** — „jako legacy", ř. 749),
- `rozdilTrasy` + písmena A/B/C (alg. 1002) pro poznámky o odlišných trasách.

---

## 2. Složitost — derivace

Struktura smyček (ř. 719–824):
```
for SmerID in {0,1}:                                  # ×2 (konstanta)
  for BodID in trasa[SmerID]:                         # B bodů  ── OUTER
    Block A (728–760): for SpojID in obsluhaKonec[BodID]:   # ≈ S spojů
                          scan dalsiBod → konec        #   B   (konecnyBod + ObsluhaBitmap)
                          scan dalsiBod → konec        #   B   (TrasaSpojZBodu)   ← 2× B
    Block B (764–776): for SpojID …:  scan → konecnyBod      # ≈ S × ≤B
    Block C (779–823): for SpojID …:  scan → konecnyBod      # ≈ S × ≤B  (jen alg 1002)
```
- Per BodID: 3 průchody přes `obsluhaKonec[BodID]` (každý ≈ S spojů) × inner body-scan (≈ B) = **O(S·B)**.
- Přes B bodů × 2 směry = **O(S · B²)**.
- `spojTrasa[smer,spoj,bod]` se zapisuje pro každý (bod, spoj) = S·B uzlů, každý budovaný B-scanem →
  **O(S·B²)** i v počtu zápisů do `^||TiskMHD`.

**S (spojů na bod):** `obsluhaKonec[smer,bod,spoj]` plní fáze 4 (ř. 601) pro každý spoj odjíždějící
z bodu → ≈ všechny spoje směru.

---

## 3. ✅ BodID je sekvenční 1..N (ověřeno) → bitmapy ohraničené délkou trasy

Bitmapy se staví/čtou přes `$e(bitmap, BodID)`. `BodID` je subscript `ModelTrasaBodDetail` (ř. 344),
ukládaný nepřečíslovaný (ř. 372). **Ověřeno na reálných MHD datech (CB_DP, TrasaID 1001+):
`ModelTrasaBodDetail` je subscriptován sekvenčním ordinálem (BodID = 1, 2, 3 … N).** Bitmapy mají
tedy délku ≈ N (délka trasy), **ne řídké/velké** → platí čisté **O(S·B²)**, žádná skrytá `O(S·B·M)`
past s megabyte stringy. (Tím odpadá hlavní teoretická obava o paměti/délce.)

---

## 4. Konkrétní hot spoty / redundance (kde optimalizovat, KDYŽ měření ukáže potřebu)

1. **Block A: dvě oddělené B-smyčky** (739–747 a 751–758) — první počítá `konecnyBod` + `ObsluhaBitmap`,
   druhá `TrasaSpojZBodu`. **Sloučitelné do jednoho průchodu** → ½ inner práce. Behavior-preserving.
2. **`konecnyBod` scan jde až do KONCE trasy** (740: `$o` dokud ≠ "") místo do skutečného konce spoje.
   Spoj končí na poslední `1` v `TrasaSpojBitmap` → dá se zastavit dřív (u krátkých spojů velká úspora).
3. **`TrasaSpojZBodu` se přepočítává per (bod, spoj)** B-scanem, ačkoli plná trasa spoje už je v
   `spojTrasa[smer,spoj]` (ř. 605). `TrasaSpojZBodu` = ta plná bitmapa maskovaná na interval
   [BodID … konecnyBod] → lze **derivovat z plné bitmapy** místo re-scanu (pozor na „vč. konečného").
4. **3 oddělené průchody** přes `obsluhaKonec[bod]` (A/B/C). B a C sloučitelné obtížně — C potřebuje
   hotový `AllStopBitmap` z B. Sloučení A+B je realističtější.

---

## 5. Riziko refaktoru (proč opatrně)

- **Byte-identický gold výstup** — MHD agenda je ověřená 1:1 proti legacy. Jakákoli změna pořadí/obsahu
  bitmap může posunout A/B/C písmena, NZ, nebo AllStop klasifikaci (9 vs 1) → vizuálně i datově jiný JŘ.
- **Subtilní sémantika `TrasaSpojZBodu` vč. konečného bodu** (komentář ř. 749) — historický bug: když
  se konečný bod vynechal, bitmapy byly prázdné a A/B se negenerovaly. Každá optimalizace #1/#3 to musí zachovat.
- **Provázané globály napříč fázemi** — `obsluhaKonec` se plní ve fázi 4 (ř. 601) a **přepisuje** zde
  (ř. 748, hodnota = konecnyBod); `spojTrasa` má 2 varianty (`[smer,spoj]` plná vs `[smer,spoj,bod]` z bodu).

---

## 6. Verifikační strategie + měřicí recipe (krok 1 = MĚŘENÍ, udělat PRVNÍ)

**Měření (rozhodne, jestli vůbec refaktorovat):**
1. Najít největší MHD linku (nejvíc spojů × zastávek) na firmě s `ALVA.MHD` daty.
2. Obalit volání `..VypocitatBitmapyObsluhy()` (ř. 315) měřením `$zh`: `s t=$zh d ..VypocitatBitmapyObsluhy() s ^jm("perf")=$zh-t` (debug global, smazat po měření).
3. Pokud i na největší lince fáze 5 trvá < ~100–200 ms → **L2 = YAGNI, zavřít** bez zásahu.

**Verifikace (povinná, pokud se přesto refaktoruje):**
1. Před: `GetData` na MHD gold lince → uložit kompletní JSON (všechny stránky).
2. Po: `GetData` → **diff proti uloženému musí být prázdný** (byte-identický).
3. Cíleně zkontrolovat stránky s **rozdíly tras (A/B/C)**, **AllStop = 9** a **konečnými spoji** — tam se projeví chyba bitmap nejdřív.

---

## 7. Závěr

„O(bodů³) rizikový refaktor" je **nadhodnocené**. Reálně **O(S·B²)** s bitmapami ohraničenými délkou
trasy (BodID sekvenční 1..N — §3 ověřeno), na běžných linkách levné (~jednotky až desítky ms).
**Nejpravděpodobnější správný výstup = změřit a zavřít jako YAGNI.** Pokud měření na extrémní lince
překvapí, jsou připravené 4 konkrétní behavior-preserving optimalizace (§4) + verifikační postup (§6).
