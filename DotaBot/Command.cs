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
		public enum Action { Add, Remove, JoinLatestGame, RemoveAll, ShowGames, RescheduleProposal };

		public DateTime time;  // used by: Add, Remove, RescheduleProposal
		public DateTime time2;  // used by: RescheduleProposal 
		public string as_player;  // used by: Add, Remove
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

			string as_player_string = "";
			if (as_player != null)
            {
				as_player_string = $" (as_player: {as_player})";
            }

			string action_string = $"action: {action}";

			return $"({time_string}{time2_string}action: {action}){as_player_string}";
		}

		public override bool Equals(object obj)
		{
			var other = obj as Command;
			if (other == null)
				return false;

			return other.time == this.time &&
				other.action == this.action && 
				other.as_player == this.as_player;
		}
	}

	// Check tests when modyfying it.
	public static class ParseCommand
	{
		public static Command Parse(string str, DateTime now)
		{
			string as_player_regex = @"\(\s*as\s(?<as_player>[^)]+)\s*\)";
			Match match = Regex.Match(str, as_player_regex, RegexOptions.IgnoreCase);

			if (match.Success)
            {
				string str_without_as_player = Regex.Replace(str, as_player_regex, "", RegexOptions.IgnoreCase);
				Command ret = ParseInternal(str_without_as_player, now);
				if (ret != null)
                {
					ret.as_player = match.Groups["as_player"].Value.Trim();
                }
				return ret;
			}

			return ParseInternal(str, now);
		}

		// TODO: rename
		private static Command ParseInternal(string str, DateTime now)
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

			// e.g. "dota 16 -> 17:40?"
			var reschedule = ParseReschedule(str, now);
			if (reschedule != null)
				return reschedule;

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

		static Command ParseReschedule(string str, DateTime now)
		{
			var regex = String.Join(@"\s*", new string[] {
				CommandPrefixRegex, TimeRegex("from_hours", "from_minutes"), "->" , TimeRegex("to_hours", "to_minutes"), @"\?", "$"});
			var match = Regex.Match(str, regex, RegexOptions.IgnoreCase);
			if (!match.Success)
				return null;

			var from_time = ParseTime(match.Groups["from_hours"].Value, match.Groups["from_minutes"].Value, now);
			if (!from_time.HasValue)
				return null;

			var to_time = ParseTime(match.Groups["to_hours"].Value, match.Groups["to_minutes"].Value, now);
			if (!to_time.HasValue)
				return null;

			return new Command { action = Command.Action.RescheduleProposal, time = from_time.Value, time2 = to_time.Value};
		}
	}
}
