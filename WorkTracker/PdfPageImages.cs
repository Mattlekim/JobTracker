namespace UiInterface;

/// <summary>
/// Draws a pdf's pages as images, so a kept statement can be read inside the
/// app looking exactly as the bank printed it.
///
/// The drawing is done by the platform - Android's PdfRenderer and Windows'
/// own pdf engine - because the app's pdf library (PdfPig) reads text out of
/// a pdf and cannot draw one. A platform with no renderer here, and a pdf
/// the platform cannot open (a password-locked one), throw instead - the
/// caller falls back to showing the rows the import reads, which can still
/// ask for the password. Do not let a throw here become "nothing happens".
/// </summary>
public static class PdfPageImages
{
    /// <summary>
    /// every page as a png, at twice the pdf's own size so small print
    /// stays readable, capped so a monster file cannot eat the phone's
    /// memory. returns the images and how many pages the file really has,
    /// so a capped file is said out loud rather than quietly cut short
    /// </summary>
    public static async Task<(List<byte[]> Pages, int Total)> RenderAsync(string path, int maxPages = 40)
    {
#if ANDROID
        return await Task.Run(() => RenderAndroid(path, maxPages));
#elif WINDOWS
        return await RenderWindows(path, maxPages);
#else
        //ios and maccatalyst have no renderer written yet - the reader's
        //fallback shows the parsed rows there instead
        await Task.CompletedTask;
        throw new NotSupportedException("This platform cannot draw a pdf");
#endif
    }

#if ANDROID
    private static (List<byte[]>, int) RenderAndroid(string path, int maxPages)
    {
        List<byte[]> pages = new List<byte[]>();

        //throws SecurityException on a password-locked pdf, which is the
        //fallback path working as intended
        using Android.OS.ParcelFileDescriptor fd = Android.OS.ParcelFileDescriptor.Open(
            new Java.IO.File(path), Android.OS.ParcelFileMode.ReadOnly);
        using Android.Graphics.Pdf.PdfRenderer renderer = new Android.Graphics.Pdf.PdfRenderer(fd);

        int total = renderer.PageCount;
        int count = Math.Min(total, maxPages);

        for (int i = 0; i < count; i++)
        {
            using Android.Graphics.Pdf.PdfRenderer.Page page = renderer.OpenPage(i);

            int width = page.Width * 2;
            int height = page.Height * 2;

            using Android.Graphics.Bitmap bitmap = Android.Graphics.Bitmap.CreateBitmap(
                width, height, Android.Graphics.Bitmap.Config.Argb8888);

            //a pdf assumes it is being printed on paper, so anything not
            //drawn must already be white - left clear it shows as black
            bitmap.EraseColor(unchecked((int)0xFFFFFFFF));

            page.Render(bitmap, null, null, Android.Graphics.Pdf.PdfRenderMode.ForDisplay);

            using MemoryStream stream = new MemoryStream();
            bitmap.Compress(Android.Graphics.Bitmap.CompressFormat.Png, 100, stream);
            pages.Add(stream.ToArray());
        }

        return (pages, total);
    }
#endif

#if WINDOWS
    private static async Task<(List<byte[]>, int)> RenderWindows(string path, int maxPages)
    {
        List<byte[]> pages = new List<byte[]>();

        Windows.Storage.StorageFile file = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);

        //throws on a password-locked pdf, which is the fallback path
        //working as intended
        Windows.Data.Pdf.PdfDocument document = await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(file);

        int total = (int)document.PageCount;
        int count = Math.Min(total, maxPages);

        for (uint i = 0; i < count; i++)
        {
            using Windows.Data.Pdf.PdfPage page = document.GetPage(i);
            using Windows.Storage.Streams.InMemoryRandomAccessStream stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();

            await page.RenderToStreamAsync(stream, new Windows.Data.Pdf.PdfPageRenderOptions()
            {
                DestinationWidth = (uint)(page.Size.Width * 2),
            });

            byte[] bytes = new byte[stream.Size];
            using (Windows.Storage.Streams.DataReader reader = new Windows.Storage.Streams.DataReader(stream.GetInputStreamAt(0)))
            {
                await reader.LoadAsync((uint)stream.Size);
                reader.ReadBytes(bytes);
            }
            pages.Add(bytes);
        }

        return (pages, total);
    }
#endif
}
