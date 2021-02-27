using DotaBot;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using static DotaBot.Parse;

namespace UnitTests
{
    [TestClass]
    public class MainTests
    {
        [TestMethod]
        public void ParseCommandTest()
        {
            var now = new DateTime(2020, 5, 12, 5, 30, 12);
            Assert.AreEqual(ParseCommand("dota 8:30?", now), 
                new Command { action = Command.Action.Add, time = new DateTime(2020, 5, 12, 8, 30, 0) });
            Assert.AreEqual(ParseCommand("dota 2:30?", now), 
                new Command { action = Command.Action.Add, time = new DateTime(2020, 5, 13, 2, 30, 0) });
            Assert.AreEqual(ParseCommand("  dota 2  ?   ", now),
                new Command { action = Command.Action.Add, time = new DateTime(2020, 5, 13, 2, 0, 0) });
            Assert.AreEqual(ParseCommand("doto 16", now),
                new Command { action = Command.Action.Add, time = new DateTime(2020, 5, 12, 16, 0, 0) });
            Assert.AreEqual(ParseCommand("dotka :45", now),
                new Command { action = Command.Action.Add, time = new DateTime(2020, 5, 12, 5, 45, 0) });
            Assert.AreEqual(ParseCommand("dotka :15", now),
                new Command { action = Command.Action.Add, time = new DateTime(2020, 5, 12, 6, 15, 0) });
            Assert.AreEqual(ParseCommand("dotka :15 +1", now),
                new Command { action = Command.Action.Add, time = new DateTime(2020, 5, 12, 6, 15, 0) });
            Assert.AreEqual(ParseCommand("dotka.15 +1", now),
                new Command { action = Command.Action.Add, time = new DateTime(2020, 5, 12, 6, 15, 0) });
            Assert.AreEqual(ParseCommand("dotka 00.00 +1", now),
                new Command { action = Command.Action.Add, time = new DateTime(2020, 5, 13, 0, 0, 0) });

            Assert.AreEqual(ParseCommand("dota 15--", now),
                new Command { action = Command.Action.Remove, time = new DateTime(2020, 5, 12, 15, 0, 0) });
            Assert.AreEqual(ParseCommand("dotka :15 -1", now),
                new Command { action = Command.Action.Remove, time = new DateTime(2020, 5, 12, 6, 15, 0) });

            Assert.AreEqual(ParseCommand("++", now),
                new Command { action = Command.Action.JoinLatestGame });
            Assert.AreEqual(ParseCommand("--", now),
                new Command { action = Command.Action.RemoveAll });
            Assert.AreEqual(ParseCommand("dota?", now),
                new Command { action = Command.Action.ShowGames });
        }
    }
}
