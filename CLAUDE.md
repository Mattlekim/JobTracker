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

## Filling an address in from where the phone is

The location button on `Layouts/NewJob`, `Layouts/NewCustomer` and `Layouts/QuickAddCustomer` all go through
`WorkTracker/AddressFromLocation.cs`. It fills in the street, town, area and postcode and deliberately leaves the
house number alone — a phone is only accurate to a few doors.

It used to work once per run of the app and then do nothing at all until the app was restarted. Nothing about the
button was wrong: `_asking` is one flag for the whole app, held from the first press until the work finishes, and
the work could stop without finishing — asking Android for a fix a second time can leave it listening for one that
never arrives. So the flag stayed set and every later press returned immediately.

Three things keep that from coming back, and all three are needed:

- `GetLocationAsync` is given a `CancellationToken` as well as the request timeout. The token is what actually
  gets control back; the last known fix is then used instead of nothing.
- The geocoder lookup is raced against `Task.Delay`, because nothing in it promises to come back either.
- `_asking` records *when* it was set and goes stale on its own, so no future hang can wedge the button for a
  session. A press while it is genuinely busy now says so rather than looking broken.

Alerts go through `Say`/`Confirm`, which do nothing when the page has gone. An alert put up on a page that has been
navigated away from never returns, and that would hang the caller — the very thing being fixed.

## Quotes

A quote is priced up work that has not been taken on. It is kept in `Job._Quotes`, saved to `quotes.rjt`, and never
goes near `_Jobs` — it is not due, cannot be done or paid for, and must not count as work anywhere.

`Layouts/Quotes` is the third page under the Work tab, next to the Overview and the List. Quotes were briefly shown
as a section at the bottom of the work list instead; they are not on that list any more, so nothing there needs to
know about them. `Job.AcceptQuote` is the only way a quote becomes work — it moves the same object across, keeping
its id, price and frequency, and sets the day it starts. `Job.DeleteQuote` throws one away.

`Job.Save` writes `quotes.rjt` every time, empty or not. It used to skip the file when there were no quotes left,
which left the previous file on disk, so the last quote accepted or deleted came back on the next start.
`Job.DeleteData` clears both lists for the same reason.

Quotes are added through `Layouts/NewJob` with `AddAsQuote` set, and `NewJob.SimplifyForQuote` cuts that form down
to what pricing work up actually needs: where it is, what it is, what it comes to, how often, notes and who to go
back to. The start date, estimated duration, starting balance, alternative price, separate customer address and the
whole messaging card are hidden — none of them have an answer until the quote is accepted, and each is already left
at the default the form sets, so a quote saves exactly as it did before. The link to an existing customer stays
put: quoting somebody already on the round is how a duplicate customer record gets made.

## Round figures

`Kernel/RoundStats.cs` works out everything `Layouts/Stats` (the fourth page under Work) shows, so the sums can be
checked with `KernelDebugger` and so one definition of *left to do* is used everywhere: not done, not cancelled, and
due today or before. Work booked in for a day still counts as left, because it is.

`ValuePerMonth` is what the round earns in a month with everything done on time. It is worked out per job from the
frequency against an average month (52.1775/12 weeks, 365.25/12 days) rather than four weeks, because thirteen
four-weekly visits happen in a year and not twelve. One offs contribute nothing — they are not coming round again.

The month by month figures come off `DateCompleated`, not `DueDate`: they are what was *done* in a month. Completed
jobs stay in `_Jobs` alongside the next visit they generated, so anything counting the round itself must skip
`IsCompleted` or it counts the same house twice.

## Booking a day in from the calendar

Tapping a day on `Layouts/CalenderView` picks it; double tapping opens the day's action sheet. Nobody double taps a
phone, so a day **still to come** that has work not booked in yet also puts a *Book All … In* button under the day
totals — one tap on the day, one on the button. Today is deliberately left out: today's work is being done, not
arranged, and a day already gone cannot be booked at all. The button counts only work that is not done, not
cancelled and not already booked, which is what `JobsToBookIn` returns.

`BookJobFormcs.BookForDate` is how a caller says which day the form should open on — it is used once and resets to
today, so a caller with no day in mind cannot pick up somebody else's. Without it the form opened on today and the
date had to be typed in again, which is wrong every time the day is already known.

## Duplicate customers

