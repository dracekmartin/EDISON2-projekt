# GTFS Realtime — DPCB: stav nasazení

> Pracovní poznámka k dokončení nasazení. Helpdesk ticket **11701** (agenda MHD, DPCB).
> Stav k **2026-05-27**.

## Kde to vázne (shrnutí)

Migrace serveru DPCB je hotová ("nový server je již používán"). **RT feed ale nikdy nebyl reálně živý** — je potřeba ho zprovoznit, ne jen restartovat. Aplikační část (M-line) je hotová; chybí infrastruktura na straně DPCB + ověření po migraci.

## Architektura (dva nezávislé směry)

```
VSTUP (živá data):
  192.168.31.210 (Caché/Edison) ──outbound──▶ TransData API (host:port = ZJISTIT)

VÝSTUP (ke Googlu):
  Google ──internet──▶ veřejný host :443 ──reverse proxy──▶ 192.168.31.210:5000
                       (např. gtfs.dpmcb.cz, NE Exchange host)
```

Služba (`ServiceGtfsRt`) = tenký převodník: HTTP `.pb` request → MLRest na Caché
`ALVA.MHD2.Linky.API.Export` (Get{VehiclePositions,TripUpdates}) → JSON chunky → protobuf.
Veškerá logika feedu je v Caché. Google = **fetch/pull** (sám stahuje z URL ~30 s), nepushuje se.

## Klíčová fakta / IP

- `192.168.31.210` = **nový DPCB server** (běží na něm služba + Caché). Stará IP byla `192.168.31.2`.
- `192.168.16.1` = M-line **lokálka** (to, co je dnes v `appsettings.json` = test, NE DPCB).
- `dpmcb.dpmcb.cz` / `176.62.236.187`: **:443 dnes Exchange/Outlook (IIS 8.5)**, :80 prázdná IIS7. Náš GTFS proxy tam není.
- `appsettings.json` je v publishi **samostatný soubor vedle .exe** (není single-file) → connection se mění editací na serveru + restart, BEZ rebuildu.
- TransData endpoint **není** v ticketu 11701 (odkaz na "11713" je slepý). Hledat v Caché integračním kódu nebo u Šťastného (DPCB IT).

## TODO — co je potřeba udělat

**Fáze 1 — vstup dat (odblokovat první):**
- [ ] DPCB IT: sdělit aktuální **endpoint TransData** (host + port) po migraci.
- [ ] DPCB IT: povolit **odchozí spojení** `192.168.31.210` → TransData endpoint.
- [ ] (My) ověřit endpoint+port z `192.168.31.210`: `Test-NetConnection <host> -Port <port>`.

**Fáze 2 — naše strana:**
- [ ] `appsettings.json` na serveru → tamní Caché (`192.168.31.210`/`localhost`) + správný DPCB login (ne `martin.hubata`).
- [ ] (Re)instalace Windows služby (`sc create`), spuštění.
- [ ] Ověřit lokálně na serveru: `http://localhost:5000/vehiclepositions.json` vrací reálná data.

**Fáze 3 — výstup ke Googlu:**
- [ ] DPCB IT: veřejný HTTPS host (samostatný hostname mimo Exchange) → reverse proxy na `192.168.31.210:5000` + cert + firewall 443.
- [ ] Rozhodnout účet Google (DPCB vs M-line).
- [ ] (My) zadat veřejnou `.pb` URL do Google Transit portálu (fetch) / vykomunikovat s Google podporou.

## První dotaz do ticketu (odesláno / k odeslání)

Připomenout DPCB body Fáze 1: **endpoint+port TransData po migraci** a **propustnost na `192.168.31.210`**.
Až potvrdí, navázat instalací služby. Veřejnou HTTPS adresu řešit zvlášť v dalším kroku.
