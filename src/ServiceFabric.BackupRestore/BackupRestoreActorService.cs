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
	public abstract class BackupRestoreActorService : ActorService, IBackupRestoreService, IBackupRestoreServiceInternal
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
			if (centralBackupStore == null) throw new ArgumentNullException(nameof(centralBackupStore));

			_centralBackupStore = centralBackupStore;
			_logCallback = logCallback;
		}
		
		/// <inheritdoc />
		public Task BeginCreateBackup(BackupOption backupOption)
		{
			return BackupRestoreServiceInternalExtensions.BeginCreateBackup(this, backupOption);
		}

		/// <inheritdoc />
		public Task BeginRestoreBackup(BackupMetadata backupMetadata)
		{
			return BackupRestoreServiceInternalExtensions.BeginRestoreBackup(this, backupMetadata);
		}

		/// <inheritdoc />
		public Task<IEnumerable<BackupMetadata>> ListBackups()
		{
			return BackupRestoreServiceInternalExtensions.ListBackups(this);
		}

		/// <inheritdoc />
		protected override Task<bool> OnDataLossAsync(RestoreContext restoreCtx, CancellationToken cancellationToken)
		{
			return BackupRestoreServiceInternalExtensions.OnDataLossAsync(this, restoreCtx, cancellationToken);
		}


		/// <inheritdoc />
		ICentralBackupStore IBackupRestoreServiceInternal.CentralBackupStore => _centralBackupStore;

		/// <inheritdoc />
		Action<string> IBackupRestoreServiceInternal.LogCallback => _logCallback;

		/// <inheritdoc />
		IStatefulServicePartition IBackupRestoreServiceInternal.Partition => Partition;

		/// <inheritdoc />
		Task<bool> IBackupRestoreServiceInternal.PostBackupCallbackAsync(BackupInfo backupInfo, CancellationToken cancellationToken)
		{
			return this.PostBackupCallbackAsync(backupInfo, cancellationToken);
		}
	}

	internal interface IBackupRestoreServiceInternal
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
