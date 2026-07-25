using Plume.Pages;
using Xunit;

namespace Plume.Tests.Pages;

public sealed class LoginGuestSlugTests
{
    [Theory]
    [InlineData("/control/party", "party")]
    [InlineData("/control/My-Show", "my-show")]
    [InlineData("/player/party?x=1", "party")]
    [InlineData("/control/party/extra", "party")]
    [InlineData("/login", null)]
    [InlineData("https://evil.example/control/party", null)]
    [InlineData("//evil.example/control/party", null)]
    public void TryExtractSlug_reads_control_or_player_path(string? returnUrl, string? expected)
    {
        Assert.Equal(expected, LoginModel.TryExtractSlug(returnUrl));
    }

    [Theory]
    [InlineData("/control/party", "/control/party")]
    [InlineData("/", "/")]
    [InlineData("https://evil.example/", null)]
    [InlineData("//evil.example/", null)]
    [InlineData(null, null)]
    public void SafeLocalRedirect_allows_same_site_paths_only(string? returnUrl, string? expected)
    {
        Assert.Equal(expected, LoginModel.SafeLocalRedirect(returnUrl));
    }
}
