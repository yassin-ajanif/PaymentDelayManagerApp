namespace PaymentDelayApp.DataAccessLayer;

public static class PaymentDelayDbPaths
{
    public static string DatabaseFilePath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PaymentDelayApp");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "app.db");
        }
    }

    public static string BuildConnectionString() => $"Data Source={DatabaseFilePath}";
}
