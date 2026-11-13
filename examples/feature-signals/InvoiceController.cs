// SPDX-License-Identifier: Apache-2.0
using Microsoft.AspNetCore.Mvc;

namespace FeatureSignals.Invoices;

// Real ASP.NET MVC attributes, but no application host: discovery must not
// claim an endpoint was actually registered or invoked.
[Route("api/invoices")]
public sealed class InvoicesController : ControllerBase
{
    [HttpPost]
    public ActionResult<string> CreateInvoice() => "proposed";
    [HttpGet("{invoiceId}")]
    public ActionResult<string> GetInvoice(string invoiceId) => invoiceId;
}
