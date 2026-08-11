using Plugin.Maui.OCR;

namespace WorkTracker
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            CrashLogger.Init();

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseOcr()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if ANDROID
            builder.ConfigureMauiHandlers(handlers =>
            {
                handlers.AddHandler(typeof(Shell), typeof(NoSwipeShellRenderer));
            });
#endif

            return builder.Build();
            
            
        }
    }
}