`Layouts/TidyCustomers` and `Kernel/CustomerMerge.cs` exist to clear up after a bug rather than to add anything.
Editing a job's details never picked up the customer the job already belonged to — the only place that read it is
`p_customerSelected`, which is suppressed while the picker is filled in — so every save fell through to the branch
that *adds* a customer, put the typed details on the new one and pointed the job at it. The original was left with
the balance and the payments but no work, which still counts towards the money owed on the work list.

`Customer.WithoutWork()` is what the page lists (quotes count as work, so a quoted customer is not offered up),
`LooksLikeSameAs` ranks the candidates address first, and `Merge` moves the payments, direct debit requests, bank
references and anything the kept record is missing before deleting the old one. The balance is the one thing it
cannot work out for itself — the duplicate was made with a *copy* of the figure, so adding the two would charge for
the same work twice — which is why the page asks whenever both records carry one and they differ.

## Bank statement imports

A statement is read once and then looked at from two sides:

- `Layouts/StatmentViewer` — money coming in, matched to customers as payments.
- `Layouts/StatementExpenses` — money going out, flagged as expenses or ignored.

`StatmentViewer` still owns the column setup (which column is the date, the reference, the amount, and now the
money out column) and remembers csv and pdf layouts apart. `ImportExport/StatementFile` picks and reads the file
for both pages. Both list their side as cards saying what will happen to each line before anything is imported —
keep the two looking and reading the same when either is changed.

The money in side reads the statement into `IncomingLine` once and both the list and the import run off that, so
what the list promises is exactly what the import does. `Payment.AlreadyRecorded` is the single definition of
"this one is already in", used by the badge and by `Payment.AddToCustomer`.

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

Because of that the whole Cloud Sync section on the settings page reads *Coming Soon* and is greyed out
(`IsEnabled="false"` on `sec_cloud`), rather than being hidden — hiding it only has people hunting for it. Take the
greying off in `Layouts/SettingLayout.xaml` when the real credentials go in.

## Experimental features

GoCardless is marked **Experimental** where a user meets it — the settings section heading, an orange badge inside
it, the toolbar item on `Layouts/ViewCustomerDetails` and the title of the action sheet that page puts up. The
`GoCardless` entry in the preferred-payment picker on `Layouts/NewCustomer` is deliberately *not* labelled: that
string is saved on the customer as the payment method, so changing the wording changes stored data.

## Saving exports to the device

Sharing hands a file to another app; `WorkTracker/DeviceFileSaver.cs` puts a copy where the device keeps downloads
so it can be found again without sending it anywhere. Android 10 and up goes through the MediaStore Downloads
collection (older phones write the file directly, after asking for the storage permission); Windows copies into the
user's Downloads folder without overwriting a file already there. iOS keeps each app's files to itself, so `CanSave`
is false there and sharing stays the only way out — anything calling this must cope with that.

The tax page's Export asks *Save To This Device* or *Share* when the platform can do both.

## Job types

`Job.JobNames` is the list of job types, edited on the settings page and saved with the settings. `DefaultJobName`
is the first of them and is what anything without a type falls back to: `Job.Load` fills a blank type in as the
file is read, the new job form picks it when a job's own type is not on the list any more, and Quick Add — which
never asks what the work is — gives it to everything it creates. A type that is on the job but no longer on the
list is left alone; it still says what the work is, and retyping it because somebody renamed an entry would lose
that.

**`Settings.Load()` must run before `Job.Load()`** (`AppShell`), because the job types come out of the settings
file. The other way round, work with no type gets the first *built in* type rather than the first of this round's
own. Only what is in memory is changed — the file catches up on the next save, like the other tidy ups done on load.

## Filtering the work list

Two things narrow `Layouts/WorkPlanner`, and they are not the same kind of thing:

- **What the list is kept to** — the date range on the filter panel, `MasterFilter`. Work due up to the end date
  and anything finished since the start one, which is what makes the list the work in hand rather than the whole
  round for ever. Booked work is not on this list at all; it has its own page.
- **A tag filter** — tapping the type, price, street, town or money tag on a job row. `SetTagFilter` takes the
  test off the job that was tapped rather than the words off its label, which is what a tag filter is: everything
  else like *this one*.

`FilterSource` is what a tag filter picks from, and it is deliberately **not** `MasterFilter`: it is the whole
round, minus what is finished. Tapping High Street to be shown three of its twelve houses, because the rest are
not due for a fortnight, is not what anybody means by tapping it.

