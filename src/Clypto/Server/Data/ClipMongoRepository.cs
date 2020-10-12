using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Clypto.Server.Data.Models;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Clypto.Shared;

namespace Clypto.Server.Data
{
    public class ClipMongoRepository : IClipRepository
    {
		private readonly IMongoDatabase _db;
		private IMongoCollection<Clip> _clips;

		public ClipMongoRepository(string connectionString, string dbName)
		{
			connectionString.GuardNotNull();
			dbName.GuardNotNull();

			var client = new MongoClient(connectionString);
			_db = client.GetDatabase(dbName);

			_clips = _db.GetCollection<Clip>("Clips");
		}

		public async Task DropDbAsync()
		{
			await _clips.Database.DropCollectionAsync("Clips");
			_clips = _db.GetCollection<Clip>("Clips");
		}

		public ClipMongoRepository(MongoClientSettings settings, string dbName)
		{
			settings.GuardNotNull();
			dbName.GuardNotNull();

			var client = new MongoClient(settings);
			var database = client.GetDatabase(dbName);

			_clips = database.GetCollection<Clip>("Clips");
		}

		public ClipMongoRepository(MongoUrl mongoUrl, string dbName)
		{
			mongoUrl.GuardNotNull();
			dbName.GuardNotNull();

			var client = new MongoClient(mongoUrl);
			var database = client.GetDatabase(dbName);

			_clips = database.GetCollection<Clip>("Clips");
		}

		public ClipMongoRepository(IConfiguration config) :
			this(config.GetConnectionString("ClyptoMongo"), config.GetValue<string>("MongoDbName"))
		{
			config.GuardNotNull();
			config.GetConnectionString("ClyptoMongo").GuardNotNull();
			config.GetValue<string>("MongoDbName").GuardNotNull();
		}

		public IQueryable<Clip> Get()
		{
			return _clips.AsQueryable().OrderBy(c => c.Name);
		}

		public IQueryable<Clip> Get(int offset, int take)
		{
			return Get().Skip(offset).Take(take);
		}

		public Task<List<Clip>> GetClipsByTags(params string[] tags) => GetClipsByTags(tags as IEnumerable<string>);

		public async Task<List<Clip>> GetClipsByTags(IEnumerable<string> tags)
		{
			var filter = new FilterDefinitionBuilder<Clip>().AnyIn(c => c.Tags, tags);
			return (await _clips.FindAsync(filter)).ToList();
		}

		public IList<string> GetAllTags()
		{
			return _clips
				.AsQueryable<Clip>()
				.Select(c => c.Tags)
				.SelectMany(l => l.ToList())
				.Distinct()
				.OrderBy(c => c)
				.ToList();
		}

		public Clip GetByCommand(string command)
		{
			return GetClipsByCommand(command).FirstOrDefault();
		}

		public IQueryable<Clip> GetClipsByCommand(string command)
		{
			return _clips.AsQueryable().Where(c => c.Command == command);
		}

		public Clip Get(string id)
		{
			return _clips.Find<Clip>(clip => clip.Id == id).FirstOrDefault();
		}

		public Clip Create(Clip clip)
		{
			_clips.InsertOne(clip);
			return clip;
		}

		public async Task<ReplaceOneResult> Update(string id, Clip clipIn)
		{
			return await _clips.ReplaceOneAsync(
				filter: new BsonDocument("_id", ObjectId.Parse(id)),
				options: new ReplaceOptions { IsUpsert = true },
				replacement: clipIn);
		}

		public void Remove(Clip clipIn)
		{
			_clips.DeleteOne(clip => clip.Id == clipIn.Id);
		}

		public void Remove(string id)
		{
			_clips.DeleteOne(clip => clip.Id == id);
		}

		public IEnumerable<Clip> GetMostPlayedClips(int take = 0, int offset = 0)
		{
			var clipQuery = _clips.AsQueryable().OrderByDescending(c => c.Counter);
			if (take != 0)
			{
				clipQuery = clipQuery.Take(take) as IOrderedMongoQueryable<Clip>;
			}
			if (offset != 0)
			{
				clipQuery = clipQuery.Skip(offset) as IOrderedMongoQueryable<Clip>;
			}
			return clipQuery;
		}
	}
}
