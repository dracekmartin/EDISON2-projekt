# Instalace GTFS Realtime služby

## 1. Publish aplikace (M-line)

```bash
dotnet publish -c Release -r win-x64 --self-contained -o ./publish
```

Před kopírováním na server uprav `appsettings.json` s připojovacími údaji zákazníka (Server, Port, Namespace, přihlašovací údaje do Caché).

## 2. Instalace na serveru zákazníka

Zkopíruj `publish/` na server (např. `C:\Services\GtfsRealtime\`) a zaregistruj jako Windows Service:

```cmd
sc create "ALVA GTFS Realtime" binPath="C:\Services\GtfsRealtime\NazevAplikace.exe" start=auto
sc start "ALVA GTFS Realtime"
```

Ověř lokálně: `http://localhost:5000/vehiclepositions.json`

## 3. IIS reverse proxy + HTTPS (zákazníkův IT)

- Nainstalovat moduly **ARR** a **URL Rewrite** do IIS
- Vytvořit site s `web.config`:

```xml
<rewrite><rules>
  <rule name="GTFS Proxy" stopProcessing="true">
    <match url="(.*)" />
    <action type="Rewrite" url="http://localhost:5000/{R:1}" />
  </rule>
</rules></rewrite>
```

- Přidat HTTPS binding s certifikátem (doporučeno: [win-acme](https://www.win-acme.com/) – Let's Encrypt zdarma)
- Otevřít port 443 na firewallu

## 4. Zadání URL do Google Transit Portálu

Po ověření veřejné dostupnosti endpointů zadat v pipeline `dpmcb-ceskebudejovice-cz` → **Realtime data**:

- Vehicle Positions: `https://dpmcb.dpmcb.cz/vehiclepositions.pb`
- Trip Updates: `https://dpmcb.dpmcb.cz/tripupdates.pb`

Google začne pollovat každých ~30 sekund. Případné chyby ve formátu dat jsou viditelné v záložce **Issues**.