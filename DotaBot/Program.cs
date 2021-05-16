using Discord;
using Discord.WebSocket;
using DotaBot;
using System;
using System.Linq;
using System.Threading.Tasks;
using static DotaBot.Logger;
using static DotaBot.ParseCommand;
using System.Threading;

public class Program
{
	private DiscordSocketClient discord;

	public static void Main(string[] args)
		=> new Program().MainAsync().GetAwaiter().GetResult();
	private static String Host = Configuration.GetConfig("Host");

	class GameReminderState
    {
		public DateTime last_check;
    }

	public async Task MainAsync()
	{
		GameReminderState game_reminder_state = new GameReminderState { last_check = CetTimeNow() };
		Timer game_reminder = new Timer(new TimerCallback(GameReminder), game_reminder_state, 
			dueTime: TimeSpan.FromSeconds(60), period: TimeSpan.FromSeconds(60));

		while (true)
		{
			try
			{
				Log("Starting Bot v11");
				Log($"Acting as `{Host}`");
				string discordToken = Configuration.GetConfig("DiscordToken");
				if (discordToken == "")
                {
					Log("Error: Discord token is empty!");
                }

				discord = new DiscordSocketClient();
				discord.Log += LogHandler;
				discord.MessageReceived += MessageReceived;
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

			Thread.Sleep(TimeSpan.FromSeconds(1));
		}
	}

	private DateTime CetTimeNow()
    {
		return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
			TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time"));
	}
	
	static TimeSpan NOTIFICATION_DELAY = TimeSpan.FromMinutes(3);
	private void GameReminder(object s)
    {
		try
        {
			var state = s as GameReminderState;
			using var db = new Db();
			var now = CetTimeNow();
			var games_to_notify = db.DotaBotGames.ToList().Where(x => 
				x.Time - NOTIFICATION_DELAY > state.last_check && 
				x.Time - NOTIFICATION_DELAY <= now).ToList();
			Log($"Game reminder found: {games_to_notify.Count} games");  // Debug only TODO: remove it

			foreach (var game in games_to_notify)
			{
				if (!IsItMyToHandle(game.GuildId, game.ChannelId))
					continue;

				var channel = new Channel(discord, db, game.GuildId, game.ChannelId);
				channel.SendReminder(game);
			}

			state.last_check = now;
		}
		catch (Exception e)
        {
			Log($"Game reminder exception: {e}");
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

	private Task MessageReceived(SocketMessage msg)
    {
		try
		{
			SocketTextChannel discord_channel = msg.Channel as SocketTextChannel;
			ulong guild = discord_channel.Guild.Id;
			if (!IsItMyToHandle(guild, discord_channel.Id))
				return Task.CompletedTask;

			string author = msg.Author.Username;
			if (author == "DotaBot")
				return Task.CompletedTask;

			var time = CetTimeNow();
			string content = msg.Content;
			Command command = Parse(content, time);
			if (command == null)
				return Task.CompletedTask;

			Log($"Recognized command: {command} ({author}: \"{content}\")");

			using var db = new Db();
			var channel = new Channel(discord, db, guild, discord_channel.Id);
			channel.CleanOldGames(time);
			channel.Execute(command, author);
		}
		catch(Exception e)
        {
			Log($"Handler failed on msg: {msg}: exception: {e}");
        }

		return Task.CompletedTask;
	}

	private Task LogHandler(LogMessage msg)
	{
		Log(msg.ToString());
		return Task.CompletedTask;
	}

}

// TODO: randomize texts
// TODO: add randomized descriptions to players e.g. "Goovie, pogromca kotletów"
// TODO: add reserve list printing when > 5 players