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
    //  set on whichever thread copied the file in, read on the main one
    private static volatile string _pending;

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
    /// what is waiting, without claiming it. A file that opened the app is
    /// here before there is a page to ask on, and taking it before there is
    /// somewhere to ask lost it for good
    /// </summary>
    public static string PeekPending()
    {
        return _pending;
    }

    //  what to say about a file that was opened with the app and could not
    //  be dealt with. It is the whole sentence rather than the file's name,
    //  because there is more than one way for it to go wrong and they have
    //  different answers - a file that is not one of ours is not the same
    //  problem as one the sending app would not part with, and telling
    //  somebody the wrong one of those sends them looking in the wrong place
    private static volatile string _pendingUnreadable;

    /// <summary>
    /// a file was opened with the app and turned out to be nothing the app
    /// knows - neither a backup nor a work list. held so AppShell can say so:
    /// opening a file with Work Tracker was a deliberate act, and silence
    /// reads as the app being broken
    /// </summary>
    public static void UnreadableFileWasOpened(string name)
    {
        CannotDealWith(name,
            "is not something Work Tracker recognises - it is not a backup (.rbf) or a shared work list (.rwk), or it could not be read. "
            + "If it should be one, it may have been renamed or damaged on the way.");
    }

    /// <summary>
    /// The file was one to open, and the app it was opened from would not
    /// hand the bytes over.
    ///
    /// That is nearly always a file the phone has not actually got yet - one
    /// still in Drive, or on an email that has never been downloaded. The
    /// provider has a record of it and nothing to give, and it says so as
    /// "file not found", which is no use to anybody reading it. So it is said
    /// as what to do about it instead.
    /// </summary>
    public static void FileCouldNotBeFetched(string name)
    {
        CannotDealWith(name,
            "could not be read from the app it was opened in. That usually means the file is not on this phone yet - one kept in Drive, or on an email, has to be downloaded first. "
            + "Save it to the phone and open it again from your Files or Downloads.");
    }

    private static void CannotDealWith(string name, string what)
    {
        _pendingUnreadable = $"{(string.IsNullOrWhiteSpace(name) ? "That file" : name)} {what}";

        Action opened = Opened;
        if (opened != null)
            opened();
    }

    /// <summary>what to say about the file that could not be dealt with, if any</summary>
    public static string TakePendingUnreadable()
    {
        string said = _pendingUnreadable;
        _pendingUnreadable = null;
        return said;
    }

    /// <summary>what could not be dealt with, without claiming it</summary>
    public static string PeekPendingUnreadable()
    {
        return _pendingUnreadable;
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
