/*
 * Copyright (c) 2026 BlinkPay
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
/// Pins what a successful response turns into, against a stub rather than the sandbox: a body the API
/// legitimately sends must come back as data, and a body that does not fit the model must come back as
/// an exception, never as null. A merchant found the gap on a quick payment whose customer had left the
/// gateway page before choosing a bank — a consent that has never changed status carries no
/// <c>status_updated_timestamp</c>, the model demanded one, and the SDK returned null without a word.
/// <para>
/// Joins the integration collection because <see cref="RetryConfiguration"/> holds the retry policies in
/// process-wide static state: the retry case below swaps one in, and the collection stops other tests
/// running while it is installed.
/// </para>
/// </summary>
[Collection("Blink Debit Collection")]
public class SuccessResponseBodyTests : IDisposable
{
    private const string TokenPath = "/oauth2/token";

    private static readonly Guid QuickPaymentId = Guid.Parse("cc235a2f-4812-4261-bab8-e3884c261482");

    private static readonly string QuickPaymentPath = $"/payments/v1/quick-payments/{QuickPaymentId}";

    /// <summary>
    /// A quick payment as the API returns it while the customer is still on the gateway page.
    /// </summary>
    private static readonly string GatewayAwaitingSubmissionBody =
        $$$"""
          {"consent":{"consent_id":"{{{QuickPaymentId}}}","creation_timestamp":"2026-10-16T09:44:21+13:00",
          "detail":{"amount":{"currency":"NZD","total":"15.28"},
          "flow":{"detail":{"redirect_to_app":false,"redirect_uri":"https://example.test/return","type":"gateway"}},
          "pcr":{"code":"","particulars":"RUC","reference":"2610KHQ78AFZ"},"type":"single"},
          "payments":[],"status":"GatewayAwaitingSubmission"},"quick_payment_id":"{{{QuickPaymentId}}}"}
          """;

    private readonly WireMockServer _server = WireMockServer.Start();

    private readonly AsyncPolicy<RestResponse> _originalAsyncRetryPolicy = RetryConfiguration.AsyncRetryPolicy;

    private readonly ILogger _logger = LoggerFactory.Create(builder => builder.AddDebug())
        .CreateLogger<SuccessResponseBodyTests>();

    public SuccessResponseBodyTests()
    {
        _server.Given(Request.Create().WithPath(TokenPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"access_token\":\"test-token\",\"token_type\":\"Bearer\",\"expires_in\":3600}"));
    }

    public void Dispose()
    {
        RetryConfiguration.AsyncRetryPolicy = _originalAsyncRetryPolicy;
        _server.Stop();
        _server.Dispose();
        GC.SuppressFinalize(this);
    }

    [Theory(DisplayName = "A quick payment whose consent has never changed status is returned without a status-updated timestamp")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GatewayAwaitingSubmissionIsReturnedAsData(bool retryEnabled)
    {
        StubQuickPayment(GatewayAwaitingSubmissionBody);

        var client = CreateClient(retryEnabled);

        var quickPayment = await client.GetQuickPaymentAsync(QuickPaymentId);

        Assert.NotNull(quickPayment);
        Assert.Equal(QuickPaymentId, quickPayment.QuickPaymentId);
        Assert.Equal(Consent.StatusEnum.GatewayAwaitingSubmission, quickPayment.Consent.Status);
        Assert.Null(quickPayment.Consent.StatusUpdatedTimestamp);
        Assert.Empty(quickPayment.Consent.Payments);
    }

    [Theory(DisplayName = "A successful response whose body does not fit the model throws rather than returning null")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnreadableSuccessBodyThrows(bool retryEnabled)
    {
        // The consent is required, so this body cannot become a QuickPaymentResponse
        StubQuickPayment($"{{\"quick_payment_id\":\"{QuickPaymentId}\"}}");

        var client = CreateClient(retryEnabled);

        var exception = await Assert.ThrowsAsync<BlinkServiceException>(() =>
            client.GetQuickPaymentAsync(QuickPaymentId));

        Assert.Contains("consent", exception.Message);
    }

    /// <summary>
    /// Builds a client against the stub. The configuration is handed over as-is rather than through the
    /// URL constructor, whose validation rightly rejects the stub's plain-HTTP loopback address. That
    /// constructor installs no retry policy either, so the retry case supplies a stand-in that never
    /// fires: what it exercises is the execution path that deserialises outside RestSharp's own call.
    /// </summary>
    private BlinkDebitClient CreateClient(bool retryEnabled)
    {
        if (retryEnabled)
        {
            RetryConfiguration.AsyncRetryPolicy = Policy<RestResponse>
                .Handle<BlinkRetryableException>()
                .RetryAsync(1);
        }

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

    private void StubQuickPayment(string body)
    {
        _server.Given(Request.Create().WithPath(QuickPaymentPath).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));
    }
}
