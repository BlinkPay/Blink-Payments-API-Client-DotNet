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
using System.Threading.Tasks;
using BlinkDebitApiClient.Model.V1;

namespace BlinkDebitApiClient.Api.V1;

/// <summary>
/// Interface for the Blink Debit API Client.
/// Provides access to all API operations for single consents, enduring consents, payments, refunds, and metadata.
/// </summary>
public interface IBlinkDebitClient
{
    // Bank Metadata Operations

    /// <summary>
    /// Get bank metadata
    /// </summary>
    /// <param name="requestHeaders">Request headers (optional)</param>
    /// <returns>List of bank metadata</returns>
    Task<List<BankMetadata>> GetMetaAsync(Dictionary<string, string?>? requestHeaders = null);

    // Single Consent Operations

    /// <summary>
    /// Create a single consent
    /// </summary>
    /// <param name="singleConsentRequest">The single consent request</param>
    /// <param name="requestHeaders">Request headers (optional)</param>
    /// <returns>The create consent response</returns>
    Task<CreateConsentResponse> CreateSingleConsentAsync(SingleConsentRequest? singleConsentRequest,
        Dictionary<string, string?>? requestHeaders = null);

    /// <summary>
    /// Get a single consent by ID
    /// </summary>
    /// <param name="consentId">The consent ID</param>
    /// <param name="requestHeaders">Request headers (optional)</param>
    /// <returns>The consent</returns>
    Task<Consent> GetSingleConsentAsync(Guid consentId, Dictionary<string, string?>? requestHeaders = null);

    /// <summary>
    /// Await authorised single consent. This method will poll until the consent is authorised or the max wait time is reached.
    /// </summary>
    /// <param name="consentId">The consent ID</param>
    /// <param name="maxWaitSeconds">Maximum wait time in seconds</param>
    /// <returns>The authorised consent</returns>
    Task<Consent> AwaitAuthorisedSingleConsentAsync(Guid consentId, int maxWaitSeconds);

    /// <summary>
    /// Revoke a single consent
    /// </summary>
    /// <param name="consentId">The consent ID</param>
    /// <param name="requestHeaders">Request headers (optional)</param>
    Task RevokeSingleConsentAsync(Guid consentId, Dictionary<string, string?>? requestHeaders = null);

    // Enduring Consent Operations

    /// <summary>
    /// Create an enduring consent
    /// </summary>
    /// <param name="enduringConsentRequest">The enduring consent request</param>
    /// <param name="requestHeaders">Request headers (optional)</param>
    /// <returns>The create consent response</returns>
    Task<CreateConsentResponse> CreateEnduringConsentAsync(EnduringConsentRequest? enduringConsentRequest,
        Dictionary<string, string?>? requestHeaders = null);

    /// <summary>
    /// Get an enduring consent by ID
    /// </summary>
    /// <param name="consentId">The consent ID</param>
    /// <param name="requestHeaders">Request headers (optional)</param>
    /// <returns>The consent</returns>
    Task<Consent> GetEnduringConsentAsync(Guid consentId, Dictionary<string, string?>? requestHeaders = null);

    /// <summary>
    /// Await authorised enduring consent. This method will poll until the consent is authorised or the max wait time is reached.
    /// </summary>
    /// <param name="consentId">The consent ID</param>
    /// <param name="maxWaitSeconds">Maximum wait time in seconds</param>
    /// <returns>The authorised consent</returns>
    Task<Consent> AwaitAuthorisedEnduringConsentAsync(Guid consentId, int maxWaitSeconds);

    /// <summary>
    /// Revoke an enduring consent
    /// </summary>
    /// <param name="consentId">The consent ID</param>
    /// <param name="requestHeaders">Request headers (optional)</param>
    Task RevokeEnduringConsentAsync(Guid consentId, Dictionary<string, string?>? requestHeaders = null);

    // Quick Payment Operations

    /// <summary>
    /// Create a quick payment (single consent + payment in one step)
    /// </summary>
    /// <param name="quickPaymentRequest">The quick payment request</param>
    /// <param name="requestHeaders">Request headers (optional)</param>
    /// <returns>The create quick payment response</returns>
    Task<CreateQuickPaymentResponse> CreateQuickPaymentAsync(QuickPaymentRequest? quickPaymentRequest,
        Dictionary<string, string?>? requestHeaders = null);

    /// <summary>
    /// Get a quick payment by ID
    /// </summary>
    /// <param name="quickPaymentId">The quick payment ID</param>
    /// <param name="requestHeaders">Request headers (optional)</param>
    /// <returns>The quick payment response</returns>
    Task<QuickPaymentResponse> GetQuickPaymentAsync(Guid quickPaymentId,
        Dictionary<string, string?>? requestHeaders = null);

    /// <summary>
    /// Await successful quick payment. This method will poll until the payment is successful or the max wait time is reached.
    /// </summary>
    /// <param name="quickPaymentId">The quick payment ID</param>
    /// <param name="maxWaitSeconds">Maximum wait time in seconds</param>
    /// <returns>The successful quick payment response</returns>
    Task<QuickPaymentResponse> AwaitSuccessfulQuickPaymentAsync(Guid quickPaymentId, int maxWaitSeconds);

    /// <summary>
    /// Revoke a quick payment
    /// </summary>
    /// <param name="quickPaymentId">The quick payment ID</param>
    /// <param name="requestHeaders">Request headers (optional)</param>
    Task RevokeQuickPaymentAsync(Guid quickPaymentId, Dictionary<string, string?>? requestHeaders = null);

    // Payment Operations

    /// <summary>
    /// Create a payment (requires existing authorised consent)
    /// </summary>
    /// <param name="paymentRequest">The payment request</param>
    /// <param name="requestHeaders">Request headers (optional)</param>
    /// <returns>The payment response</returns>
    Task<PaymentResponse> CreatePaymentAsync(PaymentRequest? paymentRequest,
        Dictionary<string, string?>? requestHeaders = null);

    /// <summary>
    /// Get a payment by ID
    /// </summary>
    /// <param name="paymentId">The payment ID</param>
    /// <param name="requestHeaders">Request headers (optional)</param>
    /// <returns>The payment</returns>
    Task<Payment> GetPaymentAsync(Guid paymentId, Dictionary<string, string?>? requestHeaders = null);

    /// <summary>
    /// Await successful payment. This method will poll until the payment is successful or the max wait time is reached.
    /// </summary>
    /// <param name="paymentId">The payment ID</param>
    /// <param name="maxWaitSeconds">Maximum wait time in seconds</param>
    /// <returns>The successful payment</returns>
    Task<Payment> AwaitSuccessfulPaymentAsync(Guid paymentId, int maxWaitSeconds);

    // Refund Operations

    /// <summary>
    /// Create a refund
    /// </summary>
    /// <param name="refundDetail">The refund details</param>
    /// <param name="requestHeaders">Request headers (optional)</param>
    /// <returns>The refund response</returns>
    Task<RefundResponse> CreateRefundAsync(RefundDetail refundDetail,
        Dictionary<string, string?>? requestHeaders = null);

    /// <summary>
    /// Get a refund by ID
    /// </summary>
    /// <param name="refundId">The refund ID</param>
    /// <param name="requestHeaders">Request headers (optional)</param>
    /// <returns>The refund</returns>
    Task<Refund> GetRefundAsync(Guid refundId, Dictionary<string, string?>? requestHeaders = null);
}
