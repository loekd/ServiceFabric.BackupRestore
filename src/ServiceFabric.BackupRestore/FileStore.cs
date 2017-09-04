using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data;
using Newtonsoft.Json;

namespace ServiceFabric.BackupRestore
{
	/// <summary>
	/// Implementation of <see cref="ICentralBackupStore"/> that stores files on disk. 
	/// Make sure to use a file share outside the cluster! (E.g. a network share on a file server.)
	/// </summary>
	public class FileStore : ICentralBackupStore
	{
		private readonly string _remoteFolderName;

		/// <summary>
		/// The name of a file that contains ServiceFabric.BackupRestore related metadata about a backup.
		/// </summary>
		public const string ServiceFabricBackupRestoreMetadataFileName = "backuprestore.metadata";

		/// <summary>
		/// The name of a file that contains ServiceFabric related metadata about a backup.
		/// </summary>
		public const string BackupMetadataFileName = "backup.metadata";

        /// <summary>
        /// The name of a file that contains ServiceFabric related metadata about a actor backup
        /// </summary>
	    public const string ActorBackupMetadataFileName = "restore.dat";

        /// <summary>
        /// The name of a file that contains ServiceFabric related metadata about an incremental backup.
        /// </summary>
        public const string IncrementalBackupMetadataFileName = "incremental.metadata"; 

		/// <summary>
		/// Creates a new instance.
		/// </summary>
		/// <param name="remoteFolderName">A shared folder that is not on any of the cluster nodes.
		/// (Except maybe for local debugging)</param>
		public FileStore(string remoteFolderName)
		{
			if (string.IsNullOrWhiteSpace(remoteFolderName))
				throw new ArgumentException("Value cannot be null or whitespace.", nameof(remoteFolderName));

			Directory.CreateDirectory(remoteFolderName);
			_remoteFolderName = remoteFolderName;
		}

		/// <inheritdoc />
		public async Task<BackupMetadata> UploadBackupFolderAsync(BackupOption backupOption, Guid servicePartitionId, string sourceDirectory,
			CancellationToken cancellationToken)
		{
			//use folder names containing the service partition and the utc date:
			var timeStamp = DateTime.UtcNow;
			string destinationFolder = CreateDateTimeFolderName(servicePartitionId, timeStamp);

			//upload
			await CopyFolderAsync(sourceDirectory, destinationFolder, cancellationToken);
			
			//create metadata to return
			var info = new BackupMetadata(servicePartitionId, timeStamp, backupOption);
			
			//store the backup id.
			await StoreBackupMetadataAsync(destinationFolder, info);
			return info;
		}

		
		/// <inheritdoc />
		public async Task DownloadBackupFolderAsync(Guid backupId, string destinationDirectory,
			CancellationToken cancellationToken)
		{
			//find the metadada
			var infos = await GetBackupMetadataPrivateAsync(backupId);
			var info = infos.Single();
			//copy the backup to the node
			await CopyFolderAsync(Path.Combine(_remoteFolderName, info.RelativeFolder.TrimStart('\\', '/')), destinationDirectory, cancellationToken);
		}

		/// <inheritdoc />
		public async Task<IEnumerable<BackupMetadata>> GetBackupMetadataAsync(Guid? backupId = null, Guid? servicePartitionId = null)
		{
			//get all metadata
			var metadata = await GetBackupMetadataPrivateAsync(backupId, servicePartitionId);
			return metadata.Select(m => new BackupMetadata(m.OriginalServicePartitionId, m.TimeStampUtc, m.BackupOption, m.BackupId));
		}

        /// <inheritdoc />
        [Obsolete("Naming issue. Call 'ScheduleBackupRestoreAsync'.")]
        public Task ScheduleBackupAsync(Guid servicePartitionId, Guid backupId)
        {
            return ScheduleBackupRestoreAsync(servicePartitionId, backupId);
        }

        /// <inheritdoc />
        public Task ScheduleBackupRestoreAsync(Guid servicePartitionId, Guid backupId)
        {
            //remember which backup to restore for which partition
            string queueFile = GetQueueFile(servicePartitionId);
			File.WriteAllText(queueFile, backupId.ToString("N"));
			return Task.FromResult(true);
		}

		/// <inheritdoc />
		public async Task<BackupMetadata> RetrieveScheduledBackupAsync(Guid servicePartitionId)
		{
			BackupMetadata backup = null;
			//retrieve the backup to restore for the provided partition
			string queueFile = GetQueueFile(servicePartitionId);
			if (!File.Exists(queueFile)) return null;

			string content = File.ReadAllText(queueFile);
			Guid id;
			if (Guid.TryParse(content, out id))
			{
				backup = (await GetBackupMetadataAsync(id)).Single();
			}
			File.Delete(queueFile);
			return backup;
		}

