using Clypto.Server.Data;
using Clypto.Server.Data.Models;
using Microsoft.Azure.Storage.Blob;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Clypto.Server.Services
{
	public class AzureBlobService
	{
		private readonly CloudBlobContainerAccessor _blobContainerAccessor;

		public AzureBlobService(CloudBlobContainerAccessor blobContainerAccessor)
		{
			_blobContainerAccessor = blobContainerAccessor;
		}

		/// <summary>
		/// Saves the clip to Azure blob storage and returns the clip object with BlobUrl and BlobName properties
		/// </summary>
		/// <param name="clip"></param>
		/// <param name="mp3Path"></param>
		/// <returns></returns>
		public async Task<Clip> SaveToBlobStorageAsync(Clip clip, string mp3Path)
		{
			var azureContainer = await _blobContainerAccessor.GetContainerAsync();
			var blob = azureContainer.GetBlockBlobReference($"{clip.Command}.mp3");
			var blobExists = await blob.ExistsAsync();

			// Grab the clipid in case the clip already exists
			string clipId = null;
			if (blobExists)
			{
				await blob.FetchAttributesAsync();
				blob.Metadata.TryGetValue("clipid", out clipId);
			}

			// Upload the new clip
			await blob.UploadFromFileAsync(mp3Path);

			// Replace the clipid with the original one. Otherwise, it gets overwritten
			if (blobExists)
			{
				blob.Metadata.Remove("clipid");
				blob.Metadata.Add("clipid", clipId);
				await blob.SetMetadataAsync();
			}

			await blob.FetchAttributesAsync();
			if (!string.IsNullOrWhiteSpace(clipId)) clip.Id = clipId;
			clip.BlobUrl = blob.Uri.OriginalString;
			clip.BlobName = blob.Name;

			// Update blob metadata with clipid
			await blob.FetchAttributesAsync();
			blob.Metadata.Remove("clipid");
			blob.Metadata.Add("clipid", clip.Id);
			await blob.SetMetadataAsync();

			return clip;
		}

		public async Task EnsureClipDownloadedAsync(Clip clip)
		{
			var clipPath = Path.Combine(Directory.GetCurrentDirectory(), "clips");
			var path = Path.Combine(clipPath, clip.FileName);

			if (!File.Exists(path))
			{
				await DownloadClip(clip);

			}
		}
		public async Task DeleteClip(Clip clip)
		{
			var container = await _blobContainerAccessor.GetContainerAsync();
			var blob = container.GetBlockBlobReference(clip.FileName);
			await blob.DeleteIfExistsAsync();
		}

		public async Task DownloadClip(Clip clip)
		{
			var clipPath = Path.Combine(Directory.GetCurrentDirectory(), "clips");

			Directory.CreateDirectory(clipPath);

			var container = await _blobContainerAccessor.GetContainerAsync();
			var blob = container.GetBlockBlobReference(clip.FileName);

			var path = Path.Combine(clipPath, clip.FileName);

			if (File.Exists(path))
			{
				File.Delete(path);
			}

			Log.Information("Downloading {url}", blob.Uri.OriginalString);
			await blob.DownloadToFileAsync(path, FileMode.OpenOrCreate);
		}

		public async Task DownloadAllClipsAsync()
		{
			var clipPath = Path.Combine(Directory.GetCurrentDirectory(), "clips");

			Log.Information("Downloading all existing clips to {clipPath} if not already exists", clipPath);

			Directory.CreateDirectory(clipPath);

			var container = await _blobContainerAccessor.GetContainerAsync();
			var blobs = await container.ListBlobsAsync();

			int downloadedCount = 0;
			foreach (var blob in blobs)
			{
				var name = ((CloudBlockBlob)blob).Name;
				var blockBlob = container.GetBlockBlobReference(name);
				await blockBlob.FetchAttributesAsync();
				var blockBlobBytesLength = blockBlob.Properties.Length;
				var path = Path.Combine(clipPath, HttpUtility.UrlEncode(name));

				// If the file doesn't exist, or if the byte size differs, pull it from azure
				if (!File.Exists(path) || new FileInfo(path).Length != blockBlobBytesLength)
				{
					if (File.Exists(path))
					{
						File.Delete(path);
					}

					Log.Information("Downloading {url}", blob.Uri.OriginalString);
					await blockBlob.DownloadToFileAsync(path, FileMode.OpenOrCreate);
					downloadedCount++;
				}
			}

			Log.Information("Downloading all existing clips complete. Downloaded {0}", downloadedCount);
		}
	}

	public static class AzureBlobExtensions
	{
		public static async Task<List<IListBlobItem>> ListBlobsAsync(this CloudBlobContainer container)
		{
			BlobContinuationToken continuationToken = null;
			List<IListBlobItem> results = new List<IListBlobItem>();
			do
			{
				var response = await container.ListBlobsSegmentedAsync(continuationToken);
				continuationToken = response.ContinuationToken;
				results.AddRange(response.Results);
			}
			while (continuationToken != null);
			return results;
		}
	}
}
