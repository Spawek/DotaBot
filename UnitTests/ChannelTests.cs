using DotaBot;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using static DotaBot.ParseCommand;
using static UnitTests.InMemoryDb;

namespace UnitTests
{
    [TestClass]
    public class ChannelTests
    {
        [TestMethod]
        public void BasicScenario()
        {
            using var db = MakeDb();

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
            var game = games.First();
            Assert.AreEqual(game.Time, new DateTime(2020, 1, 1, 21, 0, 0));
            Assert.AreEqual(game.GuildId, guild_id);
            Assert.AreEqual(game.ChannelId, channel_id);
            CollectionAssert.AreEqual(game.Players, 
                new List<Player>{
                    new Player{ Name= "muhah" },
                    new Player{ Name = "bixkog" } });

            channel.Execute(Parse("dota 21--", time), "bixkog");
            channel.Execute(Parse("--", time), "muhah");
            Assert.AreEqual(db.DotaBotGames.Count(), 0);
        }

        [TestMethod]
        public void OldGamesAreCleanedUp()
        {
            using var db = MakeDb();
            var channel = new Channel(db, 123, 234);

            var time1 = new DateTime(2020, 1, 1, 20, 30, 0);
            var time2 = new DateTime(2020, 1, 1, 21, 30, 0);
            channel.Execute(Parse("dota 21?", time1), "muhah");
            channel.CleanOldGames(time2);

            Assert.AreEqual(db.DotaBotGames.Count(), 0);
        }

        [TestMethod]
        public void PlusPlusJoinsLatestCreatedGame()
        {
            using var db = MakeDb();
            var channel = new Channel(db, 123, 234);

            var time = new DateTime(2020, 1, 1, 20, 30, 0);
            channel.Execute(Parse("dota 21?", time), "muhah");
            channel.Execute(Parse("dota 23?", time), "muhah");
            channel.Execute(Parse("dota 22?", time), "muhah");
            channel.Execute(Parse("++", time), "spawek");
            channel.Execute(Parse("--", time), "muhah");

            var games = db.DotaBotGames.ToList();
            Assert.AreEqual(games.Count, 1);
            var game = games.First();
            Assert.AreEqual(game.Time, new DateTime(2020, 1, 1, 22, 0, 0));
            CollectionAssert.AreEqual(game.Players, 
                new List<Player> { new Player { Name = "spawek" } });
        }

        [TestMethod]
        public void AsPlayerWithNoteScenario()
        {
            using var db = MakeDb();
            var channel = new Channel(db, 123, 234);

            var time = new DateTime(2020, 1, 1, 20, 30, 0);
            channel.Execute(Parse("dota 21? (why not)", time), "muhah");
            channel.Execute(Parse("(as spawek) dota 21++", time), "muhah");
            channel.Execute(Parse("(as status) ++", time), "muhah");
            channel.Execute(Parse("(as removed) ++", time), "muhah");
            channel.Execute(Parse("(as removed) --", time), "muhah");
            channel.Execute(Parse("++", time), "spawek");
            channel.Execute(Parse("++ (with note)", time), "removed");
            channel.Execute(Parse("--", time), "removed");

            var games = db.DotaBotGames.ToList();
            Assert.AreEqual(games.Count, 1);
            var game = games.First();
            Assert.AreEqual(game.Time, new DateTime(2020, 1, 1, 21, 0, 0));
            CollectionAssert.AreEqual(game.Players,
                new List<Player> { 
                    new Player { Name = "muhah", Note = "why not"},
                    new Player { Name = "spawek" },
                    new Player { Name = "status", AddedBy = "muhah" }});
        }

        [TestMethod]
        public void AsPlayerAliases()
        {
            using var db = MakeDb();
            var channel = new Channel(db, 123, 234);

            var time = new DateTime(2020, 1, 1, 20, 30, 0);
            channel.Execute(Parse("(as dragon) Dota 21?", time), "muhah");
            channel.Execute(Parse("++", time), "Jakub Łapot");

            var games = db.DotaBotGames.ToList();
            Assert.AreEqual(games.Count, 1);
            var game = games.First();
            Assert.AreEqual(game.Time, new DateTime(2020, 1, 1, 21, 0, 0));
            CollectionAssert.AreEqual(game.Players,
                new List<Player> { new Player { Name = "Jakub Łapot" } });
        }

        [TestMethod]
        public void Simple3PlayerScenario()
        {
            using var db = MakeDb();
            var channel = new Channel(db, 123, 234);

            var time = new DateTime(2020, 1, 1, 20, 30, 0);
            channel.Execute(Parse("dota 21?", time), "vifon");
            channel.Execute(Parse("++", time), "spawek");
            channel.Execute(Parse("++", time), "muhah");

            var games = db.DotaBotGames.ToList();
            Assert.AreEqual(games.Count, 1);
            var game = games.First();
            Assert.AreEqual(game.Time, new DateTime(2020, 1, 1, 21, 0, 0));
            CollectionAssert.AreEqual(game.Players,
                new List<Player> { 
                    new Player { Name = "vifon" },
                    new Player { Name = "spawek" },
                    new Player { Name = "muhah" }});

        }

        // TODO: add event for sending messages so it can be tested
    }
}
