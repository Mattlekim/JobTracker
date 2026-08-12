# JobTracker

.NET MAUI app for tracking window-cleaning rounds: customers, jobs, bookings, payments, expenses and tax reporting.

## Projects

`JobTracker.sln` only contains the two live projects:

| Project | What it is | TFM |
| --- | --- | --- |
| `WorkTracker` | The MAUI app — this is the thing you run | `net9.0-android`, `net9.0-ios`, `net9.0-maccatalyst`, `net9.0-windows10.0.19041.0` |
| `KernelDebugger` | Console harness for exercising the domain layer without a UI | `net9.0` |

`Kernel/` holds the domain model (Customer, Job, Payment, WorkDay, SaveLoad, TaxReporting…). It has its own
`Kernel.csproj`, but `WorkTracker` does not reference it — it pulls the `.cs` files in as linked `<Compile Include="..\Kernel\...">`
items. Editing a file under `Kernel/` therefore changes both the app and `KernelDebugger`.

`UiInterface/` (net6.0) and `JobTracker/` (Xamarin) are older versions kept for reference. They are not in the
solution and are not built or maintained.

## Running the app on Windows

Either works:

```powershell
dotnet run --project WorkTracker\WorkTracker.csproj -f net9.0-windows10.0.19041.0
```

or F5 in Visual Studio with the `Windows Machine` profile.

### Why this was broken, in case it regresses

The project was configured to build Windows as an MSIX package while providing no way to launch one. Two settings
were out of step with the current MAUI template:

- `WorkTracker.csproj` had no `WindowsPackageType`, so it defaulted to MSIX.
- `launchSettings.json` used `"commandName": "MsixPackage"`.

`dotnet run` cannot deploy an MSIX, so it reported `A usable launch profile could not be located`, fell back to the
raw exe, and that died with `COMException 0x80040154 (REGDB_E_CLASSNOTREG)` from the Windows App SDK
auto-initializer because the process had no package identity. Visual Studio could not run the `MsixPackage`
profile either without the single-project MSIX tooling enabled, which is where the "doesn't know how to run the
profile" message came from.

The fix was to match what `dotnet new maui` generates today: `<WindowsPackageType>None</WindowsPackageType>` in the
csproj and `"commandName": "Project"` in `launchSettings.json`. Windows now builds and runs unpackaged.

If the two ever drift apart again, the giveaway is that the failure mentions a *profile* while the underlying error
is a COM class-registration failure — that combination always means packaged build, unpackaged launch.

Only revisit this if the app is ever shipped through the Microsoft Store, which would need MSIX packaging back.
Android packaging is unaffected by this setting.

## Building for Android

```powershell
dotnet build WorkTracker\WorkTracker.csproj -f net9.0-android -c Release
```

Release Android builds produce an `.aab` (`AndroidPackageFormat`) and are signed in CI by
`.github/workflows/android-apk.yml`. The keystores at the repo root (`keystore.jks`, `worktracker.keystore`,
`ci.keystore`) are what that workflow uses.

## Domain-layer changes

For anything that does not need the UI, `KernelDebugger` is much faster to iterate on:

```powershell
dotnet run --project KernelDebugger\KernelDebugger.csproj
```

## Tax years and the data files

Anything the taxman cares about is kept one file per tax year (UK, 6 April to 5 April — `TaxCalendar`):

| File | Holds |
| --- | --- |
| `expenses-<year>.rjt` | that tax year's expenses |
| `payments-<year>.rjt` | that tax year's income |
| `statements-<year>.rjt` | the bank statements imported for it |
| `receipts/<year>/` | its receipt photos |
| `statements/<year>/` | the statement files themselves |

`<year>` is the year the tax year starts in, so `expenses-2026.rjt` is 2026/27. Folders read as `2026-27`.

Everything else — customers, jobs, quotes, remembered payees, direct debits, settings — is global and never split,
because last year's figures are meaningless without the customers they came from.

`Kernel/YearlyStore.cs` does the file handling. The important part is `WriteIfChanged`: a year is only written when
its contents actually differ, so a finished tax year keeps its timestamp and cloud sync has nothing to send for it.
That is the whole reason for the split, so **do not** write per-year files unconditionally. For the same reason the
id counter is only stored in the current year's file — writing it everywhere would touch every year on every save.

Everything still loads into one in-memory list, so queries and pages are unaware of the split.

