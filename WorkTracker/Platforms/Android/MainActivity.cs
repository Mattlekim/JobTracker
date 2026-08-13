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
    //  There is no mime type for a .rbf, so the filter has to take anything
    //  and pick the file out by its name instead. That is what the path
    //  pattern is for.
    //
    //  It is written out three times because Android's pattern matching does
    //  not back up: ".*\.rbf" is matched by taking everything and then
    //  looking for the dot, so a path with a dot anywhere earlier in it never
    //  matches at all. One pattern per dot in the path is the way round it,
    //  and it is three filters rather than one with three patterns because
    //  the singular property is the one every version of the binding has.
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
