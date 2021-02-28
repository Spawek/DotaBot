using DotaBot;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using static DotaBot.ParseCommand;

namespace UnitTests
{
    [TestClass]
    public class ChannelTests
    {
        [TestMethod]
        public void BasicScenario()
        {
            using var db = new Db("IN_MEMORY");

            ulong guild_id = 123;
            ulong channel_id = 234;
            var channel = new Channel(db, guild_id, channel_id);
            
            var time = new DateTime(2020, 1, 1, 20, 30, 0);
            channel.Execute(Parse("dota 21?", time), "muhah");
            channel.Execute(Parse("++", time), "spawek");
            channel.Execute(Parse("dota 21++", time), "bixkog");
            channel.Execute(Parse("--", time), "spawek");

            var games = db.DotaBotGames.ToList();
            Assert.AreEqual(games.Count, 1);
            Assert.AreEqual(games.First(), new DotaBotGame {
                Id = 1,
                Time = new DateTime(2020, 1, 1, 21, 0, 0),
                GuildId = guild_id,
                ChannelId = channel_id,
                Players = new string[] { "muhah", "bixkog" } }
            );

            channel.Execute(Parse("dota 21--", time), "bixkog");
            channel.Execute(Parse("--", time), "muhah");
            Assert.AreEqual(db.DotaBotGames.Count(), 0);
        }

        [TestMethod]
        public void OldGamesAreCleanedUp()
        {
            using var db = new Db("IN_MEMORY");

            ulong guild_id = 123;
            ulong channel_id = 234;
            var channel = new Channel(db, guild_id, channel_id);

            var time1 = new DateTime(2020, 1, 1, 20, 30, 0);
            var time2 = new DateTime(2020, 1, 1, 21, 30, 0);
            channel.Execute(Parse("dota 21?", time1), "muhah");
            channel.CleanOldGames(time2);

            Assert.AreEqual(db.DotaBotGames.Count(), 0);
        }

        [TestMethod]
        public void PlusPlusJoinsLatestCreatedGame()
        {
            using var db = new Db("IN_MEMORY");

            ulong guild_id = 123;
            ulong channel_id = 234;
            var channel = new Channel(db, guild_id, channel_id);

            var time = new DateTime(2020, 1, 1, 20, 30, 0);
            channel.Execute(Parse("dota 21?", time), "muhah");
            channel.Execute(Parse("dota 23?", time), "muhah");
            channel.Execute(Parse("dota 22?", time), "muhah");
            var gamesx = db.DotaBotGames.ToList();
            channel.Execute(Parse("++", time), "spawek");
            channel.Execute(Parse("--", time), "muhah");

            var games = db.DotaBotGames.ToList();
            Assert.AreEqual(games.Count, 1);
            Assert.AreEqual(games.First(), new DotaBotGame
            {
                Id = 3,
                Time = new DateTime(2020, 1, 1, 22, 0, 0),
                GuildId = guild_id,
                ChannelId = channel_id,
                Players = new string[] { "spawek" }
            });
        }
    }
}
