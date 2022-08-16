using Kernel;

namespace UiInterface
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Customer.Load();
            

        }
    }
}