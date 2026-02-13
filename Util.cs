using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using KeePass.Util.Spr;
using KeePassLib;
using KeePassLib.Utility;

namespace KeeFetch
{
    /// <summary>
    /// Utility methods for URL parsing, image processing, and data hashing.
    /// </summary>
    internal static class Util
    {
        /// <summary>
        /// Computes a truncated SHA256 hash of the provided data.
        /// Returns the first 16 bytes of the hash.
        /// </summary>
        /// <param name="data">The data to hash.</param>
        /// <returns>A 16-byte hash of the data.</returns>
        public static byte[] HashData(byte[] data)
        {
            if (data == null) return null;
            using (var sha = SHA256.Create())
            {
                byte[] full = sha.ComputeHash(data);
                byte[] truncated = new byte[16];
                Array.Copy(full, truncated, 16);
                return truncated;
            }
        }

        /// <summary>
        /// Extracts the hostname from a URL.
        /// Automatically prepends https:// if no scheme is present.
        /// </summary>
        /// <param name="url">The URL to parse.</param>
        /// <returns>The hostname, or null if parsing fails.</returns>
        public static string ExtractHost(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            try
            {
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    url = "https://" + url;

                var uri = new Uri(url);
                return uri.Host;
            }
            catch (Exception ex)
            {
                Logger.Debug("ExtractHost", ex);
                return null;
            }
        }

        /// <summary>
        /// Extracts the hostname and port from a URL.
        /// Returns just the hostname if using the default port for the scheme.
        /// </summary>
        /// <param name="url">The URL to parse.</param>
        /// <returns>The hostname with port (if non-default), or null if parsing fails.</returns>
        public static string ExtractHostWithPort(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            try
            {
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    url = "https://" + url;

                var uri = new Uri(url);
                if (uri.IsDefaultPort)
                    return uri.Host;
                return uri.Host + ":" + uri.Port;
            }
            catch (Exception ex)
            {
                Logger.Debug("ExtractHostWithPort", ex);
                return null;
            }
        }

        /// <summary>
        /// Extracts the scheme (http/https) from a URL.
        /// </summary>
        /// <param name="url">The URL to parse.</param>
        /// <returns>The scheme, or null if parsing fails or no scheme is present.</returns>
        public static string ExtractScheme(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            try
            {
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    return null;

                var uri = new Uri(url);
                return uri.Scheme;
            }
            catch (Exception ex)
            {
                Logger.Debug("ExtractScheme", ex);
                return null;
            }
        }

        /// <summary>
        /// Determines whether a host is a private/internal address.
        /// Checks for localhost, private IP ranges (RFC 1918), and internal TLDs.
        /// </summary>
        /// <param name="host">The hostname or IP address to check.</param>
        /// <returns>True if the host is private; otherwise, false.</returns>
        public static bool IsPrivateHost(string host)
        {
            if (string.IsNullOrEmpty(host))
                return true;

            string lower = host.ToLowerInvariant();
            if (lower == "localhost" ||
                lower.EndsWith(".local") ||
                lower.EndsWith(".lan") ||
                lower.EndsWith(".internal") ||
                lower.EndsWith(".corp") ||
                lower.EndsWith(".home") ||
                lower.EndsWith(".intranet") ||
                !lower.Contains("."))
                return true;

            IPAddress ip;
            if (IPAddress.TryParse(host, out ip))
            {
                if (IPAddress.IsLoopback(ip))
                    return true;

                byte[] bytes = ip.GetAddressBytes();
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    if (bytes[0] == 10)
                        return true;
                    if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                        return true;
                    if (bytes[0] == 192 && bytes[1] == 168)
                        return true;
                    if (bytes[0] == 169 && bytes[1] == 254)
                        return true;
                    if (bytes[0] == 127)
                        return true;
                }
                if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal)
                        return true;
                    if (bytes[0] == 0xfc || bytes[0] == 0xfd)
                        return true;
                }

