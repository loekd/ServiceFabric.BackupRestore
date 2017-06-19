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
        /// Asynchronously starts the creation of a backup of the state of this replica and stores that into the central store.
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
        /// Asynchronously starts a restore operation using the state indicated by <paramref name="backupMetadata"/>. 
        /// The backup is retrieved from the central store.
        /// </summary>
        /// <param name="service"></param>
        /// <param name="dataLossMode"></param>
        /// <param name="backupMetadata"></param>
        /// <returns></returns>
        public static async Task BeginRestoreBackup(this IBackupRestoreServiceInternal service, BackupMetadata backupMetadata, DataLossMode dataLossMode)
        {
            service.LogCallback?.Invoke($"BackupRestoreService - Beginning restore backup {backupMetadata.BackupId} for partition {service.Context.PartitionId}.");

            if (backupMetadata == null) throw new ArgumentNullException(nameof(backupMetadata));

            await service.CentralBackupStore.ScheduleBackupAsync(service.Context.PartitionId, backupMetadata.BackupId);

            var partitionSelector = PartitionSelector.PartitionIdOf(service.Context.ServiceName, service.Context.PartitionId);

            var operationId = Guid.NewGuid();
            await new FabricClient(FabricClientRole.Admin).TestManager.StartPartitionDataLossAsync(operationId, partitionSelector, dataLossMode);
            //Causes OnDataLossAsync to be called.

            service.LogCallback?.Invoke($"BackupRestoreService - Begun restore backup {backupMetadata.BackupId} for partition {service.Context.PartitionId}.");
        }

        /// <summary>
        /// Lists all centrally stored backups for the service <paramref name="service"/>.
        /// </summary>
        /// <param name="service"></param>
        /// <returns></returns>
        public static async Task<IEnumerable<BackupMetadata>> ListBackups(this IBackupRestoreServiceInternal service)
        {
            service.LogCallback?.Invoke($"BackupRestoreService - Listing backups");
            var backups = (await service.CentralBackupStore.GetBackupMetadataAsync(servicePartitionId: service.Context.PartitionId))
                .OrderByDescending(x => x.TimeStampUtc)
                .ToList();
            service.LogCallback?.Invoke($"BackupRestoreService - Returning {backups.Count} backups");

            return backups;
        }

        /// <summary>
        /// Lists all centrally stored backups present in <paramref name="service"/> CentralBackupStore.
        /// </summary>
        /// <param name="service"></param>
        /// <returns></returns>
        public static async Task<IEnumerable<BackupMetadata>> ListAllBackups(this IBackupRestoreServiceInternal service)
        {
            service.LogCallback?.Invoke($"BackupRestoreService - Listing backups");
            var backups = (await service.CentralBackupStore.GetBackupMetadataAsync())
                .OrderByDescending(x => x.TimeStampUtc)
                .ToList();
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
        /// This method is called during suspected data loss.
        /// You can use this method to restore the service in case of data loss.
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

            List<BackupMetadata> backupList = await GetBackupMetadataAsync(service, metadata);

            if (backupList.Count == 0)
                throw new InvalidOperationException("Failed to find any backups for this partition.");
            if (backupList[0].BackupOption != BackupOption.Full)
                throw new InvalidOperationException("Failed to find any full backups for this partition.");

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

        /// <summary>
        /// Compiles a list of <see cref="BackupMetadata"/> to be restored, based on the provided metadata.
        /// </summary>
        /// <param name="service"></param>
        /// <param name="metadata"></param>
        /// <returns></returns>
        internal static async Task<List<BackupMetadata>> GetBackupMetadataAsync(IBackupRestoreServiceInternal service, BackupMetadata metadata)
        {
            var backupList = new List<BackupMetadata>();

            if (metadata.BackupOption == BackupOption.Full)
            {
                // Taking only selected full backup
                backupList.Add(metadata);
            }
            else
            {
                // Taking full backup and all incremental backups since, reversed
                // so it finds the incremental backup first, and then loops until the full backup is found.
                var allBackups = (await service.CentralBackupStore.GetBackupMetadataAsync(servicePartitionId: metadata.OriginalServicePartitionId))
                   .OrderByDescending(x => x.TimeStampUtc)
                   .ToList();

                BackupMetadata incrementalBackup = null;
                // Looking for the latest full backup before selected
                foreach (var backupMetadata in allBackups)
                {
                    if (incrementalBackup == null && backupMetadata.BackupId != metadata.BackupId)
                    {
                        continue;
                    }
                    else
                    {
                        incrementalBackup = backupMetadata;
                        backupList.Add(backupMetadata);
                    }

                    if (backupMetadata.BackupOption == BackupOption.Full)
                    {

                        //if it's the full backup we encounter, we're done
                        break;
                    }
                }
                backupList.Reverse();
            }
           
            return backupList;
        }
    }
}