namespace KeeFetch.IconSelection
{
    internal sealed class IconRequest
    {
        public string OriginalUrl { get; set; }
        public string TargetHost { get; set; }
        public string TargetOrigin { get; set; }
        public string CacheKey { get; set; }
        public string TargetPackageName { get; set; }
        public int MaxIconSize { get; set; }
        public int TimeoutMs { get; set; }
        public bool AllowPrivateResponse { get; set; }
    }
}
