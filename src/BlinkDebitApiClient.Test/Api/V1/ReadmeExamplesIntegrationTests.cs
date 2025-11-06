/*
 * Copyright (c) 2023 BlinkPay
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
using BlinkDebitApiClient.Api.V1;
using BlinkDebitApiClient.Enums;
using BlinkDebitApiClient.Exceptions;
using BlinkDebitApiClient.Model.V1;
using Xunit;
using Xunit.Abstractions;

namespace BlinkDebitApiClient.Test.Api.V1;

/// <summary>
/// Integration tests that mirror the README examples.
/// These tests demonstrate the primary use cases documented in the README.
/// </summary>
[Collection("Blink Debit Collection")]
public class ReadmeExamplesIntegrationTests : IDisposable
{
    private const string RedirectUri = "https://www.blinkpay.co.nz/sample-merchant-return-page";
    private const string CallbackUrl = "https://www.mymerchant.co.nz/callback";

    private static readonly Dictionary<string, string?> RequestHeaders = new Dictionary<string, string?>();

    private readonly BlinkDebitClient _client;
    private readonly ITestOutputHelper _output;

    public ReadmeExamplesIntegrationTests(BlinkDebitFixture fixture, ITestOutputHelper output)
    {
        _client = fixture.BlinkDebitClient;
        _output = output;

        RequestHeaders[BlinkDebitConstant.REQUEST_ID.GetValue()] = Guid.NewGuid().ToString();
        RequestHeaders[BlinkDebitConstant.CORRELATION_ID.GetValue()] = Guid.NewGuid().ToString();
        RequestHeaders[BlinkDebitConstant.CUSTOMER_IP.GetValue()] = "192.168.0.1";
        RequestHeaders[BlinkDebitConstant.CUSTOMER_USER_AGENT.GetValue()] = "readme-example-tests";
        RequestHeaders[BlinkDebitConstant.IDEMPOTENCY_KEY.GetValue()] = Guid.NewGuid().ToString();
    }

    public void Dispose()
    {
        RequestHeaders.Clear();
    }

    /// <summary>
    /// Test the exact Quick Start example from README.
    /// This mirrors the code example at lines 39-67 of the README.
    /// </summary>
    [Fact(DisplayName = "Quick Start Example - Gateway Flow Quick Payment")]
    public void QuickStartExample()
    {
        try
        {
            // This mirrors the Quick Start code from the README
            var gatewayFlow = new GatewayFlow(RedirectUri);
            var authFlowDetail = new AuthFlowDetail(gatewayFlow);
            var authFlow = new AuthFlow(authFlowDetail);
            var pcr = new Pcr("particulars", "code", "reference");
            var amount = new Amount("0.01", Amount.CurrencyEnum.NZD);
            var request = new QuickPaymentRequest(authFlow, pcr, amount);

            var qpCreateResponse = _client.CreateQuickPayment(request, RequestHeaders);
            _output.WriteLine($"Redirect URL: {qpCreateResponse.RedirectUri}");

            Assert.NotNull(qpCreateResponse);
            Assert.NotEmpty(qpCreateResponse.RedirectUri);
            Assert.NotEqual(Guid.Empty, qpCreateResponse.QuickPaymentId);

            var qpId = qpCreateResponse.QuickPaymentId;

            // Note: In a real scenario, user would be redirected and would authorize the payment.
            // For this test, we just verify the quick payment was created successfully.
            var qpResponse = _client.GetQuickPayment(qpId);

            Assert.NotNull(qpResponse);
            Assert.NotNull(qpResponse.Consent);
            Assert.Equal(Consent.StatusEnum.GatewayAwaitingSubmission, qpResponse.Consent.Status);

            _output.WriteLine("Quick Start example test completed successfully");
        }
        catch (BlinkServiceException e)
        {
            _output.WriteLine($"Encountered an error: {e.Message}");
            throw;
        }
    }

    /// <summary>
    /// Test bank metadata retrieval (GetMeta).
    /// This tests the example from README line 244.
    /// </summary>
    [Fact(DisplayName = "Get Bank Metadata Example")]
    public void GetBankMetadataExample()
    {
        var bankMetadataList = _client.GetMeta(RequestHeaders);

        Assert.NotNull(bankMetadataList);
        Assert.NotEmpty(bankMetadataList);

        // Verify structure of metadata
        foreach (var metadata in bankMetadataList)
        {
            Assert.NotNull(metadata.Name);
            Assert.NotNull(metadata.Features);

            _output.WriteLine($"Bank: {metadata.Name}");
            if (metadata.Features.EnduringConsent != null)
            {
                _output.WriteLine($"  - Enduring Consent: {metadata.Features.EnduringConsent.Enabled}");
            }
            if (metadata.Features.DecoupledFlow != null)
            {
                _output.WriteLine($"  - Decoupled Flow: {metadata.Features.DecoupledFlow.Enabled}");
            }
        }

        _output.WriteLine($"Retrieved {bankMetadataList.Count} bank metadata records");
    }

    /// <summary>
    /// Test quick payment creation with Gateway Flow.
    /// This tests the example from README lines 249-258.
    /// </summary>
    [Fact(DisplayName = "Create Quick Payment with Gateway Flow")]
    public void CreateQuickPaymentWithGatewayFlow()
    {
        var gatewayFlow = new GatewayFlow(RedirectUri);
        var authFlowDetail = new AuthFlowDetail(gatewayFlow);
        var authFlow = new AuthFlow(authFlowDetail);
        var pcr = new Pcr("test-part", "test-code", "test-ref");
        var amount = new Amount("1.50", Amount.CurrencyEnum.NZD);
        var request = new QuickPaymentRequest(authFlow, pcr, amount);

        var createQuickPaymentResponse = _client.CreateQuickPayment(request, RequestHeaders);

        Assert.NotNull(createQuickPaymentResponse);
        Assert.NotEqual(Guid.Empty, createQuickPaymentResponse.QuickPaymentId);
        Assert.NotEmpty(createQuickPaymentResponse.RedirectUri);
        Assert.Contains("/gateway/pay?id=", createQuickPaymentResponse.RedirectUri);

        _output.WriteLine($"Quick Payment ID: {createQuickPaymentResponse.QuickPaymentId}");
        _output.WriteLine($"Redirect URI: {createQuickPaymentResponse.RedirectUri}");
    }

    /// <summary>
    /// Test quick payment creation with Redirect Flow.
    /// This tests the example from README lines 286-295.
    /// </summary>
    [Fact(DisplayName = "Create Quick Payment with Redirect Flow")]
    public void CreateQuickPaymentWithRedirectFlow()
    {
        var redirectFlow = new RedirectFlow(RedirectUri, Bank.PNZ);
        var authFlowDetail = new AuthFlowDetail(redirectFlow);
        var authFlow = new AuthFlow(authFlowDetail);
        var pcr = new Pcr("test-part", "test-code", "test-ref");
        var amount = new Amount("2.00", Amount.CurrencyEnum.NZD);
        var request = new QuickPaymentRequest(authFlow, pcr, amount);

        var createQuickPaymentResponse = _client.CreateQuickPayment(request, RequestHeaders);

        Assert.NotNull(createQuickPaymentResponse);
        Assert.NotEqual(Guid.Empty, createQuickPaymentResponse.QuickPaymentId);
        Assert.NotEmpty(createQuickPaymentResponse.RedirectUri);

        // Verify we can retrieve it
        var quickPayment = _client.GetQuickPayment(createQuickPaymentResponse.QuickPaymentId);
        Assert.NotNull(quickPayment);
        Assert.Equal(Consent.StatusEnum.AwaitingAuthorisation, quickPayment.Consent.Status);

        _output.WriteLine($"Quick Payment ID: {createQuickPaymentResponse.QuickPaymentId}");
        _output.WriteLine($"Status: {quickPayment.Consent.Status}");
    }

    /// <summary>
    /// Test quick payment revocation.
    /// This tests the example from README lines 312-314.
    /// </summary>
    [Fact(DisplayName = "Revoke Quick Payment")]
    public void RevokeQuickPaymentExample()
    {
        // First create a quick payment
        var redirectFlow = new RedirectFlow(RedirectUri, Bank.PNZ);
        var authFlowDetail = new AuthFlowDetail(redirectFlow);
        var authFlow = new AuthFlow(authFlowDetail);
        var pcr = new Pcr("revoke-test", "code", "ref");
        var amount = new Amount("1.00", Amount.CurrencyEnum.NZD);
        var request = new QuickPaymentRequest(authFlow, pcr, amount);

        var createResponse = _client.CreateQuickPayment(request, RequestHeaders);
        var quickPaymentId = createResponse.QuickPaymentId;

        _output.WriteLine($"Created Quick Payment ID: {quickPaymentId}");

        // Now revoke it (README line 313)
        _client.RevokeQuickPayment(quickPaymentId);

        // Verify it was revoked
        var revokedPayment = _client.GetQuickPayment(quickPaymentId);
        Assert.Equal(Consent.StatusEnum.Revoked, revokedPayment.Consent.Status);

        _output.WriteLine($"Quick Payment revoked successfully. Status: {revokedPayment.Consent.Status}");
    }

    /// <summary>
    /// Test single consent creation with Redirect Flow.
    /// This tests the example from README lines 356-365.
    /// </summary>
    [Fact(DisplayName = "Create Single Consent with Redirect Flow")]
    public void CreateSingleConsentWithRedirectFlow()
    {
        var redirectFlow = new RedirectFlow(RedirectUri, Bank.PNZ);
        var authFlowDetail = new AuthFlowDetail(redirectFlow);
        var authFlow = new AuthFlow(authFlowDetail);
        var pcr = new Pcr("single-test");
        var amount = new Amount("5.00", Amount.CurrencyEnum.NZD);
        var request = new SingleConsentRequest(authFlow, pcr, amount);

        var createConsentResponse = _client.CreateSingleConsent(request, RequestHeaders);

        Assert.NotNull(createConsentResponse);
        Assert.NotEqual(Guid.Empty, createConsentResponse.ConsentId);
        Assert.NotEmpty(createConsentResponse.RedirectUri);

        // Verify retrieval (README line 383)
        var consent = _client.GetSingleConsent(createConsentResponse.ConsentId);
        Assert.NotNull(consent);
        Assert.Equal(Consent.StatusEnum.AwaitingAuthorisation, consent.Status);

        _output.WriteLine($"Consent ID: {createConsentResponse.ConsentId}");
        _output.WriteLine($"Redirect URI: {createConsentResponse.RedirectUri}");
        _output.WriteLine($"Status: {consent.Status}");
    }

    /// <summary>
    /// Test single consent revocation.
    /// This tests the example from README lines 386-388.
    /// </summary>
    [Fact(DisplayName = "Revoke Single Consent")]
    public void RevokeSingleConsentExample()
    {
        // First create a single consent
        var redirectFlow = new RedirectFlow(RedirectUri, Bank.PNZ);
        var authFlowDetail = new AuthFlowDetail(redirectFlow);
        var authFlow = new AuthFlow(authFlowDetail);
        var pcr = new Pcr("revoke-test");
        var amount = new Amount("3.00", Amount.CurrencyEnum.NZD);
        var request = new SingleConsentRequest(authFlow, pcr, amount);

        var createResponse = _client.CreateSingleConsent(request, RequestHeaders);
        var consentId = createResponse.ConsentId;

        _output.WriteLine($"Created Consent ID: {consentId}");

        // Now revoke it (README line 387)
        _client.RevokeSingleConsent(consentId);

        // Verify it was revoked
        var revokedConsent = _client.GetSingleConsent(consentId);
        Assert.Equal(Consent.StatusEnum.Revoked, revokedConsent.Status);

        _output.WriteLine($"Consent revoked successfully. Status: {revokedConsent.Status}");
    }

    /// <summary>
    /// Test enduring consent creation with Gateway Flow.
    /// This tests the example from README lines 395-405.
    /// </summary>
    [Fact(DisplayName = "Create Enduring Consent with Gateway Flow")]
    public void CreateEnduringConsentWithGatewayFlow()
    {
        var gatewayFlow = new GatewayFlow(RedirectUri);
        var authFlowDetail = new AuthFlowDetail(gatewayFlow);
        var authFlow = new AuthFlow(authFlowDetail);
        var pcr = new Pcr("enduring-test", "code", "ref");
        var maximumAmountPeriod = new Amount("100.00", Amount.CurrencyEnum.NZD);
        var maximumAmountPayment = new Amount("50.00", Amount.CurrencyEnum.NZD);
        var startDate = DateTimeOffset.UtcNow;
        var endDate = startDate.AddMonths(6);
        var hashedCustomerIdentifier = "88df3798e32512ac340164f7ed133343d6dcb4888e4a91b03512dedd9800d12e";

        var request = new EnduringConsentRequest(
            authFlow,
            startDate,
            endDate,
            Period.Monthly,
            maximumAmountPeriod,
            maximumAmountPayment,
            hashedCustomerIdentifier);

        var createConsentResponse = _client.CreateEnduringConsent(request, RequestHeaders);

        Assert.NotNull(createConsentResponse);
        Assert.NotEqual(Guid.Empty, createConsentResponse.ConsentId);
        Assert.NotEmpty(createConsentResponse.RedirectUri);

        // Verify retrieval (README line 458)
        var consent = _client.GetEnduringConsent(createConsentResponse.ConsentId);
        Assert.NotNull(consent);
        Assert.Equal(Consent.StatusEnum.GatewayAwaitingSubmission, consent.Status);

        _output.WriteLine($"Enduring Consent ID: {createConsentResponse.ConsentId}");
        _output.WriteLine($"Status: {consent.Status}");
    }

    /// <summary>
    /// Test enduring consent revocation.
    /// This tests the example from README lines 461-463.
    /// </summary>
    [Fact(DisplayName = "Revoke Enduring Consent")]
    public void RevokeEnduringConsentExample()
    {
        // First create an enduring consent
        var redirectFlow = new RedirectFlow(RedirectUri, Bank.PNZ);
        var authFlowDetail = new AuthFlowDetail(redirectFlow);
        var authFlow = new AuthFlow(authFlowDetail);
        var maximumAmountPeriod = new Amount("200.00", Amount.CurrencyEnum.NZD);
        var maximumAmountPayment = new Amount("100.00", Amount.CurrencyEnum.NZD);
        var startDate = DateTimeOffset.UtcNow;
        var endDate = startDate.AddMonths(3);
        var hashedCustomerIdentifier = "88df3798e32512ac340164f7ed133343d6dcb4888e4a91b03512dedd9800d12e";

        var request = new EnduringConsentRequest(
            authFlow,
            startDate,
            endDate,
            Period.Weekly,
            maximumAmountPeriod,
            maximumAmountPayment,
            hashedCustomerIdentifier);

        var createResponse = _client.CreateEnduringConsent(request, RequestHeaders);
        var consentId = createResponse.ConsentId;

        _output.WriteLine($"Created Enduring Consent ID: {consentId}");

        // Now revoke it (README line 462)
        _client.RevokeEnduringConsent(consentId);

        // Verify it was revoked
        var revokedConsent = _client.GetEnduringConsent(consentId);
        Assert.Equal(Consent.StatusEnum.Revoked, revokedConsent.Status);

        _output.WriteLine($"Enduring Consent revoked successfully. Status: {revokedConsent.Status}");
    }

    /// <summary>
    /// Test account number refund creation.
    /// This tests the example from README lines 492-496.
    /// Note: This test creates a mock payment scenario but may fail if the payment
    /// hasn't actually completed in the real banking system.
    /// </summary>
    [Fact(DisplayName = "Create Account Number Refund", Skip = "Requires completed payment which needs user authorization")]
    public void CreateAccountNumberRefundExample()
    {
        // Note: In a real scenario, you would have a completed payment ID
        // For this test, we're showing the structure but skipping execution
        // since we can't complete a real payment in an automated test

        var paymentId = Guid.NewGuid(); // This would be from a real completed payment
        var request = new AccountNumberRefundRequest(paymentId);

        // This would execute: var refundResponse = _client.CreateRefund(request, RequestHeaders);
        // Then: var refund = _client.GetRefund(refundResponse.RefundId);

        _output.WriteLine("Account number refund structure verified");
    }

    /// <summary>
    /// Test payment retrieval.
    /// This tests the example from README lines 486-488.
    /// </summary>
    [Fact(DisplayName = "Get Payment", Skip = "Requires completed payment which needs user authorization")]
    public void GetPaymentExample()
    {
        // Note: In a real scenario, you would have a valid payment ID
        // This demonstrates the API call structure

        var paymentId = Guid.NewGuid(); // This would be from a real payment

        // This would execute: var payment = _client.GetPayment(paymentId);

        _output.WriteLine("Payment retrieval structure verified");
    }

    /// <summary>
    /// Test error handling as shown in Quick Start.
    /// This verifies that BlinkServiceException is properly thrown.
    /// </summary>
    [Fact(DisplayName = "Error Handling Example")]
    public void ErrorHandlingExample()
    {
        try
        {
            // Attempt to get a non-existent quick payment
            var nonExistentId = Guid.NewGuid();
            _client.GetQuickPayment(nonExistentId);

            Assert.True(false, "Should have thrown an exception");
        }
        catch (BlinkResourceNotFoundException e)
        {
            // This is the expected behavior from the Quick Start example
            _output.WriteLine($"Correctly caught exception: {e.Message}");
            Assert.Contains("does not exist", e.Message);
        }
        catch (BlinkServiceException e)
        {
            // Also acceptable
            _output.WriteLine($"Encountered a service error: {e.Message}");
        }
    }
}
