# Building and running (Windows)

Written for your PC, not tested here — this cloud session has no Windows
.NET tooling, so this has not actually been compiled yet. Build it and tell
me the first error if any; I'll fix it from the message.

## Prerequisites
- Visual Studio 2022 (Community edition is free) with the
  ".NET desktop development" workload, **or** the .NET 8 SDK if you prefer
  the command line (it can still target `net48` for building, since this
  is a legacy WinForms project — but WinForms *design* only works inside
  Visual Studio on Windows).
- The project targets **.NET Framework 4.8**, which runs on Windows 7 SP1,
  10 and 11 without extra install on 10/11 (it's built in); Windows 7 needs
  the .NET Framework 4.8 installer if it isn't already there.

## First build
1. Open `EpipharmRebuild.sln` in Visual Studio.
2. Let NuGet restore `System.Data.SQLite.Core` (automatic on first build).
3. Build (Ctrl+Shift+B). Fix/report any error — this hasn't been compiled
   yet in this session.

## Setting up the database before first run
The app looks for `data\epipharm.sqlite` next to the .exe (override with the
`EPIPHARM_DB_PATH` environment variable). Needs Python 3 installed. Three
steps: import the real DBF files, migrate them into the app's schema, then
copy the result next to the .exe.

```powershell
# 1. Import the legacy DBF files into a raw 1:1 SQLite dump
python importer\import_dbf.py "C:\Users\KhairouCher\Downloads\EPIPHARM EPH DE CHERIA" raw.sqlite

# 2. Migrate that into the app's real schema (also seeds the 6 accounts)
python scripts\migrate_to_app_schema.py raw.sqlite db\schema.sql db\accounts_seed.sql epipharm.sqlite
```

Read what step 2 prints — it reports exactly what didn't migrate cleanly
(a few known gaps: product supplier/lab links, delivery/receipt service
links, two bad dosage-form codes — see the comments in db/schema.sql and
the commit history for why each one is left as-is rather than guessed).

Then copy `epipharm.sqlite` into `src\Epipharm.App\bin\Debug\net48\data\`
(create the `data` folder if it isn't there yet).

This gives you the 6 accounts (new passwords — see the credentials file I
sent separately) plus your real stock, products, movements, deliveries,
receipts and allocations.

## Running
F5 in Visual Studio, or run `Epipharm.exe` directly from the build output
folder. Log in with one of the accounts from the credentials file; you'll be
asked to set a new password on first login.
