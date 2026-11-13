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
using Newtonsoft.Json;

namespace BlinkDebitApiClient.Model.V1;

/// <summary>
/// What paying with one method would charge. On a single consent or quick payment it carries
/// <see cref="Surcharge" /> and <see cref="TotalCharge" />; on an enduring consent it carries
/// <see cref="MaximumAmountPayment" /> (absent when the consent has no per-payment maximum) and
/// <see cref="MaximumAmountPeriod" />. Response only; never sent in a request.
/// </summary>
[DataContract(Name = "surcharge_option")]
public class SurchargeOption : IEquatable<SurchargeOption>, IValidatableObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurchargeOption" /> class.
    /// </summary>
    [JsonConstructorAttribute]
    protected SurchargeOption()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SurchargeOption" /> class.
    /// </summary>
    /// <param name="surcharge">The surcharge.</param>
    /// <param name="totalCharge">The total charge.</param>
    /// <param name="maximumAmountPayment">The raised per-payment maximum.</param>
    /// <param name="maximumAmountPeriod">The raised per-period maximum.</param>
    public SurchargeOption(string? surcharge = default(string),
        string? totalCharge = default(string),
        SurchargeDetail? maximumAmountPayment = default(SurchargeDetail),
        SurchargeDetail? maximumAmountPeriod = default(SurchargeDetail))
    {
        Surcharge = surcharge;
        TotalCharge = totalCharge;
        MaximumAmountPayment = maximumAmountPayment;
        MaximumAmountPeriod = maximumAmountPeriod;
    }

    /// <summary>
    /// The surcharge that would apply for this payment method.
    /// </summary>
    /// <example>&quot;0.68&quot;</example>
    [DataMember(Name = "surcharge", EmitDefaultValue = false)]
    public string? Surcharge { get; set; }

    /// <summary>
    /// The total that would be charged (amount total + surcharge).
    /// </summary>
    /// <example>&quot;100.68&quot;</example>
    [DataMember(Name = "total_charge", EmitDefaultValue = false)]
    public string? TotalCharge { get; set; }

    /// <summary>
    /// How the per-payment maximum would be raised.
    /// </summary>
    [DataMember(Name = "maximum_amount_payment", EmitDefaultValue = false)]
    public SurchargeDetail? MaximumAmountPayment { get; set; }

    /// <summary>
    /// How the per-period maximum would be raised.
    /// </summary>
    [DataMember(Name = "maximum_amount_period", EmitDefaultValue = false)]
    public SurchargeDetail? MaximumAmountPeriod { get; set; }

    /// <summary>
    /// Returns the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append("class SurchargeOption {\n");
        sb.Append("  Surcharge: ").Append(Surcharge).Append('\n');
        sb.Append("  TotalCharge: ").Append(TotalCharge).Append('\n');
        sb.Append("  MaximumAmountPayment: ").Append(MaximumAmountPayment).Append('\n');
        sb.Append("  MaximumAmountPeriod: ").Append(MaximumAmountPeriod).Append('\n');
        sb.Append("}\n");
        return sb.ToString();
    }

    /// <summary>
    /// Returns the JSON string presentation of the object
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
        return Equals(input as SurchargeOption);
    }

    /// <summary>
    /// Returns true if SurchargeOption instances are equal
    /// </summary>
    /// <param name="input">Instance of SurchargeOption to be compared</param>
    /// <returns>Boolean</returns>
    public bool Equals(SurchargeOption input)
    {
        if (input == null)
        {
            return false;
        }

        return
            (
                Surcharge == input.Surcharge ||
                (Surcharge != null &&
                 Surcharge.Equals(input.Surcharge))
            ) &&
            (
                TotalCharge == input.TotalCharge ||
                (TotalCharge != null &&
                 TotalCharge.Equals(input.TotalCharge))
            ) &&
            (
                MaximumAmountPayment == input.MaximumAmountPayment ||
                (MaximumAmountPayment != null &&
                 MaximumAmountPayment.Equals(input.MaximumAmountPayment))
            ) &&
            (
                MaximumAmountPeriod == input.MaximumAmountPeriod ||
                (MaximumAmountPeriod != null &&
                 MaximumAmountPeriod.Equals(input.MaximumAmountPeriod))
            );
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
            if (Surcharge != null)
            {
                hashCode = (hashCode * 59) + Surcharge.GetHashCode();
            }

            if (TotalCharge != null)
            {
                hashCode = (hashCode * 59) + TotalCharge.GetHashCode();
            }

            if (MaximumAmountPayment != null)
            {
                hashCode = (hashCode * 59) + MaximumAmountPayment.GetHashCode();
            }

            if (MaximumAmountPeriod != null)
            {
                hashCode = (hashCode * 59) + MaximumAmountPeriod.GetHashCode();
            }

            return hashCode;
        }
    }

    private static readonly Regex RegexAmount = new Regex(@"^\d{1,13}\.\d{1,2}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>
    /// To validate all properties of the instance
    /// </summary>
    /// <param name="validationContext">Validation context</param>
    /// <returns>Validation Result</returns>
    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        if (Surcharge != null && false == RegexAmount.Match(Surcharge).Success)
        {
            yield return new ValidationResult("Invalid value for Surcharge, must match a pattern of " + RegexAmount,
                new[] { "Surcharge" });
        }

        if (TotalCharge != null && false == RegexAmount.Match(TotalCharge).Success)
        {
            yield return new ValidationResult("Invalid value for TotalCharge, must match a pattern of " + RegexAmount,
                new[] { "TotalCharge" });
        }
    }
}
