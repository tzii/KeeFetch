using System.Collections.Generic;
using KeeFetch.IconSelection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    [TestClass]
    public class FaviconDiagnosticsTests
    {
        [TestMethod]
        public void BuildCsvRow_EscapesFieldsAndIncludesProviderMetrics()
        {
            var result = new FaviconResult
            {
                Status = FaviconStatus.Success,
                Provider = "Direct Site",
                SelectedTier = IconTier.SiteCanonical,
                WasSyntheticFallback = false,
                ElapsedMilliseconds = 1234,
                AttemptedProviders = new List<string> { "Direct Site", "Google" },
                DiagnosticsSummary = "total=2; selected=Direct Site"
            };
            result.ProviderMetrics = new List<ProviderAttemptMetric>
            {
                new ProviderAttemptMetric("Direct Site", 1200, 2, "candidate"),
                new ProviderAttemptMetric("Google", 34, 0, "skipped")
            };

            string row = FaviconDiagnostics.BuildCsvRow("Title, \"Quoted\"", "https://example.com", result);

            Assert.AreEqual("\"Title, \"\"Quoted\"\"\",https://example.com,Success,,Direct Site,SiteCanonical,false,1234,\"Direct Site, Google\",total=2; selected=Direct Site,,\"Direct Site:1200ms/2/candidate, Google:34ms/0/skipped\"", row);
        }

        [TestMethod]
        public void BuildCsvRow_ReportsSvgOnlyMissReasonFromRejectedCandidate()
        {
            var result = new FaviconResult
            {
                Status = FaviconStatus.NotFound,
                SelectedTier = IconTier.Rejected,
                ElapsedMilliseconds = 50,
                AttemptedProviders = new List<string> { "Direct Site" },
                DiagnosticsSummary = "total=1; rejected=1; selected=none"
            };
            result.RejectedCandidates = new List<IconCandidate>
            {
                new IconCandidate
                {
                    ProviderName = "Direct Site",
                    Notes = "SVG candidate detected from manifest-icon."
                }
            };

            string row = FaviconDiagnostics.BuildCsvRow("SVG only", "https://example.org", result);

            Assert.IsTrue(row.Contains(",NotFound,svg-only,"));
            Assert.IsTrue(row.Contains("Direct Site:SVG candidate detected from manifest-icon."));
        }

        [TestMethod]
        public void BuildCsvRow_ReportsInvalidUrlWhenNoProvidersWereAttempted()
        {
            var result = new FaviconResult
            {
                Status = FaviconStatus.NotFound,
                SelectedTier = IconTier.Rejected,
                ElapsedMilliseconds = 0
            };

            string row = FaviconDiagnostics.BuildCsvRow("Bad", "not a url", result);

            Assert.IsTrue(row.Contains(",NotFound,invalid-url-or-no-provider,"));
        }
    }
}
