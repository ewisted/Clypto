using System.Text.Json;
using Serilog;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Clypto.Server.Data.Models;

namespace Clypto.Server.Data
{
    public class SeedDataGenerator
    {
		private readonly IClipRepository _db;

		public SeedDataGenerator(IClipRepository db)
		{
			_db = db;
		}

		public Task CreateSeedDataFile(string seedDataFilePath)
		{
			var clips = GetSeedDataClipsFromDb();

			Log.Information("Total Clips in Prod DB: {clipCount}", clips.Count());

			clips = clips.Select(c =>
			{
				c.Name = Path.GetFileNameWithoutExtension(c.Name);
				c.BlobName = $"{c.Name}.mp3";
				c.FileName = c.BlobName;
				c.CreatedBy = c.CreatedBy;
				c.ModifiedBy = string.IsNullOrWhiteSpace(c.ModifiedBy) ? c.CreatedBy : c.ModifiedBy;
				c.ModifiedOnUtc = c.ModifiedOnUtc == default ? c.CreatedOnUtc : c.ModifiedOnUtc;
				return c;
			});
			var json = JsonSerializer.Serialize(clips, new JsonSerializerOptions
			{
				WriteIndented = true
			});
			File.WriteAllText(seedDataFilePath, json);
			return Task.CompletedTask;
		}
		private IEnumerable<Clip> GetSeedDataClipsFromDb()
		{
			var dbClips = _db.Get();
			return dbClips;
		}
	}
}
