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
using BlinkDebitApiClient.Api.V1;
using BlinkDebitApiClient.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlinkDebitApiClient.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Blink Debit API Client in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Blink Debit API Client to the specified <see cref="IServiceCollection"/> with configuration.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">A delegate to configure the <see cref="BlinkDebitClientOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <example>
    /// <code>
    /// services.AddBlinkDebitClient(options =>
    /// {
    ///     options.DebitUrl = "https://sandbox.debit.blinkpay.co.nz";
    ///     options.ClientId = configuration["BlinkPay:ClientId"];
    ///     options.ClientSecret = configuration["BlinkPay:ClientSecret"];
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddBlinkDebitClient(
        this IServiceCollection services,
        Action<BlinkDebitClientOptions> configure)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        // Configure options
        services.Configure(configure);

        // Register the client as singleton
        services.AddSingleton<IBlinkDebitClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<BlinkDebitClientOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<BlinkDebitClient>>();

            // Validate options
            options.Validate();

            // Create BlinkDebitClient with the configured options
            // Note: Timeout and retry settings are configured via RetryConfiguration static class if needed
            return new BlinkDebitClient(
                logger,
                options.DebitUrl,
                options.ClientId,
                options.ClientSecret
            );
        });

        return services;
    }

    /// <summary>
    /// Adds the Blink Debit API Client to the specified <see cref="IServiceCollection"/> using configuration.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configuration">The <see cref="IConfiguration"/> to bind from.</param>
    /// <param name="sectionName">The name of the configuration section to bind (default: "BlinkPay").</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <example>
    /// appsettings.json:
    /// <code>
    /// {
    ///   "BlinkPay": {
    ///     "DebitUrl": "https://sandbox.debit.blinkpay.co.nz",
    ///     "ClientId": "your-client-id",
    ///     "ClientSecret": "your-client-secret",
    ///     "TimeoutSeconds": 10,
    ///     "RetryEnabled": true
    ///   }
    /// }
    /// </code>
    ///
    /// Program.cs:
    /// <code>
    /// services.AddBlinkDebitClient(configuration);
    /// </code>
    /// </example>
    public static IServiceCollection AddBlinkDebitClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "BlinkPay")
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        // Bind configuration section to options
        services.Configure<BlinkDebitClientOptions>(configuration.GetSection(sectionName));

        // Register the client as singleton
        services.AddSingleton<IBlinkDebitClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<BlinkDebitClientOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<BlinkDebitClient>>();

            // Validate options
            options.Validate();

            // Create BlinkDebitClient with the configured options
            // Note: Timeout and retry settings are configured via RetryConfiguration static class if needed
            return new BlinkDebitClient(
                logger,
                options.DebitUrl,
                options.ClientId,
                options.ClientSecret
            );
        });

        return services;
    }
}
