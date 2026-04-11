namespace KeeFetch.IconSelection
{
    internal sealed class IconCandidate
    {
        public string ProviderName { get; set; }
        public string TargetHost { get; set; }
        public string SourceUrl { get; set; }
        public IconTier Tier { get; set; }

        public byte[] RawData { get; set; }
        public byte[] NormalizedPngData { get; set; }
        public string OriginalFormat { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsSvg { get; set; }
        public bool IsSynthetic { get; set; }
        public bool IsPlaceholderSuspected { get; set; }
        public bool IsBlankSuspected { get; set; }
        public double ConfidenceScore { get; set; }
        public string Notes { get; set; }
    }
}
