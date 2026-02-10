using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace KeeFetch
{
    internal static class AndroidAppMapper
    {
        private static readonly Dictionary<string, string> KnownMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "com.google.android.gm", "gmail.com" },
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

        public static bool IsAndroidUrl(string url)
        {
            return !string.IsNullOrEmpty(url) &&
                   url.StartsWith("androidapp://", StringComparison.OrdinalIgnoreCase);
        }

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
            catch
            {
                return null;
            }
        }

        public static string MapToWebDomain(string url)
        {
            string package = GetPackageName(url);
            if (package == null)
                return null;

            if (KnownMappings.TryGetValue(package, out string domain))
                return domain;

            return TryGuessFromPackage(package);
        }

        public static byte[] FetchGooglePlayIcon(string packageName, int timeoutMs, IWebProxy proxy)
        {
            if (string.IsNullOrEmpty(packageName))
                return null;

            try
            {
                string playUrl = "https://play.google.com/store/apps/details?id=" +
                                 Uri.EscapeDataString(packageName);

                var request = (HttpWebRequest)WebRequest.Create(playUrl);
                request.Timeout = timeoutMs;
                request.ReadWriteTimeout = timeoutMs * 2;
                request.AllowAutoRedirect = true;
                request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";
                request.Headers.Add(HttpRequestHeader.AcceptLanguage, "en-US,en;q=0.9");
                if (proxy != null) request.Proxy = proxy;

                string html;
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var ms = new MemoryStream())
                {
                    if (stream == null) return null;
                    byte[] buffer = new byte[8192];
                    int read;
                    long total = 0;
                    const long MaxHtmlBytes = 10 * 1024 * 1024;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ms.Write(buffer, 0, read);
                        total += read;
                        if (total > MaxHtmlBytes)
                            return null;
                    }
                    html = System.Text.Encoding.UTF8.GetString(ms.ToArray());
                }

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

                var iconRequest = (HttpWebRequest)WebRequest.Create(iconUrl);
                iconRequest.Timeout = timeoutMs;
                iconRequest.ReadWriteTimeout = timeoutMs * 2;
                iconRequest.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";
                if (proxy != null) iconRequest.Proxy = proxy;

                using (var response = (HttpWebResponse)iconRequest.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var ms = new MemoryStream())
                {
                    if (stream == null) return null;
                    stream.CopyTo(ms);
                    byte[] data = ms.ToArray();
                    return Util.IsValidImage(data) ? data : null;
                }
            }
            catch
            {
                return null;
            }
        }

        public static string TryGuessFromPackage(string package)
        {
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
