namespace UiInterface;

using Kernel;
using Microsoft.Maui.Graphics;
#if WINDOWS
using Microsoft.Maui.Graphics.Win2D;
#else
using Microsoft.Maui.Graphics.Platform;
#endif

/// <summary>
/// Receipt photos come off a phone camera at anything from three to twelve
/// megabytes each, and every one of them is kept for as long as the taxman
/// might ask for it - as well as being copied to Google Drive and into every
/// backup. A receipt only has to be readable, so the photo is scaled down to
/// a sensible size and saved as a JPEG before it is stored, which takes a
/// typical receipt from several megabytes to well under one while leaving
/// the writing perfectly legible (and leaving OCR just as able to read it).
/// </summary>
public static class ReceiptPhoto
{
    /// <summary>
    /// longest side, in pixels, a stored receipt is scaled down to. 1600 is
    /// about twice what is needed to read a receipt on screen, which leaves
    /// room to zoom in on the small print
    /// </summary>
    public const int DefaultMaxSize = 1600;

    /// <summary>jpeg quality, 1-100. 70 keeps the text crisp</summary>
    public const int DefaultQuality = 70;

    private static int _maxSize = DefaultMaxSize;
    private static int _quality = DefaultQuality;

    /// <summary>
    /// longest side kept, in pixels. anything under 400 is ignored - a
    /// receipt that small cannot be read
    /// </summary>
    public static int MaxSize
    {
        get { return _maxSize; }
        set { _maxSize = value < 400 ? DefaultMaxSize : value; }
    }

    public static int Quality
    {
        get { return _quality; }
        set { _quality = (value < 30 || value > 100) ? DefaultQuality : value; }
    }

    /// <summary>the three sizes offered on the settings page</summary>
    public static readonly string[] QualityNames = { "Small files", "Balanced", "Best quality" };

    private static readonly int[] QualitySizes = { 1200, 1600, 2400 };
    private static readonly int[] QualityLevels = { 60, 70, 85 };

    /// <summary>which of <see cref="QualityNames"/> the current setting is</summary>
    public static int QualityChoice
    {
        get
        {
            for (int i = 0; i < QualitySizes.Length; i++)
                if (MaxSize == QualitySizes[i] && Quality == QualityLevels[i])
                    return i;
            return 1;
        }
        set
        {
            if (value < 0 || value >= QualitySizes.Length)
                return;
            MaxSize = QualitySizes[value];
            Quality = QualityLevels[value];
        }
    }

    /// <summary>
    /// writes <paramref name="source"/> to <paramref name="destinationPath"/>,
    /// scaled down and re-encoded. if the photo cannot be read as an image -
    /// an odd format, or a platform that will not decode it - the original
    /// bytes are written instead, so a receipt is never lost to save space
    /// </summary>
    /// <returns>true when the photo was compressed</returns>
    public static async Task<bool> SaveCompressedAsync(Stream source, string destinationPath)
    {
        //held in memory so the photo can still be written out untouched if
        //compressing it fails part way through
        using MemoryStream original = new MemoryStream();
        await source.CopyToAsync(original);

        original.Position = 0;
        if (TryCompress(original, destinationPath))
            return true;

        original.Position = 0;
        using (FileStream dest = File.Create(destinationPath))
            await original.CopyToAsync(dest);
        return false;
    }

    private static bool TryCompress(Stream source, string destinationPath)
    {
        IImage image = null;
        IImage sized = null;
        try
        {
            image = LoadImage(source);
            if (image == null)
                return false;

            sized = image;
            if (Math.Max(image.Width, image.Height) > MaxSize)
                sized = image.Downsize(MaxSize, false);

            using (FileStream dest = File.Create(destinationPath))
                sized.Save(dest, ImageFormat.Jpeg, Quality / 100f);

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (sized != null && !ReferenceEquals(sized, image))
                sized.Dispose();
            if (image != null)
                image.Dispose();
        }
    }

    private static IImage LoadImage(Stream source)
    {
#if WINDOWS
        return new W2DImageLoadingService().FromStream(source);
#else
        return PlatformImage.FromStream(source);
#endif
    }

    /// <summary>
    /// goes back over receipt photos already stored and compresses the ones
    /// that were saved before this existed. a photo is only replaced when the
    /// new one really is smaller, so nothing is ever made worse
    /// </summary>
    /// <returns>how many photos were shrunk and how many bytes that saved</returns>
    public static async Task<(int shrunk, long saved)> RecompressStoredAsync()
    {
        int shrunk = 0;
        long saved = 0;

        string folder = Expense.GetReceiptFolderPath();
        string working = Path.Combine(FileSystem.CacheDirectory, "receipt_recompress.jpg");

        foreach (string path in Directory.GetFiles(folder))
        {
            try
            {
                long before = new FileInfo(path).Length;

                bool compressed;
                using (FileStream existing = File.OpenRead(path))
                    compressed = await SaveCompressedAsync(existing, working);

                if (!compressed)
                    continue;

                long after = new FileInfo(working).Length;

                //a photo that is already small can come back bigger once it
                //has been through the encoder again - leave those alone
                if (after >= before)
                    continue;

                File.Copy(working, path, true);
                shrunk++;
                saved += before - after;
            }
            catch
            {
                //one photo that will not reopen must not stop the rest
            }
        }

        try
        {
            if (File.Exists(working))
                File.Delete(working);
        }
        catch
        {
        }

        return (shrunk, saved);
    }

    /// <summary>how much room the stored receipt photos take up</summary>
    public static long StoredSize()
    {
        long total = 0;
        try
        {
            foreach (string path in Directory.GetFiles(Expense.GetReceiptFolderPath()))
                total += new FileInfo(path).Length;
        }
        catch
        {
        }
        return total;
    }

    /// <summary>a byte count written the way a person reads it</summary>
    public static string FormatSize(long bytes)
    {
        if (bytes >= 1024 * 1024)
            return $"{bytes / (1024f * 1024f):0.0} MB";
        if (bytes >= 1024)
            return $"{bytes / 1024f:0} KB";
        return $"{bytes} bytes";
    }
}
