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
items. Editing a file under `Kernel/` therefore changes both the app and `KernelDebugger`. **A new kernel file
must be added to `WorkTracker.csproj`'s link list by hand** or the app builds without it.

`Job` is one partial class in two files with two jobs: `Job.cs` is the round — dates, money, the rules for
what happens to a visit — and `JobDisplay.cs` is the half a page binds to — colours, formatted strings, the
tick boxes, the fold-out state. The rule for what goes where: if deleting a member could only ever break a
screen it belongs in `JobDisplay.cs`; if it could break a figure, a file or a rule about the work it belongs
in `Job.cs`, and nothing in `Job.cs` should ever need a colour.

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

Everything else — customers, jobs, quotes, remembered payees, bank accounts, direct debits, settings — is global and never split,
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

## When the data was last changed

`Kernel/DataStamp.cs` writes the date down **with the data** (`datastamp.rjt`), and it is there because a file's own
timestamp cannot answer the question. The moment the round is copied anywhere — into a backup, out of a zip, down
from Drive, off one phone on to another — every file is stamped with the day the copy was taken, and that says
nothing about how old the work in it is. A backup made this morning out of a round nobody has touched since March
is a **March round**, and putting it back over a round worked all summer is the one mistake there is no undoing.

It is kept a part at a time — the jobs, the customers, the payments, the settings — because "the jobs were last
changed in March" is worth more than one date for the lot; `LastModified` is the newest of them and `LastChanged`
says which part it was. The settings page shows it above Create Backup.

Two rules make it mean anything, and both are easy to undo by accident:

- **A save into another folder does not move the date.** That is a copy of the round being built, not the round
  changing, so `Touch` sees the folder is not `HomeFolder` and *copies* the round's date into it instead
  (`TaxYearBackup.Create` also asks for it outright, since a backup with every file rewritten a second ago has
  nothing else to go on). Move the date there and every backup would claim to be as new as the day it was made,
  which is the whole thing this exists to stop.
- **A save that changes nothing does not move it either.** That is why the whole-file savers — customers, jobs,
  quotes, expense rules, bank accounts, direct debits, balance adjustments, settings — now go through
  `YearlyStore.WriteIfChanged` like the per-year files always have, and only stamp when it says it wrote. Left
  writing unconditionally, opening the app would count as changing the round and every backup ever made would read
  as out of date the moment somebody looked at it. It also stops the cloud being handed an unchanged `jobs.rjt`.

`HomeFolder` is where the round lives — null, the data folder, for the app. It is settable so the self test can
work in a folder of its own rather than stamping the real data folder of whatever machine it runs on.

A round worked for years before any of this has no stamp in it. `SeedFromTheFiles` dates each part from the newest
file it is kept in, so the first backup off an existing phone still carries a real date rather than nothing.

## What a backup would change

`Kernel/DataSnapshot.cs` counts what is in a backup — jobs, jobs done, quotes, customers, payments and expenses,
with the money for the last two — **straight out of the zip**, without unpacking it and without touching anything
loaded. `Current()` counts the same things off the app's own lists, and `Difference` says the two side by side.

That is what `BackupRestore.RestoreAsync` puts behind **Show What Would Change** on the restore question, which is
asked in a loop so looking at the figures does not throw the answer away. Before that, if the backup's date is
older than the device's, it says so on its own and has to be got past: *restoring is not a merge* is easy to read
past, *this backup is four months older than what is on this phone* is not.

Two things it must keep getting right:

- **Money is compared only for the tax years the backup holds.** Restoring unpacks the zip over the data folder, so
  a one-year backup puts that year back and leaves the others alone — counting all of them against one year's worth
  would say a thousand payments were about to vanish when not one of them is going anywhere. `TaxYears` is read off
  the `payments-<year>.rjt` / `expenses-<year>.rjt` files actually in the zip and `Current` is given the same list.
  A backup with no money files in it says so instead of drawing the lines.
- **The figures come from the files, not from a count written down.** A stored count would be a second version of
  the truth and could disagree with what is about to be restored. Only the *date* is taken from the stamp, and a
  backup made before the stamp existed falls back to the newest file in the zip, marked as a guess where it is said
  out loud.

`DataStamp.Load()` runs again at the end of a restore: the device's data is the backup's now, and so is its date.

## Opening a backup

A backup is a `.rbf` (a zip, built by `ImportExport/TaxYearBackup`). `ImportExport/BackupRestore` is the only
thing that puts one back, from either of the two ways one is reached: the picker on the settings page, and the
phone handing the app a file that has been **opened** — from a file manager, an email or the downloads list, which
is how a backup normally reaches a new phone. Restoring replaces everything on the device, so it asks first, and
it says to restart afterwards because the pages already built are still showing what was there before.

The Android side is `ACTION_VIEW` intent filters on `MainActivity`, and it takes **two kinds** of them because
what arrives is not the file — it is a `content://` uri belonging to whichever app sent it, and only some of those
carry the name.

- **Name in the path** (the storage provider, a `file://` uri): matched by `pathPattern`. Written three times,
  once per possible dot in the path, because Android's pattern matching does not back up — `.*\.rbf` takes
  everything and then looks for the dot, so a path with a dot earlier in it never matches. `DataHost` has to be
  set or Android ignores the path of the filter altogether.
- **No name in the path** (the downloads list, MediaStore): `content://…/downloads/1000000123` has nothing to
  match on, so these are taken on **type** alone — `application/octet-stream`, which is what a file the phone has
  no type for is called, plus the zip types because a `.rbf` is a zip and anything that looks inside will say so.
  Once the file can be read it is told apart by name — and a nameless `.rwk` by its own magic bytes, which a
  provider that drops the display name cannot take away. A file that is neither is **said out loud** rather than
  ignored (`WorkShareOpen.UnreadableFileWasOpened` → the Opened File alert in `AppShell`): it used to be dropped
  silently, and "opened the file, app loads, nothing happens" was exactly how that was reported. Failures in the
  copy land in the crash log, so a silent path cannot come back unnoticed.

**Getting the bytes takes two goes** (`MainActivity.CopyTheFileOut`). `openInputStream` is the ordinary way and
what a file manager or the downloads list answers, but it says only *file not found* about a document the sending
app is not actually holding — one still in Drive, or on an email that has never been downloaded. The provider has
a record of the file and no bytes to give. Asked for as a **typed asset** (`OpenTypedAssetFileDescriptor`) it
fetches the file first, which is what that call is for and what gets a backup off an email on to a new phone. That
was a real crash log: `FileNotFoundException` out of `ContentResolver.OpenInputStream` on a Pixel, and the user was
told the file was *not something Work Tracker recognises* — which sent them looking at the backup rather than at
where it was kept. **The two failures are said differently now**: `WorkShareOpen.FileCouldNotBeFetched` for a file
the sending app would not part with (with what to do about it — save it to the phone first),
`UnreadableFileWasOpened` for one that is genuinely not ours. Both build the whole sentence, because `AppShell`
cannot tell them apart by the time it shows the alert. What was tried goes in the crash log with the uri's scheme
and authority: every attempt is caught and worked past, so there is nothing else to go on afterwards.

The second kind is the one that matters: a backup off an email or out of Drive lands in the downloads list, and
**with only the pattern filters the app never appeared in the chooser at all**. Do not take them out. For the same
reason, a backup saved with *Save To This Device* is written as `application/octet-stream` rather than left for
the phone to guess at — that is what makes it openable again from where it landed.

**A backup is told by what is in it, not by what it is called** (`BackupRestore.ContentsLookLikeBackup`, and
`IsBackup` which tries the name first). The whole point of the second filter is that the uri carries no name, so
insisting on one there refused exactly the backups that reach a new phone: a name is what a provider is free to
drop, and both the routing in `MainActivity.TakeTheFile` and the guard at the top of `RestoreAsync` used to turn a
nameless one away as *not something Work Tracker recognises*. A `.rbf` is a zip of the data folder, so any of the
app's own files inside it (`*.rjt`, `settings.txt`, `receipts/`, `statements/`) is proof enough, and a `.rwk` is
not a zip at all so there is nothing for the two to be confused over. **Do not put the name back as the only
test.**

**The file is looked at before it is claimed.** `TakePending` clears what it returns, and the offer used to take
it and *then* look for a page — but on a cold start the file is what opened the app, so there is often no page
with a handler behind it yet, and an alert on one of those either throws or never comes back. Either way the file
had already been thrown away: the app opened, nothing was said and nothing was restored, which is exactly how it
was reported. So `AppShell` peeks (`PeekPending`), waits for somewhere to ask (`WaitForSomewhereToAsk` /
`ReadyPage`), and only takes the file once there is a page — leaving it pending for a later navigation otherwise.
`OfferPendingBackup` and `OfferPendingShare` are `async void`, so both now catch: an exception out of one of those
is the app going down, and it was going down on the alert.

Two more things that made it come and go. The `Opened` events are static and a shell can be built more than once
(the crash log page builds a fresh one), so subscribing per shell left every shell ever built listening and the
oldest — long off screen — answered first and claimed the file; `AppShell.HookFileOpening` subscribes once and
asks whichever shell is current. And `MainActivity` marks an intent once its file has been taken off it, so a
file is not offered again every time Android hands the same intent back. The mark is what does that on its own —
`OnNewIntent` deliberately does **not** call `SetIntent`, because API 35 binds it as
`SetIntent(Intent, ComponentCaller)` with no one-argument form, and that method does not exist on anything older
than Android 15.

`MainActivity` is **`SingleTask`** so a file opened while the app is running reaches `OnNewIntent` rather than
starting a second copy of the app. It must not be `SingleTop`: a MAUI app has one window and it belongs to one
activity, so a second one takes the app down with *"This window is already associated with an active Activity"* —
and `SingleTop` only reuses the activity when the intent lands on the task it is already on top of, which a file
opened from a file manager does not. What arrives is somebody else's `content://` uri, readable only while that intent lives
and with no real path behind it, so it is copied into our cache first — on a background thread, because a backup
carries the receipt photos and can be big.

A file can arrive before there is anywhere to ask: on a cold start it is what opened the app. `BackupRestore`
holds it (`TakePending`) and `AppShell` offers it when it has a page — both when the file arrives and on the first
navigation, with `_askingAboutBackup` making sure the same file is not offered twice.

Windows cannot register a file type without an installer, so `CheckCommandLine` covers *Open With* instead.
**iOS is not done**: it needs `CFBundleDocumentTypes` in `Info.plist` and an `OpenUrl` override in `AppDelegate`.

## Sending a work list to someone

A handful of jobs can be handed to another copy of the app — a mate covering a week off — as a `.rwk` file.
`Kernel/WorkShare.cs` is the whole of the kernel side: the file format, the PIN encryption, the sender's key
store and the receiver's extra-work state. It is covered by the KernelDebugger self test
(`dotnet run -- selftest`), which is the fastest way to check a change to it.

**The file** is a small plain header (magic, version, a kind byte saying whether it is going out or coming
back, and a random key as a guid) with everything else — the jobs, addresses, prices, numbers — gzipped and
AES-encrypted under keys derived from a PIN (PBKDF2), with an HMAC over the ciphertext so a wrong PIN and a
damaged file are both turned away. **The key is unencrypted on purpose**: it is how a return finds its own
record on the sender's phone. The sender keeps `{key, PIN, worker name tag}` in `sentwork.rjt`
(`SentWorkRecord`), so a returned file opens itself without the PIN being typed again. That store is written
AES-scrambled under a key baked into the app — obfuscation against a file browser, not security, and the code
says so; do not mistake it for more. Records are pruned three months after the work comes back; one that never
came back is kept.

