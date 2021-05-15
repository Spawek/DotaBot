using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static DotaBot.Logger;

namespace DotaBot
{
	public class Command
	{
		public enum Action { Add, Remove, JoinLatestGame, RemoveAll, ShowGames };

		public DateTime time;  // used by: Add, Remove
		public string as_player;  // used by: Add, Remove
		public string note;  // used by: Add, AddLatest
		public Action action;

		public override string ToString()
		{
			string time_string = "";
			if (time != new DateTime())
			{
				time_string = $"time: {time}, ";
			}

			string as_player_string = "";
			if (as_player != null)
            {
				as_player_string = $" (as_player: {as_player})";
            }

			string note_string = "";
			if (note != null)
			{
				note_string = $" (note : {note})";
			}

			string action_string = $"action: {action}";

			return $"({time_string}{action_string}){as_player_string}{note_string}";
		}

		public override bool Equals(object obj)
		{
			var other = obj as Command;
			if (other == null)
				return false;

			return other.time == this.time &&
				other.action == this.action && 
				other.as_player == this.as_player &&
				other.note == this.note;
		}
	}

	// "(as Muhah) Dota 15:40 ++ (only if Dragon plays)"
	//        ||
	//        \/
	// ParseAsPlayer()
	//        ||
	// "Dota 15:40 ++ (only if Dragon plays)"
	// as "Muhah"
	//        ||
	//        \/
	// ParseNote()
	//        ||
	//        \/
	// "Dota 15:40 ++"
	// as "Muhah"
	// Note: "only if Dragon plays"
	//        ||
	//        \/
	// ParseAction()
	//        ||
	//        \/
	// Command: JoinGame
	// time: 15:40
	// as "Muhah"
	// Note: "only if Dragon plays"
	public static class ParseCommand
	{
		public static Command Parse(string str, DateTime now)
        {
			return ParseAsPlayer(str, now);
        }

		public static Command ParseAsPlayer(string str, DateTime now)
		{
			Regex as_player_regex = new Regex(@"\(\s*as\s(?<as_player>[^)]+)\s*\)", RegexOptions.IgnoreCase);
			Match match = as_player_regex.Match(str);

			if (match.Success)
            {
				string str_without_as_player = as_player_regex.Replace(str, "");
				Command ret = ParseNote(str_without_as_player, now);
				if (ret != null)
                {
					ret.as_player = match.Groups["as_player"].Value.Trim();
                }
				return ret;
			}

			return ParseNote(str, now);
		}

		public static Command ParseNote(string str, DateTime now)
        {
			Regex note_regex = new Regex(@"\((?<note>[^)]+)\)\s*$", RegexOptions.IgnoreCase);
			Match match = note_regex.Match(str);

			if (match.Success)
			{
				string str_without_as_player = note_regex.Replace(str, "");
				Command ret = ParseAction(str_without_as_player, now);
				if (ret != null)
				{
					ret.note = match.Groups["note"].Value.Trim();
				}
				return ret;
			}

			return ParseAction(str, now);
		}

		private static Command ParseAction(string str, DateTime now)
        {
			// "++" or "+1" or "dota ++"
			if (Regex.IsMatch(str, $@"^(?:\s*{CommandPrefixRegex})?\s*\+\+\s*$", RegexOptions.IgnoreCase) ||
				Regex.IsMatch(str, $@"^(?:\s*{CommandPrefixRegex})?\s*\+1\s*$", RegexOptions.IgnoreCase))
				return new Command { action = Command.Action.JoinLatestGame };
			// "--" or "-1" or "dota --"
			if (Regex.IsMatch(str, $@"^(?:\s*{CommandPrefixRegex})?\s*--\s*$", RegexOptions.IgnoreCase) || 
				Regex.IsMatch(str, $@"^(?:\s*{CommandPrefixRegex})?\s*\-1\s*$", RegexOptions.IgnoreCase))
				return new Command { action = Command.Action.RemoveAll };
			// "dota?"
			if (Regex.IsMatch(str, $@"^\s*{CommandPrefixRegex}\s*\?\s*$", RegexOptions.IgnoreCase))
				return new Command { action = Command.Action.ShowGames };

			// e.g. "dota 13:24 ++", "dota 12?", "dota 15--", "doto :30?"
			var add_remove = ParseAddRemove(str, now);
			if (add_remove != null)
				return add_remove;

			return null;
		}

		const string CommandPrefixRegex = @"(?:dota|dotka|doto|gramy)";
		static string TimeRegex(string hours_group_name, string minutes_group_name)
		{
			return $@"(?<{hours_group_name}>[0-9]?[0-9])?(?<{minutes_group_name}>(?:\.|:)[0-9][0-9])?";
		}

		static DateTime? ParseTime(string hours_string, string minutes_string, DateTime now)
        {
			int minute;
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
			if (!Int32.TryParse(hours_string, out hour))
			{
				if (hours_string == "" && minutes_string != "")
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

			var time = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);
			if (time < now - TimeSpan.FromMinutes(5))  // 5 minutes threshold added if someone wants to join a game that was just started
				time = time.AddDays(1);

			return time;
		}

		static string BuildRegex(params string?[] parts)
        {
			var elements = new List<string>();
			elements.Add("^");
			elements.AddRange(parts);
			elements.Add("$");
			return String.Join(@"\s*", elements);
		}

		static Command ParseAddRemove(string str, DateTime now)
		{
			string command_regex = @"(?<action>\+\+|--|\?||\+1|-1)";
			var regex = BuildRegex(
				CommandPrefixRegex, TimeRegex("hours", "minutes"), command_regex , "$");
			var match = Regex.Match(str, regex, RegexOptions.IgnoreCase);
			if (!match.Success)
				return null;

			var time = ParseTime(match.Groups["hours"].Value, match.Groups["minutes"].Value, now);
			if (!time.HasValue)
				return null;

			string as_player = null;
			if (match.Groups["as_player"].Value != "")
            {
				as_player = match.Groups["as_player"].Value;
            }

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

			return new Command { time = time.Value, action = action, as_player = as_player };
		}
	}
}
