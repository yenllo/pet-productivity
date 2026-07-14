using Microsoft.Maui.Storage;

namespace PetProductivity.Client.Services;

public class SettingsService
{
    private const string KeyServerUrl = "server_url";
    private const string DefaultServerUrl = PetProductivity.Shared.Constants.BaseUrl;

    public string ServerUrl
    {
        get => Preferences.Get(KeyServerUrl, DefaultServerUrl);
        set => Preferences.Set(KeyServerUrl, value);
    }
}