**Sending is opt-in** (`Settings.EnableWorkSharing`, the *Work Sharing* section on the settings page, off by
default): most rounds are one person, and the buttons would only be in the way. The setting only shows or
hides the three sending entry points — receiving is never gated, because somebody handed a work list needs no
setting to open it, and *Work Sent Out* goes by whether any send is remembered rather than by the setting, so
work already out can still be cleared after turning it off.

**Sending** is on the work list's selection toolbar (*Send To Someone*), on the **Day ▾** menu of a day on
`Layouts/BookedWork`, and on the calendar day's action sheet (*Send Booked In Jobs To Someone*) — a booked
day is the natural parcel to hand over. All three land on `Layouts/SendWork` with a list of jobs; the day
ones send the day's outstanding work (done and cancelled stay home), and sending changes nothing about the
booking. `Layouts/SendWork` asks what travels with them — prices, notes, phone numbers, and *allow them to collect* (which forces prices
on, since collecting means knowing what to collect) — plus the worker's name tag and the PIN. Anything not
ticked is simply never put in the file. The one thing sending changes on the sender's round is a tag: each job
that went out is tagged `Sent To <name>` (`WorkShare.SentTag`, the one definition, because the return has to
take off exactly what the send put on), so the lists say which work is with somebody. It goes on through
`Job.AddTagQuietly` — on the visit but **not** on the tag picker's list, which would otherwise fill up with
worker names — and *Update My Work* takes it off every job in the return, marked or not, because the work is
back home either way. The name is read back off the tags (`WorkShare.OutWith`) wherever a whole day is
titled: a booked day that has been sent says *With \<name\>* on its day heading on `Layouts/BookedWork` and
on the work list's booking summary row, so a week planned out and handed over says whose hands each day is
in at a glance. Work that is out is **not offered for sending again** (`WorkShare.IsOut`): two copies of the
same job with two people ends with the house cleaned twice or not at all, so the day menus drop their send
option once the day is out, and the work list says which picked jobs are already with somebody rather than
quietly sending fewer than were picked. Not every send comes back as a file, so **Work Sent Out** (on the work list's toolbar
while any send is remembered — `Layouts/SentWorkList`) lists every send and clears one by hand. Clearing
changes nothing on the other phone; the two buttons differ only in what this phone forgets: *Take The Sent
Tags Off* keeps the record — and the PIN on it — so a late return can still open itself, while *Forget This
Send* drops both, after which its return can never be opened here, which the page says plainly before doing
it. Tags are cleared by worker name (`WorkShare.ClearSentTags`), so two sends to the same name clear
together.

