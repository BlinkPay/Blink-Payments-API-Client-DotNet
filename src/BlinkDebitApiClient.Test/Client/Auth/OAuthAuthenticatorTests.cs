/*
 * Copyright (c) 2025 BlinkPay
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BlinkDebitApiClient.Client.Auth;
using BlinkDebitApiClient.Config;
using BlinkDebitApiClient.Exceptions;
using Newtonsoft.Json;
using RestSharp;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace BlinkDebitApiClient.Test.Client.Auth;

/// <summary>
/// Unit tests for <see cref="OAuthAuthenticator"/>'s token caching and expiry handling, against a
/// stubbed token endpoint rather than the sandbox. The deadline is derived from the OAuth2 response
/// alone, so a stub is enough to drive every branch of it, and counting the requests the stub
/// receives is what distinguishes a cached token from a refreshed one.
/// </summary>
public class OAuthAuthenticatorTests : IDisposable
{
    private const string TokenPath = "/oauth2/token";

    private readonly WireMockServer _tokenServer = WireMockServer.Start();

    public void Dispose()
    {
        _tokenServer.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact(DisplayName = "A token is fetched once and then reused until its deadline")]
    public async Task TokenIsCachedForItsLifetime()
    {
        StubToken("{\"token_type\":\"Bearer\",\"access_token\":\"test-token\",\"expires_in\":3600}");
        using var authenticator = CreateAuthenticator();

        var first = await AuthenticateAsync(authenticator);
        var second = await AuthenticateAsync(authenticator);

        Assert.Equal("Bearer test-token", first);
        Assert.Equal(first, second);
        Assert.Equal(1, TokenRequestCount);
    }

    [Fact(DisplayName = "The expiry buffer never consumes more than half of a short-lived token")]
    public async Task BufferIsCappedAtHalfOfTheTokenLifetime()
    {
        // Twenty seconds is far shorter than the five-minute buffer: uncapped, the deadline would land
        // in the past and the token would be refetched on every call rather than being usable at all.
        // The cap leaves ten seconds of validity, which is ample for two calls even on a loaded runner
        StubToken("{\"token_type\":\"Bearer\",\"access_token\":\"short-lived\",\"expires_in\":20}");
        using var authenticator = CreateAuthenticator();

        await AuthenticateAsync(authenticator);
        await AuthenticateAsync(authenticator);

        Assert.Equal(1, TokenRequestCount);
    }

    [Fact(DisplayName = "A token is refetched once its deadline has passed")]
    public async Task TokenIsRefetchedAfterItsDeadline()
    {
        // expires_in of four seconds caps the buffer at two, leaving a two-second window of validity.
        // Overshooting it is what the test needs, so a slow runner can only make this more certain
        StubToken("{\"token_type\":\"Bearer\",\"access_token\":\"short-lived\",\"expires_in\":4}");
        using var authenticator = CreateAuthenticator();

        await AuthenticateAsync(authenticator);
        await Task.Delay(TimeSpan.FromMilliseconds(2500));
        await AuthenticateAsync(authenticator);

        Assert.Equal(2, TokenRequestCount);
    }

    [Theory(DisplayName = "A token response without a usable expires_in falls back to the default lifetime")]
    [InlineData("{\"token_type\":\"Bearer\",\"access_token\":\"no-lifetime\"}")]
    [InlineData("{\"token_type\":\"Bearer\",\"access_token\":\"no-lifetime\",\"expires_in\":0}")]
    [InlineData("{\"token_type\":\"Bearer\",\"access_token\":\"no-lifetime\",\"expires_in\":-1}")]
    public async Task MissingExpiresInFallsBackToTheDefaultLifetime(string tokenResponse)
    {
        StubToken(tokenResponse);
        using var authenticator = CreateAuthenticator();

        await AuthenticateAsync(authenticator);
        await AuthenticateAsync(authenticator);

        // An assumed one-hour lifetime keeps the token cached; treating it as expired instead would
        // re-POST to the token endpoint on every single call
        Assert.Equal(1, TokenRequestCount);
    }

    [Fact(DisplayName = "A rejected token request reports the status it came back with")]
    public async Task RejectedTokenRequestReportsItsStatus()
    {
        // Wrong credentials are the likeliest real failure here, and they are worth telling apart from
        // a malformed body: both end up without an access token to use
        StubToken("{\"error\":\"invalid_client\"}", HttpStatusCode.Unauthorized);
        using var authenticator = CreateAuthenticator();

        var exception =
            await Assert.ThrowsAsync<BlinkServiceException>(() => AuthenticateAsync(authenticator));

        Assert.Contains("failed with HTTP 401", exception.Message);
        Assert.DoesNotContain("invalid_client", exception.Message);
    }

    [Fact(DisplayName = "An unreadable token response reports the parse failure, not a missing token")]
    public async Task UnreadableTokenResponseReportsTheParseFailure()
    {
        StubToken("this is not json");
        using var authenticator = CreateAuthenticator();

        var exception =
            await Assert.ThrowsAnyAsync<BlinkServiceException>(() => AuthenticateAsync(authenticator));

        Assert.DoesNotContain("returned no access token", exception.Message);
    }

    [Fact(DisplayName = "A token response carrying no access token is rejected")]
    public async Task MissingAccessTokenThrows()
    {
        StubToken("{\"token_type\":\"Bearer\"}");
        using var authenticator = CreateAuthenticator();

        var exception =
            await Assert.ThrowsAsync<BlinkServiceException>(() => AuthenticateAsync(authenticator));

        Assert.Contains("returned no access token", exception.Message);
    }

    [Fact(DisplayName = "Concurrent callers share a single token request")]
    public async Task ConcurrentCallersShareOneTokenRequest()
    {
        // The delay holds the first caller inside the refresh long enough for the rest to pile up
        // behind the semaphore, which is where a duplicate request would show up
        StubToken("{\"token_type\":\"Bearer\",\"access_token\":\"test-token\",\"expires_in\":3600}",
            delay: TimeSpan.FromMilliseconds(200));
        using var authenticator = CreateAuthenticator();

        var tokens = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => AuthenticateAsync(authenticator))));

        Assert.All(tokens, token => Assert.Equal("Bearer test-token", token));
        Assert.Equal(1, TokenRequestCount);
    }

    private int TokenRequestCount =>
        _tokenServer.LogEntries.Count(entry => entry.RequestMessage.Path == TokenPath);

    private void StubToken(string body, HttpStatusCode statusCode = HttpStatusCode.OK, TimeSpan? delay = null)
    {
        var response = Response.Create()
            .WithStatusCode(statusCode)
            .WithHeader("Content-Type", "application/json")
            .WithBody(body);

        if (delay != null)
        {
            response = response.WithDelay(delay.Value);
        }

        _tokenServer.Given(Request.Create().WithPath(TokenPath).UsingPost()).RespondWith(response);
    }

    private OAuthAuthenticator CreateAuthenticator()
    {
        // The credentials are only ever posted to the stub on localhost
        return new OAuthAuthenticator(_tokenServer.Url + TokenPath, "test-client-id", "test-client-secret",
            OAuthFlow.APPLICATION, new JsonSerializerSettings(), new Configuration());
    }

    /// <summary>
    /// Authenticates a request and returns the Authorization header the authenticator attached, which
    /// is the only public view of the token it is holding.
    /// </summary>
    private static async Task<string> AuthenticateAsync(OAuthAuthenticator authenticator)
    {
        using var client = new RestClient("http://localhost");
        var request = new RestRequest();

        await authenticator.Authenticate(client, request);

        return request.Parameters
            .First(parameter => parameter.Name == KnownHeaders.Authorization)
            .Value?
            .ToString();
    }
}