                return false;
            }

            return false;
        }

        /// <summary>
        /// Attempts to guess a domain from an entry title.
        /// Returns the title itself if it looks like a URL, or appends .com to simple names.
        /// </summary>
        /// <param name="title">The entry title to analyze.</param>
        /// <returns>A guessed domain, or null if no guess can be made.</returns>
        public static string GuessDomainFromTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;
            string t = title.Trim();

            if (t.Contains("://") || t.Contains("/") || t.Contains("."))
                return t;

            if (Regex.IsMatch(t, @"^[a-zA-Z0-9-]{2,63}$"))
            {
                string lower = t.ToLowerInvariant();
                if (lower.EndsWith("-internal") || lower.EndsWith("-corp") ||
                    lower.EndsWith("-dev") || lower.EndsWith("-staging") ||
                    lower.EndsWith("-prod") || lower.EndsWith("-test") ||
                    lower.StartsWith("intranet") || lower.StartsWith("internal"))
                    return null;
                return t + ".com";
            }

            return null;
        }

        /// <summary>
        /// Resizes an image to fit within the specified maximum dimensions.
        /// Maintains aspect ratio and outputs PNG format.
        /// </summary>
        /// <param name="data">The image data to resize.</param>
        /// <param name="maxWidth">Maximum width in pixels.</param>
        /// <param name="maxHeight">Maximum height in pixels.</param>
        /// <returns>Resized image data as PNG, or null if resizing fails.</returns>
        public static byte[] ResizeImage(byte[] data, int maxWidth, int maxHeight)
        {
            if (data == null || data.Length == 0)
                return null;

            Image image = null;
            Image scaled = null;
            try
            {
                try
                {
                    image = GfxUtil.LoadImage(data);
                }
                catch (Exception ex)
                {
                    Logger.Debug("ResizeImage", ex);
                    using (var ms = new MemoryStream(data))
                        image = Image.FromStream(ms);
                }

                if (image == null)
                    return null;

                // If image already fits within bounds, return original data to avoid re-encoding
                if (image.Width <= maxWidth && image.Height <= maxHeight)
                {
                    return data;
                }

                double ratio = Math.Min(
                    (double)maxWidth / image.Width,
                    (double)maxHeight / image.Height);

                int newW = Math.Max(1, (int)Math.Round(image.Width * ratio));
                int newH = Math.Max(1, (int)Math.Round(image.Height * ratio));

                try
                {
                    scaled = GfxUtil.ScaleImage(image, newW, newH);
                }
                catch (Exception ex)
                {
                    Logger.Debug("ResizeImage", ex);
                    scaled = new Bitmap(image, newW, newH);
                }

                using (var ms = new MemoryStream())
                {
                    scaled.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                Logger.Debug("ResizeImage", ex);
                return null;
            }
            finally
            {
                if (scaled != null) scaled.Dispose();
                if (image != null) image.Dispose();
            }
        }

        /// <summary>
        /// Validates whether the provided data is a valid image.
        /// </summary>
        /// <param name="data">The data to validate.</param>
        /// <returns>True if the data is a valid image; otherwise, false.</returns>
        public static bool IsValidImage(byte[] data)
        {
            if (data == null || data.Length < 8)
                return false;

            try
            {
                using (var ms = new MemoryStream(data))
                using (var img = Image.FromStream(ms))
                {
                    return img.Width > 0 && img.Height > 0;
                }
            }
            catch (Exception ex)
            {
                Logger.Debug("IsValidImage", ex);
                return false;
            }
        }

        /// <summary>
        /// Resolves the URL for a password entry, expanding any {REF:} placeholders.
        /// </summary>
        /// <param name="entry">The password entry.</param>
        /// <param name="db">The database containing the entry.</param>
        /// <returns>The resolved URL.</returns>
        public static string ResolveEntryUrl(PwEntry entry, PwDatabase db)
        {
            string url = entry.Strings.ReadSafe(PwDefs.UrlField);

            if (!string.IsNullOrEmpty(url) && url.Contains("{REF:"))
            {
                try
                {
                    url = SprEngine.Compile(url, new SprContext(entry, db, SprCompileFlags.References));
                }
                catch (Exception ex) { Logger.Debug("ResolveEntryUrl", ex); }
            }

            return url;
        }
    }
}
