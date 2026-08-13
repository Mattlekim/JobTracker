using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace WorkTracker
{
    //  SingleTop so that opening a .rbf while Work Tracker is already running
    //  hands the file to the app that is there (OnNewIntent) instead of
    //  starting a second copy of it on top of the first.
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]

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
    //  those, and the file is checked by name once it can be read: anything
    //  that is not a .rbf is put back down without a word.
    //
    //  The zip types are there because a .rbf is a zip, and an app that looks
    //  inside a file rather than at its name will say so. Being offered for a
    //  zip is the price of being offered for a backup that something has
    //  looked inside - and it costs nothing, because the name is still what
    //  decides whether anything happens.
    [IntentFilter(new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataSchemes = new[] { "content", "file" },
        DataMimeTypes = new[] { "application/octet-stream", "application/zip", "application/x-zip-compressed" })]
    public class MainActivity : MauiAppCompatActivity
    {

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

            Android.Net.Uri uri = intent.Data;
            string name = NameOf(uri);

            if (!UiInterface.ImportExport.BackupRestore.LooksLikeBackup(name))
                return;

            //a backup carries the receipt photos, so it can be big. copying it
            //is not something to hold the app open on - the page that offers
            //to restore is told when the copy is there
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    string copy = System.IO.Path.Combine(
                        Microsoft.Maui.Storage.FileSystem.CacheDirectory, name);

                    using (System.IO.Stream from = ContentResolver.OpenInputStream(uri))
                    {
                        if (from == null)
                            return;

                        using (System.IO.Stream to = System.IO.File.Create(copy))
                            from.CopyTo(to);
                    }

                    UiInterface.ImportExport.BackupRestore.FileWasOpened(copy);
                }
                catch
                {
                    //a file we cannot read is not worth taking the app down
                    //for. nothing is offered, and the settings page is still
                    //there to pick one by hand
                }
            });
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
