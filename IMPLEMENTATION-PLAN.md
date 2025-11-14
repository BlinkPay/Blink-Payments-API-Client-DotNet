# DotNet SDK - Auto-Generate Idempotency-Key Implementation Plan

## Overview

This implementation plan addresses **idempotency and documentation gaps** in the C# (.NET) SDK identified through comparison with the Java Plain SDK (v2) reference implementation.

**Priority**: HIGH
**Estimated Effort**: 2-3 hours implementation + 1 hour testing
**Files Modified**: ~8 files (~80-100 lines changed)

**Note**: DotNet SDK has excellent retry logic via Polly - NO retry changes needed. Token refresh buffer already excellent (5 minutes before expiry).

---

## Problem Statement

**Issue**: The DotNet SDK requires callers to manually provide `idempotency-key` for POST operations, increasing risk of:
1. Developers forgetting to provide it, leading to duplicate operations on retries
2. Developers providing the same key for different operations
3. Developers not reusing the key across retries for the same operation

**Current State**: The SDK has excellent retry logic via Polly (3 retries with exponential backoff), but idempotency-key management is caller's responsibility.

**Secondary Issue**: Missing polling behavior documentation - no table explaining auto-revoke on timeout (Java SDK has this).

**Reference**: Java Plain SDK auto-generates UUID for idempotency-key when not provided and ensures reuse across retries.

## Current Retry Implementation

The DotNet SDK already has robust retry via Polly (configured in `BlinkDebitClient.cs` lines 190-214):
- ✅ Retries: 3 attempts with exponential backoff (2^n seconds)
- ✅ Retries on: SocketException, WebException, HttpRequestException, BlinkRetryableException
- ✅ Request-id generation: Handled via `BlinkDebitConstant.REQUEST_ID_HEADER`

**No changes needed to retry logic** - it's already excellent with Polly.

## Tasks

### 1. Create Header Constants for Idempotency-Key

**File**: `src/BlinkDebitApiClient/Enums/BlinkDebitConstant.cs`

**Current State**: Already has `REQUEST_ID_HEADER = "request-id"`

**Add**:
```csharp
public const string IDEMPOTENCY_KEY_HEADER = "idempotency-key";
```

**Rationale**: Centralize header name constant for consistency.

---

### 2. Add Idempotency-Key Auto-Generation to POST Operations

**Files**: All API files in `src/BlinkDebitApiClient/Api/V1/`:
- `SingleConsentsApi.cs`
- `EnduringConsentsApi.cs`
- `QuickPaymentsApi.cs`
- `PaymentsApi.cs`
- `RefundsApi.cs`

**Pattern** (example from `SingleConsentsApi.cs`):

**Current implementation** (CreateSingleConsentAsync):
```csharp
public async Task<CreateConsentResponse> CreateSingleConsentAsync(
    Dictionary<string, string?>? requestHeaders = null,
    SingleConsentRequest? singleConsentRequest = default,
    int operationIndex = 0,
    CancellationToken cancellationToken = default)
{
    var response = await CreateSingleConsentWithHttpInfoAsync(requestHeaders, singleConsentRequest, operationIndex, cancellationToken);
    return response.Data;
}
```

**No change needed** - logic is in the `*WithHttpInfo` method.

**Find the method** `CreateSingleConsentWithHttpInfo` and locate where headers are added to the request.

**Add before the request is executed**:
```csharp
// Auto-generate idempotency-key if not provided
if (requestHeaders == null || !requestHeaders.ContainsKey(BlinkDebitConstant.IDEMPOTENCY_KEY_HEADER))
{
    if (requestHeaders == null)
    {
        requestHeaders = new Dictionary<string, string?>();
    }
    requestHeaders[BlinkDebitConstant.IDEMPOTENCY_KEY_HEADER] = Guid.NewGuid().ToString();
}
```

**Location**: Add this logic in the `*WithHttpInfo` method, BEFORE the request is built/executed, typically near where request-id is added.

---

