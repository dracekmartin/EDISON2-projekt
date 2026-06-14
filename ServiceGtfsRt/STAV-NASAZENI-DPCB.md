# GTFS Realtime — DPCB: stav nasazení

> Pracovní poznámka k dokončení nasazení. Helpdesk ticket **11701** (agenda MHD, DPCB).
> Naposledy aktualizováno **2026-06-04**.

## TL;DR

Po dlouhé migraci serveru DPCB je síťová propustnost otevřená a TransData API z `31.210` reálně odpovídá. K dokončení vstupu dat (fáze 1) chybí ostré credentials pro TDwepAPI a interní DNS pro hostname `apiext.dpmcb.cz`. Vše technické řešíme se **Šťastným**, ne s Mikasem. Aplikační část (.NET služba + Caché feed builder) je hotová a publishnutá. RT feed nikdy reálně živý nebyl — jde o úplné zprovoznění, ne restart.

## Architektura

Dva nezávislé směry. Edison v cestě **musí zůstat** (skládá GTFS feed + drží identifikátory shodné se statickým GTFS).

```
VSTUP (živá data):
  31.210 (interní LAN, Edison + Caché + .NET služba)
         │ outbound HTTPS POST
         ▼
  49.49:31123 (DMZ, TDwepAPI /api/GetOnlineData)
         │
         ▼ (zevnitř DMZ)
  TransData backoffice

VÝSTUP (Google fetch):
  Google internet ──HTTPS──▶ veřejný host (gtfs.dpmcb.cz — ZATÍM NEEXISTUJE)
                              │ reverse proxy
                              ▼
                              31.210:5000 (ServiceGtfsRt)
```

**DMZ** (Demilitarized Zone) = oddělená síťová zóna mezi LAN a internetem; bezpečnostní buffer. V DPCB: `192.168.31.0/24` = interní LAN, `192.168.49.0/24` = DMZ. Provoz mezi nimi musí být explicitně povolen.

Služba `ServiceGtfsRt` (.NET, port 5000) = tenký převodník. Na `.pb`/`.json` request → MLRest na Caché → `ALVA.MHD2.Linky.API.Export.Get{VehiclePositions,TripUpdates}` → JSON po chuncích → protobuf. Veškerá doménová logika (slučování s JŘ, identifikátory) je v Caché. Google = **fetch/pull** model (sám stahuje z URL ~30 s).

## Klíčové stroje a hostnames

| Co | Adresa / hostname | Role | Stav |
|---|---|---|---|
| Edison server | `192.168.31.210` (interní LAN) | Hostí Caché + naši .NET službu. Volá ven do DMZ na TransData. | OK, dostupný, propustnost ven funguje |
| TransData API | `192.168.49.49:31123` (DMZ) | Vstup živých dat (HTTPS, IIS host-header binding na `apiext.dpmcb.cz`) | API žije, ale ostré credentials chybí |
| `apiext.dpmcb.cz` | hostname pro TransData API | INPUT — hardcoded v `Export.cls:1536` | DNS z 31.210 **nepřekládá** (chybí záznam) |
| `gtfs.dpmcb.cz` | navrhovaný veřejný hostname | OUTPUT — kam by Google fetchoval feed | **Neexistuje**, návrh do fáze 3 |
| `dpmcb.dpmcb.cz` / `176.62.236.187` | bývalý společný host | Dnes drží Microsoft Exchange/OWA (IIS 8.5) | Pro GTFS nepoužitelný — kolize s mailem |
| M-line lokálka | `192.168.16.1` | Test prostředí (default v `appsettings.json`) | Nesouvisí s DPCB |

## ⚠️ Pozor — DVA různé sady credentials, neplést