**Receiving**: `.rwk` opens with the app exactly like a `.rbf` — the same two kinds of Android intent filter
(see *Opening a backup*; `MainActivity.TakeTheFile` routes by extension to `ImportExport/WorkShareOpen`, the
`.rwk` twin of `BackupRestore`'s pending-file holding). `AppShell.OfferPendingShare` reads the plain header
and routes: a *sent* file is offered as extra work (`WorkShare.TakeOnExtraWork` copies it, still encrypted,
to `extrawork.rwk`); a *returned* file is matched by key to its record, decrypted with the stored PIN and
pushed onto `Layouts/ReturnedWork`. A return with no matching record can only say so — the PIN for it lives
on the phone that sent it. A *sent* file whose key **is** in this phone's records is the phone's own sent
work: it is named for what it is and offered as a look at what was sent (`ReturnedWork` with `sentPreview`,
same cards, no update button) rather than as extra work — taking on your own round as somebody else's would
only end in a muddle, and the stored PIN means the look costs nothing.

**Extra work** (`Layouts/ExtraWork`) asks for the PIN every time it is entered and holds it only while the
list is open: every mark is re-encrypted straight back into `extrawork.rwk` with it, and leaving forgets
both. While it is open the tab bar is cut down to *Extra Work*, *My Work* and *Settings*
(`AppShell.EnterExtraWork` / `LeaveExtraWork` toggle `Tab.IsVisible`), so the phone's own round is out of
reach; *My Work* is a gate (`Layouts/MyWorkGate`) that warns the PIN will be wanted again, and only shows
when the phone has any work of its own. On the normal tabs, a squeegee tab (`tab_extraShortcut`) is the way
back in, and only shows while extra work is on the phone. **It takes the settings tab's slot rather than
being a sixth tab**: Android's bottom bar shows five and pushes the rest behind a More tab of its own, which
is where the squeegee — the thing that has to be one tap away — ended up. So while the squeegee is out,
`RefreshShareTabs` retitles the money tab *More* (icon `moretab.png`, a black-on-light copy of the white
swipe-action `more.png`), the settings tab hides, and `sc_moneySettings` — a second door to `SettingLayout`
declared inside the money tab — shows as its fourth page. Tapping More still lands on Payments, so the money
pages do not move; settings is one tap further away, and it is the tab that can afford that. The two
squeegee tabs both host `ExtraWork`, and settings has two doors for the same reason; the shell must never be
left standing on a tab or page that has just been hidden, which is why Enter/LeaveExtraWork make the
destination visible and current *before* `RefreshShareTabs` hides anything, and why the swap inside
`RefreshShareTabs` opens whichever settings door is taking over before it closes the other. Work there can be marked done, skipped and tagged but **not
cancelled** — the round is not that phone's to change — and *Paid* only appears when the sender allowed
collecting. *Return Work* writes the same list back out with the kind byte flipped, same key, same PIN.

**The return** (`Layouts/ReturnedWork`) shows what came back and only touches the round when *Update My Work*
is pressed: done goes through `MarkJobDone`, skipped through `WorkPlanner.MarkJobSkipped` (so a booked day is
put right too), paid through `MarkJobPaid` — done before paid, because paid needs the completed job's balance
behind it. Everything the worker touched is tagged with their name tag as well as any tags they added, which
is what the customer's history says about who was there. Jobs are matched by the sender's own job id carried
in the file; one deleted since it was sent is reported, not guessed at.

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

## Filling a number and an email in from the phone's contacts

`WorkTracker/ContactFill.cs` opens the phone's own contact picker and hands back the name, number and email, so a
customer taken down on the doorstep is not typed in twice. A contact with more than one number or email is asked
about rather than guessed at, and a field the contact has nothing for is left as it stands, so it can be used to
top up somebody half filled in. The contacts API offers no postal address, so the address is still typed (or
taken from where the phone is standing, above). It needs `READ_CONTACTS` in the Android manifest, and it says so
plainly when the device cannot pick a contact at all — Windows cannot.

It is on **`Layouts/NewCustomer`**, **`Layouts/QuickAddCustomer`** and **`Layouts/NewJob`**. On the job form the
name is only filled in when there is nothing there yet: unlike the other two, that form is also how an existing
customer is edited, and going to your contacts to put a number on somebody you already have does not mean asking
for the name you gave them to be replaced by whatever the contact is filed under.

## Texting and emailing the customers

`WorkPlanner.TextCustomers` sends **one text per customer** rather than a group message: a group message shows
every customer each other's number and cannot say anything personal, like what they owe.

`Sms.ComposeAsync` only *starts* the messaging app — it comes back as soon as that app is on screen, long before
anything has been sent. Running a list straight through it therefore fired every message off at once, each
opening the messaging app over the last, and only the final one was ever left in front of anybody: a round texted
the night before went to one house. **Never loop over `ComposeAsync` without waiting for the user in between.**
The wait is an alert offering the next customer, which cannot be answered until the messaging app has been left
and Work Tracker is back in front — and it doubles as the way out of a queue of texts part way through.

**The page they are asked on has to still be there when they are asked.** The whole run of texts is a queue of
alerts, and an alert put on a page that has been navigated away from does not fail — it never comes back, because
the handler that would have shown it has gone, so whatever is waiting on the answer waits for ever. That is what a
booked-in day with twelve customers to tell went out as: **nobody texted and nothing said about it**.
`BookJobFormcs.bnt_Confirmed` called `MsgCustomers` without waiting for it — an `async void` cannot be waited for —
and then popped the form. The first alert had already been asked for, so it appeared and could be answered; every
alert after it went nowhere. So **anything that offers messages must be awaited, and the page must not be taken
away until it is finished** — `MsgCustomers` hands back a `Task` for exactly that reason, and the confirm button
guards against being pressed twice now the form stays up through the whole queue.

`TextCustomers` and `EmailCustomers` settle where to ask before anything else (`PageToAskOn`, the same
`Handler?.MauiContext` test `AppShell.ReadyPage` makes), falling back to whatever the shell is standing on. That
is not the fix — the caller staying put is — it is so that getting it wrong again cannot be **silent**, which is
the thing that made this one so hard to notice.

`EmailCustomers` is one message with everybody in **Bcc**, so it opens the mail app once and needs none of this.
Bcc rather than To for the same reason the texts go one at a time.

`HaveBeenText`/`HaveBeenEmailed` record that the message was *put in front of somebody* — neither app tells us
whether it was actually sent.

Tapping the number or the email on `Layouts/ViewCustomerDetails` is **not** either of those. It is somebody
wanting a word — the gate was locked, they are running late — so it opens the messaging app with nothing written
in and composes directly rather than going through `TextCustomers`/`EmailCustomers`. Those two fill in the night
before wording and then mark the job as told, and a job marked as told is left out of the next round of notices,
so a quick word from this page would have quietly cost that customer the message that actually matters.

## The pages under Work

**Paper**, **List**, **All Jobs**, **Quotes**, **Stats** — in that order in `AppShell.xaml`. Paper is
`Layouts/PaperView`, and the tab used to be called Overview.

All five work pages — and the booked work and calendar pages — have the classic pull-down-to-refresh
(`RefreshView` around each page's one `CollectionView`), which builds the page again from the jobs, booked
days included. Each handler puts `IsRefreshing` back to false in a `finally`, because a throw part way
through a rebuild must not leave the spinner going round for ever.

Its **route is still `work_overview`**, and it stays that way. `AppShell_Navigated` writes the `WorkTabView`
preference off that route, so renaming it would lose which work view reopens for everybody already using the app.
A tab's title is what somebody reads; its route is what the data was written against, and the two do not have to
match. The page's own nav bar still says *Job List*.

## Quotes

A quote is priced up work that has not been taken on. It is kept in `Job._Quotes`, saved to `quotes.rjt`, and never
goes near `_Jobs` — it is not due, cannot be done or paid for, and must not count as work anywhere.

`Layouts/Quotes` is the fourth page under the Work tab, next to Paper, the List and All Jobs. Quotes were briefly shown
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

## The whole round, one row per job

`Layouts/AllJobs` is the third page under Work, between the List and the Quotes. It answers the one question
neither of the other two can: **what have I actually got?**

The work list is the work in hand and only reaches a fortnight ahead (`ResetDateFilter`), and the paper view is
the sheet you take out with you. Neither is the round. On top of that `_Jobs` keeps **every visit of a house** —
the clean done last month, the one before it, and the one still to come are three entries for one house — so a
list of the jobs is not a list of the round either.

So this page shows **one row per job**, found through `Job.SameJobKey`: the `BaseJobId` every visit is copied
from, falling back to the job's own id when an old file left it without one. Falling back matters — keying
everything with no base id together would collapse a whole round into a single house. The visit shown is the one
**next due** (`Job.NextDue`, due date first and the id to break a tie), because that is what the house is next
wanted for. `RoundStats` picks the same visit through the same two calls, so the page and the figures cannot
disagree about how many houses there are.

A house is on the round while it has work outstanding, so finished and cancelled visits are not listed, and a
finished one off is not on the round any more and is not listed either.

Each row says the price, when it is next due (and how far past it is), how often it comes round, which round it is
on and what the customer owes. Tapping one opens the same window the info button on the work list opens.

It is grouped **round, then area, then town, then street** — headings rather than indents, because a phone has no
room to indent four deep. A level the round has nothing to say about draws no heading at all rather than a row of
blanks, which is most rounds and the area. Grouping keys come off the **real** address (`Address.Area`,
`Address.City`, `SortStreet`) while the headings are drawn from the display names, so screenshot mode changes what
is on screen and not what groups with what — see *Screenshot mode*.

The whole list, headings included, is one flat `ItemsSource` on a single `CollectionView` (`AllJobsRow`, either a
heading or a house). A round is hundreds of houses; built as a stack of views in a `ScrollView` the way the Quotes
page is, it would not virtualise.

### Getting here from the figures

**Tapping a round on the stats page opens this page cut down to that round**, and the No Round row goes to the work
that is not organised yet the same way. A figure that raises a question — twelve houses on a round you thought had
twenty — is no use without the houses behind it, and hunting them out of the work list is not something anybody
will do when that list only reaches a fortnight ahead.

`AllJobs.ShowRound` takes the round and `AppShell.ShowAllJobs` moves to the page. The round is held in a static
that is **taken rather than read**, like `BookJobFormcs.BookForDate`, so a later visit to the page does not pick up
somebody else's question. **null is what says nobody has asked**: blank is a real answer, because it is the work on
no round. `ShowRound` also builds the page there and then when it has been opened before, so the round is on screen
the moment the tab changes rather than depending on the navigation to say so.

While one round is showing, a bar above the list says which with a **Show All** beside it — the same rule the work
list's tag filters follow: *do not let a filter be on with nothing on screen saying so*. The round headings are
left off while it is on, since the bar already names the round.

## Round figures

`Kernel/RoundStats.cs` works out everything `Layouts/Stats` (the fifth page under Work) shows, so the sums can be
checked with `KernelDebugger` and so one definition of *left to do* is used everywhere: not done, not cancelled, and
due today or before. Work booked in for a day still counts as left, because it is.

**Two things are counted here and they are counted differently on purpose.** What is *left to do* is a number of
**visits**, the same as the work list counts it — two visits of a house both due are two jobs and both are on the
list. How big the **round** is is a number of **houses**, worked out a job at a time through `Job.SameJobKey` (the
`BaseJobId`, or the job's own id when an old file left it without one). `_Jobs` keeps every visit of a house, and a
house is one house however many visits of it happen to be outstanding. `HousesOnTheRound`, `ValueOfTheRound`,
`MinutesForTheRound` and `ValuePerMonth` are all per house; `HousesLeft`, `ValueLeft`, `MinutesLeft` and
`HousesOverdue` are per visit.

`ValuePerMonth` is what the round earns in a month with everything done on time. It is worked out per job from the
frequency against an average month (52.1775/12 weeks, 365.25/12 days) rather than four weeks, because thirteen
four-weekly visits happen in a year and not twelve. One offs contribute nothing — they are not coming round again.

The month by month figures come off `DateCompleated`, not `DueDate`: they are what was *done* in a month. Completed
jobs stay in `_Jobs` alongside the next visit they generated, so anything counting the round itself must skip
`IsCompleted` or it counts the same house twice.

**A clean that was done counts even though the job has been cancelled since**, and the test for cancelled therefore
comes *after* the test for completed rather than before it. Cancelling says the house is not being cleaned any
more — it does not say that clean never happened, and `Job.CancelJob` gives the customer none of it back, so the
money is real. Dropping them left a month's takings short of the same days added up on the calendar, which has
always counted them (`CalenderView.PopulateDays`, which removes only `HaveCanceled && !IsCompleted`). A round that
loses houses is exactly when this shows.

`TaxReporting` counts income the same way for the same reason: left out, a round that has lost houses under-declares
what it earned. The rule is one line in three places and they have to agree — the calendar, the stats page and the
tax page are all adding up the same work.

`RoundStats.ByRound` runs the same sums a round at a time, which is the **By round** card on the page. Both go
through `Build`, so a round's figures and the totals above them cannot be worked out differently. The card hides
itself when no work is on a round, where it would only repeat the cards above it.

**A round is grouped by the job, not by the visit** (`Job.RoundsOfEveryJob`, the same rule
`SaveLoad.FillRoundsDownTheJob` uses on load: the round off the last visit that names one). Read off each visit on
its own, a house whose finished visits were left on no round — which is what an old file looks like, and what the
load-time repair cannot reach without a `BaseJobId` — went into two groups at once, and the group made of the
finished visits drew a **No Round row with no houses, no time and no value in it** while every house really was on
a round. That is the bug this replaced; do not go back to reading `Job.Round` off each visit here.

For the same reason `ByRound` **drops a round with no work left on it**. A round is a patch of work you have, so a
group holding nothing but finished and cancelled visits is not one — it could only ever draw a row of noughts. The
per-round money owed is counted off the work that is left rather than off every visit ever done, so a customer
whose houses have since moved to another round is not still counted against this one.

Each row on that card is a **way in to the round**, not just a figure — it goes to `Layouts/AllJobs` showing that
round's houses. See *Getting here from the figures* above.

**A round is asked about differently from the work in hand.** The cards above the card are today: what is left,
what is overdue, what has been done. A round is a patch of the work you either have or you do not, so the card
says how big it is and nothing else — how many houses (`HousesOnTheRound`), how long they all take
(`MinutesForTheRound`), what they come to (`ValueOfTheRound`, one time round rather than per month) and what is
owed on them. None of it moves about as the week is worked, which is the point.

What is owed is the one figure that is not simply split up: a balance belongs to a customer, not to a job. The
round as a whole counts **every** customer in debt, because somebody who owes money with no work left still owes
it; a single round counts the customers with work on that round, each once however many houses of theirs are on
it. The month by month figures are the round's takings as a whole and are not broken up at all.

## Booking a day in from the calendar

Tapping a day on `Layouts/CalenderView` picks it; double tapping opens the day's action sheet. Nobody double taps a
phone, so a day **still to come** that has work not booked in yet also puts a *Book All … In* button under the day
totals — one tap on the day, one on the button. Today is deliberately left out: today's work is being done, not
arranged, and a day already gone cannot be booked at all. The button counts only work that is not done, not
cancelled and not already booked, which is what `JobsToBookIn` returns.

A booking can be taken off again from all three places it can be looked at: the booking summary row on the work
list, the day's action sheet on the calendar, and the **Day ▾** menu on each day heading in `Layouts/BookedWork`
(which also holds Tag The Work and Change The Date — three buttons across the top of a day leave the date itself
nowhere to go on a phone). They all go through `WorkPlanner.CancelBooking`, which takes any list of jobs.
Cancelling a booking is **not** cancelling the work: the jobs go back on the round, due as they were, and
anything already done stays done.

### How much of a day is done

`Kernel/DayProgress.cs` is the one answer to "how far through this day am I", and both pages that show a day read
it: the day headings on `Layouts/BookedWork` and the day panel under the calendar. They each used to work it out
for themselves — two copies of the same loop with two copies of the same wording under them — which is two chances
to word the same day differently.

It says it two ways, because they are two questions:

- **In houses** — `CountText`, *3 of 12 done, 9 left*.
- **In money** — `ValueText`, *£45.00 of £120.00 done*. This is the one the count cannot answer: eight houses of
  twelve is not two thirds of the day's money when the four left are the expensive ones. A day with nothing done
  yet says *£120.00 to do* and a finished one says *£120.00 done*, so the chip always answers rather than
  disappearing.

On the calendar it goes **inside the existing Jobs chip** (*Jobs £45.00 of £120.00 done*) rather than in a second
one next to it, which would only carry the same total round again. On the booked work page it is a chip of its own
beside the count, since that page has no money on it otherwise.

**A clean that was done counts even though the job has been cancelled since**, so completed is tested *before*
cancelled — the same order `RoundStats`, `CalenderView.PopulateDays` and `TaxReporting` go in, and the reason the
money here agrees with the month's takings. Both pages tested cancelled first before this, so a house done and
then cancelled was quietly missing from the day it was done on. A cancelled visit that never happened is not work
and is left out either way.

`ShortMinutes` is *2h 30m* rather than `Job.SpellMinutes`' *2 hrs 30 mins*, deliberately: that one is a tag on a
row and is read on its own, this one rides in a chip beside two others on a day heading.

### Skipping work that is booked in

Skipping a job takes it off the day it was booked for. Skipping says you were there and passed the house over, and
`Job.SkipJob` pushes it out to its next visit — so the day it was booked on is not when it is being done any more.
Left booked in it read as booked for a day it was no longer due on: it stayed on that day as work still outstanding,
so the day never cleared and went on being called overdue, and the work list leaves booked work out
(`MasterFilter`), so the house was on neither page.

**The new date is measured from the day it was skipped, not from the day it was due.** A skip only really knows
about one date — the day you were there and passed the house over — and measuring off the due date pushed an
overdue house out from a date in the past: a weekly job a fortnight late, skipped today, came back **due a week
ago**, still on the list and still red, so skipping it looked like it had done nothing at all. It can only ever
put work off, so a house not due for months is not pulled forward to next week by being skipped by mistake.
`Job.DueDateBeforeSkip` remembers the date it had, because `UnSkipJob` can no longer get it back by subtracting
`SkipDays` again; a job skipped before that was kept has nothing there and falls back to the subtraction.
`SkipDays` itself is unchanged — a full frequency's worth of **weeks**, so a monthly or a daily job is pushed out
by its number of weeks rather than by its own unit.

`Job.SkipJob` is what unbooks it, in the kernel with the rest of the skip, so none of the four places work can be
skipped from — the swipes and menus on the work list, the calendar and the booked work page, and the paper view's
record sheet — can be the one that forgets. The day itself is a `Booking` in `Booking.Bookings`, which is a
**cache the jobs are the truth for**: `Booking.Rebuild` builds it from the jobs and is the only thing allowed to
fill it. It used to be patched in place as well, and every path that changed a job's booked state had to remember
to mend the cache too — skip forgot once, cancelling work forgot once, and each was a ghost day on the work list.
Now every mutator on `Booking` (`AddBooking`, `RemoveJobFromBooking`, `RemoveBooking`, `ReseduleBooking`) does the
same thing — change the flags on the jobs, rebuild — and `RemoveBooking` works off the jobs rather than the cached
day, so work booked for the date that no list was showing comes off with the rest. Anything skipping work still
goes through `WorkPlanner.MarkJobSkipped`, which refreshes the row and saves; `SkipJob` on its own leaves the
cached day stale until the next rebuild.

**The calendar keeps a second cache of the same shape, and it has the same rule.** Each `CalenderDay` holds the
jobs that fall on it, filled only by `CalenderView.PopulateDays`, and the panel under the calendar is drawn from
the picked day's list rather than from the jobs. So `RefreshPageDate` on its own only *redraws* that list —
skipping a house pushed its due date out and it stayed on screen, on a day it was no longer due on, with the day's
totals still counting it, until the page was pulled down by hand. `RefreshAfterWorkChanged` (`RebuildDays` then
`RefreshPageDate`) is what every swipe and menu on that page which touches the work goes through — done, cleared,
skipped, cancelled, paid, moved, and the job's own window — so none of them can be the one that forgets.
Rebuilding runs `CalculateDay`, which ends in `ResetColor`, so the ring is put back on the day being looked at:
it is read *before* the rebuild, because `PopulateDays` picks today when nothing is picked and a day chosen for
you is not one to ring.

Clearing a skip (the paper view's **Clear**) puts the due date back but **does not** put the booking back — the day
is gone and nothing remembers it. Book it in again if it is wanted.

**Cancelling booked-in work unbooks it the same way.** `Job.CancelJob` takes the visit off any day it was
booked for — but only a visit that never happened; a clean already done stays on the day it was done.
Left booked, the work list kept a booking summary row counting work that every list filters out: a day
with nothing behind it, which is exactly how it was reported. The UI paths that cancel
(`WorkPlanner.MarkJobCancled`, the paper view's *Cancel Job*, the customer page's toolbar) call
`Booking.RemoveJobFromBooking` so the cached day is rebuilt there and then, and `Job.Load`
unbooks cancelled-never-done work older files still carry — memory only, the file catches up on the next
save, like the other tidy ups done on load.

`BookJobFormcs.BookForDate` is how a caller says which day the form should open on — it is used once and resets to
today, so a caller with no day in mind cannot pick up somebody else's. Without it the form opened on today and the
date had to be typed in again, which is wrong every time the day is already known.

### Dragging a day on to another day

`CalenderDay.MoveDay` moves the work, and **booked work is moved by `DateJobBookinFor`, not by `DueDate`** — the
calendar puts a booked job on the day it is booked for, so moving only the due date looked right on screen and
came back to where it started the next time the page was built from the jobs. That was reported as a merge that
undid itself on a restart, and it is the thing to check first if it is ever seen again.

Work already done stays on the day it was done — that day is what the month's takings are read off — so the move
returns how many jobs actually went, and a day with nothing movable left on it says so rather than looking
ignored. `Booking.Bookings` is built from the jobs, so the move ends with `DataRefreshNotifier.NotifyDataChanged`:
without it the work list keeps a booking row for the day the work came off.

Being asked whether to tell the customers is part of moving the day, not an alternative to it. Answering **Yes**
used to send the messages and then stop, so the one case where everybody had been told the date had changed was
the case where it had not.

### What a day on the calendar is filled with

Every fill on that grid is saying something, and they are ranked. A day carrying work is filled by its work
(`CalenderDay.WorkColour` — orange for work to do, green once it is all done and a blend between the two part
way through, red for work missed, the darker orange red for work nobody has arranged). Today and the day being
looked at are **rings** rather than fills, because a fill there would cost the day the thing the colour is for.

**The weekend is the same rule again.** Saturday and Sunday are washed a little differently from the working
week (`CalenderDay.IsWeekend`, `WeekendColour`) so a month can be counted without reading the headings — but
only on a day with **nothing on it**. A day with work keeps its work colour. So the headings carry it too: Sa
and Su are written in `WeekendHeading`, which is the half of it that survives a busy month, and those are
picked off the date each column really holds rather than off the position in the list.

There are **two washes**, chosen by the theme when the colour is set, because one alpha cannot serve both: the
wash that is barely there on the dark page is a block on the near white one. Like the rest of the colours on
this page the theme is asked at build time and not watched, so a page already on screen when the phone changes
theme keeps what it was built with. The day numbers themselves are the outstanding half of that: `ResetColor`
puts them in white, which is a white number on a `#F4F6F8` page for every future day in the light theme.

## Notes on a day

`Kernel/DayNote.cs` keeps something written against a date — the van in for its MOT, a bank holiday, somebody
coming out with you. It is deliberately **about the day and not about the work**: a note on a job is a standing
note about that house (`Job.Notes`) and a tag says what one visit was like, and neither can say anything at all
about a day with no work on it, which is most of what somebody wants to write down. So it is kept against the date
alone, and the note stays put whether that day's work is moved, done or cancelled.

`daynotes.rjt` is a global file like the expense rules — a note is not a tax record, and last year's are worth as
much as this week's to anybody looking back — so it rides in every backup, comes back with a restore and is
cleared by Delete All Data.

**Blank takes the note off.** There is no separate delete: rubbing out what is written is what somebody does when
a note stops applying, and a second button for it would only be a second thing to get wrong. `Set` says whether
anything actually changed and, like `Job.SetRound`, leaves the saving to whoever asked — `DayNoteEditor` is the
one place a note is written, so there is nowhere else for the save to be forgotten.

On `Layouts/CalenderView` the note sits under the date on the day panel and is tapped to change, with a button
beside it that says which of *Add A Note* and *Edit The Note* it is about to do — a note nobody can work out how
to add is no feature at all. It is on the day's action sheet too, but that takes a double tap and nobody double
taps a phone, so the button is what it is really reached by.

A day carrying a note is **marked on the grid** (`CalenderDay.ShowNote`, a pencil), because a note you cannot see
without tapping the day is a note you never read. A mark rather than the note itself: a cell is not big enough for
a sentence, and it is only there to make somebody tap the day. It is a pencil rather than a coloured dot because
the day is already filled by its work and a dot would read as more of the same.

## Changing things from the customer's page

`Layouts/ViewCustomerDetails` is where a customer is looked at, so it is where the two figures that go wrong get
put right, each with a **Change** beside it rather than a trip through the job form:

- **Current Balance** — `Layouts/CustomerBalance`. A round taken over from somebody else starts with whatever was
  owed written down somewhere else.
- **Time For Job** — `Layouts/JobDuration`. How long the job takes is what the day is planned off, and it is
  noticed to be wrong stood at the house.

**A balance changed outside the ledgers leaves a record** (`Kernel/BalanceAdjustment.cs`,
`balanceadjustments.rjt`). The balance is normally the gap between work done and money received, and both of
those keep their history — but settling up wrote debt off and remembered nothing, and typing a balance in
overwrote the figure without a trace, so the next argument about money had a history that did not add up.
Now `Job.SettleBalance` records the write-off itself (in the kernel, so no button can forget), with an
optional reason asked for on the settle prompt, and `CustomerBalance.Apply` records what a hand-set balance
was and became. Both show in the customer's history (`History` has a third kind alongside jobs and payments)
as the line that makes the money add up. **Nothing here touches tax**: on the cash basis income is the
payments, and a write-off is precisely money that never arrived. The records ride in every backup with the
other global files, follow a merged duplicate to the kept customer, and are covered by the self test.

`Job.Minutes` is the one definition of how long a job counts as taking — its own `EstimatedTime`, or the round's
usual when it has none — so the tags, the day totals, the booking form and the round's figures cannot disagree
about it. `JobDuration.MinutesFor` is a way in to the same thing; `Describe` says which of the two a figure is,
because a house showing the usual and a house that really is half an hour are not the same thing.

The round's usual lives on **`Job.DefaultDuration`**, and `Settings.DefaultJobDuration` is a property over it
rather than a second copy: the settings own the figure, but a job cannot see the settings and every page that
shows work asks a job how long it is.

`Job.LengthText` is that length as a tag, shown on the job rows in the work list, the booked work page and the
calendar (`HaveLength` keeps it off a round that has never timed anything). `Job.SpellMinutes` is the only place
minutes are turned into words, so a row and the customer's page cannot word the same figure differently. The work
list's booking summary row gets one too, and it says the whole day, because `Booking.Refresh` adds up `Minutes`.

`JobDuration.Apply` follows `JobNextId` **forwards** from the job it was given. The job being looked at is as
likely as not one already done, and changing it there while the next clean kept the old figure would be no use;
a visit already written up keeps what it was worked to, and another job at the same house is a different job.

## Saving the job form

`Layouts/NewJob` — which is what **Edit Details** opens as well as Add Job — writes nothing until Save is
pressed. Everything typed sits in the boxes and `SaveJob` is the only thing that puts it on the job, on the
customer and on disk.

The customer picker was the exception: it pointed the job at whoever was tapped there and then, so backing out of
a form left the job moved anyway. It now only remembers who was picked (`customer`), and `SaveJob` sets
`CustomerId` from it. That is also where a **new** job for somebody already on the round got its link back — the
picker had set the id on the job object that the `if (AddNewJob) JobToAdd = new Job()` in the save then threw
away, and nothing set it on the new one.

Because nothing is written as you go, leaving is what loses work, so leaving asks. `WatchForChanges` hooks every
Entry, Editor, CheckBox, Picker and DatePicker under `sv_mainScrole` once the form has been filled in — worked out
from the tree rather than a list of names, because this form has thirty odd fields and a new one nobody added to
such a list would be silently lost. Anything changed after that sets `_dirty`.

Both ways out have to ask, and they are different things: `OnBackButtonPressed` is the phone's own back button and
the swipe from the edge, and the arrow in the nav bar is a `BackButtonBehavior` command set in the constructor.
The prompt offers Save It, which goes through the same `SaveJob` the button does — validation and all, so a form
that cannot be saved keeps the page instead of losing it. `SaveJob` returns whether it saved for that reason, and
clears `_dirty` itself.

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
money out column). The layouts belong to **`Kernel/BankAccount.cs`**: each account remembers its own csv and pdf
layouts (kept apart, as they always were), so statements from two different banks can both be imported without one
bank's columns overwriting the other's. Accounts live in `bankaccounts.rjt` (a global file: in every backup,
synced like the rest) and are looked after on `Layouts/BankAccounts`, reached through the **Banking** section on
the settings page — which is only doors: that page, `Layouts/KeptStatements` and `Layouts/ExpenseRules` do the
work. An account is added, renamed and **archived — never deleted**: the statements and expenses imported from one
are tracked against its id, which is also why a rename is safe and why *Delete All Data* leaves the accounts
alone (like the settings, they are how the app is set up rather than what it has recorded). Archiving only takes
the account out of the import question, said plainly on the page; everything imported from it stays, and
unarchiving puts it back. The layout the app kept in the settings file before accounts existed becomes the first
account on load (`BankAccount.EnsureLegacyAccount`, fed by `Settings.Load` stashing the old fields), so nobody
re-teaches columns they already taught.

`ImportExport/StatementFile` picks and reads the file for both pages, and settles which account the statement is
from. With one active account there is no question at all — the first import quietly makes the one it needs. With
more than one, picking wrong files everything on the statement against the wrong account, so the choice is guarded
three ways: each account remembers what its statement files' **headings** look like (`CsvSignature`/`PdfSignature`,
stamped on every import) and the file is matched to the account it resembles; the offer shows **the top of the
file** — headings and first few lines — so the answer is checked against something real, not memory; and a pick
that goes against what the file looks like is asked about a second time, preview included. A file nothing
recognises still shows the preview before it is filed. `BankAccount.FindBySignature` only ever guesses when
exactly **one** active account matches — two accounts at the same bank print the same headings, and guessing
between them would guess wrong half the time. Archived accounts are neither offered nor guessed; when every
account is archived the import says so and stops, rather than inventing a new account.

The money in side reads the statement into `IncomingLine` once and both the list and the import run off that, so
what the list promises is exactly what the import does. `Payment.AlreadyRecorded` is the single definition of
"this one is already in", used by the badge and by `Payment.AddToCustomer`. A payment deliberately carries no
account — it is about the customer, and the tax figures do not care which account income landed in.

Nothing is ever imported twice — and with more than one account, "twice" is asked per account. Every outgoing gets
an id built from the account, the date, the normalised payee and the amount (`Expense.StatementReference`), stored
on `Expense.ExternalReference`, so re-importing the same statement — or the next one, which overlaps it — finds the
expense already there, while the same fuel stop paid once from each of two accounts is two expenses, as it should
be. Identical transactions on the same day are told apart by an occurrence number, so two identical fuel stops on
one account still count twice. Expenses recorded before accounts existed carry the old account-less id; the
migrated account is marked `InheritsLegacyReferences` and `Expense.FindFromStatement` tries both forms for it, so
nothing already imported comes back as new. A PayPal export has no account and keeps the old id shape for the same
reason. All of this is covered by the KernelDebugger self test.

The statement file itself is kept (`Kernel/StatementRecord.cs`), filed under the tax year it covers, so the
evidence the figures were read off travels with them into backups and the cloud. A statement that straddles
5 April is kept in **both** tax years — one record and one copy of the file each — so backing up or handing over a
single year still has all of its evidence, rather than half of it sitting in a year nobody asked for. Each record
holds its own year's date range and transaction count alongside the whole file's, which is what `Crossover` keys
off. It is copied once the date column is known — which is why `StatmentViewer.ArchiveStatement` runs there and not
at the file picker. Each record carries the account it was imported against (`BankAccountId`, -1 on records from
before accounts existed and on PayPal exports); `Layouts/KeptStatements` lists them, naming the account on each
card once there is more than one account to tell apart.

A kept statement **opens inside the app** (`Layouts/StatementReader`, the Open button on the kept statements
page; Share is the old behaviour of handing the file to another app). A pdf is drawn page by page as the bank
printed it — by the **platform's** renderer (`PdfPageImages`: Android's `PdfRenderer`, Windows' own pdf engine),
because PdfPig reads text out of a pdf and cannot draw one. A platform with no renderer written (iOS/Mac) or a
pdf the platform cannot open — a password-locked one — falls back to the rows the import reads, which can still
ask for the password, and the page says when it is showing rows rather than the paper. A csv shows as the table
it is, scrolling both ways. Nothing here silently shows nothing: every dead end says what happened and points at
Share as the way out.

`Kernal/csvImporter.cs` understands quoted fields: a field in quotes keeps its commas, the quotes are not part of
the value, and `""` inside one is a single quote. It used to cut the line at every comma, which is fine for a bank
that quotes nothing and wrong for PayPal, which quotes everything — one payer called `"Smith, John"` put every
column after it out by one for that row.

## PayPal

Two separate things, neither of which needs an account connecting or a key pasting in:

- **Asking for money.** `WorkTracker/PayPal.cs` builds a paypal.me link with the amount already in it, off the
  round's own paypal.me name (settings page, saved with the settings). *Ask For Payment* on
  `Layouts/ViewCustomerDetails` asks how much, then texts it, emails it, or copies it for WhatsApp. It
  **does not mark anything paid** — the link has been sent, that is all. The money is recorded when it lands.
- **Getting it back in.** `ImportExport/PayPalStatement` reads a PayPal activity export. A bank has its columns
  pointed out once and remembered; PayPal names its own, so they are read off the headings every time and never
  saved — which is what stops a PayPal export overwriting the layout set up for the bank. `SourceIsPayPal` is the
  third layout alongside csv and pdf, and money in off one is recorded as `PaymentMethod.Paypal`
  (`StatmentViewer.ImportedPaymentMethod`) rather than Bank.

The amount taken is PayPal's **Gross** — what the customer actually sent — so a job paid by PayPal clears the
balance to the penny. The fee is in a column of its own and is not brought in; the import says so. Taking the net
instead would leave every customer a few pence short for ever.

`Kernel/ExpenseRule.cs` is what makes recurring bills look after themselves: flagging an outgoing as an expense
(or ignoring it) remembers the payee, and the next statement logs it automatically with the same category and
note. Payee text is matched through `StatementText.PayeeKey`, which strips the reference numbers and the
"direct debit"/"card payment" wrapping the bank puts around the name. Rules are editable on the
`Layouts/ExpenseRules` page, and live in `expenserules.rjt` alongside the other data files.

## How a payment is said on the payments page

`Layouts/Payments` (the first page under the Money tab) is a list of money that has come in, and the one thing
that tells one line from the next is **how it was paid**. Cash handed over at a gate, a bank transfer, a PayPal
link, a card, a cheque and a direct debit are not the same thing, so each carries its own colour and its own
picture: a disc on the left of the card with the icon in it, and the method named on a chip in the same colour.
A Saturday round is then one colour running down the page and the odd transfer in it is seen without being
looked for.

`Kernel/PaymentDisplay.cs` is the whole of it — the display half of `Payment`, split off exactly the way
`JobDisplay.cs` is split off `Job.cs`: if deleting a member could only ever break a screen it goes there, and
nothing in `Payment.cs` should ever need a colour. **It is a new kernel file, so it is in `WorkTracker.csproj`'s
link list** — one added without that builds an app missing it.

`MethodName`, `MethodColour`, `MethodIcon` and `MethodTextColour` are all one switch each on `PaymentMethod`,
and each **ends in the Other answer rather than in a case**, so a method added to the enum draws a grey chip
saying Other instead of a blank one. `MethodName` is the reading version — `Bank` is *Bank Transfer* and
`GoCardless` is *Direct Debit* — and it is deliberately **not** what the pickers use: those are built from
`Enum.GetNames` and parsed straight back (`JobStatus`, `WorkPlanner`, `UpdateJobInstance`), and the enum's own
names are what is saved, so rewording one there would change stored data.

The colours are the deep end of each hue because they are **backgrounds with white on them** — the icons are
white stroked like the toolbar's (`paycash.svg`, `paycard.svg`, `paypaypal.svg`, `paybank.svg`, `paycheque.svg`,
`paydebit.svg`, `payother.svg`, referenced as `.png` because `MauiImage` converts them at build). A pale colour
here carries neither. `MethodTextColour` is what goes on them, said once so a colour ever changed takes its text
with it.

**The customer's history says it the same way** (`Layouts/ViewCustomerDetails`, through
`WorkTracker/Kernal/History.cs`): a payment's date banner is banded in the method's colour with the method's
icon on it, rather than the one green every payment used to get, and the line under it says *Paid by Bank
Transfer* off the same `MethodName`. It is the same two properties answering on both pages, so the two cannot
drift. `Payment.PaymentType` - the raw enum name as a string - was what those pages worded a method with, and
it is gone: with `MethodName` beside it, the un-worded one would only get picked again by mistake.

