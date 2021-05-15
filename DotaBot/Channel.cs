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

		static private Dictionary<string, string> AsPlayerAliases = new Dictionary<string, string>
		{
			{ "dragon", "Jakub Łapot" },
			{ "dagon", "Jakub Łapot" },
			{ "goobie", "goovie" },
			{ "wojtek", "Bixkog" },
			{ "maciek", "Spawek" },
			{ "marcin", "grzybek" },
			{ "muha", "muhah" },
			{ "mucha", "muhah" },
			{ "muszka", "muhah" },
		};

		static private string NormalizeAsPlayer(string as_player)
        {
			if (AsPlayerAliases.TryGetValue(as_player.ToLower(), out var alias))
				return alias;
			return as_player;
		}

		private void ExecuteInner(Command command, string requester)
		{
			var player = new Player { 
				Name = command.as_player == null ? requester : NormalizeAsPlayer(command.as_player),
				Note = command.note,
				AddedBy = command.as_player == null ? null : requester
			};

			if (command.action == Command.Action.Add)
			{
				var existing_game = Games.Where(x => x.Time == command.time).FirstOrDefault();
				if (existing_game == null)
				{
					var new_game = new DotaBotGame
					{
						ChannelId = channel_id,
						GuildId = guild_id,
						Players = new List<Player> { player },
						Time = command.time
					};
					db.DotaBotGames.Add(new_game);
					SendMessage($"Będzie Dotka!\n{PrintGame(new_game)}");
				}
				else
				{
					// replace same player added by someone else
					// TODO: test
					var added_by_someone_else = existing_game.Players.FindIndex(x =>
						x.Name.ToLower() == player.Name.ToLower() &&
						x.AddedBy != null);
					if (added_by_someone_else != -1 && player.AddedBy == null)
					{
						existing_game.Players[added_by_someone_else] = player;
						SendMessage($"{player.Name} sam włączył się do gry\n{PrintGame(existing_game)}");
					}
					else if (existing_game.Players.All(x => x.Name != player.Name))
					{
						existing_game.Players.Add(player);
						SendMessage($"{player.Name} dołączył do gry\n{PrintGame(existing_game)}");
					}
					db.Entry(existing_game).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
				}
			}
			else if (command.action == Command.Action.Remove)
			{
				var game = Games.FirstOrDefault(x => x.Time == command.time);
				if (game != null)
				{
					var player_to_remove = game.Players.FirstOrDefault(x =>
						x.Name == player.Name &&
						x.AddedBy == player.AddedBy);
					if (player_to_remove != null)
					{
						game.Players.Remove(player_to_remove);
						db.Entry(game).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
						if (game.Players.Count == 0)
						{
							SendMessage($"Brak chętnych na Dotkę o {game.Time.Hour}:{game.Time.Minute:D2} :(");
							db.DotaBotGames.Remove(game);
						}
						else
						{
							SendMessage($"{player.Name} zrezygnował z gry\n{PrintGame(game)}");
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
					// Recursive call!
					ExecuteInner(new Command
						{
							action = Command.Action.Add,
							as_player = command.as_player,
							note = command.note,
							time = Games.OrderBy(x => x.Id).ToList().Last().Time
						},
						requester);
				}
			}
			else if (command.action == Command.Action.RemoveAll)
			{
				foreach (var game in db.DotaBotGames.ToList())
				{
					// Recursive call!
					ExecuteInner(new Command 
						{ 
							action = Command.Action.Remove, 
							as_player = command.as_player, 
							time = game.Time 
						},
						requester);
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
				string added_by = player.AddedBy == null ? "" : $" (dodany przez: {player.AddedBy})";
				string note = player.Note == null ? "" : $" ({player.Note})";
				s += $" {i}) {player.Name}{added_by}{note}\n";
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