### 3. Update Request Header Building Logic

**Files**: Each API class method that makes POST requests

**Methods to update**:

**SingleConsentsApi.cs**:
- `CreateSingleConsentWithHttpInfo` (line ~250-350)

**EnduringConsentsApi.cs**:
- `CreateEnduringConsentWithHttpInfo` (line ~250-350)

**QuickPaymentsApi.cs**:
- `CreateQuickPaymentWithHttpInfo` (line ~250-350)

**PaymentsApi.cs**:
- `CreatePaymentWithHttpInfo` (line ~250-350)

**RefundsApi.cs**:
- `CreateRefundWithHttpInfo` (line ~250-350)

**Pattern to apply**:

1. **Locate** where `requestHeaders` are processed (typically near `ClientUtils.AddHeaderParameter`)
2. **Add** idempotency-key auto-generation logic BEFORE headers are applied to the request
3. **Ensure** the generated key is added to the request using `ClientUtils.AddHeaderParameter`

**Example template**:
```csharp
// Existing code that processes requestHeaders
if (requestHeaders != null)
{
    foreach (var header in requestHeaders.Where(h => h.Value != null))
    {
        ClientUtils.AddHeaderParameter(localVarRequestOptions, header.Key, header.Value);
    }
}

// NEW: Auto-generate idempotency-key if not provided
var idempotencyKey = requestHeaders?.GetValueOrDefault(BlinkDebitConstant.IDEMPOTENCY_KEY_HEADER);
if (string.IsNullOrEmpty(idempotencyKey))
{
    idempotencyKey = Guid.NewGuid().ToString();
    _logger.LogDebug("Auto-generated idempotency-key: {idempotencyKey}", idempotencyKey);
}
ClientUtils.AddHeaderParameter(localVarRequestOptions, BlinkDebitConstant.IDEMPOTENCY_KEY_HEADER, idempotencyKey);

// Continue with existing request execution logic
```

**Note**: The exact location varies by method. Look for where `REQUEST_ID_HEADER` is handled as a reference point.

---

### 4. Ensure Idempotency-Key is Reused Across Retries

**File**: `src/BlinkDebitApiClient/Client/ApiClient.cs` (or where Polly retry is executed)

**Current**: Polly retry policy is configured in `BlinkDebitClient.cs` lines 203-213.

**Verification needed**: Confirm that when Polly retries, the same `RestRequest` object (with the same headers) is reused.

**If headers are regenerated on retry**: Modify the retry policy to capture the idempotency-key before the first attempt and inject it into subsequent attempts.

**Expected behavior**: Polly should automatically reuse the same request object, including headers, across retries. Verify by:
1. Adding debug logging to capture idempotency-key on each retry attempt
2. Confirming the value is identical across all attempts

**If reuse is NOT automatic**, wrap the request execution logic to preserve idempotency-key:
```csharp
string? capturedIdempotencyKey = null;

RetryConfiguration.AsyncRetryPolicy = policyBuilder
    .WaitAndRetryAsync(3,
        retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                        + TimeSpan.FromMilliseconds(RetryRandom.Next(0, 1000)),
        onRetry: (outcome, timespan, retryCount, context) =>
        {
            // Capture idempotency-key from first attempt
            if (retryCount == 1 && outcome.Result?.Headers != null)
            {
                capturedIdempotencyKey = outcome.Result.Headers
                    .FirstOrDefault(h => h.Name == BlinkDebitConstant.IDEMPOTENCY_KEY_HEADER)?.Value?.ToString();
            }
            _logger.LogInformation($"Retry {retryCount} with idempotency-key: {capturedIdempotencyKey}");
        });
```

**Expected**: Polly already handles this correctly - verify with integration test.

---

### 5. Update Documentation - CLAUDE.md

**File**: `src/BlinkDebitApiClient/CLAUDE.md`

**Add section** under "HTTP Client and Retry Logic":

