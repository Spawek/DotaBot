using Discord;
using Discord.WebSocket;
using DotaBot;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using System.Collections;

public class Program
{
	private DiscordSocketClient discord;

	public static void Main(string[] args)
		=> new Program().MainAsync().GetAwaiter().GetResult();
	private static String Host = Configuration.GetConfig("Host");

	public async Task MainAsync()
	{
		while (true)
		{
			try
			{
				Log("Starting Bot");
				Log($"Acting as `{Configuration.GetConfig("Host")}`");
				string discordToken = Configuration.GetConfig("DiscordToken");
				if (discordToken == "")
                {
					Log("Error: Discord token is empty!");
                }

				discord = new DiscordSocketClient();
				discord.Log += LogHandler;
				discord.MessageReceived += MessageReceived;
                discord.ReactionAdded += ReactionAdded;
				await discord.LoginAsync(TokenType.Bot, discordToken);
				await discord.StartAsync();
				Log("Registered to Discord");

				// Block this task until the program is closed.
				await Task.Delay(-1);
			}
			catch(Exception e)
            {
				Log($"Main exception: {e}");
            }
			System.Threading.Thread.Sleep(TimeSpan.FromSeconds(1));
		}
	}

	// Definition of routing (Discord channel -> DotaBot instance).
	// Returns true when current host should handle the event.
	bool IsItMyToHandle(ulong guild_id, ulong channel_id)
    {
		if (guild_id == 729765595398144001)  // Kuce
        {
			return Host == "Azure";
        }
		else if (guild_id == 810230681782452294 && channel_id == 810230681786646528)  // Spawek test channel
		{
			return Host == "Legion";  
		}

		return true;
	}

	private Task ReactionAdded(Cacheable<IUserMessage, ulong> data, ISocketMessageChannel channel, SocketReaction reaction)
    {
		var expected_emote = "👍";
		var msg = data.GetOrDownloadAsync().Result;

		return Task.CompletedTask;
    }

    private void SendMessage(string msg, ulong guild_id, ulong channel_id)
    {
		Log($"Sending message to guild_id: {guild_id} channel_id: {channel_id}: `{msg}`");
		discord.GetGuild(guild_id).GetTextChannel(channel_id).SendMessageAsync(msg);
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

	public class Command
    {
		public enum Action { Add, Remove, JoinLatestGame, RemoveAll, ShowGames, MoveTimeProposal };

		public DateTime time;  // used by: Add, Remove, MoveTimeProposal
		public DateTime time2;  // used by: MoveTimeProposal 
		public Action action;

		// TODO: find a better way to print the state
        public override string ToString()
        {
			string time_string = "";
			if (time != new DateTime())
            {
				time_string = $"time: {time}, ";
			}

			string time2_string = "";
			if (time2 != new DateTime())
			{
				time2_string = $"time2: {time2}, ";
			}

			string action_string = $"action: {action}";

			return $"({time_string}{time2_string}action: { action})";
        }

        public override bool Equals(object obj)
        {
			var other = obj as Command;
			if (other == null)
				return false;

			return other.time == this.time && other.action == this.action;
        }
    }

	static Command ParseAddRemoveCommands(string str, DateTime now)
    {
		var regex = @"^\s*(?:dota|dotka|doto)\s*(?<hours>[0-9]?[0-9])?(?<minutes>(?:\.|:)[0-9]{2})?\s*(?<action>\+\+|--|\?||\+1|-1)\s*$";
		var match = Regex.Match(str, regex, RegexOptions.IgnoreCase);
		if (!match.Success)
			return null;

		int minute;
		string minutes_string = match.Groups["minutes"].Value;
		if (minutes_string == "")
		{
			minute = 0;
		}
		else if (!Int32.TryParse(minutes_string.Substring(1), out minute))
		{
			return null;
		}
		if (minute > 59)
			return null;

		int hour;
		if (!Int32.TryParse(match.Groups["hours"].Value, out hour))
		{
			if (match.Groups["hours"].Value == "" && minutes_string != "")
			{
				hour = now.Hour;
				if (minute < now.Minute)
					hour = (hour + 1) % 24;
			}
			else
			{
				return null;
			}
		}

		if (hour > 23)
			return null;


		var action_string = match.Groups["action"].Value;
		Command.Action action;
		if (action_string == "++" || action_string == "?" || action_string == "" || action_string == "+1")
		{
			action = Command.Action.Add;
		}
		else if (action_string == "--" || action_string == "-1")
		{
			action = Command.Action.Remove;
		}
		else
		{
			Log($"Unhandled action string: {action_string}");
			return null;
		}

		var time = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);
		if (time < now)
			time = time.AddDays(1);

		return new Command { time = time, action = action };
	}

	// Check tests when modyfying it.
	public static Command ParseCommand(string str, DateTime now)
	{
		// "++" or "+1"
		if (Regex.IsMatch(str, @"^\s*\+\+\s*$") || Regex.IsMatch(str, @"^\s*\+1\s*$"))
			return new Command { action = Command.Action.JoinLatestGame };
		// "--" or "-1"
		if (Regex.IsMatch(str, @"^\s*--\s*$") || Regex.IsMatch(str, @"^\s*\-1\s*$"))
			return new Command { action = Command.Action.RemoveAll };
		// "dota?"
		if (Regex.IsMatch(str, @"^\s*(?:dota|dotka|doto)\s*\?\s*$", RegexOptions.IgnoreCase))
			return new Command { action = Command.Action.ShowGames };

		// e.g. "dota 13:24 ++", "dota 12?", "dota 15--", "doto :30?"
		var add_remove = ParseAddRemoveCommands(str, now);
		if (add_remove != null)
			return add_remove;

		return null;
	}

