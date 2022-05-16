using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TdLib;

namespace Megadeth;

public class Worker: BackgroundService
{
    private readonly AppSettings _appSettings;
    private TdClient _client;

    public Worker(
        TdClient client,
        AppSettings appSettings)
    {
        _client = client;
        _appSettings = appSettings;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client.UpdateReceived += async (_, update) => { await ProcessUpdates(update); };
        _appSettings.ReadyToAuthenticate.Wait();
        // We may not need to authenticate since TdLib persists session in 'td.binlog' file.
        // See 'TdlibParameters' class for more information, or:
        // https://core.telegram.org/tdlib/docs/classtd_1_1td__api_1_1tdlib_parameters.html
        if (_appSettings.AuthNeeded)
        {
            // Interactively handling authentication
            await HandleAuthentication();
        }

        // Querying info about current user and some channels
        var currentUser = await _client.GetCurrentUser();

        var fullUserName = $"{currentUser.FirstName} {currentUser.LastName}".Trim();
        Console.WriteLine($"Successfully logged in as [{currentUser.Id}] / [@{currentUser.Username}] / [{fullUserName}]");

        Console.WriteLine("Press ENTER to exit from application");

    }

    private async Task HandleAuthentication()
    {
        // Setting phone number
        await _client.ExecuteAsync(new TdApi.SetAuthenticationPhoneNumber
        {
            PhoneNumber = _appSettings.PhoneNumber
        });

        // Telegram servers will send code to us
        Console.Write("Insert the login code: ");
        var code = Console.ReadLine();

        await _client.ExecuteAsync(new TdApi.CheckAuthenticationCode
        {
            Code = code
        });

        if(!_appSettings.PasswordNeeded) { return; }

        // 2FA may be enabled. Cloud password is required in that case.
        Console.Write("Insert the password: ");
        var password = Console.ReadLine();

        await _client.ExecuteAsync(new TdApi.CheckAuthenticationPassword
        {
            Password = password
        });
    }

    private async Task ProcessUpdates(TdApi.Update update)
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
                        ApiId = _appSettings.ApiId,
                        ApiHash = _appSettings.ApiHash,
                        DeviceModel = "PC",
                        SystemLanguageCode = "en",
                        ApplicationVersion = _appSettings.ApplicationVersion,
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
                _appSettings.AuthNeeded = true;
                _appSettings.ReadyToAuthenticate.Set();
                break;

            case TdApi.Update.UpdateAuthorizationState { AuthorizationState: TdApi.AuthorizationState.AuthorizationStateWaitPassword }:
                _appSettings.AuthNeeded = true;
                _appSettings.PasswordNeeded = true;
                _appSettings.ReadyToAuthenticate.Set();
                break;

            case TdApi.Update.UpdateUser:
                _appSettings.ReadyToAuthenticate.Set();
                break;

            case TdApi.Update.UpdateConnectionState { State: TdApi.ConnectionState.ConnectionStateReady }:
                // You may trigger additional event on connection state change
                break;

            case TdApi.Update.UpdateNewMessage { Message: var m and { Content: TdApi.MessageContent.MessageText text,
                        SenderId: TdApi.MessageSender.MessageSenderUser sender } }
                when sender.UserId == _client.GetCurrentUser()?.Id:
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
}
