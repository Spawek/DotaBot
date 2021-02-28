using DotaBot;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using static DotaBot.ParseCommand;

namespace UnitTests
{
    [TestClass]
    public class CommandTests
    {
        [TestMethod]
        public void ParseCommandTest()
        {
            var now = new DateTime(2020, 5, 12, 5, 30, 12);
            Assert.AreEqual(Parse("dota 8:30?", now), 
                new Command { action = Command.Action.Add, time = new DateTime(2020, 5, 12, 8, 30, 0) });
            Assert.AreEqual(Parse("dota 2:30?", now), 
                new Command { action = Command.Action.Add, time = new DateTime(2020, 5, 13, 2, 30, 0) });
            Assert.AreEqual(Parse("  dota 2  ?   ", now),
                new Command { action = Command.Action.Add, time = new DateTime(2020, 5, 13, 2, 0, 0) });
            Assert.AreEqual(Parse("doto 16", now),
                new Command { action = Command.Action.Add, time = new DateTime(2020, 5, 12, 16, 0, 0) });
            Assert.AreEqual(Parse("dotka :45", now),
                new Command { action = Command.Action.Add, time = new DateTime(2020, 5, 12, 5, 45, 0) });
            Assert.AreEqual(Parse("dotka :15", now),
                new Command { action = Command.Action.Add, time = new DateTime(2020, 5, 12, 6, 15, 0) });
            Assert.AreEqual(Parse("dotka :15 +1", now),
                new Command { action = Command.Action.Add, time = new DateTime(2020, 5, 12, 6, 15, 0) });
            Assert.AreEqual(Parse("dotka.15 +1", now),
                new Command { action = Command.Action.Add, time = new DateTime(2020, 5, 12, 6, 15, 0) });
            Assert.AreEqual(Parse("dotka 00.00 +1", now),
                new Command { action = Command.Action.Add, time = new DateTime(2020, 5, 13, 0, 0, 0) });

            Assert.AreEqual(Parse("dota 15--", now),
                new Command { action = Command.Action.Remove, time = new DateTime(2020, 5, 12, 15, 0, 0) });
            Assert.AreEqual(Parse("dotka :15 -1", now),
                new Command { action = Command.Action.Remove, time = new DateTime(2020, 5, 12, 6, 15, 0) });

            Assert.AreEqual(Parse("++", now),
                new Command { action = Command.Action.JoinLatestGame });
            Assert.AreEqual(Parse("--", now),
                new Command { action = Command.Action.RemoveAll });
            Assert.AreEqual(Parse("dota?", now),
                new Command { action = Command.Action.ShowGames });

            Assert.AreEqual(Parse("dota 16 -> 17?", now),
                new Command
                {
                    action = Command.Action.RescheduleProposal,
                    time = new DateTime(2020, 5, 12, 16, 0, 0),
                    time2 = new DateTime(2020, 5, 12, 17, 0, 0)
                });
            Assert.AreEqual(Parse(" dota  :35->6:15 ?", now),
                new Command
                {
                    action = Command.Action.RescheduleProposal,
                    time = new DateTime(2020, 5, 12, 5, 35, 0),
                    time2 = new DateTime(2020, 5, 12, 6, 15, 0)
                });
        }

        [TestMethod]
        public void MoveTimeProposalSerializeDeserializeTest()
        {

        }
    }
}
