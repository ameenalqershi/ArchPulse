// SPDX-License-Identifier: Apache-2.0
namespace Billing.Contracts;
public sealed class RateCalculator
{
    public decimal Apply(decimal amount, decimal rate) => amount * rate;
}