### What the payments page is showing

The page opens on the **last fortnight** and says so. A round takes money every week of the year, so the whole
list is thousands of lines nobody scrolls, and what is actually being asked when this page is opened is *has so
and so paid me* — which is this fortnight's money. The two filters are the date range and which methods are on,
both worked out in `Layouts/Payments` itself off `Payment.Query()`.

**The bar above the list is never empty on this page**, unlike the work list's, because the fortnight it opens
on is itself a filter — a page quietly showing a fortnight of a year's money with nothing saying so is exactly
what the rule about filters is there to stop. It names the range, names the methods left on or left out
(whichever is the shorter half to read), counts what is showing against everything there is and totals it, with
a **Show All** beside it that turns the dates off and every method back on. The panel itself is the
`CollectionView.Header` and is taken off the list when closed, the same as the work list's — see *Filtering the
work list* for why a merely invisible header is not good enough.

The method chips are built in code from `Enum.GetValues`, so a method added to `PaymentMethod` turns up as a
chip on its own, and each carries that method's colour and icon through `Payment.ColourFor`/`IconFor`/`NameFor`
— **the static half of the switches**, which exist precisely so the chip and the row cannot word or colour the
same method differently. A chip that is off goes grey rather than empty: the icons are white, so a chip with no
fill has nothing to draw them on.

