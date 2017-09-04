using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Services.Runtime;

namespace ServiceFabric.BackupRestore
{
	/// <summary>
	/// Base class of <see cref="StatefulService"/> that enables simple backups and restore of State.
	/// Use SF remoting to talk to the Listenter named <see cref="BackupRestoreServiceEndpointName"/>.
	/// Call <see cref="BeginCreateBackup"/> to start a backup creation asynchronously.
	/// Call <see cref="BeginRestoreBackup"/> to start a restore operation asynchronously.
	/// </summary>
	public abstract class BackupRestoreService : StatefulService, IBackupRestoreService, IBackupRestoreServiceOperations
	{
		private readonly ICentralBackupStore _centralBackupStore;
		private readonly Action<string> _logCallback;

		/// <summary>
		/// Endpoint at which this service may communicate through SF remoting and <see cref="IBackupRestoreService"/>
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


		/// <inheritdoc />
		public Task BeginCreateBackup(BackupOption backupOption)
		{
			return BackupRestoreServiceOperations.BeginCreateBackup(this, backupOption);
		}

		/// <inheritdoc />
		public Task BeginRestoreBackup(BackupMetadata backupMetadata, DataLossMode dataLossMode)
		{
			return BackupRestoreServiceOperations.BeginRestoreBackup(this, backupMetadata, dataLossMode);
		}

		/// <inheritdoc />
		public Task<IEnumerable<BackupMetadata>> ListBackups()
		{
			return BackupRestoreServiceOperations.ListBackups(this);
		}

        /// <inheritdoc />
        public Task<IEnumerable<BackupMetadata>> ListAllBackups()
        {
            return BackupRestoreServiceOperations.ListAllBackups(this);
        }

        /// <inheritdoc />
        protected override Task<bool> OnDataLossAsync(RestoreContext restoreCtx, CancellationToken cancellationToken)
		{
			return BackupRestoreServiceOperations.OnDataLossAsync(this, restoreCtx, cancellationToken);
		}
		
		/// <inheritdoc />
		ICentralBackupStore IBackupRestoreServiceOperations.CentralBackupStore => _centralBackupStore;

		/// <inheritdoc />
		Action<string> IBackupRestoreServiceOperations.LogCallback => _logCallback;

		/// <inheritdoc />
		IStatefulServicePartition IBackupRestoreServiceOperations.Partition => Partition;

		/// <inheritdoc />
		Task<bool> IBackupRestoreServiceOperations.PostBackupCallbackAsync(BackupInfo backupInfo, CancellationToken cancellationToken)
		{
			return this.PostBackupCallbackAsync(backupInfo, cancellationToken);
		}
	}
}