```markdown
### Idempotency-Key Handling

For POST operations (create consent, payment, refund), the SDK automatically generates a UUID for the `idempotency-key` header if not provided by the caller.

**Auto-generation**:
- CreateSingleConsent
- CreateEnduringConsent
- CreateQuickPayment
- CreatePayment
- CreateRefund

**Key reuse**: The same idempotency-key is automatically reused across all retry attempts by Polly's retry policy, ensuring idempotent behavior even during transient failures.

**Custom key**: Callers can still provide their own idempotency-key via `requestHeaders`:
```csharp
var headers = new Dictionary<string, string?>
{
    ["idempotency-key"] = "my-custom-key-12345"
};
await client.CreateSingleConsentAsync(headers, consentRequest);
```
```

---

### 6. Update Documentation - README.md

**File**: `README.md`

**Add section** after API usage examples (similar to Java SDK):

```markdown
## Polling and Timeout Behavior

The SDK provides helper methods to wait for consent authorization and payment completion:

### Auto-Revoke on Timeout

| Method | Auto-Revokes on Timeout? | Reason |
|--------|-------------------------|--------|
| `AwaitSuccessfulQuickPaymentAsync` | ✅ **YES** | Quick payments combine consent + payment - should complete immediately or be cancelled |
| `AwaitAuthorisedSingleConsentAsync` | ❌ **NO** | Single consents require separate payment step - no funds processed if abandoned |
| `AwaitAuthorisedEnduringConsentAsync` | ✅ **YES** | Enduring consents grant ongoing access - clean up if abandoned for security |
| `AwaitSuccessfulPaymentAsync` | ❌ N/A | Payments cannot be revoked once initiated |

**Best Practices**:
- Manually revoke single or enduring consents if you determine the customer has permanently abandoned the authorization flow (before timeout expires)
- Enduring consents will auto-revoke on timeout, but earlier manual revocation improves security

### Payment Settlement and Wash-up Process

**CRITICAL**: Payments do NOT complete immediately after authorization. You MUST implement a wash-up process to check final settlement status.

**Settlement Statuses**:
- `Pending` - Payment initiated, not yet settled
- `AcceptedSettlementInProcess` - Settlement in progress
- `AcceptedSettlementCompleted` - ✅ **ONLY THIS STATUS means payment is complete**
- `Rejected` - Payment failed

**Wash-up Implementation**:
```csharp
// Poll payment status until settlement completes
public async Task<Payment> WaitForSettlement(Guid paymentId, int maxAttempts = 60)
{
    for (int i = 0; i < maxAttempts; i++)
    {
        var payment = await client.GetPaymentAsync(paymentId);

        if (payment.Status == Payment.StatusEnum.AcceptedSettlementCompleted)
        {
            return payment; // SUCCESS - funds transferred
        }

        if (payment.Status == Payment.StatusEnum.Rejected)
        {
            throw new Exception("Payment rejected");
        }

        await Task.Delay(5000); // Wait 5 seconds between checks
    }
    throw new Exception("Payment settlement timeout");
}
```

**Important**: Do NOT consider a payment complete unless status is `AcceptedSettlementCompleted`. Authorization alone does NOT guarantee funds transfer.
```

**Location**: Insert after the API usage examples section, before "Error Handling"

**Rationale**: Matches Java SDK documentation, clarifies auto-revoke behavior and critical settlement requirements

---

### 7. (Optional Enhancement) Add Dependency Injection Extension Method

**Priority**: MEDIUM (nice-to-have, not blocking)

**File**: `src/BlinkDebitApiClient/Extensions/BlinkDebitServiceCollectionExtensions.cs` (NEW FILE)

**Problem**: SDK can be manually instantiated with DI, but lacks first-class `services.AddBlinkDebitClient()` pattern common in .NET ecosystem.

**Solution**: Create extension method for seamless DI integration:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlinkDebitApiClient.Extensions;

