using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KeeFetch.IconSelection
{
    internal sealed class IconSelector
    {
        public IconSelectionResult Select(IReadOnlyList<IconCandidate> candidates,
            IReadOnlyList<string> attemptedProviders, bool allowSyntheticFallbacks)
        {
            var result = new IconSelectionResult();
            if (attemptedProviders != null)
            {
                foreach (var provider in attemptedProviders)
                {
                    if (!string.IsNullOrWhiteSpace(provider))
                        result.AttemptedProviders.Add(provider);
                }
            }

            if (candidates == null || candidates.Count == 0)
            {
                result.FinalStatus = "NotFound";
                result.DiagnosticsSummary = BuildDiagnosticsSummary(result, 0, 0);
                return result;
            }

            var survivors = new List<IconCandidate>();
            foreach (var candidate in candidates)
            {
                if (candidate == null)
                    continue;

                if (string.IsNullOrEmpty(candidate.ProviderName))
                    candidate.ProviderName = "Unknown";

                string rejection = ValidateInitialCandidate(candidate, allowSyntheticFallbacks);
                if (rejection != null)
                {
                    Reject(result, candidate, rejection);
                    continue;
                }

                if (candidate.IsBlankSuspected)
                    candidate.ConfidenceScore -= 0.35;

                candidate.ConfidenceScore = Math.Max(0.0, Math.Min(1.0, candidate.ConfidenceScore));
                survivors.Add(candidate);
            }

            bool hasStrong = survivors.Any(c => c.Tier == IconTier.SiteCanonical || c.Tier == IconTier.StrongResolved);
            bool hasStrongNonBlank = survivors.Any(c =>
                (c.Tier == IconTier.SiteCanonical || c.Tier == IconTier.StrongResolved) && !c.IsBlankSuspected);

            for (int i = survivors.Count - 1; i >= 0; i--)
            {
                var candidate = survivors[i];

                if (hasStrong && (candidate.Tier == IconTier.SyntheticFallback || candidate.IsSynthetic || candidate.IsPlaceholderSuspected))
                {
                    survivors.RemoveAt(i);
                    Reject(result, candidate, "Synthetic/placeholder-prone candidate rejected because stronger Tier 1/2 candidates exist.");
                    continue;
                }

                if (hasStrongNonBlank && candidate.IsBlankSuspected)
                {
                    survivors.RemoveAt(i);
                    Reject(result, candidate, "Blank-suspected candidate rejected because a stronger non-blank candidate exists.");
                    continue;
                }
            }

            var selected = survivors
                .OrderBy(c => (int)c.Tier)
                .ThenByDescending(c => c.ConfidenceScore)
                .ThenByDescending(c => (long)c.Width * c.Height)
                .FirstOrDefault();

            if (selected == null)
            {
                result.FinalStatus = "NotFound";
                result.DiagnosticsSummary = BuildDiagnosticsSummary(result, candidates.Count, survivors.Count);
                return result;
            }

            result.SelectedCandidate = selected;
            result.WasSyntheticFallback = selected.Tier == IconTier.SyntheticFallback || selected.IsSynthetic;
            result.FinalStatus = "Selected";
            result.DiagnosticsSummary = BuildDiagnosticsSummary(result, candidates.Count, survivors.Count);
            return result;
        }

        private static string ValidateInitialCandidate(IconCandidate candidate, bool allowSyntheticFallbacks)
        {
            if (!allowSyntheticFallbacks && (candidate.Tier == IconTier.SyntheticFallback || candidate.IsSynthetic))
                return "Synthetic fallback disabled by configuration.";

            if (candidate.IsSvg && candidate.NormalizedPngData == null)
                return "SVG candidate detected but local rasterization is not enabled.";

            if (candidate.RawData == null || candidate.RawData.Length == 0)
            {
                return "Candidate has no image payload.";
            }

            if (candidate.NormalizedPngData == null && !Util.IsValidImage(candidate.RawData))
                return "Candidate payload is not a valid image.";

            return null;
        }

        private static void Reject(IconSelectionResult result, IconCandidate candidate, string reason)
        {
            candidate.Tier = IconTier.Rejected;
            candidate.Notes = AppendNote(candidate.Notes, reason);
            result.RejectedCandidates.Add(candidate);
        }

        private static string BuildDiagnosticsSummary(IconSelectionResult result, int totalCandidates, int survivingCandidates)
        {
            var sb = new StringBuilder();
            sb.Append("attempted=[");
            sb.Append(string.Join(", ", result.AttemptedProviders));
            sb.Append("]");
            sb.Append("; candidates=");
            sb.Append(totalCandidates);
            sb.Append("; surviving=");
            sb.Append(survivingCandidates);
            sb.Append("; rejected=");
            sb.Append(result.RejectedCandidates.Count);

            if (result.SelectedCandidate != null)
            {
                sb.Append("; selected=");
                sb.Append(result.SelectedCandidate.ProviderName);
                sb.Append(" (");
                sb.Append(result.SelectedCandidate.Tier);
                sb.Append(", score=");
                sb.Append(result.SelectedCandidate.ConfidenceScore.ToString("0.00"));
                sb.Append(")");
                if (result.WasSyntheticFallback)
                    sb.Append("; synthetic=true");
            }
            else
            {
                sb.Append("; selected=none");
            }

            return sb.ToString();
        }

        private static string AppendNote(string existing, string note)
        {
            if (string.IsNullOrWhiteSpace(note))
                return existing;
            if (string.IsNullOrWhiteSpace(existing))
                return note;
            return existing + " | " + note;
        }
    }
}
