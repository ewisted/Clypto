﻿using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Clypto.Server.Data.Models
{
    public class Clip
    {
		[BsonId]
		[BsonRepresentation(BsonType.ObjectId)]
		public string Id { get; set; }
		public string Name { get; set; }
		public string Command { get; set; }
		public IEnumerable<string> Aliases { get; set; }
		public string Description { get; set; }
		public string CreatedBy { get; set; }
		public DateTime CreatedOnUtc { get; set; }
		public string ModifiedBy { get; set; }
		public DateTime ModifiedOnUtc { get; set; }
		//[BsonIgnore] TODO delete this row from all mongo instances
		//[JsonIgnore]
		public int ClipLengthMs
		{
			get
			{
				return OriginalEndTimeMs - OriginalStartTimeMs;
			}
			set { }
		}
		public string YoutubeId { get; set; }
		public int OriginalStartTimeMs { get; set; }
		public int OriginalEndTimeMs { get; set; }
		public int Counter { get; set; }
		public IEnumerable<string> Tags { get; set; }
		public string FileName { get; set; }
		public string BlobName { get; set; }
		public string BlobUrl { get; set; }
	}
}