public static class BlinkDebitServiceCollectionExtensions
{
    /// <summary>
    /// Registers BlinkDebitClient with dependency injection container
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration (appsettings.json)</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddBlinkDebitClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind appsettings.json "BlinkPay" section to BlinkPayProperties
        services.Configure<BlinkPayProperties>(configuration.GetSection("BlinkPay"));

        // Register BlinkDebitClient as singleton
        services.AddSingleton<BlinkDebitClient>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<BlinkDebitClient>>();
            var properties = sp.GetRequiredService<IOptions<BlinkPayProperties>>().Value;
            return new BlinkDebitClient(logger, properties);
        });

        return services;
    }
}
```

**Usage** in `Program.cs`:
```csharp
builder.Services.AddBlinkDebitClient(builder.Configuration);
```

**Rationale**: Reduces boilerplate, follows .NET conventions (like `AddHttpClient()`, `AddDbContext()`)

**Note**: This is an optional enhancement. Core functionality works without it.

---

## Testing Checklist

After implementation, verify:

1. **Auto-generation**: POST operations auto-generate UUID for idempotency-key when not provided
2. **Custom key honored**: Caller-provided idempotency-key is used when supplied
3. **Key reuse across retries**: Same idempotency-key used for all retry attempts (verify with debug logging)
4. **Network error retry**: Idempotency-key is preserved across retries
5. **5xx retry**: Idempotency-key is preserved across retries
6. **Documentation**: README includes auto-revoke timeout behavior table
7. **DI Extension** (optional): `AddBlinkDebitClient()` works in ASP.NET Core
8. **Existing functionality**: All existing tests pass

---

## Files to Modify

### Core Implementation (Required)
1. `src/BlinkDebitApiClient/Enums/BlinkDebitConstant.cs` - Add constant
2. `src/BlinkDebitApiClient/Api/V1/SingleConsentsApi.cs` - Add auto-generation
3. `src/BlinkDebitApiClient/Api/V1/EnduringConsentsApi.cs` - Add auto-generation
4. `src/BlinkDebitApiClient/Api/V1/QuickPaymentsApi.cs` - Add auto-generation
5. `src/BlinkDebitApiClient/Api/V1/PaymentsApi.cs` - Add auto-generation
6. `src/BlinkDebitApiClient/Api/V1/RefundsApi.cs` - Add auto-generation
7. `CLAUDE.md` - Update idempotency-key documentation
8. `README.md` - Add polling behavior documentation table

### Optional Enhancement
9. `src/BlinkDebitApiClient/Extensions/BlinkDebitServiceCollectionExtensions.cs` - NEW FILE - DI extension

**Estimated Lines Changed**:
- Core: ~80-100 lines
- With DI extension: ~120-140 lines

---

## Implementation Order

### Core Implementation
1. Add `IDEMPOTENCY_KEY_HEADER` constant to `BlinkDebitConstant.cs`
2. Update `SingleConsentsApi.CreateSingleConsentWithHttpInfo` as proof of concept
3. Test thoroughly (verify key generation and reuse)
4. Apply pattern to remaining POST operations
5. Update CLAUDE.md with idempotency-key documentation
6. Add polling behavior table to README.md
7. Run full integration test suite

### Optional DI Extension
8. Create `BlinkDebitServiceCollectionExtensions.cs` with `AddBlinkDebitClient()` method
9. Test in sample ASP.NET Core application
10. Update README.md with DI usage example

---

## Success Criteria

✅ Idempotency-key auto-generated (UUID) for all POST operations when not provided by caller
✅ Caller can still override by providing custom idempotency-key
✅ Same idempotency-key reused across all Polly retry attempts
✅ Debug logging shows idempotency-key value on each retry attempt (for verification)
✅ README.md includes polling behavior auto-revoke table
✅ All existing integration tests pass
✅ Documentation updated (CLAUDE.md and README.md)

---

## Notes

- **Retry logic**: No changes needed - Polly retry is already excellent
- **Request-id**: Already auto-generated - no changes needed
- **Token refresh**: Already handled - no changes needed
- **Scope**: This plan ONLY addresses idempotency-key auto-generation
