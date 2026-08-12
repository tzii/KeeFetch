using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    [TestClass]
    public class RegressionPackageTests
    {
        [TestMethod]
        public void RegressionManifest_HasStableIdsAndExpectedGroups()
        {
            string path = FixturePath("Regression", "KeeFetch-Test-Manifest.csv");
            string[] lines = File.ReadAllLines(path);
            Assert.AreEqual(72, lines.Length, "Header plus 71 fixtures expected.");

            var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 1; index < lines.Length; index++)
            {
                string[] fields = ParseCsvLine(lines[index]);
                Assert.IsTrue(fields.Length >= 6, "fixture_id plus five legacy columns expected.");
                Assert.IsTrue(ids.Add(fields[0]), "Duplicate fixture_id: " + fields[0]);
                groups.Add(fields[1]);
            }

            Assert.IsTrue(groups.Contains("01 Happy Paths"));
            Assert.IsTrue(groups.Contains("03 Android App URLs"));
            Assert.IsTrue(groups.Contains("06 Issue 1 Regression Corpus"));
            Assert.IsTrue(groups.Contains("08 Bulk / Concurrency"));
        }

        [TestMethod]
        public void RegressionFixtures_AllFourFilesPresent()
        {
            string[] files = new[]
            {
                FixturePath("Regression", "KeeFetch-Test-Database.kdbx"),
                FixturePath("Regression", "KeeFetch-Test-Database.xml"),
                FixturePath("Regression", "KeeFetch-Test-Manifest.csv"),
                FixturePath("Regression", "KeeFetch-Test-README.txt")
            };
            foreach (string file in files)
            {
                Assert.IsTrue(File.Exists(file), "Missing fixture file: " + file);
                Assert.IsTrue(new FileInfo(file).Length > 0, "Fixture file is empty: " + file);
            }
        }

        private static string FixturePath(params string[] parts)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures");
            foreach (string part in parts) path = Path.Combine(path, part);
            return path;
        }

        private static string[] ParseCsvLine(string line)
        {
            using (var reader = new StringReader(line))
            using (var parser = new Microsoft.VisualBasic.FileIO.TextFieldParser(reader))
            {
                parser.SetDelimiters(",");
                parser.HasFieldsEnclosedInQuotes = true;
                return parser.ReadFields();
            }
        }
    }
}
