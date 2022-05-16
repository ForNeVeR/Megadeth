namespace Megadeth;

public class AppSettings
{
    public int ApiId { get; init; }
    public string ApiHash { get; init; }
    public  string PhoneNumber { get; init; }
    public  string ApplicationVersion { get; set; }

    public  bool AuthNeeded { get; set; }
    public  bool PasswordNeeded { get; set; }
    public readonly ManualResetEventSlim ReadyToAuthenticate = new();
}
