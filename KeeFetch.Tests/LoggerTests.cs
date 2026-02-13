using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    [TestClass]
    public class LoggerTests
    {
        [TestInitialize]
        public void Setup()
        {
            Logger.Clear();
        }

        [TestMethod]
        public void Logger_AddEntries_StoresThem()
        {
            Logger.Debug("Context", "Message 1");
            Logger.Warn("Context", "Message 2");
            Logger.Error("Context", "Message 3");

            var entries = Logger.GetEntries();
            Assert.AreEqual(3, entries.Count);
            Assert.AreEqual(LogLevel.Debug, entries[0].Level);
            Assert.AreEqual(LogLevel.Warning, entries[1].Level);
            Assert.AreEqual(LogLevel.Error, entries[2].Level);
        }

        [TestMethod]
        public void Logger_MaxEntries_LimitsGrowth()
        {
            // The limit is 10000
            for (int i = 0; i < 10050; i++)
            {
                Logger.Debug("Test", "Message " + i);
            }

            var entries = Logger.GetEntries();
            Assert.AreEqual(10000, entries.Count);
            
            // Should contain the last ones
            Assert.AreEqual("Message 10049", entries[9999].Message);
        }
        
        [TestMethod]
        public void Logger_GetErrors_ReturnsOnlyErrors()
        {
            Logger.Debug("Context", "Debug");
            Logger.Error("Context", "Error 1");
            Logger.Warn("Context", "Warn");
            Logger.Error("Context", "Error 2");

            var errors = Logger.GetErrors();
            Assert.AreEqual(2, errors.Count);
            Assert.AreEqual("Error 1", errors[0].Message);
            Assert.AreEqual("Error 2", errors[1].Message);
        }
    }
}