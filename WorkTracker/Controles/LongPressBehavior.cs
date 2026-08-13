namespace UiInterface.Controles;

/// <summary>
/// A page whose rows can be held.
///
/// The behaviour finds it by walking up from the row rather than being told
/// which page it is on, because a row lives in a DataTemplate: it has no way
/// of naming the page and a binding to one from inside a template is a
/// standing source of trouble.
/// </summary>
public interface IHoldRows
{
    /// <summary>a row has been held. the item is the row's binding context</summary>
    void RowHeld(object item);
}

/// <summary>
/// A long press that actually happens on a phone.
///
/// MAUI has no long press gesture of its own. The obvious way round that is
/// to time the finger going down with a PointerGestureRecognizer, which is
/// what the job rows used to do - and on Android it never fired at all,
/// because those events are raised for a mouse or a stylus hovering rather
/// than for a finger. The hold did nothing on any page, however long it was
/// held for.
///
/// So on Android this hands the job to Android, which has had a long press
/// since the beginning: the row is made long clickable and the platform
/// decides when a press has been held long enough. That also brings the
/// buzz on the wrist that tells you it worked, which no amount of timing in
/// managed code would have given.
///
/// Everywhere else the pointer recognisers on the rows still do the timing,
/// and both go through the same handler, so a platform that fires both only
/// acts once.
/// </summary>
public class LongPressBehavior : Behavior<View>
{
    private View _view;

    protected override void OnAttachedTo(View view)
    {
        base.OnAttachedTo(view);

        _view = view;

        //the list is virtualised, so a row is handed a platform view again
        //every time it is scrolled back into view
        view.HandlerChanged += View_HandlerChanged;
        Hook();
    }

    protected override void OnDetachingFrom(View view)
    {
        base.OnDetachingFrom(view);

        view.HandlerChanged -= View_HandlerChanged;

        Unhook();
        _view = null;
    }

    private void View_HandlerChanged(object sender, EventArgs e)
    {
        Hook();
    }

    /// <summary>
    /// tells the page the row is on. the row is read for its binding context
    /// at the moment it is held rather than at the moment it was built, so a
    /// recycled row always reports the job it is showing now.
    /// </summary>
    private void Fire()
    {
        if (_view == null)
            return;

        object item = _view.BindingContext;

        for (Element e = _view; e != null; e = e.Parent)
        {
            IHoldRows page = e as IHoldRows;

            if (page != null)
            {
                page.RowHeld(item);
                return;
            }
        }
    }

#if ANDROID

    private Android.Views.View _platformView;

    private void Hook()
    {
        //never twice on the same view: a recycled row is hooked again every
        //time it comes back round
        Unhook();

        _platformView = _view == null || _view.Handler == null
            ? null
            : _view.Handler.PlatformView as Android.Views.View;

        if (_platformView == null)
            return;

        _platformView.LongClickable = true;
        _platformView.LongClick += PlatformView_LongClick;
    }

    private void Unhook()
    {
        if (_platformView == null)
            return;

        _platformView.LongClick -= PlatformView_LongClick;
        _platformView = null;
    }

    private void PlatformView_LongClick(object sender, Android.Views.View.LongClickEventArgs e)
    {
        //handled, so the press does not go on to count as a tap as well
        e.Handled = true;
        Fire();
    }

#else

    //every other platform still times the hold with the pointer recognisers
    //on the row, which is where they do work
    private void Hook()
    {
    }

    private void Unhook()
    {
    }

#endif
}
