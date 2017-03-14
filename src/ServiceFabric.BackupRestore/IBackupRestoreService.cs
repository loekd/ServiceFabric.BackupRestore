using System.Collections.Generic;
using System.Fabric;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Services.Remoting;

namespace ServiceFabric.BackupRestore
{
	public interface IBackupRestoreService : IService
	{
		/// <summary>
		/// Asynchronously starts the creation of a backup of the state of this replica and stores that into the central store.
		/// </summary>
		/// <returns></returns>
		Task BeginCreateBackup(BackupOption backupOption);

		/// <summary>
		/// Asynchronously starts a restore operation using the state indicated by <paramref name="backupMetadata"/>. 
		/// The backup is retrieved from the central store.
		/// </summary>
		/// <returns></returns>
		Task BeginRestoreBackup(BackupMetadata backupMetadata, DataLossMode dataLossMode);

		/// <summary>
		/// Lists all centrally stored backups.
		/// </summary>
		/// <returns></returns>
		Task<IEnumerable<BackupMetadata>> ListBackups();
	}
}