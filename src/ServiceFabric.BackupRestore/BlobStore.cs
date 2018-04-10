using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage;
using System.IO;
using Newtonsoft.Json;
using System.Globalization;

namespace ServiceFabric.BackupRestore
{
    public class BlobStore : ICentralBackupStore
    {
        public const string RootFolder = "root";
        public const string QueueFolder = "Queue";
        
        private readonly string _blobStorageConnectionString;
        private readonly string _blobContainerName;
        private bool _isInitialized;
        private CloudBlobClient _blobClient;
        private CloudBlobContainer _blobContainer;

        /// <summary>
        /// Gets the blob client, for test purposes.
        /// </summary>
        internal CloudBlobClient BlobClient => _blobClient;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="blobStorageConnectionString"></param>
        /// <param name="blobContainer"></param>
        public BlobStore(string blobStorageConnectionString, string blobContainer = "servicefabricbackups")
        {
            if (string.IsNullOrWhiteSpace(blobStorageConnectionString))
            {
                throw new ArgumentException(nameof(blobStorageConnectionString));
            }

            if (string.IsNullOrWhiteSpace(blobContainer))
            {
                throw new ArgumentException(nameof(blobContainer));
            }

            _blobStorageConnectionString = blobStorageConnectionString;
            _blobContainerName = blobContainer.ToLowerInvariant();
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="blobContainer"></param>
        /// <param name="cloudBlobClient"></param>
        public BlobStore(CloudBlobClient cloudBlobClient, string blobContainer = "servicefabricbackups")
        {
            _blobClient = cloudBlobClient ?? throw new ArgumentNullException(nameof(cloudBlobClient));
            if (string.IsNullOrWhiteSpace(blobContainer))
            {
                throw new ArgumentException(nameof(blobContainer));
            }
            _blobContainerName = blobContainer.ToLowerInvariant();
        }

        /// <summary>
        /// Downloads the backup files for the backup identified by <paramref name="backupId"/> from blob storage.
        /// </summary>
        /// <param name="backupId"></param>
        /// <param name="destinationDirectory"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task DownloadBackupFolderAsync(Guid backupId, string destinationDirectory, CancellationToken cancellationToken)
        {
            if (!_isInitialized)
            {
               await InitializeAsync().ConfigureAwait(false);
            }
            try
            {
                //find the metadada
                var infos = (await GetBackupMetadataPrivateAsync(backupId).ConfigureAwait(false)).ToList();
                if (infos.Count != 1)
                    throw new Exception($"Found {infos.Count} backups with id {backupId.ToString("N")}");
                var info = infos[0];

                //download the backup from blobstore to the local node
                string subFolder = info.BlockBlobUri.PrimaryUri.AbsolutePath.Substring(info.BlockBlobUri.PrimaryUri.AbsolutePath.IndexOf(RootFolder, StringComparison.Ordinal));
                var backupFolder = _blobContainer.GetDirectoryReference(subFolder);
                await DownloadFolderAsync(backupFolder, destinationDirectory, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to download backup", ex);
            }
        }
              
        /// <inheritdoc />
        public async Task<IEnumerable<BackupMetadata>> GetBackupMetadataAsync(Guid? backupId = null, Guid? servicePartitionId = null)
        {
            if (!_isInitialized)
            {
                await InitializeAsync().ConfigureAwait(false);
            }
            var result = await GetBackupMetadataPrivateAsync(backupId, servicePartitionId);
            return result.Select(x => new BackupMetadata(x.OriginalServicePartitionId, x.TimeStampUtc, x.BackupOption, x.BackupId));
        }

        /// <inheritdoc />
        public async Task<BackupMetadata> RetrieveScheduledBackupAsync(Guid servicePartitionId)
        {
            if (!_isInitialized)
            {
                await InitializeAsync().ConfigureAwait(false);
            }

            BackupMetadata backup = null;
            //retrieve the backup to restore for the provided partition
            var directoryReference = _blobContainer.GetDirectoryReference($"{RootFolder}/{QueueFolder}");
            var fileReference = directoryReference.GetBlockBlobReference(servicePartitionId.ToString("N"));
            if (!(await fileReference.ExistsAsync()))
            {
                throw new Exception($"Backup for partition {servicePartitionId:N} was not found in the queue.");
            }

            string content = await fileReference.DownloadTextAsync();
            Guid id;
            if (Guid.TryParse(content, out id))
            {
                backup = (await GetBackupMetadataAsync(id)).SingleOrDefault();
                if (!(await fileReference.ExistsAsync()))
                {
                    throw new Exception($"Backup for partition {servicePartitionId:N} with id {id:N} was not found in the metadata. (Corruption)");
                }
            }
            await fileReference.DeleteAsync();
            return backup;
        }

        /// <inheritdoc />
		[Obsolete("Naming issue. Call 'ScheduleBackupRestoreAsync'.")]
        public Task ScheduleBackupAsync(Guid servicePartitionId, Guid backupId)
        {
            return ScheduleBackupRestoreAsync(servicePartitionId, backupId);
        }

        /// <inheritdoc />
        public async Task ScheduleBackupRestoreAsync(Guid servicePartitionId, Guid backupId)
        {
            if (!_isInitialized)
            {
                await InitializeAsync().ConfigureAwait(false);
            }

            //remember which backup to restore for which partition
            var directoryReference = _blobContainer.GetDirectoryReference($"{RootFolder}/{QueueFolder}");
            var fileReference = directoryReference.GetBlockBlobReference(servicePartitionId.ToString("N"));
            await fileReference.UploadTextAsync(backupId.ToString("N"));
        }



        /// <summary>
        /// Stores the provided metadata as blob and as blob metadata.
        /// </summary>
        /// <param name="destinationFolder"></param>
        /// <param name="info"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task StoreBackupMetadataAsync(string destinationFolder, BackupMetadata info, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!_isInitialized)
            {
                await InitializeAsync().ConfigureAwait(false);
            }

            string destFileName = $"{destinationFolder}/{FileStore.ServiceFabricBackupRestoreMetadataFileName}";
            string json = JsonConvert.SerializeObject(info);
            var blockBlobReference = _blobContainer.GetBlockBlobReference(destFileName);
            await blockBlobReference.UploadTextAsync(json, cancellationToken);

            blockBlobReference.Metadata.Add(nameof(BackupMetadata.BackupId), info.BackupId.ToString("N"));
            blockBlobReference.Metadata.Add(nameof(BackupMetadata.BackupOption), info.BackupOption.ToString());
            blockBlobReference.Metadata.Add(nameof(BackupMetadata.OriginalServicePartitionId), info.OriginalServicePartitionId.ToString("N"));
            blockBlobReference.Metadata.Add(nameof(BackupMetadata.TimeStampUtc), info.TimeStampUtc.ToString("o"));

            await blockBlobReference.SetMetadataAsync();
        }

        /// <inheritdoc />
        public async Task<BackupMetadata> UploadBackupFolderAsync(BackupOption backupOption, Guid servicePartitionId, string sourceDirectory, CancellationToken cancellationToken)
        {
            if (!_isInitialized)
            {
                await InitializeAsync().ConfigureAwait(false);
            }

            //use folder names containing the service partition and the utc date:
            var timeStamp = DateTime.UtcNow;
            string destinationFolder = CreateDateTimeFolderName(servicePartitionId, timeStamp);

            //upload
            await UploadFolderAsync(sourceDirectory, destinationFolder, cancellationToken);

            //create metadata to return
            var info = new BackupMetadata(servicePartitionId, timeStamp, backupOption);

            //store the backup id.
            await StoreBackupMetadataAsync(destinationFolder, info, cancellationToken);
            return info;
        }

        /// <summary>
        /// Deletes the container. (For test purposes.)
        /// </summary>
        internal void DeleteContainer()
        {            
            _blobContainer.DeleteIfExists();
        }

        /// <summary>
		/// Creates a folder name based on <paramref name="servicePartitionId"/> and timestamp.
		/// </summary>
		/// <param name="servicePartitionId"></param>
		/// <param name="timeStamp"></param>
		/// <returns></returns>
		internal string CreateDateTimeFolderName(Guid servicePartitionId, DateTime timeStamp)
        {
            return $"{RootFolder}/{servicePartitionId:N}/{timeStamp:yyyyMMddhhmmss}";
        }
        
        /// <summary>
		/// Queries all metadata, optionally filtered by <paramref name="backupId"/> and <paramref name="servicePartitionId"/>.
		/// </summary>
		/// <param name="backupId"></param>
		/// <param name="servicePartitionId"></param>
		/// <returns></returns>
		internal async Task<IEnumerable<BlobBackupMetadata>> GetBackupMetadataPrivateAsync(Guid? backupId = null, Guid? servicePartitionId = null)
        {
            BlobContinuationToken token = null;
            var metadata = new List<BlobBackupMetadata>();
            while (true)
            {
                var blobRequestOptions = new BlobRequestOptions();
                var rootFolder = _blobContainer.GetDirectoryReference(RootFolder);
                var query = await rootFolder.ListBlobsSegmentedAsync(true, BlobListingDetails.Metadata, null, token, blobRequestOptions, null);
                foreach (var blob in query.Results.Where(f => f.Uri.AbsoluteUri.EndsWith(FileStore.ServiceFabricBackupRestoreMetadataFileName)))
                {
                    if (!(blob is CloudBlockBlob cloudBlockBlob))
                    {
                        continue;
                    }
                    cloudBlockBlob.Metadata.TryGetValue(nameof(BackupMetadata.BackupId), out string bckpId);
                    Debug.Assert(bckpId != null, nameof(bckpId) + " != null");
                    
                    cloudBlockBlob.Metadata.TryGetValue(nameof(BackupMetadata.BackupOption), out string backupOption);
                    Debug.Assert(backupOption != null, nameof(backupOption) + " != null");

                    cloudBlockBlob.Metadata.TryGetValue(nameof(BackupMetadata.OriginalServicePartitionId), out string originalServicePartitionId);
                    Debug.Assert(originalServicePartitionId != null, nameof(originalServicePartitionId) + " != null");

                    cloudBlockBlob.Metadata.TryGetValue(nameof(BackupMetadata.TimeStampUtc), out string timeStampUtc);

                    BlobBackupMetadata element = new BlobBackupMetadata(

                        new BlobStorageUri(blob.Parent.StorageUri),
                        Guid.Parse(originalServicePartitionId), 
                        DateTime.ParseExact(timeStampUtc, "o", CultureInfo.InvariantCulture), 
                        (BackupOption)Enum.Parse(typeof(BackupOption),backupOption), 
                        Guid.Parse(bckpId));

                    if ((backupId == null || backupId.Value == element.BackupId)
                        && (servicePartitionId == null || servicePartitionId.Value == element.OriginalServicePartitionId))
                    {
                        metadata.Add(element);
                    }
                }

                if (query.ContinuationToken == null)
                {
                    break;
                }
                token = query.ContinuationToken;
            }
            return metadata;          
        }

        /// <summary>
        /// Prepares this instance for use.
        /// </summary>
        /// <returns></returns>
        internal async Task InitializeAsync()
        {
            if (_isInitialized) return;

            if (_blobClient == null)
            {
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(_blobStorageConnectionString);
                _blobClient = storageAccount.CreateCloudBlobClient();
                _blobClient.DefaultRequestOptions.RetryPolicy = new Microsoft.WindowsAzure.Storage.RetryPolicies.ExponentialRetry(TimeSpan.FromSeconds(1), 10);
                _blobClient.DefaultRequestOptions.AbsorbConditionalErrorsOnRetry = true;
            }

            _blobContainer = _blobClient.GetContainerReference(_blobContainerName);
            await _blobContainer.CreateIfNotExistsAsync();            
            _isInitialized = true;
        }

        /// <summary>
        /// Downloads the backup folder structure, from blob storage to the local node
        /// </summary>
        /// <param name="location"></param>
        /// <param name="destinationDirectory"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task DownloadFolderAsync(CloudBlobDirectory location, string destinationDirectory, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(destinationDirectory);

            BlobContinuationToken token = null;
            while (true)
            {
                var blobRequestOptions = new BlobRequestOptions();
                var query = await location.ListBlobsSegmentedAsync(false, BlobListingDetails.Metadata, null, token, blobRequestOptions, null, cancellationToken);
                foreach (var blob in query.Results)
                {
                    switch (blob)
                    {
                        case CloudBlobDirectory dir:
                            string subFolder = dir.Uri.Segments.Last();
                            await DownloadFolderAsync(dir, Path.Combine(destinationDirectory, subFolder), cancellationToken);
                            break;
                        case CloudBlockBlob file:
                            string fileName = Path.GetFileName(file.Name);
                            Debug.Assert(fileName != null, nameof(fileName) + " != null");

                            await file.DownloadToFileAsync(Path.Combine(destinationDirectory, fileName), FileMode.Create, cancellationToken);
                            break;
                    }
                }
                if (query.ContinuationToken == null) return;
                token = query.ContinuationToken;
            }
        }


        /// <summary>
        /// Uploads backup folder structure in <paramref name="sourceFolder"/> to <paramref name="destinationFolder"/> in blob storage.
        /// </summary>
        /// <param name="sourceFolder"></param>
        /// <param name="destinationFolder"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<string> UploadFolderAsync(string sourceFolder, string destinationFolder,
            CancellationToken cancellationToken)
        {
            //copy all subfolders
            foreach (string directory in Directory.EnumerateDirectories(sourceFolder))
            {
                await UploadFolderAsync(directory, $"{destinationFolder}/{new DirectoryInfo(directory).Name}", cancellationToken);
            }

            //copy all files
            foreach (string sourceFileName in Directory.EnumerateFiles(sourceFolder))
            {
                if (sourceFileName == null) continue;
                string destFileName = $"{destinationFolder}/{Path.GetFileName(sourceFileName)}";
                var blockBlobReference = _blobContainer.GetBlockBlobReference(destFileName);
                await blockBlobReference.UploadFromFileAsync(sourceFileName, cancellationToken);
            }

            return destinationFolder;
        }
    }
}
