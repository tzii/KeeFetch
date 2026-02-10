using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
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
            using (var md5 = MD5.Create())
            {
                return md5.ComputeHash(data);
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

        public static string GuessDomainFromTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return title;
            string t = title.Trim();

            if (t.Contains("://") || t.Contains("/") || t.Contains("."))
                return t;

            if (Regex.IsMatch(t, @"^[a-zA-Z0-9-]{2,63}$"))
                return t + ".com";

            return t;
        }

        public static byte[] ResizeImage(byte[] data, int maxWidth, int maxHeight)
        {
            if (data == null || data.Length == 0)
                return null;

            try
            {
                Image image;
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
                        image.Dispose();
                        return ms.ToArray();
                    }
                }

                double ratio = Math.Min(
                    (double)maxWidth / image.Width,
                    (double)maxHeight / image.Height);

                int newW = (int)Math.Round(image.Width * ratio);
                int newH = (int)Math.Round(image.Height * ratio);

                if (newW < 1) newW = 1;
                if (newH < 1) newH = 1;

                Image scaled;
                try
                {
                    scaled = GfxUtil.ScaleImage(image, newW, newH);
                }
                catch
                {
                    scaled = new Bitmap(image, newW, newH);
                }

                image.Dispose();

                using (var ms = new MemoryStream())
                {
                    scaled.Save(ms, ImageFormat.Png);
                    scaled.Dispose();
                    return ms.ToArray();
                }
            }
            catch
            {
                return null;
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
                    url = SprEngine.Compile(url, new SprContext(entry, db, SprCompileFlags.All));
                }
                catch { }
            }

            return url;
        }
    }
}
