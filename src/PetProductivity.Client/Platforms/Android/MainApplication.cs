using Android.App;
using Android.Runtime;

namespace PetProductivity.Client;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
        Android.Util.Log.Info("ANTIGRAVITY", "MainApplication Constructor Started");
	}

	protected override MauiApp CreateMauiApp()
    {
        Android.Util.Log.Info("ANTIGRAVITY", "CreateMauiApp Started");
        try
        {
            var app = MauiProgram.CreateMauiApp();
            Android.Util.Log.Info("ANTIGRAVITY", "CreateMauiApp Finished Successfully");
            return app;
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("ANTIGRAVITY", $"FATAL: CreateMauiApp Failed: {ex}");
            throw;
        }
    }

    public override void OnCreate()
    {
        Android.Util.Log.Info("ANTIGRAVITY", "MainApplication OnCreate Started");
        base.OnCreate();

        Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (sender, args) =>
        {
            Android.Util.Log.Error("ANTIGRAVITY_CRASH", $"💥 Unhandled Exception: {args.Exception.Message}\nStack: {args.Exception.StackTrace}");
        };
        Android.Util.Log.Info("ANTIGRAVITY", "MainApplication OnCreate Finished");
    }
}
