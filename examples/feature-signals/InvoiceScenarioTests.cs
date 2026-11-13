// SPDX-License-Identifier: Apache-2.0
namespace Xunit
{
    // A compile-only fixture, NOT a real xUnit runner or accepted test case.
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class FactAttribute : Attribute { }
}

namespace FeatureSignals.Scenarios
{
    public sealed class InvoiceScenarioTests
    {
        [Xunit.Fact]
        public void CreateInvoice_EmitsReceipt() { }
    }
}

namespace FeatureSignals.Decoy
{
    // Same short name as MVC attribute, different full namespace.
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HttpPostAttribute : Attribute { }
    public sealed class FakeController
    {
        [HttpPost]
        public void DeleteInvoices() { }
    }
}
