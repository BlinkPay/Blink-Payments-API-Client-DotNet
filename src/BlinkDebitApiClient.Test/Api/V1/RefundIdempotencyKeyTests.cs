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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BlinkDebitApiClient.Api.V1;
using BlinkDebitApiClient.Client;
using BlinkDebitApiClient.Client.Auth;
using BlinkDebitApiClient.Config;
using BlinkDebitApiClient.Enums;
using BlinkDebitApiClient.Exceptions;
using BlinkDebitApiClient.Model.V1;
using Microsoft.Extensions.Logging;
using Polly;
using RestSharp;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace BlinkDebitApiClient.Test.Api.V1;

/// <summary>
/// Pins the idempotency-key header <see cref="RefundsApi.CreateRefundAsync"/> puts on the wire, against a
/// stub rather than the sandbox. Without a key a retried refund pays the customer twice, so what reaches
/// the server is the behaviour worth pinning — not just what the dictionary was seeded with.
/// <para>
/// Joins the integration collection because <see cref="RetryConfiguration"/> holds the retry policies in
/// process-wide static state: the retry test below swaps one in, and the collection stops other tests
/// running while it is installed.
/// </para>
/// </summary>
[Collection("Blink Debit Collection")]
public class RefundIdempotencyKeyTests : IDisposable
{
    private const string RefundPath = "/payments/v1/refunds";

    private const string TokenPath = "/oauth2/token";

    private static readonly string IdempotencyKeyHeader = BlinkDebitConstant.IDEMPOTENCY_KEY.GetValue();

    private readonly WireMockServer _server = WireMockServer.Start();

    private readonly Policy<RestResponse> _originalRetryPolicy = RetryConfiguration.RetryPolicy;

    private readonly AsyncPolicy<RestResponse> _originalAsyncRetryPolicy = RetryConfiguration.AsyncRetryPolicy;

    private readonly ILogger _logger = LoggerFactory.Create(builder => builder.AddDebug())
        .CreateLogger<RefundIdempotencyKeyTests>();

    public RefundIdempotencyKeyTests()
    {
        _server.Given(Request.Create().WithPath(TokenPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"access_token\":\"test-token\",\"token_type\":\"Bearer\",\"expires_in\":3600}"));
    }

    public void Dispose()
    {
        RetryConfiguration.RetryPolicy = _originalRetryPolicy;
        RetryConfiguration.AsyncRetryPolicy = _originalAsyncRetryPolicy;
        _server.Stop();
        _server.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact(DisplayName = "A caller-supplied idempotency key reaches the server unchanged")]
    public async Task CallerSuppliedKeyIsSentVerbatim()
    {
        StubRefundCreated(Guid.NewGuid());

        const string idempotencyKey = "a3f1c0de-5c2b-4f3a-9b7e-2c1d4e5f6a7b";
        var requestHeaders = new Dictionary<string, string?>
        {
            [IdempotencyKeyHeader] = idempotencyKey
        };

        var client = CreateClient(retryEnabled: false);

        await client.CreateRefundAsync(new AccountNumberRefundRequest(Guid.NewGuid()), requestHeaders);

        Assert.Equal(idempotencyKey, Assert.Single(SentIdempotencyKeys()));
    }

    [Fact(DisplayName = "A refund sent without an idempotency key still carries a generated one")]
    public async Task GeneratedKeyIsSentWhenTheCallerOmitsOne()
    {
        StubRefundCreated(Guid.NewGuid());

        var client = CreateClient(retryEnabled: false);

        await client.CreateRefundAsync(new AccountNumberRefundRequest(Guid.NewGuid()));

        var sentKey = Assert.Single(SentIdempotencyKeys());
        Assert.True(Guid.TryParse(sentKey, out var parsedKey), $"'{sentKey}' is not a GUID");
        Assert.NotEqual(Guid.Empty, parsedKey);
    }

    [Fact(DisplayName = "Every retry attempt of a refund carries the same generated idempotency key")]
    public async Task GeneratedKeyIsReusedAcrossRetries()
    {
        _server.Given(Request.Create().WithPath(RefundPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.InternalServerError)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"message\":\"upstream unavailable\"}"));

        // A stand-in policy, because none of the exceptions BlinkDebitClient.ConfigureRetry handles can
        // replay a refund POST that has already been sent: RestSharp reports a transport fault on the
        // API leg as a response rather than an exception, and an HTTP status only becomes an exception
        // after the policy has finished. What is under test is that a replay re-sends the request the
        // client already built, which is the same code path whatever made the policy retry.
        RetryConfiguration.AsyncRetryPolicy = Policy<RestResponse>
            .HandleResult(response => (int)response.StatusCode == 500)
            .RetryAsync(2);

        var client = CreateClient(retryEnabled: true);

        // A 5xx leaves the operation as BlinkRetryableException, which does not derive from
        // BlinkServiceException, so a caller following the README's advice does not catch it
        await Assert.ThrowsAsync<BlinkRetryableException>(() =>
            client.CreateRefundAsync(new AccountNumberRefundRequest(Guid.NewGuid())));

        var sentKeys = SentIdempotencyKeys();

        Assert.Equal(3, sentKeys.Count);
        Assert.Single(sentKeys.Distinct());
    }

    /// <summary>
    /// Builds a client against the stub. The configuration is handed over as-is rather than through the
    /// URL constructor, whose validation rightly rejects the stub's plain-HTTP loopback address.
    /// </summary>
    private BlinkDebitClient CreateClient(bool retryEnabled)
    {
        var configuration = new Configuration
        {
            BasePath = _server.Url + "/payments/v1",
            OAuthTokenUrl = _server.Url + TokenPath,
            OAuthClientId = "test-client-id",
            OAuthClientSecret = "test-client-secret",
            OAuthFlow = OAuthFlow.APPLICATION,
            RetryEnabled = retryEnabled
        };

        return new BlinkDebitClient(_logger, new ApiClient(_logger, configuration), configuration);
    }

    private void StubRefundCreated(Guid refundId)
    {
        _server.Given(Request.Create().WithPath(RefundPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.Created)
                .WithHeader("Content-Type", "application/json")
                .WithBody($"{{\"refund_id\":\"{refundId}\"}}"));
    }

    /// <summary>
    /// The idempotency key of every refund request the stub received, in order. Single() on the header
    /// values also pins that the key is sent once rather than repeated.
    /// </summary>
    private List<string> SentIdempotencyKeys()
    {
        return _server.LogEntries
            .Where(entry => entry.RequestMessage.Path == RefundPath)
            .Select(entry => entry.RequestMessage.Headers!
                .First(header => string.Equals(header.Key, IdempotencyKeyHeader,
                    StringComparison.OrdinalIgnoreCase))
                .Value.Single())
            .ToList();
    }
}
