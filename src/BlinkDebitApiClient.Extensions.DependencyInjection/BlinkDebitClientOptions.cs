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

namespace BlinkDebitApiClient.Extensions.DependencyInjection;

/// <summary>
/// Configuration options for the Blink Debit API Client.
/// </summary>
public class BlinkDebitClientOptions
{
    /// <summary>
    /// The base URL for the Blink Debit API.
    /// </summary>
    /// <example>https://sandbox.debit.blinkpay.co.nz</example>
    public string DebitUrl { get; set; } = string.Empty;

    /// <summary>
    /// The OAuth2 client ID for authentication.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// The OAuth2 client secret for authentication.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// The timeout in seconds for API requests. Default is 10 seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Whether to enable retry policy with exponential backoff. Default is true.
    /// </summary>
    public bool RetryEnabled { get; set; } = true;

    /// <summary>
    /// Validates that all required options are set.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when required options are missing.</exception>
    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(DebitUrl))
        {
            throw new InvalidOperationException(
                "BlinkDebitClientOptions.DebitUrl is required. " +
                "Set it via configuration: builder.Services.AddBlinkDebitClient(options => options.DebitUrl = \"...\")");
        }

        if (string.IsNullOrWhiteSpace(ClientId))
        {
            throw new InvalidOperationException(
                "BlinkDebitClientOptions.ClientId is required. " +
                "Set it via configuration: builder.Services.AddBlinkDebitClient(options => options.ClientId = \"...\")");
        }

        if (string.IsNullOrWhiteSpace(ClientSecret))
        {
            throw new InvalidOperationException(
                "BlinkDebitClientOptions.ClientSecret is required. " +
                "Set it via configuration: builder.Services.AddBlinkDebitClient(options => options.ClientSecret = \"...\")");
        }

        if (TimeoutSeconds <= 0)
        {
            throw new InvalidOperationException(
                "BlinkDebitClientOptions.TimeoutSeconds must be greater than 0.");
        }
    }
}
