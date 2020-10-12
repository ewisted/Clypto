using Microsoft.Azure.Storage.Blob;
using System.Threading;
using System.Threading.Tasks;

namespace Clypto.Server
{
    public class CloudBlobContainerAccessor
    {
		private readonly CloudBlobClient _client;
		private readonly SemaphoreSlim _semaphore;

		public CloudBlobContainerAccessor(CloudBlobClient client)
		{
			_client = client;
			_semaphore = new SemaphoreSlim(1, 1);
		}

		// Thread safe async singleton property accessor
		private CloudBlobContainer _blobContainer;
		public async Task<CloudBlobContainer> GetContainerAsync()
		{
			await _semaphore.WaitAsync();
			try
			{
				if (_blobContainer != null)
				{
					return _blobContainer;
				}
				else
				{
					var container = _client.GetContainerReference("soundboard-clips");
					await container.CreateIfNotExistsAsync();
					_blobContainer = container;
					return container;
				}
			}
			finally
			{
				_semaphore.Release();
			}
		}
	}
}
