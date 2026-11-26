using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KeeFetch.IconSelection;

namespace KeeFetch
{
    internal static class FaviconDiagnostics
    {
        public static string BuildCsvHeader()
        {
            return "entry,url,status,miss_reason,provider,tier,synthetic,elapsed_ms,attempted_providers,diagnostics_summary,rejected_candidates,provider_metrics";
        }

        public static string BuildCsvRow(string entryLabel, string resolvedUrl, FaviconResult result)
        {
            string status = result != null ? result.Status.ToString() : FaviconStatus.NotFound.ToString();
            string provider = result != null && !string.IsNullOrEmpty(result.Provider)
                ? result.Provider
                : string.Empty;
            string tier = result != null ? result.SelectedTier.ToString() : IconTier.Rejected.ToString();
            string synthetic = result != null && result.WasSyntheticFallback ? "true" : "false";
            string elapsed = result != null ? Math.Max(0L, result.ElapsedMilliseconds).ToString() : "0";
            string attempted = FormatAttemptedProviders(result);
            string summary = result != null ? result.DiagnosticsSummary : "no-selection";
            string rejected = FormatRejectedCandidates(result);
            string providerMetrics = FormatProviderMetrics(result);
            string missReason = InferMissReason(result);

            return string.Join(",", new[]
            {
                Csv(entryLabel),
                Csv(resolvedUrl),
                Csv(status),
                Csv(missReason),
                Csv(provider),
                Csv(tier),
                Csv(synthetic),
                Csv(elapsed),
                Csv(attempted),
                Csv(summary),
                Csv(rejected),
                Csv(providerMetrics)
            });
        }

        public static string BuildLogLine(string entryLabel, string resolvedUrl, FaviconResult result)
        {
            string provider = result != null && !string.IsNullOrEmpty(result.Provider)
                ? result.Provider
                : "none";
            string tier = result != null ? result.SelectedTier.ToString() : IconTier.Rejected.ToString();
            string synthetic = result != null && result.WasSyntheticFallback ? "true" : "false";
            string attempted = FormatAttemptedProviders(result);
            string summary = result != null ? result.DiagnosticsSummary : "no-selection";
            string elapsed = result != null ? Math.Max(0L, result.ElapsedMilliseconds) + "ms" : "0ms";
            string rejected = FormatRejectedCandidates(result);
            string providerMetrics = FormatProviderMetrics(result);
            string missReason = InferMissReason(result);

            string line = string.Format(
                "[{0}] url={1}; provider={2}; tier={3}; synthetic={4}; elapsed={5}; missReason={6}; attempted=[{7}]; summary={8}",
                entryLabel ?? string.Empty,
                resolvedUrl ?? string.Empty,
                provider,
                tier,
                synthetic,
                elapsed,
                missReason,
                attempted,
                summary ?? string.Empty);

            if (!string.IsNullOrWhiteSpace(rejected))
                line += "; rejected=" + rejected;
            if (!string.IsNullOrWhiteSpace(providerMetrics))
                line += "; providers=" + providerMetrics;

            return line;
        }

        private static string InferMissReason(FaviconResult result)
        {
            if (result == null)
                return "no-selection";

            if (result.Status == FaviconStatus.Success)
                return string.Empty;

            bool attemptedProvider = result.AttemptedProviders != null &&
                                     result.AttemptedProviders.Any(p => !string.IsNullOrWhiteSpace(p));
            if (!attemptedProvider)
                return "invalid-url-or-no-provider";

            if (result.RejectedCandidates != null)
            {
                if (result.RejectedCandidates.Any(c => Contains(c != null ? c.Notes : null, "svg")))
                    return "svg-only";
                if (result.RejectedCandidates.Any(c =>
                    Contains(c != null ? c.Notes : null, "blank") ||
                    (c != null && (c.IsBlankSuspected || c.IsPlaceholderSuspected))))
                    return "blank-or-placeholder";
                if (result.RejectedCandidates.Count > 0)
                    return "rejected-candidate";
            }

            if (result.ProviderMetrics != null)
            {
                if (result.ProviderMetrics.Any(m => m != null && Contains(m.Outcome, "cancel")))
                    return "cancelled";
                if (result.ProviderMetrics.Any(m => m != null && Contains(m.Outcome, "error")))
                    return "provider-error";
                if (result.ProviderMetrics.Any(m => m != null && Contains(m.Outcome, "timeout")))
                    return "timeout";
            }

            return "no-candidate";
        }

