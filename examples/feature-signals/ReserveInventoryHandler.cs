// SPDX-License-Identifier: Apache-2.0
namespace FeatureSignals.Inventory;

// Name-only candidate; deliberately no DI/dispatcher registration.
public sealed class ReserveInventoryHandler
{
    public bool Handle(string sku) => !string.IsNullOrWhiteSpace(sku);
}

// Decoy: a handler-ish class with no Handle entry point.
public sealed class InventoryHelper
{
    public bool Handle(string sku) => sku.Length > 1;
}
