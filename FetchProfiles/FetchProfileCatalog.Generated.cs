using System;
using System.Collections.Generic;

namespace KeeFetch.FetchProfiles
{
    internal static partial class FetchProfileCatalog
    {
        private static List<FetchProfileDefinition> CreateManagedProfiles()
        {
            List<FetchProfileDefinition> profiles = new List<FetchProfileDefinition>();

            profiles.Add(new FetchProfileDefinition(
                "bulk-fast",
                "Fast",
                "Shortest path. Tries direct site, then a compact strong-resolver chain with reduced time budgets for faster large batches.",
                "Large batch fetching with reduced latency",
                new string[] { "direct-site", "google", "twenty-icons" },
                4000,
                2500,
                15000,
                false,
                true,
                true,
                true,
                "docs/benchmarks/v1.3-provider-study.md"));

            profiles.Add(new FetchProfileDefinition(
                "everyday",
                "Balanced",
                "Recommended default. Uses direct site, Google, and a lightweight synthetic fallback to balance coverage and batch speed.",
                "Default everyday use balancing coverage and speed",
                new string[] { "direct-site", "google", "favicone" },
                6000,
                3500,
                22000,
                true,
                true,
                true,
                true,
                "docs/benchmarks/v1.3-provider-study.md"));

            profiles.Add(new FetchProfileDefinition(
                "privacy",
                "Privacy",
                "Privacy-focused mode. Uses only direct site resolution with no third-party requests.",
                "Privacy-sensitive fetching without third-party providers",
                new string[] { "direct-site" },
                6000,
                3500,
                22000,
                false,
                false,
                false,
                true,
                "docs/benchmarks/v1.3-provider-study.md"));

            profiles.Add(new FetchProfileDefinition(
                "max-coverage",
                "Thorough",
                "Availability-first mode. Uses the full resolver chain with the largest time budgets and synthetic fallbacks for maximum coverage.",
                "Maximum coverage with full resolver chain",
                new string[] { "direct-site", "twenty-icons", "duckduckgo", "google", "yandex", "favicone", "icon-horse" },
                10000,
                5000,
                45000,
                true,
                false,
                true,
                true,
                "docs/benchmarks/v1.3-provider-study.md"));

            return profiles;
        }
    }
}
