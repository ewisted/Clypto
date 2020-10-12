using Clypto.Server.Data.Models;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clypto.Server.Commands
{
	public static class EmbedFactory
	{
		/// <summary>
		/// Builds up to 6000 character embeds containing alphanumerically sorted columns of up to 1024 characters each for the clips that are passed in.
		/// </summary>
		/// <param name="clipsInDb"></param>
		/// <returns></returns>
		public static Task<List<DiscordEmbed>> GetListClipEmbeds(IEnumerable<Clip> clipsInDb)
		{
			// Define our list of embeds to return, as well as the first embed
			var embeds = new List<DiscordEmbed>();
			var builder = new DiscordEmbedBuilder();
			builder.WithColor(new DiscordColor(114, 137, 218));
			builder.WithTitle("These are the audio clips command you can use");
			// Define the main columns object as well as trackers for the loops previous state
			var columns = new Columns();
			var otherColumn = new StringBuilder();
			char lastStartingChar = 'a';
			string lastColumnTitle = "";
			var currentSB = new StringBuilder();

			// Iterate through each clip in the database, and sort it into the correct column
			foreach (var clip in clipsInDb)
			{
				// Define the first character of the clip name and the string we'll use to embed the clip name
				char firstChar = clip.Name[0];
				var name = clip.Name + "\n";
				// Check if the embed is getting close to the limits placed by Discord
				// Max of 6000 characters and no more than 25 columns (the checks are set under by a bit, just to be safe)
				if (columns.TotalLength + otherColumn.Length + currentSB.Length + name.Length >= 5900 || columns.LetterDescriptions.Count >= 22)
				{
					// If this gets hit, the embed needs to be added to the collection and reset
					// If the current string builder has clips in it, it also needs to be added to the collection and reset
					if (currentSB.Length > 0)
					{
						// If this is a continuation from a previous column, add "(Continued)" to the title of this one
						var newTitle = char.ToUpper(lastStartingChar).ToString();
						var title = lastColumnTitle == newTitle ? newTitle + " (Continued)" : newTitle;
						columns.LetterDescriptions.Add(title, currentSB);
						// Reset state
						currentSB = new StringBuilder();
						lastColumnTitle = newTitle;
					}
					// Add the columns to the current embed as fields, build it, and add it to the collection
					embeds.Add(builder
						.AddFieldsFromColumns(columns)
						.Build());
					// Reset state
					builder = new DiscordEmbedBuilder();
					builder.WithColor(new DiscordColor(114, 137, 218));
					builder.WithTitle("These are the audio clips command you can use (Continued)");
					columns = new Columns();
				}
				// If its not a recognized character, the clip name goes in the other column
				if (!Char.IsLetterOrDigit(firstChar))
				{
					otherColumn.Append(name);
					continue;
				}
				// If the first character of the clip name is a number, it should be added to the numbers column
				if (int.TryParse(firstChar.ToString(), out int result))
				{
					columns.NumberDescriptions.Append(name);
					// Update the total length of the embed for tracking
					columns.TotalLength += name.Length;
					continue;
				}
				// Check if we hit a new character or if the column length is too long
				// If it is, add the current column to the collection and reset it
				// Max column length on an embed field is 1024
				if (char.ToLower(lastStartingChar) != char.ToLower(firstChar) || currentSB.Length + name.Length >= 1024)
				{
					// Update the total length of the embed for tracking
					columns.TotalLength += currentSB.Length;
					// If this is a continuation from a previous column, add "(Continued)" to the title of this one
					var newTitle = char.ToUpper(lastStartingChar).ToString();
					var title = lastColumnTitle == newTitle ? newTitle + " (Continued)" : newTitle;
					columns.LetterDescriptions.Add(title, currentSB);
					// Reset state
					currentSB = new StringBuilder();
					lastColumnTitle = newTitle;
				}
				// Add the clip name to the current string builder
				// Since we've made it past all the previous filters, we can be confident this won't break any rules
				currentSB.Append(name);
				lastStartingChar = firstChar;
			}
			// Add the final alphanumeric column to the collection
			var lastTitle = char.ToUpper(lastStartingChar).ToString();
			var t = lastColumnTitle == lastTitle ? lastTitle + " (Continued)" : lastTitle;
			columns.LetterDescriptions.Add(t, currentSB);

			// Add anything that wasn't recognized to the end in an other column
			columns.OtherDescriptions = otherColumn;
			embeds.Add(builder
				.AddFieldsFromColumns(columns, true)
				.Build());
			// Return the embed messages to send out to Discord
			return Task.FromResult(embeds);
		}
	}

	public class Columns
	{
		// Total amount of characters between all column descriptions
		public int TotalLength { get; set; }
		// Dictionary with the column titles as keys and the clips as values
		public Dictionary<string, StringBuilder> LetterDescriptions { get; set; }
		// Clips that begin with a number
		public StringBuilder NumberDescriptions { get; set; }
		// Clips that begin with an unrecognized character
		public StringBuilder OtherDescriptions { get; set; }

		public Columns()
		{
			TotalLength = 0;
			LetterDescriptions = new Dictionary<string, StringBuilder>();
			NumberDescriptions = new StringBuilder();
			OtherDescriptions = new StringBuilder();
		}
	}

	public static class EmbedBuilderExtensions
	{
		/// <summary>
		/// Takes string builder "columns" and builds them into embed fields to be added to the current embed
		/// </summary>
		/// <param name="builder"></param>
		/// <param name="title"></param>
		/// <param name="columns"></param>
		/// <returns></returns>
		public static DiscordEmbedBuilder AddFieldsFromColumns(this DiscordEmbedBuilder builder, Columns columns, bool isFinalEmbed = false)
		{
			// If the number column is populated, add it to the embed
			if (columns.NumberDescriptions.Length > 0)
			{
				builder.AddField("0-9", columns.NumberDescriptions.ToString(), true);
			}
			// Iterate through each letter-column and add them to the embed
			foreach (var col in columns.LetterDescriptions)
			{
				builder.AddField(col.Key, col.Value.ToString(), true);
			}
			// If this will be the last embed message send, add the other field
			if (isFinalEmbed)
			{
				builder.AddField("Other", columns.OtherDescriptions.ToString(), true);
			}

			return builder;
		}
	}
}
