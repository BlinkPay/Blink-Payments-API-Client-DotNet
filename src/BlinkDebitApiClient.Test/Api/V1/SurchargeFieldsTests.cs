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
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BlinkDebitApiClient.Api.V1;
using BlinkDebitApiClient.Client;
using BlinkDebitApiClient.Client.Auth;
using BlinkDebitApiClient.Config;
using BlinkDebitApiClient.Enums;
using BlinkDebitApiClient.Model.V1;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace BlinkDebitApiClient.Test.Api.V1;

/// <summary>
/// Surcharge fields are read from responses and never written into a request, even when a request reuses an
/// amount taken from a surcharged response: Blink rejects them in a consent request body.
/// </summary>
public class SurchargeFieldsTests : IDisposable
{
    private const string TokenPath = "/oauth2/token";
    private const string BasePath = "/payments/v1";

    private static readonly Guid RedirectSurchargedId = Guid.Parse("11111111-1111-4111-8111-000000000001");
    private static readonly Guid GatewayOptionsId = Guid.Parse("11111111-1111-4111-8111-000000000002");
    private static readonly Guid UnsurchargedId = Guid.Parse("11111111-1111-4111-8111-000000000003");
    private static readonly Guid ExplicitNullId = Guid.Parse("11111111-1111-4111-8111-000000000004");
    private static readonly Guid QuickOptionsId = Guid.Parse("11111111-1111-4111-8111-000000000005");
    private static readonly Guid EnduringBothMaximaId = Guid.Parse("11111111-1111-4111-8111-000000000006");
    private static readonly Guid EnduringPeriodOnlyId = Guid.Parse("11111111-1111-4111-8111-000000000007");
    private static readonly Guid PaymentSurchargedId = Guid.Parse("11111111-1111-4111-8111-000000000008");

    private const string Timestamps =
        """
        "creation_timestamp":"2026-11-10T09:00:00+13:00","status_updated_timestamp":"2026-11-10T09:00:01+13:00"
        """;

    private const string Pcr =
        """
        "pcr":{"particulars":"particulars","code":"code","reference":"reference"}
        """;

    private const string RedirectFlow =
        """
        {"detail":{"type":"redirect","bank":"PNZ","redirect_uri":"https://www.blinkpay.co.nz/sample-merchant-return-page"}}
        """;

    private const string GatewayFlow =
        """
        {"detail":{"type":"gateway","redirect_uri":"https://www.blinkpay.co.nz/sample-merchant-return-page"}}
        """;

    private const string SurchargedAmount =
        """
        {"currency":"NZD","total":"100.00","surcharge":"0.68","total_charge":"100.68"}
        """;

    private const string PlainAmount =
        """
        {"currency":"NZD","total":"100.00"}
        """;

    private const string SurchargedPeriodMaximum =
        """
        "maximum_amount_period":{"currency":"NZD","total":"400.00","surcharge":"2.72","total_charge":"402.72"}
        """;

    private static readonly string[] ReadOnlyKeys = ["surcharge", "total_charge", "surcharge_options"];

    private readonly WireMockServer _server = WireMockServer.Start();

    private readonly ILogger _logger = LoggerFactory.Create(builder => builder.AddDebug())
        .CreateLogger<SurchargeFieldsTests>();

