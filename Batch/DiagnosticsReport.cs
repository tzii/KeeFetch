using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace KeeFetch.Batch
{
    /// <summary>
    /// Diagnostics for one run, kept in memory until the user exports them.
    /// Entries are identified by their position in the run and web addresses
    /// are redacted, so the report can be attached to a bug report without
    /// exposing titles, credentials or query strings.
    /// </summary>
    internal sealed class DiagnosticsReport
    {
        private readonly List<string> logLines;
        private readonly List<string> csvRows;
        private readonly List<string> errorLines;

        public DiagnosticsReport(IEnumerable<string> logLines, IEnumerable<string> csvRows,
            IEnumerable<string> errorLines)
        {
            this.logLines = Snapshot(logLines);
            this.csvRows = Snapshot(csvRows);
            this.errorLines = Snapshot(errorLines);
        }

        public bool IsEmpty
        {
            get { return logLines.Count == 0 && csvRows.Count == 0 && errorLines.Count == 0; }
        }

        public string BuildLogText()
        {
            var lines = new List<string>(logLines);
            if (errorLines.Count > 0)
            {
                if (lines.Count > 0)
                    lines.Add(string.Empty);
                lines.Add("Processing errors:");
                lines.AddRange(errorLines);
            }

            return string.Join(Environment.NewLine, lines.ToArray());
        }

        public string BuildCsvText()
        {
            var lines = new List<string>();
            lines.Add(FaviconDiagnostics.BuildCsvHeader());
            lines.AddRange(csvRows);
            return string.Join(Environment.NewLine, lines.ToArray());
        }

        /// <summary>Writes the report as CSV when the path ends in .csv, otherwise as a text log.</summary>
        public void WriteTo(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A file path is required.", "path");

            bool csv = string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase);
            // Spreadsheet programs need the byte order mark to read UTF-8 CSV.
            File.WriteAllText(path, csv ? BuildCsvText() : BuildLogText(), new UTF8Encoding(csv));
        }

        private static List<string> Snapshot(IEnumerable<string> values)
        {
            var list = new List<string>();
            if (values == null)
                return list;

            foreach (string value in values)
            {
                if (value != null)
                    list.Add(value);
            }

            return list;
        }
    }
}
