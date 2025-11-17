# BlinkDebitApiClient.Extensions.DependencyInjection

Dependency Injection extensions for `BlinkDebitApiClient` to enable seamless integration with ASP.NET Core and other .NET applications using `Microsoft.Extensions.DependencyInjection`.

## Installation

```bash
dotnet add package BlinkDebitApiClient
dotnet add package BlinkDebitApiClient.Extensions.DependencyInjection
```

## Quick Start

### Option 1: Configure with Action

```csharp
using BlinkDebitApiClient.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add Blink Debit Client with inline configuration
builder.Services.AddBlinkDebitClient(options =>
{
    options.DebitUrl = "https://sandbox.debit.blinkpay.co.nz";
    options.ClientId = builder.Configuration["BlinkPay:ClientId"]!;
    options.ClientSecret = builder.Configuration["BlinkPay:ClientSecret"]!;
    options.TimeoutSeconds = 10; // Optional, default is 10
    options.RetryEnabled = true;  // Optional, default is true
});

var app = builder.Build();
```

### Option 2: Configure from appsettings.json

**appsettings.json:**
```json
{
  "BlinkPay": {
    "DebitUrl": "https://sandbox.debit.blinkpay.co.nz",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "TimeoutSeconds": 10,
    "RetryEnabled": true
  }
}
```

**Program.cs:**
```csharp
using BlinkDebitApiClient.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add Blink Debit Client from configuration
builder.Services.AddBlinkDebitClient(builder.Configuration);

var app = builder.Build();
```

## Usage

### Complete Working Example

Here's a complete example showing how to create a quick payment with the DI extension:

```csharp
using BlinkDebitApiClient.Api.V1;
using BlinkDebitApiClient.Exceptions;
using BlinkDebitApiClient.Extensions.DependencyInjection;
using BlinkDebitApiClient.Model.V1;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Blink Debit Client from configuration
builder.Services.AddBlinkDebitClient(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Quick payment endpoint
app.MapPost("/api/quick-payment", async (IBlinkDebitClient blinkClient, ILogger<Program> logger) =>
{
    try
    {
        // Create a quick payment request
        var gatewayFlow = new GatewayFlow("https://www.example.com/return");
        var authFlowDetail = new AuthFlowDetail(gatewayFlow);
        var authFlow = new AuthFlow(authFlowDetail);
        var pcr = new Pcr("Payment"); // Particulars only (max 12 chars)
        var amount = new Amount("1.25", Amount.CurrencyEnum.NZD);
        var request = new QuickPaymentRequest(authFlow, pcr, amount);

        // Create the payment
        var response = await blinkClient.CreateQuickPaymentAsync(request);

        return Results.Ok(new
        {
            quickPaymentId = response.QuickPaymentId,
            redirectUri = response.RedirectUri
        });
    }
    catch (BlinkServiceException ex)
    {
        logger.LogError(ex, "Failed to create quick payment");
        return Results.Problem(ex.Message, statusCode: 500);
    }
})
.WithName("CreateQuickPayment")
.WithOpenApi();

app.Run();
```

### Inject into Controllers

