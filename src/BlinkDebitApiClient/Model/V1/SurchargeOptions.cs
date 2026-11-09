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
using Newtonsoft.Json;

namespace BlinkDebitApiClient.Model.V1;

/// <summary>
/// A best-effort preview of the surcharge each payment method would add, returned while a gateway consent is in
/// <c>GatewayAwaitingSubmission</c>. Only methods the merchant surcharges are included, card only when the consent
/// can be paid by card. Response only; never sent in a request.
/// </summary>
[DataContract(Name = "surcharge_options")]
public class SurchargeOptions : IEquatable<SurchargeOptions>, IValidatableObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurchargeOptions" /> class.
    /// </summary>
    [JsonConstructorAttribute]
    protected SurchargeOptions()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SurchargeOptions" /> class.
    /// </summary>
    /// <param name="bank">The bank option.</param>
    /// <param name="card">The card option.</param>
    public SurchargeOptions(SurchargeOption? bank = default(SurchargeOption),
        SurchargeOption? card = default(SurchargeOption))
    {
        Bank = bank;
        Card = card;
    }

    /// <summary>
    /// The surcharge that would apply when paying via bank.
    /// </summary>
    [DataMember(Name = "bank", EmitDefaultValue = false)]
    public SurchargeOption? Bank { get; set; }

    /// <summary>
    /// The surcharge that would apply when paying via card. Single consents and quick payments only.
    /// </summary>
    [DataMember(Name = "card", EmitDefaultValue = false)]
    public SurchargeOption? Card { get; set; }

    /// <summary>
    /// Returns the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append("class SurchargeOptions {\n");
        sb.Append("  Bank: ").Append(Bank).Append('\n');
        sb.Append("  Card: ").Append(Card).Append('\n');
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
        return Equals(input as SurchargeOptions);
    }

    /// <summary>
    /// Returns true if SurchargeOptions instances are equal
    /// </summary>
    /// <param name="input">Instance of SurchargeOptions to be compared</param>
    /// <returns>Boolean</returns>
    public bool Equals(SurchargeOptions input)
    {
        if (input == null)
        {
            return false;
        }

        return
            (
                Bank == input.Bank ||
                (Bank != null &&
                 Bank.Equals(input.Bank))
            ) &&
            (
                Card == input.Card ||
                (Card != null &&
                 Card.Equals(input.Card))
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
            if (Bank != null)
            {
                hashCode = (hashCode * 59) + Bank.GetHashCode();
            }

            if (Card != null)
            {
                hashCode = (hashCode * 59) + Card.GetHashCode();
            }

            return hashCode;
        }
    }

    /// <summary>
    /// To validate all properties of the instance
    /// </summary>
    /// <param name="validationContext">Validation context</param>
    /// <returns>Validation Result</returns>
    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        yield break;
    }
}
