// SPDX-License-Identifier: Apache-2.0
namespace ArchPulse.DataJourney;

[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Method)]
public sealed class SensitiveSourceAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method)]
public sealed class SensitiveSinkAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method)]
public sealed class DeclaredSanitizerAttribute : Attribute;
