using System;

namespace KeeFetch.FetchProfiles
{
    internal sealed class ProviderDefinition
    {
        public ProviderDefinition(string id, string displayName, bool isThirdParty,
            bool isSyntheticCapable, bool isPlaceholderProne)
        {
            Id = id;
            DisplayName = displayName;
            IsThirdParty = isThirdParty;
            IsSyntheticCapable = isSyntheticCapable;
            IsPlaceholderProne = isPlaceholderProne;
        }

        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public bool IsThirdParty { get; private set; }
        public bool IsSyntheticCapable { get; private set; }
        public bool IsPlaceholderProne { get; private set; }
    }
}
