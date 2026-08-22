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
                "tries 3 icon source(s) in order (the site itself, Google, Twenty Icons) within a 15s total budget; stops as soon as a strong resolver returns a high-confidence icon.",
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
                "tries 6 icon source(s) in order (the site itself, Twenty Icons, DuckDuckGo, Google, Yandex, Icon Horse) within a 45s total budget; queries every source in the chain before selecting; allows a generated fallback icon when no real icon is found.",
                "Default everyday use balancing coverage and speed",
                new string[] { "direct-site", "twenty-icons", "duckduckgo", "google", "yandex", "icon-horse" },
                10000,
                5000,
                45000,
                true,
                false,
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
                "Thorough",
                "tries 2 icon source(s) in order (the site itself, Yandex) within a 22s total budget; stops as soon as a strong resolver returns a high-confidence icon.",
                "Maximum coverage with the study-selected resolver chain",
                new string[] { "direct-site", "yandex" },
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
