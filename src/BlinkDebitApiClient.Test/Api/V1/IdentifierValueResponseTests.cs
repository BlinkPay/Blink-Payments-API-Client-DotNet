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
using System.Reflection;
using System.Threading.Tasks;
using BlinkDebitApiClient.Api.V1;
using BlinkDebitApiClient.Client;
using BlinkDebitApiClient.Client.Auth;
using BlinkDebitApiClient.Config;
using BlinkDebitApiClient.Enums;
using BlinkDebitApiClient.Exceptions;
using BlinkDebitApiClient.Model.V1;
using Microsoft.Extensions.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace BlinkDebitApiClient.Test.Api.V1;

/// <summary>
/// The API keeps <c>identifier_value</c> but sends <c>null</c> once the bank has the request. A consent GET must read
/// that as null, and the property must say it can be.
/// </summary>
public class IdentifierValueResponseTests : IDisposable
{
    private const string TokenPath = "/oauth2/token";

    private static readonly Guid ConsentId = Guid.Parse("a5d7c994-1a4a-4061-9987-c07d8ab34e28");

    private const string ConsentFields =
        """
        "creation_timestamp":"2026-10-27T04:10:00+13:00","status_updated_timestamp":"2026-10-27T04:10:01+13:00",
        "payments":[]
        """;

    private const string SingleDetailFields =
        """
        "pcr":{"particulars":"particulars"},"amount":{"currency":"NZD","total":"0.01"},"type":"single"
        """;

    private static readonly string DecoupledSingleConsentBody =
        $$$"""
           {"consent_id":"{{{ConsentId}}}",{{{ConsentFields}}},"status":"AwaitingAuthorisation",
           "detail":{"flow":{"detail":{"type":"decoupled","bank":"PNZ","identifier_type":"phone_number",
           "identifier_value":null,"callback_url":"https://www.blinkpay.co.nz/callback"}},{{{SingleDetailFields}}} } }
           """;

    private const string RevokedHint =
        """
        {"type":"decoupled","bank":"PNZ","identifier_type":"phone_number","identifier_value":null}
        """;

    private readonly WireMockServer _server = WireMockServer.Start();

    private readonly ILogger _logger = LoggerFactory.Create(builder => builder.AddDebug())
        .CreateLogger<IdentifierValueResponseTests>();

    public IdentifierValueResponseTests()
    {
        _server.Given(Request.Create().WithPath(TokenPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"access_token\":\"test-token\",\"token_type\":\"Bearer\",\"expires_in\":3600}"));
    }

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact(DisplayName = "A decoupled consent whose identifier value is null is returned with a null identifier value")]
    public async Task DecoupledFlowReadsNullIdentifierValue()
    {
        Stub($"/payments/v1/single-consents/{ConsentId}", DecoupledSingleConsentBody);

        var consent = await CreateClient().GetSingleConsentAsync(ConsentId);

        Assert.Equal(Consent.StatusEnum.AwaitingAuthorisation, consent.Status);
        var flow = consent.Detail.GetSingleConsentRequest().Flow.Detail.GetDecoupledFlow();
        Assert.Null(flow.IdentifierValue);
        Assert.Equal(IdentifierType.PhoneNumber, flow.IdentifierType);
        Assert.Equal(Bank.PNZ, flow.Bank);
    }

    [Fact(DisplayName = "A gateway decoupled hint whose identifier value is null is returned with a null identifier value")]
    public async Task GatewayDecoupledFlowHintReadsNullIdentifierValue()
    {
        Stub($"/payments/v1/quick-payments/{ConsentId}", QuickPaymentBody(RevokedHint));

        var quickPayment = await CreateClient().GetQuickPaymentAsync(ConsentId);

        Assert.Equal(Consent.StatusEnum.Revoked, quickPayment.Consent.Status);
        var flowHint = quickPayment.Consent.Detail.GetSingleConsentRequest().Flow.Detail.GetGatewayFlow().FlowHint
            .GetDecoupledFlowHint();
        Assert.Null(flowHint.IdentifierValue);
        Assert.Equal(IdentifierType.PhoneNumber, flowHint.IdentifierType);
    }

    [Fact(DisplayName = "A gateway decoupled hint without an identifier value key is still rejected")]
    public async Task GatewayDecoupledFlowHintWithoutTheKeyIsRejected()
    {
        Stub($"/payments/v1/quick-payments/{ConsentId}",
            QuickPaymentBody("""{"type":"decoupled","bank":"PNZ","identifier_type":"phone_number"}"""));

        await Assert.ThrowsAnyAsync<BlinkServiceException>(() => CreateClient().GetQuickPaymentAsync(ConsentId));
    }

    [Theory(DisplayName = "IdentifierValue is declared nullable")]
    [InlineData(typeof(DecoupledFlow))]
    [InlineData(typeof(DecoupledFlowHint))]
    public void IdentifierValueIsNullable(Type model)
    {
        var property = model.GetProperty(nameof(DecoupledFlow.IdentifierValue))!;

        Assert.Equal(NullabilityState.Nullable, new NullabilityInfoContext().Create(property).ReadState);
    }

    [Fact(DisplayName = "A decoupled flow or hint cannot be built without an identifier value")]
    public void RequestsStillNeedAnIdentifierValue()
    {
        Assert.Throws<BlinkInvalidValueException>(() =>
            new DecoupledFlow(Bank.PNZ, IdentifierType.PhoneNumber, null, "https://www.blinkpay.co.nz/callback"));
        Assert.Throws<BlinkInvalidValueException>(() =>
            new DecoupledFlowHint(Bank.PNZ, IdentifierType.PhoneNumber, null));
    }

    private static string QuickPaymentBody(string flowHint) =>
        $$$"""
           {"quick_payment_id":"{{{ConsentId}}}","consent":{"consent_id":"{{{ConsentId}}}",{{{ConsentFields}}},
           "status":"Revoked","detail":{"flow":{"detail":{"type":"gateway",
           "redirect_uri":"https://www.blinkpay.co.nz/sample-merchant-return-page","flow_hint":{{{flowHint}}} } },
           {{{SingleDetailFields}}} } } }
           """;

    private void Stub(string path, string body)
    {
        _server.Given(Request.Create().WithPath(path).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));
    }

    private BlinkDebitClient CreateClient()
    {
        var configuration = new Configuration
        {
            BasePath = _server.Url + "/payments/v1",
            OAuthTokenUrl = _server.Url + TokenPath,
            OAuthClientId = "test-client-id",
            OAuthClientSecret = "test-client-secret",
            OAuthFlow = OAuthFlow.APPLICATION,
            RetryEnabled = false
        };

        return new BlinkDebitClient(_logger, new ApiClient(_logger, configuration), configuration);
    }
}
