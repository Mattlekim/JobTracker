using Android.OS;
using Android.Views;
using AndroidX.ViewPager2.Widget;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;

namespace WorkTracker
{
    /// <summary>
    /// Stops the top tabs (work overview / list) changing page when the user
    /// swipes - horizontal swiping is reserved for the job swipe actions.
    /// The views are still switched by tapping the tab headers.
    /// </summary>
    public class NoSwipeShellRenderer : ShellRenderer
    {
        protected override IShellSectionRenderer CreateShellSectionRenderer(ShellSection shellSection)
        {
            return new NoSwipeShellSectionRenderer(this);
        }
    }

    public class NoSwipeShellSectionRenderer : ShellSectionRenderer
    {
        public NoSwipeShellSectionRenderer(IShellContext shellContext) : base(shellContext)
        {
        }

        public override global::Android.Views.View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            global::Android.Views.View view = base.OnCreateView(inflater, container, savedInstanceState);
            if (view is ViewGroup vg)
                DisableSwipePaging(vg);
            return view;
        }

        static void DisableSwipePaging(ViewGroup group)
        {
            for (int i = 0; i < group.ChildCount; i++)
            {
                global::Android.Views.View child = group.GetChildAt(i);
                if (child is ViewPager2 pager)
                    pager.UserInputEnabled = false;
                else if (child is ViewGroup g)
                    DisableSwipePaging(g);
            }
        }
    }
}
