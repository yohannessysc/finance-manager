using System.Net;

namespace FinanceManager.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class SessionTests(ApiFactory factory)
{
    [Fact]
    public async Task SigningOutOtherDevicesEndsOnlyTheirSessions()
    {
        const string email = "devices@example.com";
        var deviceA = await ApiSession.SignUpAsync(factory, email);

        var deviceB = new ApiSession(factory.CreateHttpsClient());
        var login = await deviceB.PostAsync("/api/v1/auth/login",
            new { email, password = "Correct-Horse-Battery-9" });
        Assert.True(login.IsSuccessStatusCode);

        // Both devices are signed in independently.
        Assert.Equal(HttpStatusCode.OK, (await deviceA.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await deviceB.GetAsync("/api/v1/auth/me")).StatusCode);

        var revoke = await deviceB.PostAsync("/api/v1/sessions/revoke-others");
        Assert.True(revoke.IsSuccessStatusCode, await revoke.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, (await deviceA.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await deviceB.GetAsync("/api/v1/auth/me")).StatusCode);
    }
}
