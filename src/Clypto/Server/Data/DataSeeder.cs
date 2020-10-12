using Clypto.Server.Data.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Clypto.Server.Data
{
    public class DataSeeder
    {
		private readonly IClipRepository _db;

		public DataSeeder(IClipRepository db)
		{
			_db = db;
		}

		public async Task SeedDataAsync(string seedDataPath)
		{
			Log.Information("Beginning data seed");

			var jsonClips = GetClipsFromJson(seedDataPath);

			int clipsAddedCount = 0;
			int clipsUpdatedCount = 0;
			int clipsRemovedCount = 0;
			foreach (var jsonClip in jsonClips)
			{
				var dbClipsFromCommand = _db.GetClipsByCommand(jsonClip.Command).ToArray();

				// If there are multiple clips, remove all but the first
				// This fixes a temporary duplication issue
				if (dbClipsFromCommand.Count() > 1)
				{
					// Remove all but the first
					var removeClips = dbClipsFromCommand.Skip(1);

					// Replace the list with only the first entry
					dbClipsFromCommand = dbClipsFromCommand.Take(1).ToArray();

					// Remove all db records marked for removal
					foreach (var removeClip in removeClips)
					{
						_db.Remove(removeClip);
						clipsRemovedCount++;
					}
				}

				var existingClip = dbClipsFromCommand.FirstOrDefault();

				if (existingClip == null)
				{
					// If not exists, add a new record
					_db.Create(jsonClip);
					clipsAddedCount++;
				}
				else
				{
					// If exists, update record. Don't update Counter

					// Don't try this at home kids
					// This is a low effort way to deep copy the entire object instance.
					// Should probably use ICloneable or some Reflection magic instead. Maybe later - Kevin
					
					var updatedClip = JsonSerializer.Deserialize<Clip>(JsonSerializer.Serialize(jsonClip));

					// Keep the original counter
					updatedClip.Counter = existingClip.Counter;

					if (updatedClip.Id != existingClip.Id)
					{
						Log.Information("IDs differ for {name}", existingClip.Name);
						Log.Information("Removing {id}", existingClip.Id);
						_db.Remove(existingClip.Id);
						clipsRemovedCount++;
						Log.Information("Adding {id}", updatedClip.Id);
						_db.Create(updatedClip);
						clipsAddedCount++;
					}
					else
					{
						var result = await _db.Update(existingClip.Id, updatedClip);
						if (result.ModifiedCount > 0)
						{
							Log.Information("Updated clip record for {name}", existingClip.Name);
							clipsUpdatedCount++;
						}
					}
				}
			}

			Log.Information("{0} records added, {1} records updated, {2} records deleted, {3} records unchanged", clipsAddedCount, clipsUpdatedCount, clipsRemovedCount, jsonClips.Count() - clipsAddedCount - clipsUpdatedCount);
			//return Task.CompletedTask;
		}

		private IEnumerable<Clip> GetClipsFromJson(string seedDataPath)
		{
			var clips = JsonSerializer.Deserialize<Clip[]>(File.ReadAllText(seedDataPath));
			return clips;
		}
	}
}
