namespace PaymentDelayApp.DataAccessLayer;

/// <summary>Cross-process single-instance guard for the desktop app (named mutex).</summary>
public static class PaymentDelayAppSingleInstance
{
    public const string MutexName = "PaymentDelayApp_SingleInstance_v1";
}
