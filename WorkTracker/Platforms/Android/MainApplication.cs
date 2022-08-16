using Android.App;
using Android.Runtime;
using Android.Content;
using Android.Telephony;
namespace WorkTracker
{

    public class AndroidGloable
    {
        public static MainApplication Main_Application;
        public static MainActivity Main_Activity;




    }

    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
            AndroidGloable.Main_Application = this;
            Context context = Android.App.Application.Context;
            Microsoft.Maui.ApplicationModel.Platform.Init(this);
            
            //      SmsManager sms =  
            //    sms.SendTextMessage(null, "", "", null, null);

            // var v = context.GetSystemService("smsmanagerservice");
            //     Android.Views.TextService.TextServicesManager v = context.GetSystemService(Context.TextServicesManagerService) as Android.Views.TextService.TextServicesManager;            

        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}