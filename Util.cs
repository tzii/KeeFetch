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
    internal static class Util
    {
        public static byte[] HashData(byte[] data)
        {
            using (var sha = SHA256.Create())
            {
                byte[] full = sha.ComputeHash(data);
                byte[] truncated = new byte[16];
                Array.Copy(full, truncated, 16);
                return truncated;
            }
        }

        public static IWebProxy GetKeePassProxy()
        {
            try
            {
                return WebRequest.DefaultWebProxy;
            }
            catch
            {
                return null;
            }
        }

        public static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            url = url.Trim();

            if (url.StartsWith("androidapp://", StringComparison.OrdinalIgnoreCase))
                return null;

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            try
            {
                var uri = new Uri(url);
                return uri.GetLeftPart(UriPartial.Authority);
            }
            catch
            {
                return null;
            }
        }

        public static string ExtractHost(string url)
        {
            try
            {
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    url = "https://" + url;

                var uri = new Uri(url);
                return uri.Host;
            }
            catch
            {
                return null;
            }
        }

        public static string ExtractOrigin(string url)
        {
            try
            {
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    url = "https://" + url;

                var uri = new Uri(url);
                return uri.GetLeftPart(UriPartial.Authority);
            }
            catch
            {
                return null;
            }
        }

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
                catch
                {
                    using (var ms = new MemoryStream(data))
                        image = Image.FromStream(ms);
                }

                if (image == null)
                    return null;

                if (image.Width <= maxWidth && image.Height <= maxHeight)
                {
                    using (var ms = new MemoryStream())
                    {
                        image.Save(ms, ImageFormat.Png);
                        return ms.ToArray();
                    }
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
                catch
                {
                    scaled = new Bitmap(image, newW, newH);
                }

                using (var ms = new MemoryStream())
                {
                    scaled.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                if (scaled != null) scaled.Dispose();
                if (image != null) image.Dispose();
            }
        }

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
            catch
            {
                return false;
            }
        }

        public static string ResolveEntryUrl(PwEntry entry, PwDatabase db)
        {
            string url = entry.Strings.ReadSafe(PwDefs.UrlField);

            if (!string.IsNullOrEmpty(url) && url.Contains("{REF:"))
            {
                try
                {
                    url = SprEngine.Compile(url, new SprContext(entry, db, SprCompileFlags.References));
                }
                catch { }
            }

            return url;
        }
    }
}
