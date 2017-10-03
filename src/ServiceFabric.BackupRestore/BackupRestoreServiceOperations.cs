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
    /// <summary>
    /// Provides methods that can be called to implement <see cref="IBackupRestoreServiceOperations"/>
    /// </summary>
    public static class BackupRestoreServiceOperations
    {
        /// <summary>
        /// Asynchronously starts the creation of a backup of the state of this replica and stores that into the central store.
        /// This method completes and returns before the backup process is completely done.
        /// </summary>
        /// <param name="service"></param>
        /// <param name="backupOption"></param>
        /// <returns></returns>
        public static async Task BeginCreateBackup(this IBackupRestoreServiceOperations service, BackupOption backupOption)
        {
            try
            {
                var backupDescription = new BackupDescription(backupOption, service.PostBackupCallbackAsync);
                await service.BackupAsync(backupDescription);
            }
            catch (Exception ex)
            {
                string message = $"Failed to create backup information for partition {service.Context.PartitionId}";
                service.LogCallback?.Invoke($"{nameof(BackupRestoreServiceOperations)} - {nameof(BeginCreateBackup)} failed for partition: {service.Context.PartitionId}. Message:{message} - Error: {ex.Message}");
                throw new Exception(message, ex);
            }
            service.LogCallback?.Invoke($"{nameof(BackupRestoreServiceOperations)} - {nameof(BeginCreateBackup)} succeeded for partition {service.Context.PartitionId}.");
        }

        /// <summary>
        /// Asynchronously starts a restore operation using the state indicated by <paramref name="backupMetadata"/>. 
        /// The backup is retrieved from the central store. 
        /// This method completes and returns before the backup restore process is completely done.
        /// </summary>
        /// <param name="service"></param>
        /// <param name="dataLossMode"></param>
        /// <param name="backupMetadata"></param>
        /// <returns></returns>
        public static async Task BeginRestoreBackup(this IBackupRestoreServiceOperations service, BackupMetadata backupMetadata, DataLossMode dataLossMode)
        {
            service.LogCallback?.Invoke($"BackupRestoreService - Beginning restore backup {backupMetadata.BackupId} for partition {service.Context.PartitionId}.");

            try
            {
                if (backupMetadata == null) throw new ArgumentNullException(nameof(backupMetadata));

                await service.CentralBackupStore.ScheduleBackupRestoreAsync(service.Context.PartitionId, backupMetadata.BackupId);

                var partitionSelector = PartitionSelector.PartitionIdOf(service.Context.ServiceName, service.Context.PartitionId);

                var operationId = Guid.NewGuid();
                await new FabricClient(FabricClientRole.Admin).TestManager.StartPartitionDataLossAsync(operationId, partitionSelector, dataLossMode);
                //Causes OnDataLossAsync to be called later on. 
            }
            catch (Exception ex)
            {
                string message = $"Failed to restore backup for partition {service.Context.PartitionId}";
                service.LogCallback?.Invoke($"{nameof(BackupRestoreServiceOperations)} - {nameof(BeginRestoreBackup)} failed for partition: {service.Context.PartitionId}. Message:{message} - Error: {ex.Message}");
                throw new Exception(message, ex);
            }
            service.LogCallback?.Invoke($"{nameof(BackupRestoreServiceOperations)} - {nameof(BeginRestoreBackup)} succeeded {backupMetadata.BackupId} for partition {service.Context.PartitionId}.");
        }

        /// <summary>
        /// Lists all centrally stored backups for the service <paramref name="service"/>.
        /// </summary>
        /// <param name="service"></param>
        /// <returns></returns>
        public static async Task<List<BackupMetadata>> ListBackups(this IBackupRestoreServiceOperations service)
        {
            service.LogCallback?.Invoke($"{nameof(BackupRestoreServiceOperations)} - {nameof(ListBackups)} - Listing backups");

            var backups = (await service.CentralBackupStore.GetBackupMetadataAsync(servicePartitionId: service.Context.PartitionId))
                .OrderByDescending(x => x.TimeStampUtc)
                .ToList();

            service.LogCallback?.Invoke($"{nameof(BackupRestoreServiceOperations)} - {nameof(ListBackups)} - Returning {backups.Count} backups");

            return backups;
        }

        /// <summary>
        /// Lists all centrally stored backups present in <paramref name="service"/> CentralBackupStore.
        /// </summary>
        /// <param name="service"></param>
        /// <returns></returns>
        public static async Task<List<BackupMetadata>> ListAllBackups(this IBackupRestoreServiceOperations service)
        {
            service.LogCallback?.Invoke($"{nameof(BackupRestoreServiceOperations)} - {nameof(ListAllBackups)} - Listing all backups");

            var backups = (await service.CentralBackupStore.GetBackupMetadataAsync())
                .OrderByDescending(x => x.TimeStampUtc)
                .ToList();

            service.LogCallback?.Invoke($"{nameof(BackupRestoreServiceOperations)} - {nameof(ListAllBackups)} - Returning all {backups.Count} backups");
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
        public static async Task<bool> PostBackupCallbackAsync(this IBackupRestoreServiceOperations service, BackupInfo backupInfo, CancellationToken cancellationToken)
        {
            service.LogCallback?.Invoke($"{nameof(BackupRestoreServiceOperations)} - {nameof(ListAllBackups)} - Performing PostBackupCallbackAsync'.");

            var metadata = await service.CentralBackupStore.UploadBackupFolderAsync(backupInfo.Option, service.Context.PartitionId,
                backupInfo.Directory, cancellationToken);

            service.LogCallback?.Invoke($"{nameof(BackupRestoreServiceOperations)} - {nameof(ListAllBackups)} - Performed PostBackupCallbackAsync backupID:'{metadata.BackupId}'.");

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
        public static async Task<bool> OnDataLossAsync(this IBackupRestoreServiceOperations service, RestoreContext restoreCtx, CancellationToken cancellationToken)
        {
            //Ususally caused by BeginRestoreBackup

            //First, query the scheduled metadata for this partition backup
            service.LogCallback?.Invoke($"{nameof(BackupRestoreServiceOperations)} - {nameof(OnDataLossAsync)} - Starting for partition: {service.Context.PartitionId}.");
            BackupMetadata metadata;
            try
            {
                metadata = await service.CentralBackupStore.RetrieveScheduledBackupAsync(service.Context.PartitionId);
            }
            catch (Exception ex)
            {
                string message = $"Failed to find backup information for partition {service.Context.PartitionId}";
                service.LogCallback?.Invoke($"{nameof(BackupRestoreServiceOperations)} - {nameof(OnDataLossAsync)} - Failed for partition: {service.Context.PartitionId}. Message:{message} - Error: {ex.Message}");
                throw new Exception(message, ex);
            }

            if (metadata == null) return false;

            //Get all metadata for all backups related to this one, if it's an incremental one
            List<BackupMetadata> backupList;

            try
            {
                backupList = await GetBackupMetadataAsync(service, metadata);

                if (backupList.Count < 1)
                    throw new InvalidOperationException("Failed to find any backups for this partition.");

                if (backupList[0].BackupOption != BackupOption.Full)
                    throw new InvalidOperationException("Failed to find any full backups for this partition.");
            }
            catch (Exception ex)
            {
                string message = $"Failed to find backup information for partition {service.Context.PartitionId}";
                service.LogCallback?.Invoke($"{nameof(BackupRestoreServiceOperations)} - {nameof(OnDataLossAsync)} - Failed for partition: {service.Context.PartitionId}. Message:{message} - Error: {ex.Message}");
                throw new Exception(message, ex);
            }

            //download central to local
            string localBackupFolder = Path.Combine(service.Context.CodePackageActivationContext.WorkDirectory, Guid.NewGuid().ToString("N"));

            try
            {
                foreach (var backupMetadata in backupList)
                {
                    string subFolder = Path.Combine(localBackupFolder, backupMetadata.TimeStampUtc.ToString("yyyyMMddhhmmss"));
                    await service.CentralBackupStore.DownloadBackupFolderAsync(backupMetadata.BackupId, subFolder, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                string message = $"Failed to download backup data for partition {service.Context.PartitionId}";
                service.LogCallback?.Invoke($"{nameof(BackupRestoreServiceOperations)} - {nameof(OnDataLossAsync)} - Failed for partition: {service.Context.PartitionId}. Message:{message} - Error: {ex.Message}");
                throw new Exception(message, ex);
            }

            //restore
            try
            {
                var restoreDescription = new RestoreDescription(localBackupFolder, RestorePolicy.Force);
                await restoreCtx.RestoreAsync(restoreDescription, cancellationToken);
            }
            catch (Exception ex)
            {
                string message = $"Failed to restore backup for partition {service.Context.PartitionId}";
                service.LogCallback?.Invoke($"{nameof(BackupRestoreServiceOperations)} - {nameof(OnDataLossAsync)} - Failed for partition: {service.Context.PartitionId}. Message:{message} - Error: {ex.Message}");
                throw new Exception(message, ex);
            }
            finally
            {
                Directory.Delete(localBackupFolder, true);
            }

            //success!!
            service.LogCallback?.Invoke($"BackupRestoreService - OnDataLossAsync complete for partition {service.Context.PartitionId}.");

            return true;
        }

        /// <summary>
        /// Compiles a list of <see cref="BackupMetadata"/> to be restored, based on the provided metadata.
        /// </summary>
        /// <param name="service"></param>
        /// <param name="metadata"></param>
        /// <returns></returns>
        internal static async Task<List<BackupMetadata>> GetBackupMetadataAsync(IBackupRestoreServiceOperations service, BackupMetadata metadata)
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