Two things this page must keep doing. The list is sorted **newest first**, which is what a date range is for —
the payments file is in the order the money was recorded, which put this fortnight at the bottom of a year. And
it sorts a **copy**: `Payment.Query()` hands back the master list itself, so sorting what it returns would
quietly rearrange the kernel's own list.

Two small things the page reads off the payment rather than working out itself: `HasReference` keeps a column of
blank references off every cash payment, and `ShowAge` keeps the row from saying *Today* twice, since
`PaymentDate` and `PaymentDaysAgo` both say it. A payment matched to nobody says so in red
(`CustomerTextColour`) rather than in the same grey as a customer's name, which is the one line on the card
that wants doing something about.

## Importing a round off a spreadsheet

`ImportExport/RoundSheetParser` reads the .xlsx (straight over the zip/XML, so there is no NuGet package to add for
Android) and `ImportExport/CustomerImporter` maps its rows onto customers and jobs, matched on house number plus
street: a house not on the round is created, one already there has its price, frequency, TNB and front price
brought up to date.

A sheet says where the houses are, what they cost and how often they come round, and **nothing above the street**.
Four things it cannot say are asked once for the whole file on `Layouts/ImportSheet` — the town and area, which
round the work goes on, whether everybody starts owing nothing, and whether the whole lot is due on one day. They
used to be a run of `DisplayPromptAsync` alerts (town, then area), which gives no way back to an answer already
given and no room to say what any of them mean. The page decides nothing: it hands back an
`ImportExport.ImportOptions` and the importer does the work, so an import started from somewhere else would behave
the same.

- **The round** goes on through `Job.SetRound`, so it lands on **every visit** of a house rather than on the one
  the import happened to touch — the same rule as everywhere else a round is set. A **blank round asks for
  nothing**: work already on one keeps it, and new work starts on none. Taking a whole sheet's houses *off* their
  rounds is not something an import should be able to do by being left alone. A round typed in rather than picked
  is new, so the settings are saved when `Job.RoundNames` has grown, exactly as the work list does it.
- **Starting everybody at nothing owed** is for a round taken on off somebody else's spreadsheet: what a sheet
  carries is the work, not the ledger. Each balance cleared leaves a `BalanceAdjustment` write-off behind it, like
  every other balance changed by hand — see *Changing things from the customer's page*. A customer already owing
  nothing has nothing to record, which is also what keeps a customer with two houses on the sheet from being
  written off twice.
