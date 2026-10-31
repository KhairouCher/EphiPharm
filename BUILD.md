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
`EPIPHARM_DB_PATH` environment variable). Build it with Python for now:

```powershell
cd db
python -c "
import sqlite3
db = sqlite3.connect('epipharm.sqlite')
db.executescript(open('accounts_seed.sql').read())
db.executescript(open('schema.sql').read())
db.commit()
"
```

Then copy `db\epipharm.sqlite` into `src\Epipharm.App\bin\Debug\net48\data\`
(create the `data` folder if it isn't there yet).

This gives you the 6 accounts (new passwords — see the credentials file I
sent separately) and an empty schema. Importing the real stock/movement data
into this schema (not just the raw 1:1 DBF dump importer already has) is the
next piece of work — see MENU_MAP.md and the TODO in db/schema.sql.

## Running
F5 in Visual Studio, or run `Epipharm.exe` directly from the build output
folder. Log in with one of the accounts from the credentials file; you'll be
asked to set a new password on first login.
