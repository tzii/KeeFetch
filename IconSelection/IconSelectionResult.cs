using System.Collections.Generic;

namespace KeeFetch.IconSelection
{
    internal sealed class IconSelectionResult
    {
        public IconSelectionResult()
        {
            RejectedCandidates = new List<IconCandidate>();
            AttemptedProviders = new List<string>();
            FinalStatus = "NotFound";
            DiagnosticsSummary = string.Empty;
        }

        public IconCandidate SelectedCandidate { get; set; }
        public List<IconCandidate> RejectedCandidates { get; private set; }
        public List<string> AttemptedProviders { get; private set; }
        public string FinalStatus { get; set; }
        public bool WasSyntheticFallback { get; set; }
        public string DiagnosticsSummary { get; set; }
    }
}
