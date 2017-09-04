using System;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data;

namespace ServiceFabric.BackupRestore
{
    /// <summary>
    /// This interface must be implemented to perform backups and restores. 
    /// Implement this service to enable calls to <see cref="BackupRestoreServiceOperations"/>
    /// Sample implementation in <see cref="BackupRestoreService"/> and <see cref="BackupRestoreActorService"/>.
    /// </summary>
	public interface IBackupRestoreServiceOperations
	{
		/// <summary>
		/// Gets the implementation of <see cref="ICentralBackupStore"/>
		/// </summary>
		ICentralBackupStore CentralBackupStore { get; }
		/// <summary>
		/// Gets the Stateful Sevice Context.
		/// </summary>
		StatefulServiceContext Context { get; }
		/// <summary>
		/// Gets an optional log callback
		/// </summary>
		Action<string> LogCallback { get; }
		/// <summary>
		/// Get the Stateful Service Partition
		/// </summary>
		IStatefulServicePartition Partition { get; }
		/// <summary>
		/// Gets a callback that will be invoked after a local backup has been created.
		/// </summary>
		/// <param name="backupInfo"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		Task<bool> PostBackupCallbackAsync(BackupInfo backupInfo, CancellationToken cancellationToken);
		/// <summary>
		/// Begins a local backup operation
		/// </summary>
		/// <param name="backupDescription"></param>
		/// <returns></returns>
		Task BackupAsync(BackupDescription backupDescription);
	}
}