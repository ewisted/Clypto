using Clypto.Server.Data.Models;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Clypto.Server.Data
{
    public interface IClipRepository
    {
		IQueryable<Clip> Get();
		IQueryable<Clip> Get(int offset, int take);
		Clip Get(string id);
		Clip Create(Clip clip);
		Task<ReplaceOneResult> Update(string id, Clip clipIn);
		void Remove(Clip clipIn);
		void Remove(string id);
		Task<List<Clip>> GetClipsByTags(params string[] tags);
		Task<List<Clip>> GetClipsByTags(IEnumerable<string> tags);
		IList<string> GetAllTags();
		Clip GetByCommand(string command);
		IQueryable<Clip> GetClipsByCommand(string command);
		IEnumerable<Clip> GetMostPlayedClips(int take = 0, int offset = 0);
		Task DropDbAsync();
	}
}
