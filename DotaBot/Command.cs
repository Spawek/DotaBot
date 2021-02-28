using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using static DotaBot.Logger;

namespace DotaBot
{
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

	public static class ParseCommand
	{
		// Check tests when modyfying it.
		public static Command Parse(string str, DateTime now)
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
			var add_remove = ParseAddRemove(str, now);
			if (add_remove != null)
				return add_remove;

			return null;
		}

		static Command ParseAddRemove(string str, DateTime now)
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
	}
}
