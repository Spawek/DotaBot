using DotaBot;
using System;
using System.Collections.Generic;
using System.Text;

namespace UnitTests
{
    public static class InMemoryDb
    {
        public static Db MakeDb()
        {
            var db = new Db("IN_MEMORY");
            db.DotaBotGames.RemoveRange(db.DotaBotGames);
            db.SaveChanges();
            return db;
        }
    }
}
