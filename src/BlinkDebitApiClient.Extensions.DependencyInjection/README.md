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

### Inject into Controllers

```csharp
using BlinkDebitApiClient.Api.V1;
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
    public async Task<IActionResult> CreateQuickPayment([FromBody] QuickPaymentRequest request)
    {
        try
        {
            var payment = await _blinkClient.CreateQuickPaymentAsync(request);
            return Ok(payment);
        }
        catch (Exception ex)
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
            return Ok(payment);
        }
        catch (Exception ex)
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
| `DebitUrl` | `string` | Yes | - | Base URL for the Blink Debit API (e.g., `https://sandbox.debit.blinkpay.co.nz`) |
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
