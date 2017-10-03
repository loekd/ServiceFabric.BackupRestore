using System.Collections.Generic;
using System.Fabric;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Services.Remoting;

namespace ServiceFabric.BackupRestore
{
    /// <summary>
    /// This interface must be implemented to interact with the backup & restore operations of a service.
    /// </summary>
	public interface IBackupRestoreService : IService
	{
        /// <summary>
        /// Asynchronously starts the creation of a backup of the state of this replica and stores that into the central store.
        /// This method completes and returns before the backup process is completely done.
        /// </summary>
        /// <returns></returns>
        Task BeginCreateBackup(BackupOption backupOption);

        /// <summary>
        /// Asynchronously starts a restore operation using the state indicated by <paramref name="backupMetadata"/>. 
        /// The backup is retrieved from the central store.
        /// This method completes and returns before the backup restore process is completely done.
        /// </summary>
        /// <returns></returns>
        Task BeginRestoreBackup(BackupMetadata backupMetadata, DataLossMode dataLossMode);

        /// <summary>
        /// Lists all centrally stored backups for the service, inside the CentralBackupStore.
        /// </summary>
        /// <returns></returns>
        Task<List<BackupMetadata>> ListBackups();

        /// <summary>
        /// Lists all centrally stored backups present for the service, inside the CentralBackupStore.
        /// </summary>
        /// <returns></returns>
		Task<List<BackupMetadata>> ListAllBackups();
        
    }
}