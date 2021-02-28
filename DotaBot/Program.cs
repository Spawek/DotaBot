using Discord;
using Discord.WebSocket;
using DotaBot;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using System.Collections;
using static DotaBot.Logger;
using static DotaBot.ParseCommand;

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
				Log($"Acting as `{Host}`");
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

			var cetTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
				TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time"));
			string content = msg.Content;
			Command command = Parse(content, cetTime);
			if (command == null)
				return Task.CompletedTask;

			Log($"Recognized command: {command} ({author}: \"{content}\")");

			using (var db = new Db())
            {
				var channel = new Channel(discord, db, guild, discord_channel.Id);
				channel.CleanOldGames(cetTime);
				channel.Execute(command, author);
			}

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

// TODO: reminder 5 min before game + on the time of the game (only if >= 2 players)
// TODO: add mentions to the reminder
// TODO: add @all on creating a new dota
// TODO: randomize texts
// TODO: add randomized descriptions to players e.g. "Goovie, pogromca kotletów"
// TODO: add reserve list printing when > 5 players