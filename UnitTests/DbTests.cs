using Microsoft.VisualStudio.TestTools.UnitTesting;
using DotaBot;
using System;
using System.Collections.Generic;
using System.Text;
using static UnitTests.InMemoryDb;
using System.Linq;

namespace UnitTests
{
    [TestClass]
    public class DbTests
    {
        [TestMethod]
        public void PlayersSerializeDeserializeTest()
        {
            using var db = MakeDb();

            var players = new List<Player> {
                new Player { Name = "spawek", AddedBy = "muhah", Note = "why not" },
                new Player { Name = "bixkog", AddedBy = "muhah", Note = "why not too" }};
            db.DotaBotGames.Add(new DotaBotGame { Players = players });
            db.SaveChanges();

            var games = db.DotaBotGames.ToList();
            Assert.AreEqual(games.Count, 1);
            CollectionAssert.AreEqual(games[0].Players, players);
        }
    }
}
