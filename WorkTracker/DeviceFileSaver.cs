namespace UiInterface;

/// <summary>
/// Sharing an export hands the file to another app - mail, drive, whatsapp -
/// which is no help to somebody who only wants the spreadsheet on the device
/// they are stood at. This puts a copy where the device keeps downloads, so
/// it can be found again in Files or in Explorer without going through
/// anything else.
///
/// The file is expected to have been written already (exports are built in
/// the cache folder), so this only ever copies.
/// </summary>
public static class DeviceFileSaver
{
    /// <summary>
    /// true when this platform has somewhere the user can get at a file
    /// again. iOS keeps each app's files to itself, so there the only way out
    /// is still to share it
    /// </summary>
    public static bool CanSave
    {
        get
        {
#if ANDROID || WINDOWS
            return true;
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// copies an already written file somewhere the user can find it
    /// </summary>
    /// <param name="sourcePath">the file as it was written, usually in the cache folder</param>
    /// <param name="fileName">what it should be called where it lands</param>
    /// <param name="mimeType">what kind of file it is, so the device opens it with the right app</param>
    /// <returns>where it ended up, worded for telling the user</returns>
    public static async Task<string> SaveAsync(string sourcePath, string fileName, string mimeType)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new FileNotFoundException("There is nothing to save.");

        fileName = SafeName(fileName);

#if ANDROID
        //before android 10 downloads was an ordinary folder and writing to it
        //needed asking. from 10 the store does the writing, and asking for
        //the permission is refused because the app no longer holds it
        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            PermissionStatus status = await Permissions.RequestAsync<Permissions.StorageWrite>();
            if (status != PermissionStatus.Granted)
                throw new UnauthorizedAccessException(
                    "Work Tracker needs permission to write to storage before it can save the file.");
        }

        return await Task.Run(() => SaveAndroid(sourcePath, fileName, mimeType));
#elif WINDOWS
        return await Task.Run(() => SaveToDownloadsFolder(sourcePath, fileName));
#else
        await Task.CompletedTask;
        throw new NotSupportedException("This device cannot save files outside the app. Share it instead.");
#endif
    }

    /// <summary>
    /// takes out anything a file system will not have in a name. the name is
    /// built from a tax year rather than typed, so this is a backstop
    /// </summary>
    private static string SafeName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "Work Tracker Export";

        foreach (char bad in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(bad, '_');

        return fileName;
    }

#if WINDOWS
    /// <summary>
    /// on a pc the downloads folder is where anything saved out of an app is
    /// looked for first. an export is never written over the top of an older
    /// one, so last week's figures are still there to compare against
    /// </summary>
    private static string SaveToDownloadsFolder(string sourcePath, string fileName)
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (!Directory.Exists(folder))
            folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        Directory.CreateDirectory(folder);

        string target = FreeName(folder, fileName);
        File.Copy(sourcePath, target, false);
        return target;
    }

    /// <summary>the given name, or the next one along that is not taken</summary>
    private static string FreeName(string folder, string fileName)
    {
        string target = Path.Combine(folder, fileName);
        if (!File.Exists(target))
            return target;

        string stem = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);

        for (int i = 2; i < 1000; i++)
        {
            target = Path.Combine(folder, $"{stem} ({i}){extension}");
            if (!File.Exists(target))
                return target;
        }

        throw new IOException($"There are already too many files called {fileName} in {folder}.");
    }
#endif

#if ANDROID
    /// <summary>
    /// android 10 put a stop to apps writing where they liked, so the
    /// downloads collection is asked to make the file and hands back
    /// somewhere to write it. older phones just write the file
    /// </summary>
    private static string SaveAndroid(string sourcePath, string fileName, string mimeType)
    {
        Android.Content.Context context = Android.App.Application.Context;

        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            Java.IO.File folder = Android.OS.Environment.GetExternalStoragePublicDirectory(
                Android.OS.Environment.DirectoryDownloads);
            if (folder == null)
                throw new IOException("This phone has no Downloads folder.");

            folder.Mkdirs();
            string target = Path.Combine(folder.AbsolutePath, fileName);
            File.Copy(sourcePath, target, true);
            return target;
        }

        Android.Content.ContentValues values = new Android.Content.ContentValues();
        values.Put(Android.Provider.MediaStore.IMediaColumns.DisplayName, fileName);
        values.Put(Android.Provider.MediaStore.IMediaColumns.RelativePath, Android.OS.Environment.DirectoryDownloads);
        if (!string.IsNullOrEmpty(mimeType))
            values.Put(Android.Provider.MediaStore.IMediaColumns.MimeType, mimeType);

        Android.Net.Uri saved = context.ContentResolver.Insert(
            Android.Provider.MediaStore.Downloads.ExternalContentUri, values);
        if (saved == null)
            throw new IOException("Android would not make the file in Downloads.");

        using (Stream input = File.OpenRead(sourcePath))
        using (Stream output = context.ContentResolver.OpenOutputStream(saved))
        {
            if (output == null)
                throw new IOException("Android would not let the file be written to Downloads.");
            input.CopyTo(output);
        }

        return "Downloads/" + StoredName(context, saved, fileName);
    }

    /// <summary>
    /// android renames a download rather than writing over one already there,
    /// so what it decided to call the file is read back before it is
    /// mentioned to the user
    /// </summary>
    private static string StoredName(Android.Content.Context context, Android.Net.Uri saved, string fallback)
    {
        try
        {
            string[] wanted = new string[] { Android.Provider.MediaStore.IMediaColumns.DisplayName };
            using (Android.Database.ICursor cursor = context.ContentResolver.Query(saved, wanted, null, null, null))
            {
                if (cursor != null && cursor.MoveToFirst())
                {
                    string name = cursor.GetString(0);
                    if (!string.IsNullOrWhiteSpace(name))
                        return name;
                }
            }
        }
        catch
        {
            //not worth failing a save that has already worked
        }

        return fallback;
    }
#endif
}