The single `expenses.rjt` / `payment.rjt` files from before this are migrated on load: read, split into years, then
deleted so nothing deleted since can come back. `CloudSync.PullLegacyFilesAsync` covers the other direction — a
device installed fresh against a Drive still holding the old files pulls them down once (guarded by a preference so
it never happens twice).

Backups are built by `ImportExport/TaxYearBackup`. Manual backup asks which tax years to include once there is more
than one (`Layouts/SelectTaxYears`); the tax page can save any year on its own with its receipts and statements.
The global files go into every backup regardless. Restoring is unchanged — the zip unpacks over the data folder, so
a one-year backup puts that year back and leaves the others alone. Backup files are written to the cache folder,
not next to the data.

## Receipt photos

`WorkTracker/ReceiptPhoto.cs` scales every receipt photo down and re-encodes it as a JPEG before it is written into
the receipts folder, because each one is kept for as long as the records are, backed up, and synced to Drive.
It uses `Microsoft.Maui.Graphics` (`PlatformImage` everywhere except Windows, where the same job is done by
`W2DImageLoadingService`), so there is no extra dependency. If the photo cannot be decoded the original bytes are
written instead — a receipt is never lost to save space.

The size and quality are on the settings page, along with a button that goes back over photos taken before this
existed. That one only replaces a photo when the new file really is smaller.

## Bank statement imports

A statement is read once and then looked at from two sides:

- `Layouts/StatmentViewer` — money coming in, matched to customers as payments.
- `Layouts/StatementExpenses` — money going out, flagged as expenses or ignored.

`StatmentViewer` still owns the column setup (which column is the date, the reference, the amount, and now the
money out column) and remembers csv and pdf layouts apart. `ImportExport/StatementFile` picks and reads the file
for both pages.

Nothing is ever imported twice. Every outgoing gets an id built from the date, the normalised payee and the
amount (`Expense.StatementReference`), stored on `Expense.ExternalReference`, so re-importing the same statement —
or the next one, which overlaps it — finds the expense already there. Identical transactions on the same day are
told apart by an occurrence number, so two identical fuel stops still count twice.

The statement file itself is kept (`Kernel/StatementRecord.cs`), filed under the tax year it covers, so the
evidence the figures were read off travels with them into backups and the cloud. A statement that straddles
5 April is kept in **both** tax years — one record and one copy of the file each — so backing up or handing over a
single year still has all of its evidence, rather than half of it sitting in a year nobody asked for. Each record
holds its own year's date range and transaction count alongside the whole file's, which is what `Crossover` keys
off. It is copied once the date column is known — which is why `StatmentViewer.ArchiveStatement` runs there and not
at the file picker. `Layouts/KeptStatements` lists them.

`Kernel/ExpenseRule.cs` is what makes recurring bills look after themselves: flagging an outgoing as an expense
(or ignoring it) remembers the payee, and the next statement logs it automatically with the same category and
note. Payee text is matched through `StatementText.PayeeKey`, which strips the reference numbers and the
"direct debit"/"card payment" wrapping the bank puts around the name. Rules are editable on the
`Layouts/ExpenseRules` page, and live in `expenserules.rjt` alongside the other data files.

## Google Drive sync

`WorkTracker/CloudSync.cs` syncs the `.rjt` data files and receipt photos with the user's Drive `appDataFolder`
over an OAuth authorization-code + PKCE flow (browser sign-in, loopback redirect).

`BuiltInClientId` and `BuiltInClientSecret` are currently stand-ins containing the marker `REPLACE-ME`. Anything
carrying that marker is treated as "no credential supplied", which keeps the paste-your-own-key fields visible on
the settings page. Dropping the real Desktop-app credentials in over those two constants is the only change
needed — `HasBuiltInClient` then flips to true and the settings page collapses down to a single Connect button.

Until then, sign-in cannot complete: the values are not registered with Google, so pressing Connect shows the
setup instructions instead. To test the real flow before the app has its own credentials, create a Desktop-app
OAuth client and paste the Client ID and Secret into the settings fields.

## Versioning

`ApplicationDisplayVersion` and `ApplicationVersion` live in `WorkTracker/WorkTracker.csproj` and must both be
bumped for a Play Store upload. `BuildDate` is stamped into the assembly automatically at build time and surfaced
on the settings page, so it needs no manual update.
