using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace KeeFetch
{
    /// <summary>
    /// Maps Android app URLs (androidapp://) to web domains and fetches icons from Google Play.
    /// </summary>
    internal static class AndroidAppMapper
    {
        private const string UserAgentString =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";

        // Limit HTML scan to 256 KB (sufficient for finding og:image or img tags)
        private const long MaxPlayStoreHtmlBytes = 256 * 1024;
        private const long MaxIconBytes = 512 * 1024;

        private static readonly HttpClient SharedHttpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5, // Reduced for safety
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            return new HttpClient(handler);
        }

        private static readonly Dictionary<string, string> KnownMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "com.google.android.gm", "gmail.com" },
            // ... (rest of mappings preserved below) ...
            { "com.google.android.youtube", "youtube.com" },
            { "com.google.android.apps.maps", "maps.google.com" },
            { "com.google.android.apps.photos", "photos.google.com" },
            { "com.google.android.apps.docs", "docs.google.com" },
            { "com.google.android.calendar", "calendar.google.com" },
            { "com.google.android.keep", "keep.google.com" },
            { "com.google.android.apps.translate", "translate.google.com" },
            { "com.google.android.dialer", "google.com" },
            { "com.google.android.contacts", "contacts.google.com" },
            { "com.google.android.googlequicksearchbox", "google.com" },
            { "com.google.android.apps.messaging", "messages.google.com" },

            { "com.facebook.katana", "facebook.com" },
            { "com.facebook.orca", "messenger.com" },
            { "com.facebook.lite", "facebook.com" },
            { "com.instagram.android", "instagram.com" },
            { "com.whatsapp", "whatsapp.com" },

            { "com.twitter.android", "x.com" },
            { "com.twitter.android.lite", "x.com" },

            { "com.snapchat.android", "snapchat.com" },
            { "com.linkedin.android", "linkedin.com" },
            { "com.pinterest", "pinterest.com" },
            { "com.reddit.frontpage", "reddit.com" },
            { "com.tumblr", "tumblr.com" },
            { "com.tiktok.android", "tiktok.com" },
            { "com.zhiliaoapp.musically", "tiktok.com" },

            { "com.microsoft.office.outlook", "outlook.com" },
            { "com.microsoft.teams", "teams.microsoft.com" },
            { "com.microsoft.office.word", "office.com" },
            { "com.microsoft.office.excel", "office.com" },
            { "com.microsoft.office.powerpoint", "office.com" },
            { "com.microsoft.skydrive", "onedrive.com" },
            { "com.microsoft.todos", "to-do.microsoft.com" },
            { "com.microsoft.bing", "bing.com" },

            { "com.amazon.mShop.android.shopping", "amazon.com" },
            { "com.amazon.kindle", "kindle.amazon.com" },
            { "com.amazon.avod.thirdpartyclient", "primevideo.com" },
            { "com.amazon.mp3", "music.amazon.com" },
            { "com.amazon.dee.app", "alexa.amazon.com" },
            { "in.amazon.mShop.android.shopping", "amazon.in" },

            { "com.spotify.music", "spotify.com" },
            { "com.netflix.mediaclient", "netflix.com" },
            { "com.disney.disneyplus", "disneyplus.com" },
            { "com.hbo.hbonow", "hbomax.com" },
            { "com.hulu.plus", "hulu.com" },
            { "com.apple.android.music", "music.apple.com" },
            { "com.pandora.android", "pandora.com" },
            { "com.soundcloud.android", "soundcloud.com" },
            { "com.deezer.android.app", "deezer.com" },
            { "com.crunchyroll.crunchyroid", "crunchyroll.com" },

            { "com.paypal.android.p2pmobile", "paypal.com" },
            { "com.venmo", "venmo.com" },
            { "com.squareup.cash", "cash.app" },
            { "com.stripe.android.dashboard", "stripe.com" },
            { "com.coinbase.android", "coinbase.com" },
            { "com.binance.dev", "binance.com" },
            { "piuk.blockchain.android", "blockchain.com" },
            { "com.robinhood.android", "robinhood.com" },

            { "com.dropbox.android", "dropbox.com" },
            { "com.evernote", "evernote.com" },
            { "com.todoist", "todoist.com" },
            { "com.slack", "slack.com" },
            { "us.zoom.videomeetings", "zoom.us" },
            { "com.tiktok.plus", "tiktok.com" },
            { "com.agilebits.onepassword", "1password.com" },
            { "com.notion.id", "notion.so" },
            { "com.bitwarden.android", "bitwarden.com" },
            { "me.lyft.android", "lyft.com" },
            { "com.fitbit.FitbitMobile", "fitbit.com" },
            { "com.calm.android", "calm.com" },
            { "com.atlassian.android.trello", "trello.com" },
            { "com.discord", "discord.com" },
            { "org.telegram.messenger", "telegram.org" },
            { "com.viber.voip", "viber.com" },
            { "org.signal.android", "signal.org" },
            { "com.skype.raider", "skype.com" },

            { "com.github.android", "github.com" },
            { "com.atlassian.android.jira.core", "jira.atlassian.com" },
            { "com.trello", "trello.com" },
            { "org.wordpress.android", "wordpress.com" },
            { "com.notionlabs.notion", "notion.so" },
            { "com.figma.mirror", "figma.com" },

            { "com.ebay.mobile", "ebay.com" },
            { "com.alibaba.aliexpresshd", "aliexpress.com" },
            { "com.shopify.mobile", "shopify.com" },
            { "com.contextlogic.wish", "wish.com" },
            { "com.etsy.android", "etsy.com" },

            { "com.booking", "booking.com" },
            { "com.airbnb.android", "airbnb.com" },
            { "com.expedia.bookings", "expedia.com" },
            { "com.tripadvisor.tripadvisor", "tripadvisor.com" },
            { "com.ubercab", "uber.com" },
            { "com.lyft.android", "lyft.com" },

            { "com.adobe.reader", "acrobat.adobe.com" },
            { "com.adobe.lrmobile", "lightroom.adobe.com" },
            { "com.adobe.psmobile", "photoshop.adobe.com" },

            { "com.duolingo", "duolingo.com" },
            { "com.grammarly.android.keyboard", "grammarly.com" },
            { "com.canva.editor", "canva.com" },

            { "com.shazam.android", "shazam.com" },
            { "com.waze", "waze.com" },
            { "com.yelp.android", "yelp.com" },
            { "com.zhihu.android", "zhihu.com" },
            { "com.nianticlabs.pokemongo", "pokemongolive.com" },

            { "com.valve.android.steamcommunity", "steampowered.com" },
            { "com.epicgames.fortnite", "epicgames.com" },
            { "com.riotgames.league.wildrift", "leagueoflegends.com" },
            { "com.supercell.clashofclans", "supercell.com" },

            { "com.strava", "strava.com" },
            { "com.nike.plusgps", "nike.com" },
            { "com.myfitnesspal.android", "myfitnesspal.com" },
            { "cc.fitbit.android", "fitbit.com" },

            { "com.bitdefender.security", "bitdefender.com" },
            { "com.kaspersky.kes", "kaspersky.com" },
            { "com.avast.android.mobilesecurity", "avast.com" },

            { "com.google.android.apps.chromecast.app", "google.com/chromecast" },
            { "com.google.android.apps.fitness", "fit.google.com" },
            { "com.google.android.apps.authenticator2", "google.com" },
            { "com.authy.authy", "authy.com" },
            { "com.lastpass.lpandroid", "lastpass.com" },
            { "com.onepassword.android", "1password.com" },
            { "com.x8bit.bitwarden", "bitwarden.com" },
        };

        /// <summary>
        /// Determines whether the URL is an Android app URL (androidapp://).
        /// </summary>
        /// <param name="url">The URL to check.</param>
        /// <returns>True if the URL is an Android app URL; otherwise, false.</returns>
        public static bool IsAndroidUrl(string url)
        {
            return !string.IsNullOrEmpty(url) &&
                   url.StartsWith("androidapp://", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Extracts the package name from an Android app URL.
        /// </summary>
        /// <param name="url">The Android app URL.</param>
        /// <returns>The package name, or null if extraction fails.</returns>
        public static string GetPackageName(string url)
        {
            if (!IsAndroidUrl(url))
                return null;

            try
            {
                string withoutScheme = url.Substring("androidapp://".Length).TrimStart('/');
                int slashIndex = withoutScheme.IndexOf('/');
                string package = slashIndex >= 0 ? withoutScheme.Substring(0, slashIndex) : withoutScheme;
                package = package.Trim();
                return string.IsNullOrEmpty(package) ? null : package;
            }
            catch (Exception ex)
            {
                Logger.Debug("GetPackageName", ex);
                return null;
            }
        }

        /// <summary>
        /// Maps an Android app URL to its corresponding web domain.
        /// Uses known mappings or attempts to guess from the package name.
        /// </summary>
        /// <param name="url">The Android app URL.</param>
        /// <returns>The web domain, or null if mapping fails.</returns>
        public static string MapToWebDomain(string url)
        {
            string package = GetPackageName(url);
            if (package == null)
                return null;

            if (KnownMappings.TryGetValue(package, out string domain))
                return domain;

            return TryGuessFromPackage(package);
        }

        /// <summary>
        /// Fetches the icon for an Android app from the Google Play Store.
        /// </summary>
        /// <param name="packageName">The Android package name.</param>
        /// <param name="timeoutMs">Timeout in milliseconds.</param>
        /// <param name="proxy">Web proxy to use, or null for default.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Icon image data, or null if fetching fails.</returns>
        public static async Task<byte[]> FetchGooglePlayIconAsync(string packageName, int timeoutMs, IWebProxy proxy,
            CancellationToken token = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(packageName))
                return null;

            try
            {
                token.ThrowIfCancellationRequested();

                string playUrl = "https://play.google.com/store/apps/details?id=" +
                                 Uri.EscapeDataString(packageName);

                string html = null;

                using (var request = new HttpRequestMessage(HttpMethod.Get, playUrl))
                {
                    request.Headers.Add("User-Agent", UserAgentString);
                    request.Headers.Add("Accept-Language", "en-US,en;q=0.9");

                    using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        cts.CancelAfter(timeoutMs);

                        var response = await SharedHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
                        if (!response.IsSuccessStatusCode) return null;

                        using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var ms = new MemoryStream())
                        {
                            if (stream == null) return null;
                            byte[] buffer = new byte[8192];
                            int read;
                            long total = 0;
                            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false)) > 0)
                            {
                                await ms.WriteAsync(buffer, 0, read, cts.Token).ConfigureAwait(false);
                                total += read;
                                if (total > MaxPlayStoreHtmlBytes)
                                    break; // Stop reading after limit - we likely have the head
                            }
                            html = System.Text.Encoding.UTF8.GetString(ms.ToArray());
                        }
                    }
                }

                if (string.IsNullOrEmpty(html)) return null;

                var imgPattern = new Regex(
                    @"<img[^>]*\bsrc\s*=\s*[""']([^""']*googleusercontent\.com[^""']*=s\d+[^""']*)[""']",
                    RegexOptions.IgnoreCase);

                var match = imgPattern.Match(html);
                if (!match.Success)
                {
                    imgPattern = new Regex(
                        @"<img[^>]*\bsrc\s*=\s*[""']([^""']*play-lh\.googleusercontent\.com[^""']+)[""']",
                        RegexOptions.IgnoreCase);
                    match = imgPattern.Match(html);
                }

                if (!match.Success)
                    return null;

                string iconUrl = match.Groups[1].Value;
                iconUrl = Regex.Replace(iconUrl, @"=s\d+", "=s128");
                iconUrl = iconUrl.Replace("&amp;", "&");

                token.ThrowIfCancellationRequested();

                using (var iconRequest = new HttpRequestMessage(HttpMethod.Get, iconUrl))
                {
                    iconRequest.Headers.Add("User-Agent", UserAgentString);

                    using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        cts.CancelAfter(timeoutMs);
                        var response = await SharedHttpClient.SendAsync(iconRequest, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
                        if (!response.IsSuccessStatusCode) return null;

                        using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var ms = new MemoryStream())
                        {
                            if (stream == null) return null;
                            byte[] buffer = new byte[8192];
                            int read;
                            long total = 0;
                            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false)) > 0)
                            {
                                await ms.WriteAsync(buffer, 0, read, cts.Token).ConfigureAwait(false);
                                total += read;
                                if (total > MaxIconBytes) return null;
                            }
                            byte[] data = ms.ToArray();
                            return Util.IsValidImage(data) ? data : null;
                        }
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Logger.Warn("FetchGooglePlayIconAsync", ex);
                return null;
            }
        }

        /// <summary>
        /// Attempts to guess a web domain from an Android package name.
        /// Reverses com.example.app to example.com.
        /// </summary>
        /// <param name="package">The package name.</param>
        /// <returns>The guessed domain, or null if guessing fails.</returns>
        public static string TryGuessFromPackage(string package)
        {
            if (string.IsNullOrEmpty(package)) return null;

            string[] parts = package.Split('.');
            if (parts.Length < 2)
                return null;

            if (parts[0].Equals("com", StringComparison.OrdinalIgnoreCase) ||
                parts[0].Equals("org", StringComparison.OrdinalIgnoreCase) ||
                parts[0].Equals("net", StringComparison.OrdinalIgnoreCase) ||
                parts[0].Equals("io", StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length >= 2)
                    return parts[1] + "." + parts[0];
            }

            return null;
        }
    }
}