The tag filters were switched off for a long time — `GetJobs` set `Filter = null` before it ever ran one — because
a list quietly showing a fraction of the round with no way back out is worse than no filter at all. That is what
`ShowActiveFilter` is for: while a tag filter is on, the bar above the list says what is being shown and how much
of it, with a Clear, whether the filter panel is open or not. **Do not let a filter be on with nothing on screen
saying so.** The bar's Clear takes off the tag filter only; the panel's Reset puts everything back, dates
included.

## Job tags

A tag says what *this time* of doing the job was like — front only, nobody in, the gate was locked. It is not
what the job is (that is the job type) and it is not a standing note about the house (that is `Job.Notes`), which
is why it lives on `Job.Tags` and why `Job.DeepCopy` deliberately does not copy it: the next visit is a fresh copy
of the job, so it starts with no tags and the finished visit keeps the ones it was given for good. That is what
makes the customer's history on `Layouts/ViewCustomerDetails` able to say which visits were like that — the whole
point of the feature. **Do not add `Tags` to `DeepCopy`**, or every visit from then on inherits the last one's.

They show on the job rows in the work list, the calendar and the booked work page, on the day heading of a booked
day, and in the customer's history. On the paper view they go under the note in the record's own colour rather
than as a filled tag — a column of filled tags down that page reads as ink blots — and they come off
`PaperItem.JobI3`, the visit whose mark is in the record column, which is the one that row is writing up.

`Job.TagNames` is the list offered when something is tagged, edited on the settings page and saved with the
settings exactly like `Job.JobNames`. It is only the list to pick from: taking a tag off it never changes a day
already worked. A tag typed in rather than picked is added to it (`Job.RememberTag`), so it only has to be typed
once — which is why anything that tags something saves the settings if the list has grown. Settings written
before tags existed have no `TagNames` element at all and read back as null, which keeps the built in list; an
empty list is a round that has deleted the lot on purpose and is left empty.

Tagging a *booking* means tagging the work on it. A booking is worked out from the jobs and never saved
(`Kernel/Booking.cs`), so it has nowhere of its own to keep a tag, and a tag kept there would be gone by the time
the history was looked at. `Booking.TagJobs`/`UntagJobs`/`TagsOn` do the work; a day tagged as a whole shows its
tags on the day heading in `Layouts/BookedWork` because its jobs are carrying them.

`Layouts/TagPicker` is the one place that asks which tag to put on, so tagging one house and tagging a whole day
ask the same question in the same words, and it saves whatever it changes. It is used from the booking form,
the booked work page and the work list. `Layouts/JobStatus` is the exception: it edits its own copy of the tags
and writes them on Save, like everything else on that page. It takes off only what was taken off *there* rather
than writing its list over the top, because ticking the job done in the same save puts the tag bar's tags on it
and clearing the lot first would take those straight back off.

### The tag bar

Tagging house by house will not happen while you are stood at a gate with wet hands, and a day is usually all the
same anyway — everything front only because of the weather, a whole street with nobody in. `Job.AutoTags` is what
the tag bar is set to, and **`Job.MarkJobDone` puts them on**. That is deliberately in the kernel rather than in
each of the places work can be marked done — the swipes, the paper view, the job's own window, the calendar — so
none of them can be the one that forgets. `UnMarkJobDone` takes them back off again, so clearing a job swiped by
mistake really is an undo.

`Controles/TagBar` is the bar itself, one control used by `WorkPlanner`, `BookedWork`, `CalenderView` and
`PaperView` rather than the same row written four times. It folds away to a single small button while nothing is
set, because most days nothing is — but once something *is* set the tags stay on show whether it is folded or
not. **Do not let it fold away over a tag that is still going on everything marked done**: that is the one thing
it must not do, and it is also what makes it safe to keep `AutoTags` in the settings file so the setting survives
the app being closed mid-round. All four bars show the same setting, so a change on one tells the others.

On the calendar the bar is pinned above the list instead of riding along in its header, where it would scroll off
the top of the page while it was being worked from.

## Versioning

`ApplicationDisplayVersion` and `ApplicationVersion` live in `WorkTracker/WorkTracker.csproj` and must both be
bumped for a Play Store upload. `BuildDate` is stamped into the assembly automatically at build time and surfaced
on the settings page, so it needs no manual update.
