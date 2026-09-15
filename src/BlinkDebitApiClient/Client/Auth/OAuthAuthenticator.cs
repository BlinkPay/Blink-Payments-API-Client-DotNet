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
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using BlinkDebitApiClient.Config;
using BlinkDebitApiClient.Exceptions;
using Newtonsoft.Json;
using RestSharp;
using RestSharp.Authenticators;

namespace BlinkDebitApiClient.Client.Auth;

/// <summary>
/// An authenticator for OAuth2 authentication flows
/// </summary>
public class OAuthAuthenticator : AuthenticatorBase, IDisposable
{
    /// <summary>
    /// How long before the actual expiry the token is treated as expired, so that it is refreshed
    /// ahead of time rather than mid-request.
    /// </summary>
    private const int ExpiryBufferSeconds = 300;

    /// <summary>
    /// The lifetime assumed when the token response omits <c>expires_in</c>, which RFC 6749 section 5.1
    /// only recommends rather than requires. BlinkPay issues one-hour tokens, so a response missing the
    /// field is treated as an ordinary one.
    /// </summary>
    private const int DefaultExpiresInSeconds = 3600;

    private readonly string _tokenUrl;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _grantType;
    private readonly JsonSerializerSettings _serializerSettings;
    private readonly IReadableConfiguration _configuration;
    private readonly SemaphoreSlim _tokenRefreshSemaphore = new SemaphoreSlim(1, 1);

    /// <summary>
    /// The <see cref="Environment.TickCount64"/> reading at which the current token must be refreshed.
    /// A monotonic clock is used rather than the wall clock so that a clock adjustment cannot extend
    /// the lifetime of a token. Zero until the first token is fetched, which makes the authenticator
    /// start out with an expired token.
    /// </summary>
    private long _tokenExpiryMillis;

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
        // Fast path: if the token is valid, return immediately without acquiring the semaphore. The
        // token must be read after IsTokenExpired(), whose interlocked read fences it: reading it
        // first would allow a token from before the last refresh to be paired with the new deadline
        if (!IsTokenExpired())
        {
            var currentToken = Token;
            if (!string.IsNullOrEmpty(currentToken))
            {
                return new HeaderParameter(KnownHeaders.Authorization, currentToken);
            }
        }

        // Slow path: token needs refresh, acquire semaphore to ensure only one thread refreshes
        await _tokenRefreshSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            // Double-check pattern: another thread might have refreshed while we were waiting
            if (string.IsNullOrEmpty(Token) || IsTokenExpired())
            {
                var (token, expiryMillis) = await GetToken().ConfigureAwait(false);

                // Publish the deadline after the token it describes: a reader that sees the new token
                // against the old deadline merely takes the slow path and re-checks, whereas the
                // reverse order would hand out an expired token as though it were fresh
                Token = token;
                Interlocked.Exchange(ref _tokenExpiryMillis, expiryMillis);
            }

            return new HeaderParameter(KnownHeaders.Authorization, Token);
        }
        finally
        {
            _tokenRefreshSemaphore.Release();
        }
    }

    /// <summary>
    /// Marks the cached token as expired, so that the next request fetches a fresh one. Called when the
    /// API rejects the credential the token represents, which is the only evidence this client gets that
    /// a token died before the deadline it was given.
    /// <para>
    /// Only the deadline is dropped, never <see cref="AuthenticatorBase.Token"/> itself: a caller on the
    /// fast path would otherwise attach an empty Authorization header. Zeroing the deadline sends it
    /// down the slow path instead, where the semaphore collapses concurrent callers into one refresh.
    /// </para>
    /// </summary>
    public void Invalidate()
    {
        Interlocked.Exchange(ref _tokenExpiryMillis, 0);
    }

    /// <summary>
    /// Checks if the token has expired or is about to expire, against the deadline recorded when the
    /// token was fetched. The deadline comes from the OAuth2 token response rather than from the
    /// token itself: the access token is opaque to this client and its claims are unverified here.
    /// </summary>
    /// <returns>True if the token is expired or about to expire, false otherwise.</returns>
    private bool IsTokenExpired()
    {
        return Environment.TickCount64 >= Interlocked.Read(ref _tokenExpiryMillis);
    }

    /// <summary>
    /// Gets the token from the OAuth2 server.
    /// </summary>
    /// <returns>The authentication token and the monotonic deadline at which it must be refreshed.</returns>
    private async Task<(string Token, long ExpiryMillis)> GetToken()
    {
        // The configured timeout applies here as it does to every other request: RestSharp would
        // otherwise fall back to its own default, and a token fetch could outlast the caller's budget
        var clientOptions = new RestClientOptions(_tokenUrl)
        {
            Timeout = TimeSpan.FromMilliseconds(_configuration.Timeout)
        };

        using var client = new RestClient(clientOptions,
            configureSerialization: s => s.UseSerializer(() => new CustomJsonCodec(_serializerSettings, _configuration)));

        var request = new RestRequest()
            .AddParameter("grant_type", _grantType)
            .AddParameter("client_id", _clientId)
            .AddParameter("client_secret", _clientSecret);
        var response = await client.ExecutePostAsync<TokenResponse>(request).ConfigureAwait(false);
        if (response.StatusCode == 0)
        {
            // No status code means the server never answered. Any exception is rethrown as it stands,
            // because the retry policy handles the transport failures by type and would stop
            // recognising them behind a wrapper; it is only wrapped once the policy gives up on it
            if (response.ErrorException != null)
            {
                ExceptionDispatchInfo.Capture(response.ErrorException).Throw();
            }

            throw new BlinkServiceException($"OAuth2 token request to {_tokenUrl} got no response");
        }

        if (!response.IsSuccessStatusCode)
        {
            // The status is what tells wrong credentials apart from a malformed body. The body itself
            // is deliberately left out: it echoes back what was sent, the client ID included
            throw new BlinkServiceException(
                $"OAuth2 token request to {_tokenUrl} failed with HTTP {(int)response.StatusCode}");
        }

        if (response.ErrorException != null)
        {
            // The server answered, but the body could not be read. The codec has already wrapped the
            // parse failure, and its message says more than a missing access token would
            ExceptionDispatchInfo.Capture(response.ErrorException).Throw();
        }

        var token = response.Data;
        if (token == null || string.IsNullOrEmpty(token.AccessToken))
        {
            throw new BlinkServiceException(
                $"OAuth2 token request to {_tokenUrl} returned no access token");
        }

        var expiresIn = token.ExpiresIn > 0 ? token.ExpiresIn : DefaultExpiresInSeconds;

        // Never give up more than half the lifetime of a short-lived token to the buffer
        var bufferSeconds = Math.Min(ExpiryBufferSeconds, expiresIn / 2);
        var expiryMillis = Environment.TickCount64 + (expiresIn - bufferSeconds) * 1000L;

        return ($"{token.TokenType} {token.AccessToken}", expiryMillis);
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