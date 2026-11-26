using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace KeeFetch
{
    /// <summary>
    /// Embedded KeeFetch artwork shared by menus and dialogs, so every window
    /// shows the KeeFetch icon instead of the generic WinForms one.
    /// </summary>
    internal static class UiAssets
    {
        private static readonly object iconLock = new object();
        private static Icon windowIcon;
        private static bool windowIconLoaded;

        internal static Image LoadImage(string fileName, int size)
        {
            string resourceName = "KeeFetch.Assets.Icons." + fileName;
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                        return null;

                    using (Image image = Image.FromStream(stream))
                    {
                        // The source artwork is large; bicubic resampling keeps
                        // the small sizes legible instead of aliased.
                        var bitmap = new Bitmap(size, size);
                        using (Graphics g = Graphics.FromImage(bitmap))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                            g.Clear(Color.Transparent);
                            g.DrawImage(image, new Rectangle(0, 0, size, size));
                        }
                        return bitmap;
                    }
                }
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>The KeeFetch app icon for window title bars, or null when unavailable.</summary>
        internal static Icon WindowIcon
        {
            get
            {
                lock (iconLock)
                {
                    if (!windowIconLoaded)
                    {
                        windowIconLoaded = true;
                        Bitmap bitmap = LoadImage("keefetch-app.png", 32) as Bitmap;
                        if (bitmap != null)
                        {
                            // The HICON lives for the process; KeePass keeps
                            // plugin artwork loaded for its whole lifetime.
                            windowIcon = Icon.FromHandle(bitmap.GetHicon());
                        }
                    }

                    return windowIcon;
                }
            }
        }

        /// <summary>
        /// Gives a KeeFetch dialog the KeeFetch icon and registers it with
        /// KeePass while it is open, so KeePass can close it before locking
        /// the workspace instead of leaving it on top of a locked database.
        /// </summary>
        internal static void PrepareDialog(Form form)
        {
            if (form == null)
                return;

            ApplyWindowIcon(form);
            bool registered = false;
            form.Load += delegate
            {
                try
                {
                    KeePass.UI.GlobalWindowManager.AddWindow(form);
                    registered = true;
                }
                catch (Exception ex)
                {
                    // KeePass is not running, e.g. in tests.
                    Logger.Debug("PrepareDialog", ex);
                }
            };
            form.FormClosed += delegate
            {
                if (!registered)
                    return;

                registered = false;
                try
                {
                    KeePass.UI.GlobalWindowManager.RemoveWindow(form);
                }
                catch (Exception ex)
                {
                    Logger.Debug("PrepareDialog", ex);
                }
            };
        }

        internal static void ApplyWindowIcon(Form form)
        {
            if (form == null)
                return;

            Icon icon = WindowIcon;
            if (icon != null)
            {
                form.Icon = icon;
                form.ShowIcon = true;
            }
        }
    }
}
