namespace KeeFetch.IconProviders
{
    internal interface IIconProvider
    {
        string Name { get; }
        byte[] GetIcon(string host, int size, int timeoutMs, System.Net.IWebProxy proxy);
    }
}
