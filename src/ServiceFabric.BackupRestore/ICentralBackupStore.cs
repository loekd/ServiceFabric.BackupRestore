using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data;

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
		/// <param name="sourceDirectory">The source folder on the current Node.</param>
		/// <param name="backupOption">Indicates the type of backup.</param>
		/// <param name="servicePartitionId">Partition Guid of replica</param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		Task<BackupMetadata> UploadBackupFolderAsync(BackupOption backupOption, Guid servicePartitionId, string sourceDirectory, CancellationToken cancellationToken);


		/// <summary>
		/// Saves metadata for an uploaded backup.
		/// </summary>
		/// <param name="destinationFolder"></param>
		/// <param name="info"></param>
		Task StoreBackupMetadataAsync(string destinationFolder, BackupMetadata info, CancellationToken cancellationToken = default(CancellationToken));

		/// <summary>
		/// Copies contents from the last known backup folder to the provided <paramref name="destinationDirectory"/> on the node.
		/// </summary>
		/// <param name="backupId"></param>
		/// <param name="destinationDirectory"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		Task DownloadBackupFolderAsync(Guid backupId, string destinationDirectory, CancellationToken cancellationToken);

		/// <summary>
		/// Lists all known backup metadata.
		/// </summary>
		/// <returns></returns>
		Task<IEnumerable<BackupMetadata>> GetBackupMetadataAsync(Guid? backupId = null, Guid? servicePartitionId = null);

		/// <summary>
		/// Schedules a backup to be restored.
		/// </summary>
		/// <param name="servicePartitionId"></param>
		/// <param name="backupId"></param>
		/// <returns></returns>
		[Obsolete("Naming issue. Call 'ScheduleBackupRestoreAsync'.")]
        Task ScheduleBackupAsync(Guid servicePartitionId, Guid backupId);

        /// <summary>
		/// Schedules a backup to be restored.
		/// </summary>
		/// <param name="servicePartitionId"></param>
		/// <param name="backupId"></param>
		/// <returns></returns>
        Task ScheduleBackupRestoreAsync(Guid servicePartitionId, Guid backupId);

        /// <summary>
        /// Returns the id of a scheduled backup to be restored, for the provided <paramref name="servicePartitionId"/> or null.
        /// </summary>
        /// <param name="servicePartitionId"></param>
        /// <returns>backupId</returns>
        Task<BackupMetadata> RetrieveScheduledBackupAsync(Guid servicePartitionId);
	}
}