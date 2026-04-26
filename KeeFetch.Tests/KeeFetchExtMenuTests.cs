using System.Linq;
using System.Windows.Forms;
using KeePass.Plugins;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    [TestClass]
    public class KeeFetchExtMenuTests
    {
        [TestMethod]
        public void GetMenuItem_MainMenu_SubcommandsHaveIcons()
        {
            var plugin = new KeeFetchExt();

            ToolStripMenuItem menu = plugin.GetMenuItem(PluginMenuType.Main);

            Assert.IsNotNull(menu);
            ToolStripMenuItem[] commands = menu.DropDownItems
                .OfType<ToolStripMenuItem>()
                .ToArray();

            Assert.AreEqual(2, commands.Length);
            Assert.IsTrue(commands.All(command => command.Image != null));
        }
    }
}
