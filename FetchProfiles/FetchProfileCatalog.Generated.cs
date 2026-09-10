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
                "tries 2 icon source(s) in order (the site itself, Yandex) within a 22s total budget; stops as soon as a strong resolver returns a high-confidence icon.",
                "Large batch fetching with reduced latency",
                new string[] { "direct-site", "yandex" },
                6000,
                3500,
                22000,
                false,
                true,
                true,
                true,
                "docs/benchmarks/v1.3-provider-study.md"));

            profiles.Add(new FetchProfileDefinition(
                "everyday",
                "Balanced",
                "tries 3 icon source(s) in order (the site itself, Google, Twenty Icons) within a 15s total budget; stops as soon as a strong resolver returns a high-confidence icon.",
                "Default everyday use balancing coverage and speed",
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
                "privacy",
                "Privacy",
                "tries 1 icon source(s) in order (the site itself) within a 22s total budget; stops as soon as a strong resolver returns a high-confidence icon.",
                "Privacy-sensitive fetching without third-party providers",
                new string[] { "direct-site" },
                6000,
                3500,
                22000,
                false,
                true,
                false,
                true,
                "docs/benchmarks/v1.3-provider-study.md"));

            profiles.Add(new FetchProfileDefinition(
                "max-coverage",
                "Precise",
                "tries 2 icon source(s) in order (the site itself, Google) within a 22s total budget; stops as soon as a strong resolver returns a high-confidence icon.",
                "Favor icon precision over finding an icon for every entry",
                new string[] { "direct-site", "google" },
                6000,
                3500,
                22000,
                false,
                true,
                true,
                true,
                "docs/benchmarks/v1.3-provider-study.md"));

            return profiles;
        }
    }
}
