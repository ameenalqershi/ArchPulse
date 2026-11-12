// SPDX-License-Identifier: Apache-2.0
using Billing.Contracts;
namespace Billing.Service;
public sealed class InvoiceService
{
    public decimal CreateInvoice(decimal total) =>
        new RateCalculator().Apply(total, 0.15m);
}
public sealed class PaymentService
{
    public decimal PayInvoice(decimal sum) => new InvoiceService().CreateInvoice(sum);
}
