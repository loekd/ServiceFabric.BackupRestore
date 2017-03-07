using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Services.Runtime;

namespace ServiceFabric.BackupRestore
{
	internal static class BackupRestoreServiceInternalExtensions
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="service"></param>
		/// <param name="backupOption"></param>
		/// <returns></returns>
		public static async Task BeginCreateBackup(this IBackupRestoreServiceInternal service, BackupOption backupOption)
		{
			var backupDescription = new BackupDescription(backupOption, service.PostBackupCallbackAsync);
			await service.BackupAsync(backupDescription);

			service.LogCallback?.Invoke($"BackupRestoreService - BeginCreateBackup for partition {service.Context.PartitionId}.");
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="service"></param>
		/// <param name="backupMetadata"></param>
		/// <returns></returns>
		public static async Task BeginRestoreBackup(this IBackupRestoreServiceInternal service, BackupMetadata backupMetadata)
		{
			service.LogCallback?.Invoke($"BackupRestoreService - Beginning restore backup {backupMetadata.BackupId} for partition {service.Context.PartitionId}.");

			if (backupMetadata == null) throw new ArgumentNullException(nameof(backupMetadata));

			await service.CentralBackupStore.ScheduleBackupAsync(service.Context.PartitionId, backupMetadata.BackupId);

			var partitionSelector = PartitionSelector.PartitionKeyOf(service.Context.ServiceName,
				((Int64RangePartitionInformation)service.Partition.PartitionInfo).LowKey);

			var operationId = Guid.NewGuid();
			await new FabricClient(FabricClientRole.Admin).TestManager.StartPartitionDataLossAsync(operationId, partitionSelector, DataLossMode.FullDataLoss);
			//Causes OnDataLossAsync to be called.

			service.LogCallback?.Invoke($"BackupRestoreService - Begun restore backup {backupMetadata.BackupId} for partition {service.Context.PartitionId}.");
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="service"></param>
		/// <returns></returns>
		public static async Task<IEnumerable<BackupMetadata>> ListBackups(this IBackupRestoreServiceInternal service)
		{
			service.LogCallback?.Invoke($"BackupRestoreService - Listing backups");
			var backups = (await service.CentralBackupStore.GetBackupMetadataAsync()).ToList();
			service.LogCallback?.Invoke($"BackupRestoreService - Returning {backups.Count} backups");

			return backups;
		}

		/// <summary>
		/// Called after the call to <see cref="StatefulServiceBase.BackupAsync(Microsoft.ServiceFabric.Data.BackupDescription)"/> has completed. Used
		/// to save a copy of that backup in the central store.
		/// </summary>
		/// <param name="service"></param>
		/// <param name="backupInfo"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static async Task<bool> PostBackupCallbackAsync(this IBackupRestoreServiceInternal service, BackupInfo backupInfo, CancellationToken cancellationToken)
		{
			service.LogCallback?.Invoke($"BackupRestoreService - performing PostBackupCallbackAsync'.");
			var metadata = await service.CentralBackupStore.UploadBackupFolderAsync(backupInfo.Option, service.Context.PartitionId,
				backupInfo.Directory, cancellationToken);
			service.LogCallback?.Invoke($"BackupRestoreService - performed PostBackupCallbackAsync backupID:'{metadata.BackupId}'.");

			return true;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="service"></param>
		/// <param name="restoreCtx"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static async Task<bool> OnDataLossAsync(this IBackupRestoreServiceInternal service, RestoreContext restoreCtx, CancellationToken cancellationToken)
		{
			//caused by BeginRestoreBackup
			service.LogCallback?.Invoke($"BackupRestoreService - OnDataLossAsync starting for partition: {service.Context.PartitionId}.");

			var metadata = await service.CentralBackupStore.RetrieveScheduledBackupAsync(service.Context.PartitionId);
			if (metadata == null) return false;

			var backupList = (await service.CentralBackupStore.GetBackupMetadataAsync(null, metadata.OriginalServicePartitionId)).ToList()
				.Where(md => md.BackupOption == BackupOption.Full || md.TimeStampUtc >= metadata.TimeStampUtc);

			string localBackupFolder = Path.Combine(service.Context.CodePackageActivationContext.WorkDirectory, Guid.NewGuid().ToString("N"));
			foreach (var backupMetadata in backupList)
			{
				string subFolder = Path.Combine(localBackupFolder, backupMetadata.TimeStampUtc.ToString("yyyyMMddhhmmss"));
				await service.CentralBackupStore.DownloadBackupFolderAsync(backupMetadata.BackupId, subFolder, cancellationToken);
			}
			var restoreDescription = new RestoreDescription(localBackupFolder, RestorePolicy.Force);
			await restoreCtx.RestoreAsync(restoreDescription, cancellationToken);

			Directory.Delete(localBackupFolder, true);
			service.LogCallback?.Invoke($"BackupRestoreService - OnDataLossAsync complete for partition {service.Context.PartitionId}.");

			return true;
		}
	}
}