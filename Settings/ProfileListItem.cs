namespace KeeFetch.Settings
{
    internal sealed class ProfileListItem
    {
        public ProfileListItem(string id, string displayName)
        {
            Id = id;
            DisplayName = displayName;
        }

        public string Id { get; private set; }
        public string DisplayName { get; private set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