		/// <inheritdoc />
		public Task StoreBackupMetadataAsync(string destinationFolder, BackupMetadata info, CancellationToken cancellationToken = default(CancellationToken))
		{
			return Task.Run(() =>
			{
				string file = Path.Combine(destinationFolder, ServiceFabricBackupRestoreMetadataFileName);
				string json = JsonConvert.SerializeObject(info);
				File.WriteAllText(file, json);
			});
		}

		/// <summary>
		/// Queries all metadata, optionally filtered by <paramref name="backupId"/>.
		/// </summary>
		/// <param name="backupId"></param>
		/// <param name="servicePartitionId"></param>
		/// <returns></returns>
		internal Task<IEnumerable<FileBackupMetadata>> GetBackupMetadataPrivateAsync(Guid? backupId = null, Guid? servicePartitionId = null)
		{
			return Task.Run(() =>
			{
				var query = from d in Directory.GetDirectories(_remoteFolderName, "*", SearchOption.AllDirectories)
					let dirInfo = new DirectoryInfo(d)
					let dirParentInfo = dirInfo.Parent
					let metadata = BackupMetadataFromDirectory(d)
					where (File.Exists(Path.Combine(d, BackupMetadataFileName))				//find folders with a SF metadata file
					        || File.Exists(Path.Combine(d, ActorBackupMetadataFileName))
                            || File.Exists(Path.Combine(d, IncrementalBackupMetadataFileName)))
						  && (backupId == null || metadata?.BackupId == backupId.Value)     //and optionally with the provided backup id
						  && (servicePartitionId == null || metadata?.OriginalServicePartitionId == servicePartitionId.Value)     //and optionally with the servicePartitionId
					orderby dirInfo.Parent?.Name, dirInfo.Name
					select
					new FileBackupMetadata(//build a metadata description
 
						d.Replace(_remoteFolderName, string.Empty), metadata.OriginalServicePartitionId, metadata.TimeStampUtc, metadata.BackupOption, metadata.BackupId);       

				return query;
			});
		}

		/// <summary>
		/// Finds the single backup metadata file in the folder, deserializes the content to an instance of <see cref="BackupMetadata"/>.
		/// </summary>
		/// <param name="directory"></param>
		/// <returns></returns>
		internal static BackupMetadata BackupMetadataFromDirectory(string directory)
		{
			string file = Path.Combine(directory, ServiceFabricBackupRestoreMetadataFileName);
			if (!File.Exists(file)) return null;

			string json = File.ReadAllText(file);
			var metadata =JsonConvert.DeserializeObject<BackupMetadata>(json);
			return metadata;
		}
		
		/// <summary>
		/// Creates a folder name based on <paramref name="servicePartitionId"/> and timestamp.
		/// </summary>
		/// <param name="servicePartitionId"></param>
		/// <param name="timeStamp"></param>
		/// <returns></returns>
		internal string CreateDateTimeFolderName(Guid servicePartitionId, DateTime timeStamp)
		{
			return Path.Combine(_remoteFolderName, servicePartitionId.ToString("N"), timeStamp.ToString("yyyyMMddhhmmss"));
		}

		/// <summary>
		/// Performs a deep copy from <paramref name="sourceFolder"/> to <paramref name="destinationFolder"/>.
		/// </summary>
		/// <param name="sourceFolder"></param>
		/// <param name="destinationFolder"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		private static Task<string> CopyFolderAsync(string sourceFolder, string destinationFolder,
			CancellationToken cancellationToken)
		{
			Directory.CreateDirectory(destinationFolder);
			return Task.Run(() =>
			{
				//copy all subfolders
				foreach (string directory in Directory.EnumerateDirectories(sourceFolder))
				{
					CopyFolderAsync(directory, Path.Combine(destinationFolder, new DirectoryInfo(directory).Name), cancellationToken)
						.ConfigureAwait(false)
						.GetAwaiter()
						.GetResult();
				}

				//copy all files
				foreach (string sourceFileName in Directory.EnumerateFiles(sourceFolder))
				{
					if (sourceFileName == null) continue;
					string destFileName = Path.Combine(destinationFolder, Path.GetFileName(sourceFileName));
					File.Copy(sourceFileName, destFileName, true);
				}

				return destinationFolder;
			}, cancellationToken);
		}

		/// <summary>
		/// Creates a marker file that indicates which backup to restore for which partition.
		/// </summary>
		/// <param name="servicePartitionId"></param>
		/// <returns></returns>
		private string GetQueueFile(Guid servicePartitionId)
		{
			string queueFolder = GetQueueFolder();
			return Path.Combine(queueFolder, servicePartitionId.ToString("N"));
		}

		/// <summary>
		/// Creates and returns a folder that contains files that indicate which backup to restore for which partition.
		/// </summary>
		/// <returns></returns>
		private string GetQueueFolder()
		{
			string queueFolder = Path.Combine(_remoteFolderName, "Queue");
			Directory.CreateDirectory(queueFolder);
			return queueFolder;
		}
	}
}