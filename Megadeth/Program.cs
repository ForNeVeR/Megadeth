using TdLib;
using TDLib.Bindings;

namespace Megadeth;

internal static class Program
{
    private const int ApiId = 0;
    private const string ApiHash = "";
    // PhoneNumber must contain international phone with (+) prefix.
    // For example +16171234567
    private const string PhoneNumber = "";
    private const string ApplicationVersion = "0.0.1";

    private static TdClient _client;
    private static readonly ManualResetEventSlim ReadyToAuthenticate = new();

    private static bool _authNeeded;
    private static bool _passwordNeeded;

    private static TdApi.User _currentUser;

    private static async Task Main()
    {
        // Creating Telegram client and setting minimal verbosity to Fatal since we don't need a lot of logs :)
        _client = new TdClient();
        _client.Bindings.SetLogVerbosityLevel(TdLogLevel.Warning);

        // Subscribing to all events
        _client.UpdateReceived += async (_, update) => { await ProcessUpdates(update); };

        // Waiting until we get enough events to be in 'authentication ready' state
        ReadyToAuthenticate.Wait();

        // We may not need to authenticate since TdLib persists session in 'td.binlog' file.
        // See 'TdlibParameters' class for more information, or:
        // https://core.telegram.org/tdlib/docs/classtd_1_1td__api_1_1tdlib_parameters.html
        if (_authNeeded)
        {
            // Interactively handling authentication
            await HandleAuthentication();
        }

        // Querying info about current user and some channels
        _currentUser = await GetCurrentUser();

        var fullUserName = $"{_currentUser.FirstName} {_currentUser.LastName}".Trim();
        Console.WriteLine($"Successfully logged in as [{_currentUser.Id}] / [@{_currentUser.Username}] / [{fullUserName}]");

        Console.WriteLine("Press ENTER to exit from application");
        Console.ReadLine();
    }

    private static async Task HandleAuthentication()
    {
        // Setting phone number
        await _client.ExecuteAsync(new TdApi.SetAuthenticationPhoneNumber
        {
            PhoneNumber = PhoneNumber
        });

        // Telegram servers will send code to us
        Console.Write("Insert the login code: ");
        var code = Console.ReadLine();

        await _client.ExecuteAsync(new TdApi.CheckAuthenticationCode
        {
            Code = code
        });

        if(!_passwordNeeded) { return; }

        // 2FA may be enabled. Cloud password is required in that case.
        Console.Write("Insert the password: ");
        var password = Console.ReadLine();

        await _client.ExecuteAsync(new TdApi.CheckAuthenticationPassword
        {
            Password = password
        });
    }

    private static async Task ProcessUpdates(TdApi.Update update)
    {
        // Since Tdlib was made to be used in GUI application we need to struggle a bit and catch required events to determine our state.
        // Below you can find example of simple authentication handling.
        // Please note that AuthorizationStateWaitOtherDeviceConfirmation is not implemented.

        switch (update)
        {
            case TdApi.Update.UpdateAuthorizationState { AuthorizationState: TdApi.AuthorizationState.AuthorizationStateWaitTdlibParameters }:
                // TdLib creates database in the current directory.
                // so create separate directory and switch to that dir.
                var filesLocation = Path.Combine(AppContext.BaseDirectory, "db");
                await _client.ExecuteAsync(new TdApi.SetTdlibParameters
                {
                    Parameters = new TdApi.TdlibParameters
                    {
                        ApiId = ApiId,
                        ApiHash = ApiHash,
                        DeviceModel = "PC",
                        SystemLanguageCode = "en",
                        ApplicationVersion = ApplicationVersion,
                        DatabaseDirectory = filesLocation,
                        FilesDirectory = filesLocation,
                        // More parameters available!
                    }
                });
                break;

            case TdApi.Update.UpdateAuthorizationState { AuthorizationState: TdApi.AuthorizationState.AuthorizationStateWaitEncryptionKey }:
                await _client.ExecuteAsync(new TdApi.CheckDatabaseEncryptionKey());
                break;

            case TdApi.Update.UpdateAuthorizationState { AuthorizationState: TdApi.AuthorizationState.AuthorizationStateWaitPhoneNumber }:
            case TdApi.Update.UpdateAuthorizationState { AuthorizationState: TdApi.AuthorizationState.AuthorizationStateWaitCode }:
                _authNeeded = true;
                ReadyToAuthenticate.Set();
                break;

            case TdApi.Update.UpdateAuthorizationState { AuthorizationState: TdApi.AuthorizationState.AuthorizationStateWaitPassword }:
                _authNeeded = true;
                _passwordNeeded = true;
                ReadyToAuthenticate.Set();
                break;

            case TdApi.Update.UpdateUser:
                ReadyToAuthenticate.Set();
                break;

            case TdApi.Update.UpdateConnectionState { State: TdApi.ConnectionState.ConnectionStateReady }:
                // You may trigger additional event on connection state change
                break;

            case TdApi.Update.UpdateNewMessage { Message: var m and { Content: TdApi.MessageContent.MessageText text,
                        SenderId: TdApi.MessageSender.MessageSenderUser sender } }
                when sender.UserId == _currentUser?.Id:
                var message = text.Text.Text;

                Console.WriteLine($"Self message received: {message}");

                if (message.StartsWith("/megadeth "))
                {
                    var nickname = message.Substring("/megadeth ".Length).Trim();
                    var repliedMessage = await _client.GetRepliedMessageAsync(chatId: m.ChatId, messageId: m.Id);

                    var reactions = await _client.GetMessageAddedReactionsAsync(chatId: m.ChatId, messageId: repliedMessage.Id, limit: 30);

                    foreach (var reaction in reactions.Reactions)
                    {
                        var userId = reaction.SenderId as TdApi.MessageSender.MessageSenderUser;
                        if (userId == null) continue;

                        var userInfo =  await _client.GetUserAsync(userId.UserId);
                        var userName = string.IsNullOrWhiteSpace(userInfo.FirstName)
                            ? userInfo.LastName
                            : $"{userInfo.FirstName} ${userInfo.LastName}";
                        userName = userName.Trim();

                        if (userName.StartsWith(nickname))
                        {
                            await _client.BanChatMemberAsync(m.ChatId, userId);
                        }
                    }
                }

                break;

            default:
                // ReSharper disable once EmptyStatement
                ;
                // Add a breakpoint here to see other events
                break;
        }
    }

    private static async Task<TdApi.User> GetCurrentUser()
    {
        return await _client.ExecuteAsync(new TdApi.GetMe());
    }
}
