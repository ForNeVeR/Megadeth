using TdLib;

namespace Megadeth;

public static class TdLibExtensions
{
    public static async Task<TdApi.User> GetCurrentUser(this TdClient client)
    {
        return await client.ExecuteAsync(new TdApi.GetMe());
    }
}
