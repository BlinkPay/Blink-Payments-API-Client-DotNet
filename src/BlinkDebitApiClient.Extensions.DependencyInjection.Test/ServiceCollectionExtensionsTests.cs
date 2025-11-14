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
using BlinkDebitApiClient.Api.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BlinkDebitApiClient.Extensions.DependencyInjection.Test;

public class ServiceCollectionExtensionsTests
{
    [Fact(DisplayName = "AddBlinkDebitClient with action configures and registers client successfully")]
    public void AddBlinkDebitClient_WithAction_RegistersClient()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        // Act
        services.AddBlinkDebitClient(options =>
        {
            options.DebitUrl = "https://sandbox.debit.blinkpay.co.nz";
            options.ClientId = "test-client-id";
            options.ClientSecret = "test-client-secret";
            options.TimeoutSeconds = 15;
            options.RetryEnabled = true;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var client = serviceProvider.GetService<IBlinkDebitClient>();
        Assert.NotNull(client);
        Assert.IsAssignableFrom<BlinkDebitClient>(client);
    }

    [Fact(DisplayName = "AddBlinkDebitClient with configuration registers client successfully")]
    public void AddBlinkDebitClient_WithConfiguration_RegistersClient()
    {
        // Arrange
        var configDictionary = new Dictionary<string, string?>
        {
            ["BlinkPay:DebitUrl"] = "https://sandbox.debit.blinkpay.co.nz",
            ["BlinkPay:ClientId"] = "test-client-id",
            ["BlinkPay:ClientSecret"] = "test-client-secret",
            ["BlinkPay:TimeoutSeconds"] = "20",
            ["BlinkPay:RetryEnabled"] = "false"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDictionary)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        // Act
        services.AddBlinkDebitClient(configuration);

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var client = serviceProvider.GetService<IBlinkDebitClient>();
        Assert.NotNull(client);
        Assert.IsAssignableFrom<BlinkDebitClient>(client);
    }

    [Fact(DisplayName = "AddBlinkDebitClient with custom section name registers client successfully")]
    public void AddBlinkDebitClient_WithCustomSectionName_RegistersClient()
    {
        // Arrange
        var configDictionary = new Dictionary<string, string?>
        {
            ["CustomSection:DebitUrl"] = "https://sandbox.debit.blinkpay.co.nz",
            ["CustomSection:ClientId"] = "test-client-id",
            ["CustomSection:ClientSecret"] = "test-client-secret"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDictionary)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        // Act
        services.AddBlinkDebitClient(configuration, "CustomSection");

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var client = serviceProvider.GetService<IBlinkDebitClient>();
        Assert.NotNull(client);
        Assert.IsAssignableFrom<BlinkDebitClient>(client);
    }

    [Fact(DisplayName = "AddBlinkDebitClient registers singleton")]
    public void AddBlinkDebitClient_RegistersSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        services.AddBlinkDebitClient(options =>
        {
            options.DebitUrl = "https://sandbox.debit.blinkpay.co.nz";
            options.ClientId = "test-client-id";
            options.ClientSecret = "test-client-secret";
        });

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var client1 = serviceProvider.GetService<IBlinkDebitClient>();
        var client2 = serviceProvider.GetService<IBlinkDebitClient>();

        // Assert
        Assert.NotNull(client1);
        Assert.NotNull(client2);
        Assert.Same(client1, client2); // Same instance = singleton
    }

    [Fact(DisplayName = "AddBlinkDebitClient with null services throws ArgumentNullException")]
    public void AddBlinkDebitClient_WithNullServices_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection? services = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            services!.AddBlinkDebitClient(options =>
            {
                options.DebitUrl = "https://sandbox.debit.blinkpay.co.nz";
                options.ClientId = "test-client-id";
                options.ClientSecret = "test-client-secret";
            });
        });
    }

    [Fact(DisplayName = "AddBlinkDebitClient with null configure action throws ArgumentNullException")]
    public void AddBlinkDebitClient_WithNullConfigureAction_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        Action<BlinkDebitClientOptions>? configure = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            services.AddBlinkDebitClient(configure!);
        });
    }

    [Fact(DisplayName = "AddBlinkDebitClient with missing DebitUrl throws InvalidOperationException")]
    public void AddBlinkDebitClient_WithMissingDebitUrl_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        services.AddBlinkDebitClient(options =>
        {
            options.ClientId = "test-client-id";
            options.ClientSecret = "test-client-secret";
            // DebitUrl is missing
        });

        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
        {
            serviceProvider.GetService<IBlinkDebitClient>();
        });
    }

    [Fact(DisplayName = "AddBlinkDebitClient with missing ClientId throws InvalidOperationException")]
    public void AddBlinkDebitClient_WithMissingClientId_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        services.AddBlinkDebitClient(options =>
        {
            options.DebitUrl = "https://sandbox.debit.blinkpay.co.nz";
            options.ClientSecret = "test-client-secret";
            // ClientId is missing
        });

        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
        {
            serviceProvider.GetService<IBlinkDebitClient>();
        });
    }

    [Fact(DisplayName = "AddBlinkDebitClient with missing ClientSecret throws InvalidOperationException")]
    public void AddBlinkDebitClient_WithMissingClientSecret_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        services.AddBlinkDebitClient(options =>
        {
            options.DebitUrl = "https://sandbox.debit.blinkpay.co.nz";
            options.ClientId = "test-client-id";
            // ClientSecret is missing
        });

        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
        {
            serviceProvider.GetService<IBlinkDebitClient>();
        });
    }
}
