// SPDX-License-Identifier: Apache-2.0
using ArchPulse.DataJourney;

namespace JourneyFixture;

public static class StorageAdapter
{
    [SensitiveSink]
    public static void Store(string value) { }
}
