namespace UiInterface.ImportExport;

using Kernel;

/// <summary>
/// A shared work list (.rwk) handed to the app to open, the same way a
/// backup is: from a file manager, an email, the downloads list, or the
/// command line on a desktop. It can arrive before there is a page to ask
/// anything on - on a cold start it is what opened the app - so it is held
/// here until AppShell can deal with it.
///
/// What is done with it depends on which way the file is going, and that is
/// read out of the plain header without a PIN: work somebody sent becomes
/// this phone's Extra Work, and work coming back finds its own PIN under
/// the key the sender kept (see Kernel/WorkShare.cs).
/// </summary>
public static class WorkShareOpen
{
    private static string _pending;

    /// <summary>the phone has handed us a .rwk to open</summary>
    public static void FileWasOpened(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        _pending = path;

        Action opened = Opened;
        if (opened != null)
            opened();
    }

    /// <summary>raised when a shared work list has been handed to the app</summary>
    public static event Action Opened;

    /// <summary>
    /// the file waiting to be dealt with, if any. taking it clears it, so
    /// the same file cannot be offered twice
    /// </summary>
    public static string TakePending()
    {
        string path = _pending;
        _pending = null;
        return path;
    }

    /// <summary>
    /// a .rwk given to the app on the command line - how a desktop opens a
    /// file with an app, since Windows cannot register the type without an
    /// installer
    /// </summary>
    public static void CheckCommandLine()
    {
        string[] args;

        try
        {
            args = Environment.GetCommandLineArgs();
        }
        catch
        {
            return;
        }

        for (int i = 1; i < args.Length; i++)
            if (WorkShare.LooksLikeShare(args[i]) && File.Exists(args[i]))
            {
                FileWasOpened(args[i]);
                return;
            }
    }
}
