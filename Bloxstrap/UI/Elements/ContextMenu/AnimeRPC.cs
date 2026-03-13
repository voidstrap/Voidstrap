using DiscordRPC;
using DiscordRPC.Message;
using Voidstrap;
using System.Linq;

public static class DiscordManager
{
    private static DiscordRpcClient _client;

    public static void Initialize(string clientId)
    {
        if (_client != null) return; // already initialized
        _client = new DiscordRpcClient(clientId);
        _client.Initialize();
    }

    public static void UpdatePresence(string title, string stateUrl, (string label, string link)[] buttons, string largeImageKey = null)
    {
        if (_client == null) return;

        var rpcButtons = buttons?.Select(b => new DiscordRPC.Button { Label = b.label, Url = b.link }).ToArray();

        var assets = new Assets
        {
            SmallImageKey = "https://voidstrapp.netlify.app/Image/Voidstrap.png",
            SmallImageText = "Voidstrap"
        };

        if (!string.IsNullOrWhiteSpace(largeImageKey))
        {
            assets.LargeImageKey = largeImageKey;
            assets.LargeImageText = title ?? "";
        }

        _client.SetPresence(new DiscordRPC.RichPresence
        {
            Details = title ?? "",
            State = stateUrl ?? "",
            Assets = assets,
            Buttons = rpcButtons
        });
    }

    public static void Shutdown()
    {
        _client?.Dispose();
        _client = null;
    }
}