| | (A) Caché login | (B) TransData login |
|---|---|---|
| Kde sedí | `appsettings.json` na 31.210 vedle .exe | Hardcoded v `Export.cls:1543` v Caché |
| Účel | .NET služba se přes MLRest přihlašuje do Caché | Caché se přihlašuje do TDwepAPI |
| Default v repu | `martin.hubata` / Mlp 167 (M-line lokální test) | `Login=205 / Heslo=heslo` (placeholder!) |
| Kdo dodá ostré | M-line / DPCB Caché admin (na 31.210) | **Šťastný / TransData** |
| Jak změnit | Editace `appsettings.json` + restart služby, BEZ rebuildu | Editace Caché třídy + překompilace |

## Ověřeno k 2026-06-04 (z 31.210 přes RDP)

- ✅ TCP `31.210 → 49.49:31123` OPEN
- ✅ TLS handshake funguje (Tls12,Tls11,Tls)
- ✅ IIS host-header `apiext.dpmcb.cz` akceptován
- ✅ `POST https://.../TDwepAPI/api/GetOnlineData` vrací JSON odpověď (nikoliv IIS error)
- ❌ Credentials `205/heslo` z Caché kódu vrací `{"Message":"An error has occurred."}` (stejně jako `x/x` → auth fail)
- ❌ `Resolve-DnsName apiext.dpmcb.cz` z 31.210 → `DNS_ERROR_RCODE_NAME_ERROR`
- ✅ Caché integrační kód (`Export.cls` v `C:/DEV/EDISON/MHD2Linky/CACHE/ALVA/MHD2/Linky/API/`) přečten — víme přesně, co volá

## Co blokuje a kdo to musí vyřešit

**Šťastný (TransData):**
- [ ] Ostré produkční credentials pro `/TDwepAPI/api/GetOnlineData` — bezpečným kanálem (Bitwarden / signál / telefon), NE přes helpdesk plain-text.
- [ ] Interní DNS záznam `apiext.dpmcb.cz → 192.168.49.49` (nebo nám povolit přidat hosts entry na 31.210).

**My (M-line) — až budou (A) a (B):**
- [ ] Doplnit credentials do `Export.cls` (varianta b níže) NEBO refaktorovat na čtení z Caché parametru/globalu (varianta a, čistější).
- [ ] Překompilovat Caché třídu na 31.210.
- [ ] Připravit `appsettings.json` s produkčním CB_DP účtem (řešíme my, ne Šťastný).
- [ ] Deploy: kopie publish/ na 31.210, `sc create` + start, ověřit lokálně `http://localhost:5000/vehiclepositions.json`.

**Pozdější fáze 3 (až bude vstup živý):**
- [ ] Veřejný HTTPS host (`gtfs.dpmcb.cz` nebo podobně, **mimo Exchange**) → reverse proxy na `31.210:5000` + TLS cert + otevřený 443.
- [ ] Rozhodnutí účtu Google (DPCB vs M-line) — otevřené z 2/2026.
- [ ] Zaregistrovat `.pb` URL v Google Transit Data Sharing portálu.
- [ ] Odpovědět Mikasovi na jeho návrh vyřadit Edison — Edison zůstává (architektonický důvod: skládá feed + drží ID).

## Bezpečnost credentials — 3 varianty od nejlepší

- **a) Nejčistší:** refaktorujeme `Export.cls` na čtení `Login`/`Heslo` z Caché parametru/globalu (např. `^||ML.Config.TDLogin`). Šťastný hodnoty zadá přímo na 31.210 v Caché terminálu. M-line heslo nikdy nevidí.
- **b) Pragmaticky:** Šťastný pošle credentials přes secure note. Doplníme do Caché kódu **přímo na 31.210**, NEPUSHNEME do `C:/DEV/EDISON` repa.
- **c) Špatně:** plaintext v helpdesk ticketu (auditní stopa navždy).

## Komunikační kanály

- **Šťastný (TransData)** = primární technický kontakt pro vše ohledně TransData API + DPCB infrastruktury. Sem jde aktuální mail.
- **Mikas (DPCB)** = ticketový kontakt; čeká na něj odpověď k jeho návrhu vyřadit Edison (řešíme v pozdější fázi).
