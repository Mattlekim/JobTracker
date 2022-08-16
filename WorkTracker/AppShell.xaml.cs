using Kernel;
using UiInterface.Layouts;

namespace WorkTracker
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            Customer.Load();
            Job.Load();
            Payment.Load();
            Settings.Load();
         
            InitializeComponent();
            
        }
    }
}