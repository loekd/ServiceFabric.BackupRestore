using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace ServiceFabric.BackupRestore
{
	/// <summary>
	/// Base class of <see cref="StatefulService"/> that enables simple backups and restore of State.
	/// Use SF remoting to talk to the Listenter named <see cref="BackupRestoreServiceEndpointName"/>.
	/// Call <see cref="BeginCreateBackup"/> to start a backup creation asynchronously.
	/// Call <see cref="BeginRestoreBackup"/> to start a restore operation asynchronously.
	/// </summary>
	public abstract class BackupRestoreService : StatefulService, IBackupRestoreService
	{
		private readonly ICentralBackupStore _centralBackupStore;
		private readonly Action<string> _logCallback;

		/// <summary>
		/// Endpoint at which this service communicates through SF remoting and <see cref="IBackupRestoreService"/>
		/// </summary>
		public const string BackupRestoreServiceEndpointName = "BackupRestoreServiceEndPoint";

		/// <summary>
		/// Creates a new instance, using the provided arguments.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="centralBackupStore"></param>
		/// <param name="logCallback"></param>
		protected BackupRestoreService(StatefulServiceContext context, ICentralBackupStore centralBackupStore, Action<string> logCallback)
			: base(context)
		{
			if (centralBackupStore == null) throw new ArgumentNullException(nameof(centralBackupStore));

			_centralBackupStore = centralBackupStore;
			_logCallback = logCallback;
		}

		/// <summary>
		/// Creates a new instance, using the provided arguments.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="reliableStateManagerReplica"></param>
		/// <param name="centralBackupStore"></param>
		/// <param name="logCallback"></param>
		protected BackupRestoreService(StatefulServiceContext context, IReliableStateManagerReplica reliableStateManagerReplica, ICentralBackupStore centralBackupStore, Action<string> logCallback)
			: base(context, reliableStateManagerReplica)
		{
			if (centralBackupStore == null) throw new ArgumentNullException(nameof(centralBackupStore));

			_centralBackupStore = centralBackupStore;
			_logCallback = logCallback;
		}

		/// <summary>
		/// Returns a Service Remoting Listener that can be used to perform backup and restore operations on this replica. 
		/// Call 'base' when overriding this method!
		/// </summary>
		/// <returns>A collection of listeners.</returns>
		protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
		{
			yield return new ServiceReplicaListener(this.CreateServiceRemotingListener, BackupRestoreServiceEndpointName);
		}
		
		/// <inheritdoc />
		public async Task BeginCreateBackup()
		{
			var backupDescription = new BackupDescription(BackupOption.Full, PostBackupCallbackAsync);
			await BackupAsync(backupDescription);

			_logCallback?.Invoke($"BackupRestoreService - BeginCreateBackup for partition {Context.PartitionId}.");
		}

		/// <inheritdoc />
		public async Task BeginRestoreBackup(BackupMetadata backupMetadata)
		{
			_logCallback?.Invoke($"BackupRestoreService - Beginning restore backup {backupMetadata.BackupId} for partition {Context.PartitionId}.");

			if (backupMetadata == null) throw new ArgumentNullException(nameof(backupMetadata));

			await _centralBackupStore.ScheduleBackupAsync(Context.PartitionId, backupMetadata.BackupId);

			var partitionSelector = PartitionSelector.PartitionKeyOf(Context.ServiceName,
				((Int64RangePartitionInformation) Partition.PartitionInfo).LowKey);

			var operationId = Guid.NewGuid();
			await new FabricClient(FabricClientRole.Admin).TestManager.StartPartitionDataLossAsync(operationId, partitionSelector, DataLossMode.FullDataLoss);
			//Causes OnDataLossAsync to be called.

			_logCallback?.Invoke($"BackupRestoreService - Begun restore backup {backupMetadata.BackupId} for partition {Context.PartitionId}.");
		}

		/// <inheritdoc />
		public async Task<IEnumerable<BackupMetadata>> ListBackups()
		{
			_logCallback?.Invoke($"BackupRestoreService - Listing backups");
			var backups = (await _centralBackupStore.GetBackupMetadataAsync()).ToList();
			_logCallback?.Invoke($"BackupRestoreService - Returning {backups.Count} backups");

			return backups;
		}

		/// <inheritdoc />
		protected override async Task<bool> OnDataLossAsync(RestoreContext restoreCtx, CancellationToken cancellationToken)
		{
			//caused by BeginRestoreBackup
			_logCallback?.Invoke($"BackupRestoreService - OnDataLossAsync starting for partition: {Context.PartitionId}.");
			
			var metadata = await _centralBackupStore.RetrieveScheduledBackupAsync(Context.PartitionId);
			if (metadata == null) return false;
			
			string localBackupFolder = Path.Combine(Context.CodePackageActivationContext.WorkDirectory, Guid.NewGuid().ToString("N"));
			await _centralBackupStore.DownloadBackupFolderAsync(metadata.BackupId, localBackupFolder, cancellationToken);

			var restoreDescription = new RestoreDescription(localBackupFolder, RestorePolicy.Force);
			await restoreCtx.RestoreAsync(restoreDescription, cancellationToken);

			Directory.Delete(localBackupFolder, true);
			_logCallback?.Invoke($"BackupRestoreService - OnDataLossAsync complete for partition {Context.PartitionId}.");

			return true;
		}

		/// <summary>
		/// Called after the call to <see cref="StatefulServiceBase.BackupAsync(Microsoft.ServiceFabric.Data.BackupDescription)"/> has completed. Used
		/// to save a copy of that backup in the central store.
		/// </summary>
		/// <param name="backupInfo"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		private async Task<bool> PostBackupCallbackAsync(BackupInfo backupInfo, CancellationToken cancellationToken)
		{
			_logCallback?.Invoke($"BackupRestoreService - performing PostBackupCallbackAsync'.");
			var metadata = await _centralBackupStore.UploadBackupFolderAsync(Context.PartitionId, backupInfo.Directory, cancellationToken);
			_logCallback?.Invoke($"BackupRestoreService - performed PostBackupCallbackAsync backupID:'{metadata.BackupId}'.");

			return true;
		}
	}
}