	private Task MessageReceived(SocketMessage msg)
    {
		try
		{
			SocketTextChannel channel = msg.Channel as SocketTextChannel;
			SocketGuild guild = channel.Guild;
			if (!IsItMyToHandle(guild.Id, channel.Id))
				return Task.CompletedTask;

			var cetTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
				TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time"));

			CleanOldGames(cetTime);
			string author = msg.Author.Username;
			if (author == "DotaBot")
				return Task.CompletedTask;

			string content = msg.Content;
			Command command = ParseCommand(content, cetTime);
			if (command == null)
				return Task.CompletedTask;

			Log($"Recognized command: {command} ({author}: \"{content}\")");

			ExecuteCommand(command, guild.Id, channel.Id, author);
		}
		catch(Exception e)
        {
			Log($"Handler failed on msg: {msg}: exception: {e}");
        }

		return Task.CompletedTask;
	}

	private void CleanOldGames(DateTime now)
	{
		using var db = new Db();
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

    private void ExecuteCommand(Command command, ulong guild_id, ulong channel_id, string player)
	{
		using var db = new Db();
		using var transaction = db.Database.BeginTransaction();

		var games = db.DotaBotGames.AsQueryable().Where(x => x.GuildId == guild_id && x.ChannelId == channel_id);
        if (command.action == Command.Action.Add)
		{
			var matched = games.Where(x => x.Time == command.time).FirstOrDefault();
			if (matched == null)
			{
				var new_game = new DotaBotGame
				{
					ChannelId = channel_id,
					GuildId = guild_id,
					Players = new string[] { player },
					Time = command.time
				};
				db.DotaBotGames.Add(new_game);
				SendMessage($"Będzie Dotka!\n{PrintGame(new_game)}", guild_id, channel_id);
			}
			else
			{
				if (!matched.Players.AsSpan().Contains(player))
				{
					var list = new List<string>(matched.Players);
					list.Add(player);
                    //list = list.OrderByDescending(x => x != "goovie").ToList();
                    matched.Players = list.ToArray(); 

					SendMessage($"{player} dołączył do gry\n{PrintGame(matched)}", guild_id, channel_id);
				}
			}
		}
		else if (command.action == Command.Action.Remove)
		{
			var matched = games.Where(x => x.Time == command.time).FirstOrDefault();
			if (matched != null)
			{
				if (matched.Players.AsSpan().Contains(player))
				{
					var list = new List<string>(matched.Players);
					list.Remove(player);
					matched.Players = list.ToArray();
					
					if (matched.Players.Length == 0)
					{
						SendMessage($"Brak chętnych na Dotkę o {matched.Time.Hour}:{matched.Time.Minute:D2} :(", guild_id, channel_id);
						db.DotaBotGames.Remove(matched);
					}
					else
					{
						SendMessage($"{player} zrezygnował z gry\n{PrintGame(matched)}", guild_id, channel_id);
					}
				}
			}
		}
		else if (command.action == Command.Action.JoinLatestGame)
		{
			if (games.Count() == 0)
			{
				SendMessage($"Nie ma żadnych gier, żebyś mógł dołączyć", guild_id, channel_id);
			}
			else
			{
				ExecuteCommand(new Command
				{
					action = Command.Action.Add,
                    time = db.DotaBotGames.AsQueryable().Where(x =>
						x.GuildId == guild_id &&
						x.ChannelId == channel_id).OrderBy(x => x.Time).ToList().First().Time
				},
					guild_id,
					channel_id,
					player);
			}
		}
		else if (command.action == Command.Action.RemoveAll)
		{
			foreach (var game in db.DotaBotGames.ToList())
			{
				ExecuteCommand(new Command { action = Command.Action.Remove, time = game.Time }, guild_id, channel_id, player);
			}
		}
		else if (command.action == Command.Action.ShowGames)
        {
			if (games.Count() == 0)
            {
				SendMessage("Nie ma żadnych zaplanowanych gier. Zaproponuj swoją!", guild_id, channel_id);
            }
            else
            {
				var s = "Szykuje się granie:";
                foreach (var game in games)
                {
					s += $"{PrintGame(game)}\n";
                }
				SendMessage(s, guild_id, channel_id);
            }
        }
		else
		{
			Log($"Unhandled action: {command.action}");
		}

		db.SaveChanges();
		transaction.Commit();
	}


	private Task LogHandler(LogMessage msg)
	{
		Log(msg.ToString());
		return Task.CompletedTask;
	}


	private static void Log(string msg)
    {
		Console.WriteLine(msg);
		System.Diagnostics.Trace.TraceInformation(msg);
    }
}

// TODO: reminder 5 min before game + on the time of the game (only if >= 2 players)
// TODO: add mentions to the reminder
// TODO: add @all on creating a new dota
// TODO: randomize texts
// TODO: add randomized descriptions to players e.g. "Goovie, pogromca kotletów"
// TODO: github + deploy z githuba