- **One due date for all of it** is for a sheet that has not been kept up to date; left off, each house is worked
  out from the last clean ticked on the sheet and how often it comes round, which is what a sheet that *has* been
  kept up is for. Three sorts of visit are left where they are: a clean already written up (that day is what a
  month's takings are read off), a cancelled one, and **a day already booked in** — the calendar puts booked work
  on `DateJobBookinFor` rather than on the due date, so moving the date under a booking would say one thing on the
  calendar and another on the round. Anything left behind is counted and said in the summary rather than passed
  over quietly.

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

Because of that the settings page has **no Cloud Sync section at all** — it was shown greyed out as *Coming
Soon* for a while and has since been taken out until the feature is real. The engine (`CloudSync.cs`, and the
`CloudSync.Start()` call in `AppShell`) is still in place; when the real credentials go in, the section and its
handlers come back in `Layouts/SettingLayout.xaml` — the old ones are in the git history of that page.

## Experimental features

GoCardless is marked **Experimental** where a user meets it — the settings section heading, an orange badge inside
it, the toolbar item on `Layouts/ViewCustomerDetails` and the title of the action sheet that page puts up. **Work
Sharing** carries the same marking on the settings page — section heading and badge — while sending work out is
still being proven; only the settings section says so, because the sending buttons it shows are gated behind it
anyway and receiving somebody's work should not greet them with a warning. The
`GoCardless` entry in the preferred-payment picker on `Layouts/NewCustomer` is deliberately *not* labelled: that
string is saved on the customer as the payment method, so changing the wording changes stored data.

## Saving exports to the device

Sharing hands a file to another app; `WorkTracker/DeviceFileSaver.cs` puts a copy where the device keeps downloads
so it can be found again without sending it anywhere. Android 10 and up goes through the MediaStore Downloads
collection (older phones write the file directly, after asking for the storage permission); Windows copies into the
user's Downloads folder without overwriting a file already there. iOS keeps each app's files to itself, so `CanSave`
is false there and sharing stays the only way out — anything calling this must cope with that.

The tax page's Export, the manual backup on the settings page and the tax page's *Save This/Other Tax Years*
all ask *Save To This Device* or *Share* when the platform can do both — a backup saved onto the device is a
real backup on its own, and it is written as `application/octet-stream` so tapping it in the downloads list
offers Work Tracker back.

## The tax export and MTD

The app cannot file to HMRC itself — that needs HMRC-recognised software — so the road to a free MTD filing
is **bridging software**: a tool off HMRC's compatible-software list that reads the figures out of a
spreadsheet and files them. `ImportExport/TaxReportWriter` serves that two ways:

- **The spreadsheet** writes **every HMRC box every time, zeros included**, so each figure sits in the same
  cell in every export. That is what lets a bridging tool be pointed at the cells once and stay pointed —
  a layout that shifts with which boxes happen to have spending in them breaks the links the quarter
  something new is bought. **Do not go back to writing only the boxes with figures in.**
- **`WriteMtdCsv`** is the bare quarterly figures as a table — one row per period, always every column, ISO
  dates, invariant decimal point — for MTD software that imports rather than links. The Export button on the
  tax page asks which shape is wanted.

Both carry the estimates warning inside the file, because an export travels without the page it came from.

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

## Deleting a job type, a tag or a round

All three are only the list of what is *offered* — the type, the tag and the round itself live on the job — so a
cross beside each one on the settings page takes it off that list, and **only while nothing is carrying it**.
Deleting one that is in use could not undo any work, but it would leave jobs labelled with something that is not
on any list and cannot be picked again, which nothing can put right afterwards.

`Job.UsingJobType`, `Job.UsingTag` and `Job.UsingRound` do the counting, in the kernel with the data, and they
count the **quotes** as well as the work — a quote carries a type and a round like anything else. Refusing says
how many are carrying it and what to do about it, because a refusal with no reason reads as a broken button.

Two extra guards: the **last** job type cannot go, since `Job.DefaultJobName` is what work with no type of its own
is called; and a tag that is set on the tag bar (`Job.AutoTags`) counts as in use even when no visit carries it
yet, because it is going on to everything marked done. A blank row is not a name and is deleted without asking.

A round is a patch of the work — a village, a day of the week, whatever it is actually split into. `Job.Round`
is a plain string on the job (blank for work that is not on one) with `Job.RoundNames` as the list to pick from,
edited on the settings page and saved with the settings like `JobNames` and `TagNames`. It starts **empty**:
a round is named by whoever has one, and a made up default would only be in the way.

Unlike a tag it belongs to the job rather than to one visit, so **`Job.DeepCopy` does carry it over** — the next
clean at a house is on the same round as the last one.

A round is a thing about **the job**, like how long it takes — where the house is does not change between one
clean and the next. So **`Job.SetRound` puts it on every visit of the job**, and `Job.EveryVisit` finds them by
**`BaseJobId`**: that is what says a run of visits is all the same job, it is carried by `DeepCopy`, and it is
repaired on load for files that never had one (`FixBaseIdBug`). Following `PreviousJobId`/`JobNextId` instead
would split a job in two wherever a link is missing.

Setting it on the visit in front of you was no use: that visit is as likely as not already done, and the next one
was copied off it before the round was set — so the house showed up on no round from its next clean onwards,
which is what made the stats page keep saying work had no round after a whole list had been put on one.

`SaveLoad.FillRoundsDownTheJob` puts right the rounds set that way, filling each job's round down from its newest
visit that has one, so a round already put together does not have to be put together again. Only what is in
memory is changed — the file catches up on the next save, like the other tidy ups done on load.

The other half of that: the work list only reaches a **fortnight** ahead (`ResetDateFilter`), so a house not due
for a month is not on it to be picked at all. **Put On A Round** says how many are still on no round afterwards,
and that the dates on the filter panel are how to reach them.

Both work pages put it first: `Layouts/WorkPlanner` sorts by round then by date, and `Layouts/PaperView` groups by
round *and* street (a street worked on two rounds is two lots of work) with a heading naming each round. Work on
no round sorts **last** rather than first, so what nobody has organised is not the top of every page. The paper
view only draws round headings when some work is actually on a round, so a round nobody uses changes nothing.

Filtering: tap the round tag on a job row in the work list, or pick Round from the paper view's View menu
(`Kernal/Fiilters/RoundFilter`, which offers the rounds work is really on plus No Round — the rounds *in use*
rather than the settings list, because a round taken off that list still has its work).

`Layouts/RoundPicker` is the one place that asks which round. **Put On A Round** on the work list's selection
toolbar is what makes this usable on an existing round: filling one in a house at a time through the job form is
not something anybody will do.

## What a house looks like on a list

**`Controles/JobCard` is the one place a house on a list is drawn**: a card with the address in bold, the price
beside it, the town and area quiet underneath, when it is due and what is owed said in colour as text, and the
tags under that. The work list, the booked work page, the calendar's day list and All Jobs all put this control
in their row template rather than each writing the row — it is the same round looked at four ways and it should
not read as four different apps. The paper view is deliberately not one of them: that page is a printed sheet,
not a list of cards. The extra-work and returned-work pages build their cards in code from work-share data
rather than from `Job` rows, so they are not on it either.

What differs between the pages is said in **options** on the control rather than in copies — plain properties
set once in the template. The `Show*` bools turn pieces on and off (the info button, the due
line, each kind of chip, the tags, the notes); an option that is on still waits for the job to agree, because
the per-job question stays a binding underneath it. `CollapseCancelled` folds cancelled work to the struck-out
line the work list uses, `CollapseCompleted` folds done work to the calendar's faded tap-to-reopen line — one
fold per page, not both. The three styles say how things are worded: `AddressStyle` (the number alone when the
street is already the heading, as on All Jobs, falling back to whoever lives there), `DueStyle` (the worked
relative wording, or the date written out with how far past it is), `OwedStyle` (only when something is owed,
or always answered — owes, in credit, nothing owed) and `PriceStyle` (`Price £12`, or the bare figure off
`EffectivePrice`).

Everything **around** the card stays the page's own: the swipe actions, the hold, the row tap and the desktop
context menu all go on the `JobCard` element in the template exactly as they went on the old Border. What is
**inside** the card that can be tapped comes back out as events carrying the job, because a page cannot reach
inside a template: `InfoClicked` and `PartTapped` for the work list's
filter taps — gated by `EnableFilterTaps`, off by default, because a tap recogniser on a label swallows the
tap a page's own row gesture was waiting for. All Jobs hands the card its one line the control cannot word
itself — how often the house comes round and which round it is on — through `ExtraCaption`, because the round
is read off the whole job through that page's grouping. The `AltColour` stripes are only the customer page's
now; the gap between cards is what tells one house from the next everywhere else.

`ShowExtraChips` — TNB, ENB and a direct debit on its way — is **on everywhere**, the booked work page
included. It was off on that page alone, for no better reason than that the hand written row the control
replaced had never had the chips, so the one list with a date on it was the one list that did not say which
houses want telling the night before. That is the page they matter most on. Do not turn it off again.

**The booked work page and the calendar's day list read street by street**, the way All Jobs does: a small
street heading, then that street's houses under it with the number alone on each card
(`AddressStyle="NumberOnly"`, which also takes the street off the card's folded lines — it is the heading).
`Controles/StreetSplit.WithHeadings` is the one splitter: it sorts street by street and up each street by
house number — the same three keys All Jobs sorts on, keyed off the **real** street (`Job.SortStreet`) with
the heading drawn from the display name, so screenshot mode masks what is on screen without changing what
groups together. Each page's rows are then a mix of `StreetHeading` and `Job`, told apart by
`StreetSplitTemplateSelector` — the heading template is a label and nothing else, so a heading cannot be
swiped, held or tapped like work. On the booked work page the split lives inside `BookingGroup`, whose
`Jobs` list keeps the actual work for everything done to a day (sending, tagging, moving, cancelling); on
the calendar it is `ShowDaysWork`, and the owed view stays a flat list because it is sorted by what is owed,
not walked up a street. Done work now stays on its street — chip on the booked page, folded line on the
calendar — rather than sinking to the bottom of the day, so a street is one run of houses however far
through it the day is.

What the work list keeps on top of that card is what that page is *for*: the
info button and **the tags** — what the work is, whose round it is on, how long it takes, TNB/ENB, a direct
debit waiting, and what was different about the visit. That is the page the round is worked off, so what is on
the job stays on show.

**The due and owed colours are not the tag colours.** `DueColorCode`/`OwedColorCode` are chip *backgrounds*, made
to be read white-on-colour — as text on a card, Yellow and LightBlue cannot be read at all. `JobDisplay` therefore
has `DueTextColour` and `OwedTextColour` alongside them, saying the same states in colours that work as text on
either theme, and they are the ones `AllJobs` already used. Anything drawing a job as text rather than as a chip
wants those two.

Everything the row can be tapped for survived the change: street, town, area, type, price, round and money owed
each still filter the list (see below), and the swipes, the hold, and the right-click menu are untouched.

### Reading a day on paper instead of as cards

`JobCard.RowStyle` is the second way the same control draws a house: `Card` is everything above, `Paper` is the
round book's row — one tight line of the house, the TNB/ENB badges, the price on its green, whatever is written
down about it with the visit's tags under that, what is owed, and the **mark** for what happened to it. No card
behind it, no gap between rows and a rule underneath, so a whole day fits a phone screen the way a page of the
book does.

**It is one setting for the calendar and the booked work page and nothing else** (`WorkTracker/DayListView.cs`,
the *Calendar and Booked Work* section on the settings page, list view to begin with). Those two are the same day
looked at twice, so a round read one way on one of them and the other way on the next would only be two habits to
keep up. The work list and All Jobs are not in it — they are the round rather than a day — and `Layouts/PaperView`
is already the paper view. It is a preference and not a setting in the data files, like the paper view's own view
options and the calendar's `HideDueWork`: how a page is being read is not something about the round, so it has no
business travelling in a backup.

Three things about it that are easy to undo:

- **The pages bind it, they do not set it.** A row on a virtualised list is built when it is scrolled into view
  and handed a different house every time it comes back round, so a card that read the setting once and
  remembered the answer would draw whatever it was when that row happened to be built — half a day in cards and
  half of it on paper, which is the shape of the bug the tick boxes on the work list once had. `DayListView.Current`
  raises `PropertyChanged`, the two templates bind to it, and every row that exists is told.
- **The paper pieces are built only if a card is ever asked to draw one** (`JobCard.EnsurePaperRow`). A round is
  hundreds of houses and every list in the app draws cards unless this is turned on, so building both bodies in
  the constructor would be a dozen labels a row that nobody ever sees.
- **Every option is still the page's.** `ApplyPaper` asks the same `Show*` bools and the same `AddressStyle`,
  `OwedStyle` and folds the card does, so a page with its notes or its extra chips turned off gets the same answer
  whichever way its days are being read. What a paper row deliberately drops is the town and area (the street
  heading above the row already says where this is) and the due date (a day list's day *is* the date) — neither
  fits on one line, and a paper row that wraps is not a paper row. The **info button stays**, smaller, on a page
  that asked for one: the paper view can do without it because a row there opens the job when it is tapped, but
  the calendar's day list has no tap on its rows at all, so taking it off would leave nothing to open a house
  with but the swipe.

**The marks are kept once, on the job** — `Job.PaperMark`/`Job.MarkDone` and friends in `Kernel/JobDisplay.cs`.
They used to live on `PaperView.PaperItem`, which was fine while that page was the only thing that drew them;
`PaperItem.StringDone` and the rest are now properties forwarding to the job's, so the settings page still edits
them under *Paper View* by the names everything already knows, and a visit cannot be marked one way on the
calendar and another on the sheet. `PaperMark` asks the same questions in the same order the paper view's record
columns do — **completed before cancelled**, like every other count of the work.

## Putting the prices up

A price rise is **agreed before it happens**, so the thing that matters about one is the **day it starts** —
that is what the customer was told and what they ring up about. `Job.PriceRiseDate`, `Job.PriceRiseTo` and
`Job.PriceRiseWas` hold it, and like the round they belong to the **job** rather than to one visit: `DeepCopy`
carries all three, which is the whole trick — a visit that will not exist for another month still comes out at
the agreed price with nobody having to remember.

`Job.SetPriceRise` writes it on to every visit (`EveryVisit`, by `BaseJobId`) and then applies it. A visit takes
the new price when its **due date** is on or after the day:

- **A clean already written up is never repriced.** The completed visit, its payment and the customer's balance
  are one record — `MarkJobDone` has already put `EffectivePrice` on the balance — so going back over it would
  leave the three disagreeing. A cancelled visit is not work and is left alone for the same reason.
- **Visits are repriced where they are made**, not by whatever pressed the button: `NextVisit` applies the rise
  to each fresh copy, `SkipJob` re-applies it because a skip pushes a due date out and can carry a visit over
  the day, and `Job.ApplyPriceRises` runs over the whole list on load for a rise whose day came round while the
  app was shut. That is in the kernel with the rest of the money so none of the places work is written up from
  can be the one that forgets.
- **`Job.CurrentPrice` is what the house is charged as things stand** — the price on the visit next due, found
  through the same `NextDue` the lists use. Not `Price` off whichever visit you happen to be holding: that one
  is as likely as not a clean already done at last year's figure, which is exactly what made the customer's page
  the wrong place to read a price off.

`Layouts/PriceRise` is the one page that asks, so a street and the whole round are put up the same way. It takes
a list of jobs, keeps **one visit per house** (`Job.SameJobKey` — a list picked off the work list can easily hold
two visits of the same house, and putting a rise on twice would raise it twice), and offers by an amount, by a
percentage (rounded to the nearest 50p, because a percentage rarely lands on a price anybody would quote) or, for
a single job, straight to a new price. **It lists what it is about to do before it does it** — house by house, old
price to new, with the total — because a round repriced by accident is not something to find out about afterwards.

It is reached from three places, all of which hand it jobs and none of which decide anything:

- the work list's selection toolbar (*Price Increase*), for a street or a handful of houses;
- `Layouts/AllJobs`, whose toolbar puts up **whatever the page is showing** — the whole round, or the one round
  the bar above the list already names. That page is where a round-wide rise belongs: the work list only reaches
  a fortnight ahead (`ResetDateFilter`), and half a round put up is worse than none of it;
- the customer's page, for the one house.

**The customer's page says the price and the rise** (`ShowPriceRise`, `PriceRiseText`, `PriceRiseTextColour` in
`JobDisplay.cs`), worded by whether it has happened yet — *goes up from £10 to £12 on 1 April* while it is still
to come, *went up* after. It stays on show afterwards on purpose: "it went up in April" is the answer to the same
question.

## Filtering the work list

Two things narrow `Layouts/WorkPlanner`, and they are not the same kind of thing:

- **What the list is kept to** — the date range on the filter panel, `MasterFilter`. Work due up to the end date
  and anything finished since the start one, which is what makes the list the work in hand rather than the whole
  round for ever. Booked work is not on this list at all; it has its own page.
- **A tag filter** — tapping the type, price, street, town or money tag on a job row. `SetTagFilter` takes the
  test off the job that was tapped rather than the words off its label, which is what a tag filter is: everything
  else like *this one*.

The filter panel is the work list's `CollectionView.Header`, not a pinned row above it: it is a big thing to
leave taking up a phone screen, so scrolling takes it out of the way. **Closing it takes it off the list
altogether** (`ShowFilterPanel` sets `lv_Jobs.Header` to null) rather than only hiding it — a header that is
merely invisible still holds its place, which left the top job sitting a panel's worth of empty space down the
screen. That is also why opening it scrolls back to the top (`ScrollToTheTop`) — a panel at the top of the list's content is off screen if the list is scrolled, and
the button would look like it had done nothing. The small bars — the tag bar, what is selected, and what is being
filtered by — stay pinned, because those have to be on screen to be any use.

`FilterSource` is what a tag filter picks from, and it is deliberately **not** `MasterFilter`: it is the whole
round, minus what is finished. Tapping High Street to be shown three of its twelve houses, because the rest are
not due for a fortnight, is not what anybody means by tapping it.

The tag filters were switched off for a long time — `GetJobs` set `Filter = null` before it ever ran one — because
a list quietly showing a fraction of the round with no way back out is worse than no filter at all. That is what
`ShowActiveFilter` is for: while a tag filter is on, the bar above the list says what is being shown and how much
of it, with a Clear, whether the filter panel is open or not. **Do not let a filter be on with nothing on screen
saying so.** The bar's Clear takes off the tag filter only; the panel's Reset puts everything back, dates
included.

## Picking jobs out of the work list

`Job.SelectionMode` is one switch for the whole round — either every row on `Layouts/WorkPlanner` has a tick box
or none of them do — and **`Job.SetSelectionMode` is the only thing that may change it**, because it is also what
tells every job the answer has changed.

It used to be set through a property on the job that read a static behind the scenes while only raising
`PropertyChanged` on the one job it was set on. The list is virtualised, so any row built afterwards — anything
scrolled into view — read the static and drew a tick box while the rest of the list had none, and the rows that
were never told took no notice of being switched off either. That is where tick boxes appearing on their own came
from. `SelectionModeEnabled` is now worked out rather than stored, so a row built at any point gives the same
answer as every other row, and the booking summary rows never show one because they are not work.

The way out is the bar across the top of the list, not just the toolbar item: on a phone the toolbar's Cancel is
as likely as not to be behind the ... menu, which is no use as the way out of a mode you did not mean to be in.

**The tick box is not on the card, and it must not go back on it.** It is drawn by the row in
`WorkPlanner.xaml`, in a `Grid` column of its own **outside** the `SwipeView`, because the two cannot share a
row. The swipe takes the touch the moment the finger moves at all, so a box under it was swallowed and only
ticked if you dragged a little as you tapped; and turning the swipe off to stop that greys the box out with
it, since `IsEnabled` goes down the whole tree. `Job.SwipeUnlessPicking` is what turns the swipe off while the
ticks are on — a binding, like `SelectionModeEnabled`, so a row scrolled into view mid-pick is told the same
as the rest. Only the work list binds it; the calendar has no ticks and keeps plain `EnabledSwipe`.

**Nothing may reach into the list and set any of this.** `StartSelectingJobs` and its three opposite numbers
used to walk `lv_Jobs.GetVisualTreeDescendants()` setting `cb.IsVisible` and `sv.IsEnabled` by hand. Only the
rows realised at that moment were touched and recycled rows kept whatever they were left with, so **half the
tick boxes came up greyed out and half did not** — the same virtualisation hole as the one above, in a
different property. The walks are gone.

**What is picked is the ticks, and only the ticks.** There used to be a `_selectedJobs` list of ids beside
them, and it came apart: `SetSelectionMode(false)` unticks every job, each untick fires the box's
`CheckedChanged`, and that took the id back out of the very list the booking was about to be built from —
picking five houses and being handed an **empty booking form**, and only for the rows that were on screen,
which is why it came and went. `Picked()` reads `Job.Selected()` now. For the same reason anything acting on
what was picked must **take the list before turning the ticks off**, and `Row_SelectionToggled` decides
nothing: it fires for a row being recycled as much as for a finger, so a handler that decided there would be
deciding off the wrong job.

**Select All** is on that bar (as *All*, turning into *None* once everything is picked) and on the toolbar in
words. It picks the list **as it stands, filter and all** — booking a whole street in is tapping the street's tag
and then this — and it goes through the same `ToggleSelected` a tap does, so nothing can disagree about what is
picked. The booking summary rows are left out of it, because they are not work.
Holding a row starts picking jobs out with that row already picked, the same hold as `BookedWork`.
The finger coming up off a hold arrives as a tap too, which is what `HoldJustHappened` is there to swallow.
The row's tap and the list's `SelectionChanged` both land in `RowTapped`, which ignores the second of two
reports of the same tap.

### Holding a row

MAUI has no long press gesture, and the obvious way round that — timing the finger going down with a
`PointerGestureRecognizer` — **does not work on a phone at all**: those events are raised for a mouse or a stylus
hovering, not for a finger. The hold did nothing on any page for as long as it was built that way, and no amount
of a longer timer, a bigger move tolerance or an extra `TapGestureRecognizer` on the row was ever going to change
that.

`Controles/LongPressBehavior.cs` makes the row's platform view `LongClickable` on Android and lets Android decide
when a press has been held long enough. **That is not enough on its own, because the rows are `SwipeView`s.** The
swipe takes the finger the moment it moves at all — which a finger held on a phone always does — and the pending
long press is cancelled with it, so nothing on the row ever hears about the hold. That is why holding a row did
nothing however it was hooked up.

So the swipe is read rather than fought: `swip_started` records when it began, and a swipe that ran as long as a
hold and **opened nothing** was not a swipe, it was somebody holding the row (`HoldWasReallyASwipe` on the work
list, `swip_ended` on the booked work page). Anything that opens the swipe actions is a swipe and is left alone.

The behaviour finds the page by walking up `Parent` from the row and looking for `IHoldRows` rather than being
bound to a command: a row lives in a `DataTemplate`, so it has no way of naming its page, and a behaviour is not
an `Element` — an ancestor binding from inside one has nothing to walk. It reads the row's binding context at the
moment it is held rather than when it was built, because the list is virtualised and a row shows a different job
every time it comes back round. That is also why it hooks `HandlerChanged`: a recycled row is handed a new
platform view each time.

Both pages take the two paths through one method (`HoldToSelect`, `ShowHoldOptions`), which ignores the same row
being held twice inside a second, so a platform that raises both only acts once.

## Screenshot mode

`Kernel/ScreenshotMode.cs` swaps the road, town, area and postcode on screen for made up ones, so the round can be
photographed for a listing or a help page without putting a customer's address in front of everybody. House
numbers are left as they are. It is turned on under **Debug** on the settings page.

The same real name always comes out as the same made up one, and no two real names share one. That is not just
for looks: streets still group as streets and towns as towns, so a screenshot shows the app behaving exactly as
it does on the real round.

**It is display only, and the split is what makes that safe.** `Location.DisplayStreet` and friends are separate
from `Location.Street` and friends: forms, saving, exports, statement matching and customer merging all read the
real address, and only the places that put it on screen read the display ones. Mask the fields themselves and the
next job edited would save a made up street. `PaperView.PaperItem` keeps `PropertyStreet` real for the same
reason — rows are matched on it and a house added from a street heading is built out of it — and shows
`DisplayStreet` instead.

**The setting is deliberately not saved.** `ScreenshotMode.On` is a plain static that `Settings.Save` knows
nothing about, so it is off again the next time the app starts. That is the only way to be sure a round is never
quietly showing made up addresses weeks later.

## Toolbar icons

The toolbar items everybody already knows the picture for carry one: Search is a magnifier, Filters a funnel,
anything that adds is a plus, and Select Jobs is a ticked box (`Resources/Images/search.svg`, `filter.svg`,
`add.svg`, `select.svg`). They are Feather style
24×24 like the rest of the icons, and referenced as `search.png` — `MauiImage` turns the svg into a png at build.

They are stroked **white**, unlike the icons used on the swipe actions and the tab bar, because the toolbar is the
Shell nav bar: green in the light theme and nearly black in the dark one. A black stroked icon disappears into
both.

`info.svg` is white for the same reason and it is not a toolbar icon: it sits on the blue disc of the info button
on a job row, where it was black on blue and barely there. It is drawn as the **i on its own**, without Feather's
ring around it — the button is already a circle, so the ring was a second circle inside the first, which at 30
odd pixels reads as a smudge rather than as anything. The button is the same size on the work list as on the
calendar; it is the same button, so it should not change between the two pages.

Every item keeps its `Text` alongside the icon. Android shows that text on a long press and reads it out in the
... menu, so an icon never leaves somebody guessing — and an item that goes to Secondary is text only anyway.
Only put an icon on something whose picture is genuinely obvious; the rest say what they do in words.

The work list and the paper view **lead with the same three** — Search, Filters, Add Job, in that order. It is
the same round looked at two ways, so what is on the bar does not move between them. The paper view's Filters
toggles one panel holding everything about what the sheet is showing: a *Filter By* picker (All Jobs, City,
Area, Round — it has no dates of its own, those are the *Show All Jobs* box on the same panel), the mark-done
date and the show-cancelled box. The filter chooser used to be an action sheet under Filters with the boxes
under a separate *Option* menu, which was two places to look for one idea.

### The work list's toolbar is built in code

`WorkPlanner.UpdateToolBar` is the only thing that puts items on it, and it works them out again from scratch
every time: on each change of mode (`UpdateToolBarNoraml`, `UpdateToolBarSelectJobs`, `UpdateToolBarViewBooking`,
which now only set the mode) and on the way back to the page.

**Nothing may be declared in `WorkPlanner.xaml`.** Every mode starts by emptying the collection, so an item put on
in the xaml is thrown away by the first rebuild and never comes back — which is what happened to Search: picking
jobs out once and coming back out of it lost it for the rest of the run. Filters and Select Jobs failed the other
way round, added in the constructor only if the round had work *at that moment*, so a first run — or a page built
before the jobs were loaded — kept a toolbar with nothing on it but Add Job. That is why the test for work is
inside the rebuild rather than beside the `Add`.

## Tooltips

`ToolTipProperties.Text` works on Android — it comes up on a **long press** — and on hover on Windows. That makes
it worth putting on anything with no words of its own: the info button on a job row, the tag bar's buttons, and
so on. It is not worth relying on for anything a user has to know, because nobody long presses a control to ask
what it is. **Never put one on a row that has a hold gesture** (the work list and booked work rows), where the
long press already means something else. Anything genuinely not obvious gets a line of grey text under it
instead, the way the filter panel explains itself.

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
already worked — which is exactly why an entry **in use cannot be deleted**. See *Deleting a job type, a tag or a
round* below. A tag typed in rather than picked is added to it (`Job.RememberTag`), so it only has to be typed
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

## Keeping the app quick

A round is hundreds of houses and `_Jobs` holds **every visit of every one of them ever done**, so anything that
walks the whole list to draw a page of twenty rows gets slower every month the app is used. Four rules came out of
going over why the app felt sluggish, and each one is easy to undo by accident.

**Ask `Customer.ById` for a customer, never `Customer.Query("id", ...)`.** Looking up who lives at a house is the
most asked question in the app - what is owed, whether they are in credit, whose payment that was - and it is
asked once per row on every list. `Query` answers it by copying the whole customer list, turning every id into a
string and throwing all but one away. `ById` is a dictionary. The index is rebuilt when the customer list changes,
which is what `Customer.InvalidateIndex` is for: **anything that adds, deletes or reloads customers has to call
it** (`Add`, `Delete`, `DeleteData` and `Customer.Load` all do). The count is checked as well as a backstop, but a
restore can bring back the same number of different customers, so the count alone is not enough.

`Job.MatchCustomer` and `Payment.MatchCustomer` hold on to the answer, and both check the **id** against what they
cached rather than only checking it for null - a job can be pointed at a different customer while it is in memory,
which is exactly what merging two records of the same person does.

**`Job.Refresh` has to name every property a job row binds to.** The work list keeps its rows now
(`WorkPlanner.SyncSourceJobs`) instead of handing the `CollectionView` a brand new collection every time, which
used to throw every row away and build it again - so anything missing off `Refresh` was quietly put right by that
rebuild and is not any more. A row that stays put shows exactly what `Refresh` tells it about. If a job row is
given something new to show, say it in `Refresh`; the pieces of the card are in `Controles/JobCard`.

`SyncSourceJobs` matches rows by identity (`Job` does not compare by value), takes out what has gone and puts the
rest in order one row at a time, so a job swiped done moves one row and the rest of the page is untouched.
Past `RowsWorthPatching` differences it gives up and swaps the collection wholesale, because a search being typed
into changes nearly every row and patching that costs more than starting again.

**A getter a row binds to must not raise change notifications.** `JobFormattedDueTime` works the wording and the
colour out together and sets `DueColorCode`/`DueColorTextCode` as it goes - so reading one bound property was
firing two more binding updates, and each of those sent the bindings round again. The three colour setters
therefore say nothing when the colour has not actually changed (`SameColour` - `Color` is a class, so the named
colours hand back a fresh object each time and reference equality would call every one of them a change). Same
reason the hex colours are `static readonly` fields rather than `Color.FromArgb("#...")` inside the getters:
that call takes the string apart every time it is made.

**`Controles/JobCard` settles its options once.** Every `Show*`/`Style` setter calls `Apply`, which takes about
thirty bindings off and puts them back; All Jobs sets eight options, so each row used to do that nine times over
to reach the answer it would have reached once. `Apply` now does nothing until `SettleOptions` has run, which
happens when the card is parented or given a binding context - after the template has finished setting the
options and before anything is drawn. **Do not put `Apply()` back in the constructor.** An option set from code
afterwards still applies straight away.

**Prefer `Job.RefreshJobs(jobs)` to `Job.RefreshJobs()`.** The one with no arguments tells every visit ever done
about two dozen properties. Every list page builds itself again when it is navigated to, so the jobs worth telling
are the ones on the page. The pull-to-refresh keeps the big hammer on purpose - that gesture means *build all of
it again*.

### Still on the list

The heaviest thing left is **saving**. `Job.Save` serialises every job that has ever existed to XML, on the UI
thread, and a single swipe of a job done costs that plus `Customer.Save` and `Payment.Save`. Coalescing a user
action into one write, and moving it off the UI thread, is the next real win - it is left alone here because
changing when data reaches the disk is not something to do without being able to run the app.

Two others worth knowing about: none of the list templates except `Layouts/AllJobs` set `x:DataType`, so their
bindings are resolved by reflection rather than compiled, which is the single biggest documented MAUI list win;
and `Debug` builds of a MAUI Android app are dramatically slower than `Release` ones, so judge how the app feels
from a Release build before chasing anything else.

## Versioning

`ApplicationDisplayVersion` and `ApplicationVersion` live in `WorkTracker/WorkTracker.csproj` and must both be
bumped for a Play Store upload. `BuildDate` is stamped into the assembly automatically at build time and surfaced
on the settings page, so it needs no manual update.

## Improvements considered and not done yet

Proposed during the work-sharing and balance-records work, agreed worth doing, and deliberately left for later.
Enough context here to pick each one up cold:

- **Derived customer balance.** Stop storing `Customer.Balance` and compute it: completed visits (at
  `EffectivePrice`) − payments − write-offs, plus hand-set records. The compensating `+=`/`-=` scattered
  through done/undone/paid/merge is the drift risk behind the duplicate-customer bug, and deriving removes
  the class. The groundwork is in: `BalanceAdjustment` keeps the write-offs and hand-set figures that make
  derivation possible, and each visit keeps the price it was charged at. Still needed: a one-time migration
  turning each customer's stored balance into an opening `SetByHand` record so nothing changes on screen,
  self-test coverage *before* the switch, and a sweep of every `Balance +=`/`-=` site. Do this only after
  the adjustment records have been in real use for a while — it is the one change here with regression risk.
  (`Customer.CalculateCustomerBill` is an old stab at the same idea; it ignores adjustments and alternative
  prices, so replace it rather than build on it.)

- **Accruals bad debt.** If the accruals figures on the tax page are ever used seriously: income there is
  counted off completed visits, so debt written off later stays in declared income. The write-off records
  are exactly the bad-debt line — subtract the period's write-offs in `TaxSummary.Build`'s accruals branch.
  On the cash basis (the default) nothing is needed; a write-off is money that never arrived.

- **iOS file opening.** `.rbf` and `.rwk` cannot be opened into the app on iOS: needs
  `CFBundleDocumentTypes` in `Info.plist` and an `OpenUrl` override in `AppDelegate`, routing to
  `BackupRestore.FileWasOpened` / `WorkShareOpen.FileWasOpened` by extension the way `MainActivity` does.
  Only matters if the iOS build is ever actually shipped.

- **Row wrappers (the rest of the Job split).** `Job.cs`/`JobDisplay.cs` separates the files; the display
  state (`IsSelected`, colours, `CollapsedInList`…) still lives on the shared job objects, so two pages
  showing the same job share row state. The full fix is wrapper row view-models per list with bindings
  prefixed onto them — a large churn across every virtualised list page for a cleanliness payoff. Not worth
  it until it blocks something; do it one page at a time if ever.