```csharp
using BlinkDebitApiClient.Api.V1;
using BlinkDebitApiClient.Exceptions;
using BlinkDebitApiClient.Model.V1;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IBlinkDebitClient _blinkClient;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IBlinkDebitClient blinkClient, ILogger<PaymentsController> logger)
    {
        _blinkClient = blinkClient;
        _logger = logger;
    }

    [HttpPost("quick-payment")]
    public async Task<IActionResult> CreateQuickPayment()
    {
        try
        {
            // Create the payment request
            var gatewayFlow = new GatewayFlow("https://www.example.com/return");
            var authFlowDetail = new AuthFlowDetail(gatewayFlow);
            var authFlow = new AuthFlow(authFlowDetail);
            var pcr = new Pcr("Payment"); // Particulars only (max 12 chars)
            var amount = new Amount("1.25", Amount.CurrencyEnum.NZD);
            var request = new QuickPaymentRequest(authFlow, pcr, amount);

            // Create the payment
            var payment = await _blinkClient.CreateQuickPaymentAsync(request);
            return Ok(new
            {
                quickPaymentId = payment.QuickPaymentId,
                redirectUri = payment.RedirectUri
            });
        }
        catch (BlinkServiceException ex)
        {
            _logger.LogError(ex, "Failed to create quick payment");
            return StatusCode(500, "Payment creation failed");
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPayment(Guid id)
    {
        try
        {
            var payment = await _blinkClient.GetQuickPaymentAsync(id);
            return Ok(new
            {
                quickPaymentId = payment.QuickPaymentId,
                status = payment.Consent.Status.ToString()
            });
        }
        catch (BlinkServiceException ex)
        {
            _logger.LogError(ex, "Failed to retrieve payment {PaymentId}", id);
            return NotFound();
        }
    }
}
```

### Inject into Background Services

```csharp
using BlinkDebitApiClient.Api.V1;

public class PaymentProcessorService : BackgroundService
{
    private readonly IBlinkDebitClient _blinkClient;
    private readonly ILogger<PaymentProcessorService> _logger;

    public PaymentProcessorService(IBlinkDebitClient blinkClient, ILogger<PaymentProcessorService> logger)
    {
        _blinkClient = blinkClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Process payments...
                var banks = await _blinkClient.GetMetaAsync();
                _logger.LogInformation("Retrieved {Count} banks", banks.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payments");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
```

## Configuration Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| `DebitUrl` | `string` | Yes | - | Base URL for the Blink Debit API (e.g., `https://sandbox.debit.blinkpay.co.nz` for sandbox or `https://debit.blinkpay.co.nz` for production) |
| `ClientId` | `string` | Yes | - | OAuth2 client ID for authentication |
| `ClientSecret` | `string` | Yes | - | OAuth2 client secret for authentication |
| `TimeoutSeconds` | `int` | No | `10` | Timeout in seconds for API requests |
| `RetryEnabled` | `bool` | No | `true` | Enable retry policy with exponential backoff |

## Environment-Specific Configuration

Use different `appsettings.{Environment}.json` files:

**appsettings.Development.json:**
```json
{
  "BlinkPay": {
    "DebitUrl": "https://sandbox.debit.blinkpay.co.nz",
    "ClientId": "dev-client-id",
    "ClientSecret": "dev-client-secret"
  }
}
```

**appsettings.Production.json:**
```json
{
  "BlinkPay": {
    "DebitUrl": "https://debit.blinkpay.co.nz",
    "ClientId": "prod-client-id",
    "ClientSecret": "prod-client-secret"
  }
}
```

## Security Best Practices

### Use Azure Key Vault or AWS Secrets Manager

```csharp
// Azure Key Vault
builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{builder.Configuration["KeyVaultName"]}.vault.azure.net/"),
    new DefaultAzureCredential());

// Then access secrets
builder.Services.AddBlinkDebitClient(options =>
{
    options.DebitUrl = builder.Configuration["BlinkPay-DebitUrl"]!;
    options.ClientId = builder.Configuration["BlinkPay-ClientId"]!;
    options.ClientSecret = builder.Configuration["BlinkPay-ClientSecret"]!;
});
```

### Use User Secrets for Development

```bash
dotnet user-secrets init
dotnet user-secrets set "BlinkPay:ClientId" "your-client-id"
dotnet user-secrets set "BlinkPay:ClientSecret" "your-client-secret"
```

## License

MIT License - see [LICENSE](../../LICENSE) for details

## Support

For issues and questions, please visit:
- [GitHub Issues](https://github.com/BlinkPay/Blink-Debit-API-Client-DotNet/issues)
- [Documentation](https://github.com/BlinkPay/Blink-Debit-API-Client-DotNet)
