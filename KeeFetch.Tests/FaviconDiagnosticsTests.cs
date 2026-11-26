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
        public void RedactUrl_RemovesCredentialsQueryAndFragment()
        {
            Assert.AreEqual("https://example.com/login",
                FaviconDiagnostics.RedactUrl("https://alice:s3cret@example.com/login?token=abc#top"));
            Assert.AreEqual("http://192.168.1.1:8080/",
                FaviconDiagnostics.RedactUrl(" http://admin@192.168.1.1:8080/?x=1 "));
            Assert.AreEqual("example.com/path",
                FaviconDiagnostics.RedactUrl("bob:pw@example.com/path?q=1"));
            Assert.AreEqual(string.Empty, FaviconDiagnostics.RedactUrl(null));
        }

        [TestMethod]
        public void RedactUrl_KeepsOnlyTheSchemeOfCommandsAndLocalPaths()
        {
            // KeePass cmd:// URLs are command lines and can contain a password.
            Assert.AreEqual("cmd:", FaviconDiagnostics.RedactUrl("cmd://putty.exe -pw hunter2 host"));
            Assert.AreEqual("cmd:", FaviconDiagnostics.RedactUrl(@"CMD://""C:\Tools\app.exe"" --password x"));
            Assert.AreEqual("file:", FaviconDiagnostics.RedactUrl("file:///C:/Users/alice/notes.txt"));
            Assert.AreEqual("file:", FaviconDiagnostics.RedactUrl(@"C:\Users\alice\notes.txt"));
            Assert.AreEqual("file:", FaviconDiagnostics.RedactUrl(@"\\server\alice\vault"));

            Assert.AreEqual("androidapp://com.example.app/",
                FaviconDiagnostics.RedactUrl("androidapp://com.example.app"));
            Assert.AreEqual("localhost:8080/admin",
                FaviconDiagnostics.RedactUrl("localhost:8080/admin?session=1"));
        }

        [TestMethod]
        public void BuildCsvRow_NeutralizesSpreadsheetFormulas()
        {
            string row = FaviconDiagnostics.BuildCsvRow("=HYPERLINK(\"x\")", "+cmd", null);

            StringAssert.StartsWith(row, "\"'=HYPERLINK(\"\"x\"\")\",'+cmd,");
        }

        [TestMethod]
        public void DiagnosticsReport_KeepsRunOrderAndAppendsErrors()
        {
            var report = new KeeFetch.Batch.DiagnosticsReport(
                new[] { "[#1] first", null, "[#3] third" },
                new[] { "#1,row", null, "#3,row" },
                new[] { "[#2] boom" });

            Assert.IsFalse(report.IsEmpty);
            Assert.AreEqual(
                "[#1] first" + System.Environment.NewLine + "[#3] third" + System.Environment.NewLine +
                System.Environment.NewLine + "Processing errors:" + System.Environment.NewLine + "[#2] boom",
                report.BuildLogText());
            Assert.AreEqual(
                FaviconDiagnostics.BuildCsvHeader() + System.Environment.NewLine + "#1,row" +
                System.Environment.NewLine + "#3,row",
                report.BuildCsvText());
            StringAssert.StartsWith(FaviconDiagnostics.BuildCsvHeader(), "entry,url,");
            Assert.IsTrue(new KeeFetch.Batch.DiagnosticsReport(null, null, null).IsEmpty);
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
