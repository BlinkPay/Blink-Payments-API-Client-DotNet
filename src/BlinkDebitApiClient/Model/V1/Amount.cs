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
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using BlinkDebitApiClient.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace BlinkDebitApiClient.Model.V1;

/// <summary>
/// Amount with currency.
/// </summary>
[DataContract(Name = "amount")]
public class Amount : IEquatable<Amount>, IValidatableObject
{
    /// <summary>
    /// The currency. Only NZD is supported.
    /// </summary>
    /// <value>The currency. Only NZD is supported.</value>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum CurrencyEnum
    {
        /// <summary>
        /// Enum NZD for value: NZD
        /// </summary>
        [EnumMember(Value = "NZD")] NZD = 1
    }


    /// <summary>
    /// The currency. Only NZD is supported.
    /// </summary>
    /// <value>The currency. Only NZD is supported.</value>
    /// <example>&quot;NZD&quot;</example>
    [DataMember(Name = "currency", IsRequired = true, EmitDefaultValue = true)]
    public CurrencyEnum Currency { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Amount" /> class.
    /// </summary>
    [JsonConstructorAttribute]
    protected Amount()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Amount" /> class.
    /// </summary>
    /// <param name="total">The amount. (required).</param>
    /// <param name="currency">The currency. Only NZD is supported. (required).</param>
    public Amount(string total = default(string), CurrencyEnum currency = default(CurrencyEnum))
    {
        // to ensure "total" is required (not null)
        Total = total ??
                throw new BlinkInvalidValueException("total is a required property for Amount and cannot be null");
        Currency = currency;
    }

    /// <summary>
    /// The amount.
    /// </summary>
    /// <value>The amount.</value>
    /// <example>&quot;100.00&quot;</example>
    [DataMember(Name = "total", IsRequired = true, EmitDefaultValue = true)]
    public string Total { get; set; }

    /// <summary>
    /// The surcharge applied to the payment, where surcharging applies. Read-only: returned by Blink and never
    /// sent in a request, so a response amount can be reused in a request without the API rejecting it.
    /// </summary>
    /// <value>The surcharge, or <c>null</c> when none was returned. A zero surcharge on a gateway card payment is
    /// omitted too, so <c>null</c> alone does not mean surcharging was not applied.</value>
    /// <example>&quot;0.68&quot;</example>
    [DataMember(Name = "surcharge", EmitDefaultValue = false)]
    public string? Surcharge { get; private set; }

    /// <summary>
    /// The total charged to the customer, inclusive of any surcharge (<see cref="Total" /> + <see cref="Surcharge" />).
    /// On an enduring consent's maxima it is the raised maximum. Read-only: returned by Blink and never sent in a
    /// request.
    /// </summary>
    /// <value>The total charge, or <c>null</c> when none was returned.</value>
    /// <example>&quot;100.68&quot;</example>
    [DataMember(Name = "total_charge", EmitDefaultValue = false)]
    public string? TotalCharge { get; private set; }

    /// <summary>
    /// Blink rejects <c>surcharge</c> and <c>total_charge</c> in a consent request, and <see cref="Amount" /> is
    /// shared with the request models, so these response-only values are never serialised.
    /// </summary>
    public bool ShouldSerializeSurcharge() => false;

    /// <summary>
    /// See <see cref="ShouldSerializeSurcharge" />.
    /// </summary>
    public bool ShouldSerializeTotalCharge() => false;

    /// <summary>
    /// Returns the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append("class Amount {\n");
        sb.Append("  Total: ").Append(Total).Append('\n');
        sb.Append("  Currency: ").Append(Currency).Append('\n');
        sb.Append("  Surcharge: ").Append(Surcharge).Append('\n');
        sb.Append("  TotalCharge: ").Append(TotalCharge).Append('\n');
        sb.Append("}\n");
        return sb.ToString();
    }

    /// <summary>
    /// Returns the JSON string presentation of the object. <see cref="Surcharge" /> and <see cref="TotalCharge" />
    /// are response-only and omitted, so a round trip through this JSON drops them.
    /// </summary>
    /// <returns>JSON string presentation of the object</returns>
    public virtual string ToJson()
    {
        return JsonConvert.SerializeObject(this, Formatting.Indented);
    }

    /// <summary>
    /// Returns true if objects are equal
    /// </summary>
    /// <param name="input">Object to be compared</param>
    /// <returns>Boolean</returns>
    public override bool Equals(object input)
    {
        return Equals(input as Amount);
    }

    /// <summary>
    /// Returns true if Amount instances are equal
    /// </summary>
    /// <param name="input">Instance of Amount to be compared</param>
    /// <returns>Boolean</returns>
    public bool Equals(Amount input)
    {
        if (input == null)
        {
            return false;
        }

        return
            (
                Total == input.Total ||
                (Total != null &&
                 Total.Equals(input.Total))
            ) &&
            (
                Currency == input.Currency ||
                Currency.Equals(input.Currency)
            ) &&
            Surcharge == input.Surcharge &&
            TotalCharge == input.TotalCharge;
    }

    /// <summary>
    /// Gets the hash code
    /// </summary>
    /// <returns>Hash code</returns>
    public override int GetHashCode()
    {
        unchecked // Overflow is fine, just wrap
        {
            var hashCode = 41;
            if (Total != null)
            {
                hashCode = (hashCode * 59) + Total.GetHashCode();
            }

            hashCode = (hashCode * 59) + Currency.GetHashCode();
            if (Surcharge != null)
            {
                hashCode = (hashCode * 59) + Surcharge.GetHashCode();
            }

            if (TotalCharge != null)
            {
                hashCode = (hashCode * 59) + TotalCharge.GetHashCode();
            }

            return hashCode;
        }
    }

    /// <summary>
    /// Compiled regex pattern for validating the amount format of Total, Surcharge and TotalCharge.
    /// </summary>
    private static readonly Regex RegexTotal = new Regex(@"^\d{1,13}\.\d{1,2}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>
    /// To validate all properties of the instance
    /// </summary>
    /// <param name="validationContext">Validation context</param>
    /// <returns>Validation Result</returns>
    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        // Total (string) pattern
        if (false == RegexTotal.Match(Total).Success)
        {
            yield return new ValidationResult("Invalid value for Total, must match a pattern of " + RegexTotal,
                new[] { "Total" });
        }

        if (Surcharge != null && false == RegexTotal.Match(Surcharge).Success)
        {
            yield return new ValidationResult("Invalid value for Surcharge, must match a pattern of " + RegexTotal,
                new[] { "Surcharge" });
        }

        if (TotalCharge != null && false == RegexTotal.Match(TotalCharge).Success)
        {
            yield return new ValidationResult("Invalid value for TotalCharge, must match a pattern of " + RegexTotal,
                new[] { "TotalCharge" });
        }
    }
}