        private static string FormatAttemptedProviders(FaviconResult result)
        {
            if (result == null || result.AttemptedProviders == null)
                return string.Empty;

            return string.Join(", ", result.AttemptedProviders
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToArray());
        }

        private static string FormatRejectedCandidates(FaviconResult result)
        {
            if (result == null || result.RejectedCandidates == null)
                return string.Empty;

            return string.Join(" || ", result.RejectedCandidates
                .Where(c => c != null)
                .Select(c => string.Format("{0}:{1}", c.ProviderName, c.Notes))
                .ToArray());
        }

        private static string FormatProviderMetrics(FaviconResult result)
        {
            if (result == null || result.ProviderMetrics == null)
                return string.Empty;

            return string.Join(", ", result.ProviderMetrics
                .Where(m => m != null)
                .Select(m => string.Format("{0}:{1}ms/{2}/{3}",
                    m.ProviderName, m.ElapsedMilliseconds, m.CandidateCount, m.Outcome))
                .ToArray());
        }

        /// <summary>
        /// Reduces an entry address to scheme, host, port and path so a
        /// diagnostics report can be shared: credentials in the user-info
        /// part, query strings and fragments are removed.
        /// </summary>
        public static string RedactUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;

            string trimmed = url.Trim();

            // Only web and Android app addresses help in a report. Other
            // schemes carry local paths (file:) or command lines (a KeePass
            // cmd:// URL can hold a password), so only the scheme is kept.
            string scheme = LeadingScheme(trimmed);
            if (scheme != null && !IsReportableScheme(scheme))
                return scheme + ":";

            Uri uri;
            bool isAbsolute = Uri.TryCreate(trimmed, UriKind.Absolute, out uri);
            if (isAbsolute && uri.IsFile)
                return "file:";
            if (isAbsolute && !string.IsNullOrEmpty(uri.Host))
                return uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped);

            int cut = trimmed.IndexOfAny(new[] { '?', '#' });
            if (cut >= 0)
                trimmed = trimmed.Substring(0, cut);

            // Remove "user:password@" from the authority part.
            int authorityStart = trimmed.IndexOf("//", StringComparison.Ordinal);
            authorityStart = authorityStart >= 0 ? authorityStart + 2 : 0;
            int authorityEnd = trimmed.IndexOf('/', authorityStart);
            if (authorityEnd < 0)
                authorityEnd = trimmed.Length;
            if (authorityEnd > authorityStart)
            {
                int at = trimmed.LastIndexOf('@', authorityEnd - 1, authorityEnd - authorityStart);
                if (at >= 0)
                    trimmed = trimmed.Substring(0, authorityStart) + trimmed.Substring(at + 1);
            }

            return trimmed;
        }

        /// <summary>The lower-case scheme of a "scheme://..." value, or null.</summary>
        private static string LeadingScheme(string value)
        {
            int separator = value.IndexOf("://", StringComparison.Ordinal);
            if (separator <= 0)
                return null;

            for (int i = 0; i < separator; i++)
            {
                char c = value[i];
                bool letter = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
                bool allowed = letter || (i > 0 && ((c >= '0' && c <= '9') || c == '+' || c == '-' || c == '.'));
                if (!allowed)
                    return null;
            }

            return value.Substring(0, separator).ToLowerInvariant();
        }

        private static bool IsReportableScheme(string scheme)
        {
            return scheme == "http" || scheme == "https" || scheme == "androidapp";
        }

        private static string Csv(string value)
        {
            value = value ?? string.Empty;

            // Spreadsheet programs evaluate cells that start with these
            // characters as formulas; a leading apostrophe keeps them text.
            if (value.Length > 0 && "=+-@\t\r".IndexOf(value[0]) >= 0)
                value = "'" + value;

            bool needsQuotes = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
            if (!needsQuotes)
                return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static bool Contains(string value, string fragment)
        {
            return value != null &&
                   value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
