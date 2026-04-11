# CLI: Running Against Prod

## Build

Always use a Release build. Never `dotnet run` against prod.

```
cd /media/dan/fdrive/codeprojects/LeoBloom
dotnet build Src/LeoBloom.CLI -c Release
```

## Run

```
LEOBLOOM_ENV=Production Src/LeoBloom.CLI/bin/Release/net10.0/LeoBloom.CLI <command>
```

## Config

- `appsettings.Production.json` in `Src/LeoBloom.CLI/`
- Database: `leobloom_prod`, user `leobloom_hobson`
- Password: `$LEOBLOOM_DB_PASSWORD` env var
- Logs: `/media/dan/fdrive/leobloomlogs/`

## Rebuild When

After any code change that affects the CLI or its dependencies.
The Release build is a point-in-time snapshot — it doesn't pick up
source changes until rebuilt.
