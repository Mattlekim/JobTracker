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
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            return builder.Build();
            
            
        }
    }
}