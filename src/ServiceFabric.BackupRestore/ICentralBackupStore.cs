using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceFabric.BackupRestore
{
	/// <summary>
	/// Central store in which Service Partitions can store their backups.
	/// </summary>
	public interface ICentralBackupStore
	{
		/// <summary>
		/// Copies contents from the provided <paramref name="sourceDirectory"/> on the node, to a new backup folder in the central store.
		/// </summary>
		/// <param name="sourceDirectory"></param>
		/// <param name="servicePartitionId"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		Task<BackupMetadata> UploadBackupFolderAsync(Guid servicePartitionId, string sourceDirectory, CancellationToken cancellationToken);


		/// <summary>
		/// Saves metadata for an uploaded backup.
		/// </summary>
		/// <param name="destinationFolder"></param>
		/// <param name="info"></param>
		Task StoreBackupMetadataAsync(string destinationFolder, BackupMetadata info);

		/// <summary>
		/// Copies contents from the last known backup folder to the provided <paramref name="destinationDirectory"/> on the node.
		/// </summary>
		/// <param name="backupInfoId"></param>
		/// <param name="destinationDirectory"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		Task DownloadBackupFolderAsync(Guid backupInfoId, string destinationDirectory, CancellationToken cancellationToken);

		/// <summary>
		/// Lists all known backup metadata.
		/// </summary>
		/// <returns></returns>
		Task<IEnumerable<BackupMetadata>> GetBackupMetadataAsync(Guid? backupInfoId = null);

		/// <summary>
		/// Schedules a backup to be restored.
		/// </summary>
		/// <param name="servicePartitionId"></param>
		/// <param name="backupId"></param>
		/// <returns></returns>
		Task ScheduleBackupAsync(Guid servicePartitionId, Guid backupId);

		/// <summary>
		/// Returns the id of a scheduled backup to be restored, for the provided <paramref name="servicePartitionId"/> or null.
		/// </summary>
		/// <param name="servicePartitionId"></param>
		/// <returns>backupId</returns>
		Task<BackupMetadata> RetrieveScheduledBackupAsync(Guid servicePartitionId);
	}
}