using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace WorkTracker
{
    //  SingleTask, so that a .rbf opened while Work Tracker is already
    //  running is handed to the app that is there (OnNewIntent) rather than
    //  starting a second copy of it.
    //
    //  It has to be SingleTask and not SingleTop. There is one window in a
    //  MAUI app and it belongs to one activity, so a second activity kills
    //  the app on the spot:
    //
    //      This window is already associated with an active Activity
    //      (WorkTracker.MainActivity)
    //
    //  SingleTop only reuses the activity when the new intent lands on the
    //  task it is already sitting on top of. A file opened from a file
    //  manager does not: it comes in on that app's task, so android built a
    //  second one, and the app went down before the backup could be offered.
    //  SingleTask is what says there is only ever one of these - android
    //  finds it wherever it is, brings it back and delivers the intent to it.
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTask, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]

    //  Opening a backup with Work Tracker.
    //
    //  A .rbf tapped in a file manager, in the downloads list or on an email
    //  offers this app, which is how a backup normally reaches a new phone -
    //  the alternative is finding it again through the picker on the settings
    //  page, from an app that has just been installed and knows nothing.
    //
    //  There is no mime type for a .rbf, so there is nothing to ask for by
    //  name, and it takes two filters rather than one because what arrives
    //  is not the file - it is a content uri belonging to whichever app sent
    //  it, and only some of those carry the file's name.
    //
    //  The first filter is for the ones that do (the storage provider, and a
    //  file uri): it matches the name at the end of the path. It is written
    //  out three times because Android's pattern matching does not back up -
    //  ".*\.rbf" is matched by taking everything and then looking for the
    //  dot, so a path with a dot anywhere earlier in it never matches at all,
    //  and one pattern per dot is the way round that. The host has to be
    //  there for any of it to be read: Android ignores the path of a filter
    //  that does not name a host.
    [IntentFilter(new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataSchemes = new[] { "content", "file" },
        DataHost = "*",
        DataMimeType = "*/*",
        DataPathPattern = ".*\\\\.rbf")]
    [IntentFilter(new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataSchemes = new[] { "content", "file" },
        DataHost = "*",
        DataMimeType = "*/*",
        DataPathPattern = ".*\\\\..*\\\\.rbf")]
    [IntentFilter(new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataSchemes = new[] { "content", "file" },
        DataHost = "*",
        DataMimeType = "*/*",
        DataPathPattern = ".*\\\\..*\\\\..*\\\\.rbf")]

    //  The second filter is for the ones that do not. A backup in the
    //  downloads list is content://...downloads/1000000123 - the name is not
    //  in there anywhere, so there is nothing for a pattern to match and the
    //  filter above never fires. That is the case that matters most, because
    //  a backup off an email or out of Drive is exactly where it lands.
    //
    //  All that is left to go on is the type, and a file the phone has no
    //  type for is application/octet-stream. So Work Tracker is offered for
    //  those, and what it really is is settled once it can be read.
    //
    //  Settled by what is *in* it, not by its name. A uri that carries no
    //  name is exactly the case this filter is here for, so a name is the one
    //  thing there is no point insisting on: a backup is a zip of the data
    //  folder and a work list has its own magic bytes, and both say so from
    //  the inside. Anything that is neither is said out loud rather than put
    //  back down without a word.
    //
    //  The zip types are there because a .rbf is a zip, and an app that looks
    //  inside a file rather than at its name will say so. Being offered for a
    //  zip is the price of being offered for a backup that something has
    //  looked inside - and it costs nothing, because a zip that is not one of
    //  ours has none of our files in it and is turned away on that.
    [IntentFilter(new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataSchemes = new[] { "content", "file" },
        DataMimeTypes = new[] { "application/octet-stream", "application/zip", "application/x-zip-compressed" })]

    //  A shared work list (.rwk) opens with the app the same two ways a
    //  backup does: by name where the uri carries one, written out once per
    //  possible dot for the same reason as above, and on type alone for the
    //  downloads list - which the octet-stream filter above already covers.
    //  The name is checked once the file can be read, so being offered for
    //  somebody else's file costs nothing.
    [IntentFilter(new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataSchemes = new[] { "content", "file" },
        DataHost = "*",
        DataMimeType = "*/*",
        DataPathPattern = ".*\\\\.rwk")]
    [IntentFilter(new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataSchemes = new[] { "content", "file" },
        DataHost = "*",
        DataMimeType = "*/*",
        DataPathPattern = ".*\\\\..*\\\\.rwk")]
    [IntentFilter(new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataSchemes = new[] { "content", "file" },
        DataHost = "*",
        DataMimeType = "*/*",
        DataPathPattern = ".*\\\\..*\\\\..*\\\\.rwk")]
    public class MainActivity : MauiAppCompatActivity
    {
        /// <summary>put on an intent once its file has been taken off it</summary>
        private const string AlreadyTaken = "WorkTracker.FileTaken";

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            Microsoft.Maui.ApplicationModel.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
        public MainActivity()
        {

            AndroidGloable.Main_Activity = this;



        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            //the app is being started by the file, so this runs before there
            //is anything to ask on. BackupRestore holds on to it
            TakeTheFile(Intent);
        }

        protected override void OnNewIntent(Intent intent)
        {
            base.OnNewIntent(intent);

            //  The intent the activity is holding is deliberately left as it
            //  is. SetIntent would say "this is the one now", but API 35
            //  bound it as SetIntent(Intent, ComponentCaller) and the one
            //  argument form is gone - and calling the two argument one would
            //  go looking for a method that does not exist on any phone older
            //  than android 15, which is most of them.
            //
            //  It costs nothing here. What stops a file being offered twice
            //  is the mark put on the intent below, and the intent the
            //  activity keeps holding is the one that has already been
            //  marked, so a recreation finds it dealt with either way.

            //already running: the file arrives here instead
            TakeTheFile(intent);
        }

        /// <summary>
        /// copies whatever was opened into our own cache and hands it on.
        ///
        /// What comes in is a content uri belonging to somebody else's app,
        /// readable only for as long as this intent lives, and with no real
        /// path behind it - so it cannot just be unzipped where it lies.
        /// </summary>
        private void TakeTheFile(Intent intent)
        {
            if (intent == null || intent.Action != Intent.ActionView || intent.Data == null)
                return;

            //  An intent is handed back on every recreation of the activity -
            //  a rotation, a theme change, android rebuilding the app after
            //  reclaiming it - and the file that opened the app would be
            //  offered again each time. Marking it says this one has been
            //  dealt with; it rides on the intent, so it survives with it.
            if (intent.GetBooleanExtra(AlreadyTaken, false))
                return;
            intent.PutExtra(AlreadyTaken, true);

            Android.Net.Uri uri = intent.Data;
            string name = NameOf(uri);

            //the file is copied first and told apart afterwards. it used to
            //be told apart by name alone, before copying - but a content uri
            //does not have to carry a name at all, and a file that arrived
            //nameless was dropped without a word: the app opened and nothing
            //happened, which is exactly how it was reported. a shared work
            //list can be recognised by its own first bytes instead, and a
            //file that is neither is said out loud rather than ignored -
            //being opened *with* Work Tracker was a deliberate act, and
            //silence reads as the app being broken
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    //a display name is whatever the sending app wrote down
                    //and can carry a path with it - only the last part of it
                    //is ours to write into the cache
                    name = System.IO.Path.GetFileName(name ?? string.Empty);

                    if (string.IsNullOrWhiteSpace(name))
                        name = "opened-file";

                    string copy = System.IO.Path.Combine(
                        Microsoft.Maui.Storage.FileSystem.CacheDirectory, name);

                    string why;
                    if (!CopyTheFileOut(uri, copy, out why))
                    {
                        //  the app that sent the file would not hand it over,
                        //  which is a different thing from a file that is not
                        //  one of ours and has a different answer - so it is
                        //  said differently. What was tried goes in the log:
                        //  every attempt was caught and worked past, so there
                        //  is no other way to know which of them failed how
                        CrashLogger.Log($"MainActivity.TakeTheFile ({Describe(uri, name)})",
                            new System.Exception(why));

                        UiInterface.ImportExport.WorkShareOpen.FileCouldNotBeFetched(name);
                        return;
                    }

                    if (Kernel.WorkShare.LooksLikeShare(name))
                        UiInterface.ImportExport.WorkShareOpen.FileWasOpened(copy);
                    else if (UiInterface.ImportExport.BackupRestore.LooksLikeBackup(name))
                        UiInterface.ImportExport.BackupRestore.FileWasOpened(copy);
                    else if (Kernel.WorkShare.ReadHeader(copy) != null)
                        //named something else - or nothing - on the way, but
                        //the magic bytes say what it is
                        UiInterface.ImportExport.WorkShareOpen.FileWasOpened(copy);
                    else if (UiInterface.ImportExport.BackupRestore.ContentsLookLikeBackup(copy))
                        //  and the same for a backup, which is the case that
                        //  matters: the downloads list hands over
                        //  content://.../downloads/1000000123 and a mail app
                        //  hands over whatever it cached the attachment as, so
                        //  the .rbf on the end is gone by the time it gets
                        //  here. Judged on the name alone every backup that
                        //  arrived that way - which is how a backup reaches a
                        //  new phone - was turned away as unrecognised. A
                        //  backup is a zip of the data folder and says so from
                        //  the inside, so that is what it is told by.
                        UiInterface.ImportExport.BackupRestore.FileWasOpened(copy);
                    else
                        UiInterface.ImportExport.WorkShareOpen.UnreadableFileWasOpened(name);
                }
                catch (System.Exception ex)
                {
                    //a file we cannot read is not worth taking the app down
                    //for - but it is worth a line in the crash log, because
                    //"nothing happened" cannot be chased without one
                    CrashLogger.Log($"MainActivity.TakeTheFile ({Describe(uri, name)})", ex);
                    UiInterface.ImportExport.WorkShareOpen.UnreadableFileWasOpened(name);
                }
            });
        }

        /// <summary>
        /// Gets the bytes of whatever was opened out of the app that sent it
        /// and into our own cache.
        ///
        /// It takes two goes at it, because openInputStream - the ordinary
        /// way, and the one a file manager or the downloads list answers -
        /// says only "file not found" about a document the sending app is not
        /// actually holding. A backup sitting in Drive, or on an email that
        /// has never been downloaded, is exactly that: the provider has a
        /// record of the file and no bytes to give. Asked for as a *typed
        /// asset* the provider goes and fetches it first, which is what that
        /// call is for, and it is the one that gets a backup off an email on
        /// to a new phone.
        ///
        /// Neither is allowed to throw out of here. A file that cannot be got
        /// hold of is worth saying plainly and is not worth taking the app
        /// down for, so what was tried is handed back for the log instead.
        /// </summary>
        /// <returns>true when the file is now sitting in <paramref name="destination"/></returns>
        private bool CopyTheFileOut(Android.Net.Uri uri, string destination, out string why)
        {
            System.Collections.Generic.List<string> tried = new System.Collections.Generic.List<string>();

            try
            {
                using (System.IO.Stream from = ContentResolver.OpenInputStream(uri))
                    if (from != null)
                    {
                        using (System.IO.Stream to = System.IO.File.Create(destination))
                            from.CopyTo(to);

                        why = string.Empty;
                        return true;
                    }

                tried.Add("openInputStream: gave nothing back");
            }
            catch (System.Exception ex)
            {
                tried.Add($"openInputStream: {ex.Message}");
            }

            try
            {
                using (Android.Content.Res.AssetFileDescriptor asset =
                           ContentResolver.OpenTypedAssetFileDescriptor(uri, "*/*", (Android.OS.Bundle)null))
                    if (asset != null)
                        using (System.IO.Stream from = asset.CreateInputStream())
                            if (from != null)
                            {
                                using (System.IO.Stream to = System.IO.File.Create(destination))
                                    from.CopyTo(to);

                                why = string.Empty;
                                return true;
                            }

                tried.Add("openTypedAssetFileDescriptor: gave nothing back");
            }
            catch (System.Exception ex)
            {
                tried.Add($"openTypedAssetFileDescriptor: {ex.Message}");
            }

            why = string.Join("; ", tried);
            return false;
        }

        /// <summary>
        /// what is known about a uri that would not open, for the log. The
        /// path itself is left out: it is somebody's file name and the log
        /// gets sent to us
        /// </summary>
        private static string Describe(Android.Net.Uri uri, string name)
        {
            if (uri == null)
                return $"no uri, name={name}";

            return $"{uri.Scheme}://{uri.Authority} name={name}";
        }

        /// <summary>
        /// the name the file was sent under. a content uri does not have to
        /// carry one in its path - one out of the downloads list is a number -
        /// so the app that sent it is asked first
        /// </summary>
        private string NameOf(Android.Net.Uri uri)
        {
            try
            {
                //OpenableColumns.DISPLAY_NAME, by its value rather than
                //through the binding, which has moved between versions
                using (Android.Database.ICursor cursor = ContentResolver.Query(uri, null, null, null, null))
                    if (cursor != null && cursor.MoveToFirst())
                    {
                        int column = cursor.GetColumnIndex("_display_name");
                        if (column >= 0)
                        {
                            string named = cursor.GetString(column);
                            if (!string.IsNullOrWhiteSpace(named))
                                return named;
                        }
                    }
            }
            catch
            {
                //not every provider answers a query
            }

            string path = uri.LastPathSegment ?? uri.Path ?? string.Empty;
            return System.IO.Path.GetFileName(path);
        }
    }
}
