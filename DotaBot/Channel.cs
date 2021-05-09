using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static DotaBot.Logger;

namespace DotaBot
{
    public class Channel
    {
        public Channel(DiscordSocketClient discord, Db db, ulong guild_id, ulong channel_id)
        {
            this.discord = discord;
            this.db = db;
            this.guild_id = guild_id;
            this.channel_id = channel_id;
        }

        // Test only - sets discord to null, which causes ChannelState to not send messages.
        public Channel(Db db, ulong guild_id, ulong channel_id) : this(null, db, guild_id, channel_id){}

        private void SendMessage(string msg)
        {
            Log($"Sending message to guild_id: {guild_id} channel_id: {channel_id}:\n`{msg}`");
            if (discord != null)
            {
                // TODO: verify if the message was actually sent
                // TODO: measure time from receiving msg to sending answer
                discord.GetGuild(guild_id).GetTextChannel(channel_id).SendMessageAsync(msg);
            }
        }

		private IQueryable<DotaBotGame> Games => db.DotaBotGames.AsQueryable()
			.Where(x => x.GuildId == guild_id)
			.Where(x => x.ChannelId == channel_id);

		// Split to `Execute` and `ExecuteInner` is required as `ExecuteInner` calls `ExecuteInner`
		// and transaction can't be created inside another transaction.
		public void Execute(Command command, string player)
        {
			using var transaction = db.Database.BeginTransaction();

			ExecuteInner(command, player);

			db.SaveChanges();
			transaction.Commit();
		}

		private void ExecuteInner(Command command, string player)
		{
			string target_player = command.as_player == null ? player : $"{command.as_player} (added by {player})";
			if (command.action == Command.Action.Add)
			{
				var matched = Games.Where(x => x.Time == command.time).FirstOrDefault();
				if (matched == null)
				{
					var new_game = new DotaBotGame
					{
						ChannelId = channel_id,
						GuildId = guild_id,
						Players = new string[] { target_player },
						Time = command.time
					};
					db.DotaBotGames.Add(new_game);
					SendMessage($"Będzie Dotka!\n{PrintGame(new_game)}");
				}
				else
				{
					if (!matched.Players.AsSpan().Contains(target_player))
					{
						var list = new List<string>(matched.Players);
						list.Add(target_player);
						//list = list.OrderByDescending(x => x != "goovie").ToList();
						matched.Players = list.ToArray();

						SendMessage($"{target_player} dołączył do gry\n{PrintGame(matched)}");
					}
				}
			}
			else if (command.action == Command.Action.Remove)
			{
				var matched = Games.Where(x => x.Time == command.time).FirstOrDefault();
				if (matched != null)
				{
					if (matched.Players.AsSpan().Contains(target_player))
					{
						var list = new List<string>(matched.Players);
						list.Remove(target_player);
						matched.Players = list.ToArray();

						if (matched.Players.Length == 0)
						{
							SendMessage($"Brak chętnych na Dotkę o {matched.Time.Hour}:{matched.Time.Minute:D2} :(");
							db.DotaBotGames.Remove(matched);
						}
						else
						{
							SendMessage($"{target_player} zrezygnował z gry\n{PrintGame(matched)}");
						}
					}
				}
			}
			else if (command.action == Command.Action.JoinLatestGame)
			{
				if (Games.Count() == 0)
				{
					SendMessage($"Nie ma żadnych gier, żebyś mógł dołączyć");
				}
				else
				{
					ExecuteInner(new Command
						{
							action = Command.Action.Add,
							time = Games.OrderBy(x => x.Id).ToList().Last().Time  // TODO: if ids are not created monotonically: add game creation time to DB
						},
						target_player);
				}
			}
			else if (command.action == Command.Action.RemoveAll)
			{
				foreach (var game in db.DotaBotGames.ToList())
				{
					ExecuteInner(new Command { action = Command.Action.Remove, time = game.Time }, target_player);
				}
			}
			else if (command.action == Command.Action.ShowGames)
			{
				if (Games.Count() == 0)
				{
					SendMessage("Nie ma żadnych zaplanowanych gier. Zaproponuj swoją!");
				}
				else
				{
					var s = "Szykuje się granie:";
					foreach (var game in Games)
					{
						s += $"{PrintGame(game)}\n";
					}
					SendMessage(s);
				}
			}
			else
			{
				Log($"Unhandled action: {command.action}");
			}
		}

		public void CleanOldGames(DateTime now)
		{
			using var transaction = db.Database.BeginTransaction();

			foreach (var game in db.DotaBotGames.ToList())
			{
				if (game.Time < now - TimeSpan.FromMinutes(5))
				{
					db.DotaBotGames.Remove(game);
				}
			}

			db.SaveChanges();
			transaction.Commit();
		}

		public string PrintGame(DotaBotGame game)
		{
			var s = $"```\nZespół na {game.Time.Hour}:{game.Time.Minute:D2}\n";
			int i = 1;
			foreach (var player in game.Players)
			{
				s += $" {i}) {player}\n";
				i++;
			}
			s += "```";

			return s;
		}


		DiscordSocketClient discord;
        private Db db;
        private ulong guild_id;
        private ulong channel_id;
    }
}
