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
using System.IdentityModel.Tokens.Jwt;
using System.Threading;
using System.Threading.Tasks;
using BlinkDebitApiClient.Config;
using BlinkDebitApiClient.Enums;
using Newtonsoft.Json;
using RestSharp;
using RestSharp.Authenticators;

namespace BlinkDebitApiClient.Client.Auth;

/// <summary>
/// An authenticator for OAuth2 authentication flows
/// </summary>
public class OAuthAuthenticator : AuthenticatorBase, IDisposable
{
    private readonly string _tokenUrl;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _grantType;
    private readonly JsonSerializerSettings _serializerSettings;
    private readonly IReadableConfiguration _configuration;
    private readonly SemaphoreSlim _tokenRefreshSemaphore = new SemaphoreSlim(1, 1);
    private bool _disposed;

    /// <summary>
    /// Initialize the OAuth2 Authenticator
    /// </summary>
    public OAuthAuthenticator(
        string tokenUrl,
        string clientId,
        string clientSecret,
        OAuthFlow? flow,
        JsonSerializerSettings serializerSettings,
        IReadableConfiguration configuration) : base("")
    {
        _tokenUrl = tokenUrl;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _serializerSettings = serializerSettings;
        _configuration = configuration;

        if (flow == OAuthFlow.APPLICATION) _grantType = "client_credentials";
    }

    /// <summary>
    /// Creates an authentication parameter from an access token.
    /// Thread-safe implementation using SemaphoreSlim to prevent concurrent token refresh attempts.
    /// </summary>
    /// <param name="accessToken">Access token to create a parameter from.</param>
    /// <returns>An authentication parameter.</returns>
    protected override async ValueTask<Parameter> GetAuthenticationParameter(string accessToken)
    {
        // Fast path: if token is valid, return immediately without acquiring semaphore
        if (!string.IsNullOrEmpty(Token) && !IsTokenExpired(Token))
        {
            return new HeaderParameter(KnownHeaders.Authorization, Token);
        }

        // Slow path: token needs refresh, acquire semaphore to ensure only one thread refreshes
        await _tokenRefreshSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            // Double-check pattern: another thread might have refreshed while we were waiting
            if (string.IsNullOrEmpty(Token) || IsTokenExpired(Token))
            {
                Token = await GetToken().ConfigureAwait(false);
            }

            return new HeaderParameter(KnownHeaders.Authorization, Token);
        }
        finally
        {
            _tokenRefreshSemaphore.Release();
        }
    }

    /// <summary>
    /// Checks if the token has expired or is about to expire (within 5 minutes).
    /// </summary>
    /// <param name="token">The token to check.</param>
    /// <returns>True if the token is expired or about to expire, false otherwise.</returns>
    private bool IsTokenExpired(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token.Replace(BlinkDebitConstant.BEARER.GetValue(), string.Empty));
            // Add 5-minute buffer to refresh before actual expiration
            var expiryWithBuffer = jwtToken.ValidTo.AddMinutes(-5);
            return expiryWithBuffer <= DateTimeOffset.UtcNow;
        }
        catch
        {
            // If token cannot be parsed, consider it expired
            return true;
        }
    }

    /// <summary>
    /// Gets the token from the OAuth2 server.
    /// </summary>
    /// <returns>An authentication token.</returns>
    private async Task<string> GetToken()
    {
        using var client = new RestClient(_tokenUrl,
            configureSerialization: s => s.UseSerializer(() => new CustomJsonCodec(_serializerSettings, _configuration)));

        var request = new RestRequest()
            .AddParameter("grant_type", _grantType)
            .AddParameter("client_id", _clientId)
            .AddParameter("client_secret", _clientSecret);
        var response = await client.PostAsync<TokenResponse>(request).ConfigureAwait(false);
        return $"{response.TokenType} {response.AccessToken}";
    }

    /// <summary>
    /// Disposes the resources used by the OAuth authenticator.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the resources used by the OAuth authenticator.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _tokenRefreshSemaphore?.Dispose();
        }

        _disposed = true;
    }
}