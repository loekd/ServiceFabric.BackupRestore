using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Data;

namespace ServiceFabric.BackupRestore
{
	public abstract class BackupRestoreActorService : ActorService, IBackupRestoreService, IBackupRestoreServiceOperations
	{
		private readonly ICentralBackupStore _centralBackupStore;
		private readonly Action<string> _logCallback;

		/// <summary>
		/// Endpoint at which this service may communicate through SF remoting and <see cref="IBackupRestoreService"/>
		/// </summary>
		public const string BackupRestoreServiceEndpointName = "BackupRestoreActorServiceEndPoint";

		/// <summary>
		/// Creates a new instance, using the provided arguments.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="actorTypeInfo"></param>
		/// <param name="centralBackupStore"></param>
		/// <param name="logCallback"></param>
		/// <param name="actorFactory"></param>
		/// <param name="stateManagerFactory"></param>
		/// <param name="stateProvider"></param>
		/// <param name="settings"></param>
		protected BackupRestoreActorService(StatefulServiceContext context,
			ActorTypeInformation actorTypeInfo,
			ICentralBackupStore centralBackupStore,
			Action<string> logCallback, Func<ActorService, ActorId, ActorBase> actorFactory = null,
			Func<ActorBase, IActorStateProvider, IActorStateManager> stateManagerFactory = null,
			IActorStateProvider stateProvider = null,
			ActorServiceSettings settings = null)
			: base(context, actorTypeInfo, actorFactory, stateManagerFactory, stateProvider, settings)
		{
		    _centralBackupStore = centralBackupStore ?? throw new ArgumentNullException(nameof(centralBackupStore));
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
		public Task<List<BackupMetadata>> ListBackups()
		{
			return BackupRestoreServiceOperations.ListBackups(this);
		}

        /// <inheritdoc />
		public Task<List<BackupMetadata>> ListAllBackups()
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