    public SurchargeFieldsTests()
    {
        _server.Given(Request.Create().WithPath(TokenPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"access_token\":\"test-token\",\"token_type\":\"Bearer\",\"expires_in\":3600}"));

        StubGet($"single-consents/{RedirectSurchargedId}",
            SingleConsent(RedirectSurchargedId, "AwaitingAuthorisation", RedirectFlow, SurchargedAmount));
        StubGet($"single-consents/{GatewayOptionsId}",
            SingleConsent(GatewayOptionsId, "GatewayAwaitingSubmission", GatewayFlow, PlainAmount,
                """
                ,"surcharge_options":{"bank":{"surcharge":"0.68","total_charge":"100.68"},
                "card":{"surcharge":"1.49","total_charge":"101.49"}}
                """));
        StubGet($"single-consents/{UnsurchargedId}",
            SingleConsent(UnsurchargedId, "AwaitingAuthorisation", RedirectFlow, PlainAmount));
        StubGet($"single-consents/{ExplicitNullId}",
            SingleConsent(ExplicitNullId, "AwaitingAuthorisation", RedirectFlow,
                """{"currency":"NZD","total":"100.00","surcharge":null,"total_charge":null}""",
                ""","surcharge_options":null"""));
        var quickPaymentConsent = SingleConsent(QuickOptionsId, "GatewayAwaitingSubmission", GatewayFlow, PlainAmount,
            ""","surcharge_options":{"bank":{"surcharge":"0.68","total_charge":"100.68"}}""");
        StubGet($"quick-payments/{QuickOptionsId}",
            $$$"""{"quick_payment_id":"{{{QuickOptionsId}}}","consent":{{{quickPaymentConsent}}} }""");
        StubGet($"enduring-consents/{EnduringBothMaximaId}",
            EnduringConsent(EnduringBothMaximaId,
                SurchargedPeriodMaximum + "," +
                """
                "maximum_amount_payment":{"currency":"NZD","total":"100.00","surcharge":"0.68","total_charge":"100.68"}
                """,
                """
                ,"surcharge_options":{"bank":{"maximum_amount_payment":{"surcharge":"0.68","total_charge":"100.68"},
                "maximum_amount_period":{"surcharge":"2.72","total_charge":"402.72"}}}
                """));
        StubGet($"enduring-consents/{EnduringPeriodOnlyId}",
            EnduringConsent(EnduringPeriodOnlyId, SurchargedPeriodMaximum,
                """
                ,"surcharge_options":{"bank":{"maximum_amount_period":{"surcharge":"2.72","total_charge":"402.72"}}}
                """));
        StubGet($"payments/{PaymentSurchargedId}",
            $$$"""
               {"payment_id":"{{{PaymentSurchargedId}}}","type":"single","status":"AcceptedSettlementCompleted",
               "accepted_reason":"source_bank_payment_sent",{{{Timestamps}}},
               "detail":{"consent_id":"{{{RedirectSurchargedId}}}",{{{Pcr}}},"amount":{{{SurchargedAmount}}} },
               "refunds":[]}
               """);

        StubPost("single-consents", $"{{\"consent_id\":\"{Guid.NewGuid()}\",\"redirect_uri\":\"https://example.test\"}}");
        StubPost("enduring-consents", $"{{\"consent_id\":\"{Guid.NewGuid()}\",\"redirect_uri\":\"https://example.test\"}}");
        StubPost("quick-payments",
            $"{{\"quick_payment_id\":\"{Guid.NewGuid()}\",\"redirect_uri\":\"https://example.test\"}}");
        StubPost("payments", $"{{\"payment_id\":\"{Guid.NewGuid()}\"}}");
        StubPost("refunds", $"{{\"refund_id\":\"{Guid.NewGuid()}\"}}");
    }

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact(DisplayName = "A redirect single consent returns the amount's surcharge and total charge")]
    public async Task RedirectSingleConsentReadsSurcharge()
    {
        var consent = await CreateClient().GetSingleConsentAsync(RedirectSurchargedId);

        var amount = consent.Detail.GetSingleConsentRequest().Amount;
        Assert.Equal("100.00", amount.Total);
        Assert.Equal("0.68", amount.Surcharge);
        Assert.Equal("100.68", amount.TotalCharge);
        Assert.Null(consent.SurchargeOptions);
    }

    [Fact(DisplayName = "A gateway single consent awaiting submission returns bank and card surcharge options")]
    public async Task GatewaySingleConsentReadsSurchargeOptions()
    {
        var consent = await CreateClient().GetSingleConsentAsync(GatewayOptionsId);

        Assert.Null(consent.Detail.GetSingleConsentRequest().Amount.Surcharge);
        Assert.Null(consent.Detail.GetSingleConsentRequest().Amount.TotalCharge);
        var options = consent.SurchargeOptions;
        Assert.NotNull(options);
        Assert.Equal("0.68", options.Bank.Surcharge);
        Assert.Equal("100.68", options.Bank.TotalCharge);
        Assert.Null(options.Bank.MaximumAmountPayment);
        Assert.Null(options.Bank.MaximumAmountPeriod);
        Assert.Equal("1.49", options.Card.Surcharge);
        Assert.Equal("101.49", options.Card.TotalCharge);
    }

    [Fact(DisplayName = "A gateway quick payment awaiting submission returns surcharge options on its consent")]
    public async Task QuickPaymentReadsSurchargeOptions()
    {
        var quickPayment = await CreateClient().GetQuickPaymentAsync(QuickOptionsId);

        var options = quickPayment.Consent.SurchargeOptions;
        Assert.NotNull(options);
        Assert.Equal("0.68", options.Bank.Surcharge);
        Assert.Equal("100.68", options.Bank.TotalCharge);
        Assert.Null(options.Card);
    }

    [Fact(DisplayName = "A gateway enduring consent returns raised maxima and their surcharge options")]
    public async Task EnduringConsentReadsRaisedMaxima()
    {
        var consent = await CreateClient().GetEnduringConsentAsync(EnduringBothMaximaId);

        var detail = consent.Detail.GetEnduringConsentRequest();
        Assert.Equal("400.00", detail.MaximumAmountPeriod.Total);
        Assert.Equal("2.72", detail.MaximumAmountPeriod.Surcharge);
        Assert.Equal("402.72", detail.MaximumAmountPeriod.TotalCharge);
        Assert.NotNull(detail.MaximumAmountPayment);
        Assert.Equal("100.00", detail.MaximumAmountPayment.Total);
        Assert.Equal("0.68", detail.MaximumAmountPayment.Surcharge);
        Assert.Equal("100.68", detail.MaximumAmountPayment.TotalCharge);

        var bank = consent.SurchargeOptions.Bank;
        Assert.Null(bank.Surcharge);
        Assert.Null(bank.TotalCharge);
        Assert.Equal("0.68", bank.MaximumAmountPayment.Surcharge);
        Assert.Equal("100.68", bank.MaximumAmountPayment.TotalCharge);
        Assert.Equal("2.72", bank.MaximumAmountPeriod.Surcharge);
        Assert.Equal("402.72", bank.MaximumAmountPeriod.TotalCharge);
        Assert.Null(consent.SurchargeOptions.Card);
    }

    [Fact(DisplayName = "An enduring consent without a per-payment maximum is read with a null maximum")]
    public async Task EnduringConsentWithoutPaymentMaximumIsRead()
    {
        var consent = await CreateClient().GetEnduringConsentAsync(EnduringPeriodOnlyId);

        var detail = consent.Detail.GetEnduringConsentRequest();
        Assert.Null(detail.MaximumAmountPayment);
        Assert.Equal("400.00", detail.MaximumAmountPeriod.Total);
        Assert.Equal("402.72", detail.MaximumAmountPeriod.TotalCharge);
        var bank = consent.SurchargeOptions.Bank;
        Assert.Null(bank.MaximumAmountPayment);
        Assert.Equal("2.72", bank.MaximumAmountPeriod.Surcharge);
        Assert.Equal("402.72", bank.MaximumAmountPeriod.TotalCharge);
    }

    [Fact(DisplayName = "A payment returns the amount's surcharge and total charge")]
    public async Task PaymentReadsSurcharge()
    {
        var payment = await CreateClient().GetPaymentAsync(PaymentSurchargedId);

        Assert.Equal("100.00", payment.Detail.Amount.Total);
        Assert.Equal("0.68", payment.Detail.Amount.Surcharge);
        Assert.Equal("100.68", payment.Detail.Amount.TotalCharge);
    }

    [Theory(DisplayName = "An unsurcharged consent, with the fields absent or null, reads them as null")]
    [InlineData("11111111-1111-4111-8111-000000000003")]
    [InlineData("11111111-1111-4111-8111-000000000004")]
    public async Task UnsurchargedConsentReadsNulls(string consentId)
    {
        var consent = await CreateClient().GetSingleConsentAsync(Guid.Parse(consentId));

        var amount = consent.Detail.GetSingleConsentRequest().Amount;
        Assert.Equal("100.00", amount.Total);
        Assert.Null(amount.Surcharge);
        Assert.Null(amount.TotalCharge);
        Assert.Null(consent.SurchargeOptions);
    }

    [Fact(DisplayName = "A single consent detail reused from a surcharged response is sent without surcharge fields")]
    public async Task SingleConsentRequestOmitsSurcharge()
    {
        var client = CreateClient();
        var request = (await client.GetSingleConsentAsync(RedirectSurchargedId)).Detail.GetSingleConsentRequest();
        Assert.Equal("0.68", request.Amount.Surcharge);

        await client.CreateSingleConsentAsync(request);

        var body = SentBody("single-consents");
        AssertNoReadOnlyKeys(body);
        AssertAmount(body["amount"], "100.00");
    }

    [Fact(DisplayName = "A quick payment built with a surcharged response amount is sent without surcharge fields")]
    public async Task QuickPaymentRequestOmitsSurcharge()
    {
        var client = CreateClient();
        var amount = await SurchargedConsentAmount(client);

        await client.CreateQuickPaymentAsync(new QuickPaymentRequest(
            new AuthFlow(new AuthFlowDetail(new GatewayFlow("https://www.blinkpay.co.nz/sample-merchant-return-page"))),
            new Pcr("particulars"), amount));

        var body = SentBody("quick-payments");
        AssertNoReadOnlyKeys(body);
        AssertAmount(body["amount"], "100.00");
    }

    [Fact(DisplayName = "An enduring consent built with surcharged response maxima is sent without surcharge fields")]
    public async Task EnduringConsentRequestOmitsSurcharge()
    {
        var client = CreateClient();
        var maxima = (await client.GetEnduringConsentAsync(EnduringBothMaximaId)).Detail.GetEnduringConsentRequest();
        Assert.Equal("2.72", maxima.MaximumAmountPeriod.Surcharge);
        Assert.Equal("0.68", maxima.MaximumAmountPayment.Surcharge);

        await client.CreateEnduringConsentAsync(EnduringRequest(maxima.MaximumAmountPeriod,
            maxima.MaximumAmountPayment));

        var body = SentBody("enduring-consents");
        AssertNoReadOnlyKeys(body);
        AssertAmount(body["maximum_amount_period"], "400.00");
        AssertAmount(body["maximum_amount_payment"], "100.00");
    }

    [Fact(DisplayName = "An enduring consent without a per-payment maximum is sent without the key")]
    public async Task EnduringConsentRequestWithoutPaymentMaximumOmitsTheKey()
    {
        var client = CreateClient();
        var maxima = (await client.GetEnduringConsentAsync(EnduringPeriodOnlyId)).Detail.GetEnduringConsentRequest();

        await client.CreateEnduringConsentAsync(EnduringRequest(maxima.MaximumAmountPeriod, null));

        var body = SentBody("enduring-consents");
        Assert.False(body.ContainsKey("maximum_amount_payment"));
        AssertNoReadOnlyKeys(body);
        AssertAmount(body["maximum_amount_period"], "400.00");
    }

    [Fact(DisplayName = "A payment built with a surcharged response amount is sent without surcharge fields")]
    public async Task PaymentRequestOmitsSurcharge()
    {
        var client = CreateClient();
        var amount = (await client.GetPaymentAsync(PaymentSurchargedId)).Detail.Amount;
        Assert.Equal("0.68", amount.Surcharge);

        await client.CreatePaymentAsync(new PaymentRequest(RedirectSurchargedId, null, amount));

        var body = SentBody("payments");
        AssertNoReadOnlyKeys(body);
        AssertAmount(body["amount"], "100.00");
    }

    [Fact(DisplayName = "A partial refund built with a surcharged response amount is sent without surcharge fields")]
    public async Task PartialRefundRequestOmitsSurcharge()
    {
        var client = CreateClient();
        var amount = (await client.GetPaymentAsync(PaymentSurchargedId)).Detail.Amount;
        Assert.Equal("0.68", amount.Surcharge);

        await client.CreateRefundAsync(new PartialRefundRequest(PaymentSurchargedId, amount,
            new Pcr("particulars", "code", "reference"), "https://www.blinkpay.co.nz/sample-merchant-return-page"));

        var body = SentBody("refunds");
        AssertNoReadOnlyKeys(body);
        AssertAmount(body["amount"], "100.00");
    }

    private static string SingleConsent(Guid id, string status, string flow, string amount, string extra = "") =>
        $$$"""
           {"consent_id":"{{{id}}}","status":"{{{status}}}",{{{Timestamps}}},"payments":[],
           "detail":{"type":"single","flow":{{{flow}}},{{{Pcr}}},"amount":{{{amount}}} }{{{extra}}} }
           """;

    private static string EnduringConsent(Guid id, string maxima, string extra) =>
        $$$"""
           {"consent_id":"{{{id}}}","status":"GatewayAwaitingSubmission",{{{Timestamps}}},"payments":[],
           "detail":{"type":"enduring","flow":{{{GatewayFlow}}},"from_timestamp":"2026-11-10T09:00:00+13:00",
           "period":"monthly",{{{maxima}}} }{{{extra}}} }
           """;

    private static EnduringConsentRequest EnduringRequest(Amount maximumAmountPeriod, Amount? maximumAmountPayment) =>
        new(new AuthFlow(new AuthFlowDetail(
                new GatewayFlow("https://www.blinkpay.co.nz/sample-merchant-return-page"))),
            DateTimeOffset.Parse("2026-11-10T09:00:00+13:00"), null, Period.Monthly, maximumAmountPeriod,
            maximumAmountPayment);

    private async Task<Amount> SurchargedConsentAmount(BlinkDebitClient client)
    {
        var amount = (await client.GetSingleConsentAsync(RedirectSurchargedId)).Detail.GetSingleConsentRequest()
            .Amount;
        Assert.Equal("0.68", amount.Surcharge);
        return amount;
    }

    private static void AssertNoReadOnlyKeys(JObject body)
    {
        var leaked = body.Descendants().OfType<JProperty>().Select(property => property.Name)
            .Where(name => ReadOnlyKeys.Contains(name));
        Assert.Empty(leaked);
    }

    private static void AssertAmount(JToken? amount, string total)
    {
        Assert.NotNull(amount);
        Assert.Equal(total, (string?)amount["total"]);
        Assert.Equal("NZD", (string?)amount["currency"]);
    }

    private JObject SentBody(string resource)
    {
        var entry = Assert.Single(_server.LogEntries, entry =>
            entry.RequestMessage.Method == "POST" && entry.RequestMessage.Path == $"{BasePath}/{resource}");
        return JObject.Parse(entry.RequestMessage.Body!);
    }

    private void StubGet(string resource, string body)
    {
        _server.Given(Request.Create().WithPath($"{BasePath}/{resource}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));
    }

    private void StubPost(string resource, string body)
    {
        _server.Given(Request.Create().WithPath($"{BasePath}/{resource}").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.Created)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));
    }

    private BlinkDebitClient CreateClient()
    {
        var configuration = new Configuration
        {
            BasePath = _server.Url + BasePath,
            OAuthTokenUrl = _server.Url + TokenPath,
            OAuthClientId = "test-client-id",
            OAuthClientSecret = "test-client-secret",
            OAuthFlow = OAuthFlow.APPLICATION,
            RetryEnabled = false
        };

        return new BlinkDebitClient(_logger, new ApiClient(_logger, configuration), configuration);
    }
}
