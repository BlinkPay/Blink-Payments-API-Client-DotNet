/*
 * Copyright (c) 2026 BlinkPay
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

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using BlinkDebitApiClient.Model.V1;
using Newtonsoft.Json;
using Xunit;

namespace BlinkDebitApiClient.Test.Model.V1;

public class SurchargeModelTests
{
    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);

        return results;
    }

    [Theory(DisplayName = "A surcharge option or detail with well-formed or absent values is accepted")]
    [InlineData("0.68", "100.68")]
    [InlineData(null, null)]
    public void AcceptsWellFormedOrAbsentValues(string? surcharge, string? totalCharge)
    {
        Assert.Empty(Validate(new SurchargeOption(surcharge, totalCharge)));
        Assert.Empty(Validate(new SurchargeDetail(surcharge, totalCharge)));
    }

    [Theory(DisplayName = "A surcharge option or detail with a malformed value is rejected")]
    [InlineData("abc", "100.68", "Surcharge")]
    [InlineData("0.68", "abc", "TotalCharge")]
    public void RejectsMalformedValues(string surcharge, string totalCharge, string member)
    {
        Assert.Contains(Validate(new SurchargeOption(surcharge, totalCharge)),
            result => result.MemberNames.Contains(member));
        Assert.Contains(Validate(new SurchargeDetail(surcharge, totalCharge)),
            result => result.MemberNames.Contains(member));
    }

    [Fact(DisplayName = "A returned amount with a malformed surcharge is rejected")]
    public void RejectsMalformedAmountSurcharge()
    {
        var amount = JsonConvert.DeserializeObject<Amount>(
            """{"currency":"NZD","total":"100.00","surcharge":"abc","total_charge":"100.68"}""")!;

        Assert.Contains(Validate(amount), result => result.MemberNames.Contains("Surcharge"));
    }

    [Theory(DisplayName = "Amounts that differ only by surcharge or total charge are not equal")]
    [InlineData("""{"currency":"NZD","total":"100.00","surcharge":"0.50","total_charge":"100.68"}""")]
    [InlineData("""{"currency":"NZD","total":"100.00","surcharge":"0.68","total_charge":"100.50"}""")]
    [InlineData("""{"currency":"NZD","total":"100.00"}""")]
    public void AmountsDifferingOnlyBySurchargeAreNotEqual(string other)
    {
        const string surcharged = """{"currency":"NZD","total":"100.00","surcharge":"0.68","total_charge":"100.68"}""";

        Assert.NotEqual(JsonConvert.DeserializeObject<Amount>(surcharged), JsonConvert.DeserializeObject<Amount>(other));
        Assert.Equal(JsonConvert.DeserializeObject<Amount>(surcharged), JsonConvert.DeserializeObject<Amount>(surcharged));
    }
}
