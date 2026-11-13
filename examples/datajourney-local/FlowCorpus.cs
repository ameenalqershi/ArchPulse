// SPDX-License-Identifier: Apache-2.0
using System;

namespace ArchPulse.DataJourney
{
    // Only intentionally declared, controlled sample annotations. These are
    // NOT proofs of confidentiality, sanitizer correctness or authorization.
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Method)]
    public sealed class SensitiveSourceAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class SensitiveSinkAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DeclaredSanitizerAttribute : Attribute { }
}

namespace DataJourney
{
    using ArchPulse.DataJourney;

    public static class Fixture
    {
        [SensitiveSink]
        public static void Log(string value) { }

        [DeclaredSanitizer]
        public static string Escape(string value) => value; // intentionally INSECURE dummy

        [SensitiveSource]
        public static string ReadSecret() => "DO-NOT-STORE-PAYLOAD";

        public static string Unmodeled(string value) => value;

        public static void UnsafeDirect([SensitiveSource] string token)
        {
            var alias = token;
            Log(alias);
        }

        public static void NoCandidateInSubset([SensitiveSource] string token)
        {
            var publicValue = "non-sensitive-placeholder";
            Log(publicValue);
        }

        public static void AnnotatedSanitizerNotProof([SensitiveSource] string token)
        {
            var claimed = Escape(token);
            Log(claimed);
        }

        public static void SourceMethodCall()
        {
            var source = ReadSecret();
            Log(source);
        }

        public static void OpaqueTransformation([SensitiveSource] string token)
        {
            var value = Unmodeled(token);
            Log(value);
        }

        public static void BranchUncertain([SensitiveSource] string token, bool flag)
        {
            var value = token;
            if (flag) value = "public";
            Log(value);
        }

        public static string ReturnRaw([SensitiveSource] string token)
        {
            var result = token;
            return result;
        }

        public static void ReassignmentKillsCandidate([SensitiveSource] string token)
        {
            var result = token;
            result = "public";
            Log(result);
        }

        // Name-only decoy attributes do not create dataflow annotations.
        public static void AnnotationDecoy([Fake.SensitiveSource] string token)
        {
            Log(token);
        }
    }
}

namespace Fake
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class SensitiveSourceAttribute : Attribute { }
}
