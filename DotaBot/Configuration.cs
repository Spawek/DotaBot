using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace DotaBot
{
    static class Configuration
    {

		static IConfigurationRoot config_file = new ConfigurationBuilder()
			.SetBasePath(System.IO.Directory.GetCurrentDirectory())
			.AddJsonFile("appsettings.json")
			.Build();

		public static string GetConfig(string key)
		{
			var env = Environment.GetEnvironmentVariable($"APPSETTING_{key}");
			if (env != null)
				return env;

			return config_file[$"AppSettings:{key}"];
		}

		public static string GetConnectionString(string key)
		{
			var env = Environment.GetEnvironmentVariable($"SQLAZURECONNSTR_{key}");
			if (env != null)
				return env;

			return config_file[$"ConnectionStrings:{key}"];
		}
	}
}
