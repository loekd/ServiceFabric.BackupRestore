using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using ServiceFabric.BackupRestore;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System.Threading;

namespace MyStatefulService
{
	/// <summary>
	/// Inherits from <see cref="BackupRestoreService"/>
	/// </summary>
	internal sealed class MyStatefulService : StatefulService, IBackupRestoreService, IBackupRestoreServiceOperations, IMyStatefulService
	{
        private readonly ICentralBackupStore _centralBackupStore;
        private readonly Action<string> _logCallback;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="centralBackupStore"></param>
        /// <param name="logCallback"></param>
        public MyStatefulService(StatefulServiceContext context, ICentralBackupStore centralBackupStore, Action<string> logCallback) 
			: base(context)
		{
            _centralBackupStore = centralBackupStore ?? throw new ArgumentNullException(nameof(centralBackupStore));
            _logCallback = logCallback;
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="reliableStateManagerReplica"></param>
        /// <param name="centralBackupStore"></param>
        /// <param name="logCallback"></param>
		public MyStatefulService(StatefulServiceContext context, IReliableStateManagerReplica reliableStateManagerReplica, ICentralBackupStore centralBackupStore, Action<string> logCallback) 
			: base(context, reliableStateManagerReplica)
		{
            _centralBackupStore = centralBackupStore ?? throw new ArgumentNullException(nameof(centralBackupStore));
            _logCallback = logCallback;
        }

		/// <summary>
		/// Returns a Service Remoting Listener that can be used to perform backup and restore operations on this replica. 
		/// </summary>
		/// <returns>A collection of listeners.</returns>
		protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
		{
            //Enable interaction, to allow external callers to trigger backups and restores, by using Service Remoting through IBackupRestoreService
            yield return new ServiceReplicaListener(this.CreateServiceRemotingListener, BackupRestoreService.BackupRestoreServiceEndpointName);
		}

        /// <summary>
        /// Call this to set some state. 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
		public async Task SetState(string value)
		{
			var myDictionary = await StateManager.GetOrAddAsync<IReliableDictionary<string, string>>("myDictionary");

			using (var tx = StateManager.CreateTransaction())
			{
				await myDictionary.AddOrUpdateAsync(tx, "MyState", value, (key, old) => value);
				await tx.CommitAsync();
			}

			ServiceEventSource.Current.ServiceMessage(Context, $"MyStatefulService - Set state to '{value}'.");
		}

        /// <summary>
        /// Call this to get the state that was set by calling <see cref="SetState(string)"/>
        /// </summary>
        /// <returns></returns>
		public async Task<string> GetState()
		{
			var myDictionary = await StateManager.GetOrAddAsync<IReliableDictionary<string, string>>("myDictionary");

			using (var tx = StateManager.CreateTransaction())
			{
				var result = await myDictionary.TryGetValueAsync(tx, "MyState");
				string state = result.HasValue ? result.Value : "<<<undefined>>>";
				ServiceEventSource.Current.ServiceMessage(Context, $"MyStatefulService - Get state '{state}'.");
				return state;
			}
		}

        /// <inheritdoc />
        protected sealed override Task<bool> OnDataLossAsync(RestoreContext restoreCtx, CancellationToken cancellationToken)
        {
            //after data loss, we'll restore a backup here:
            return BackupRestoreServiceOperations.OnDataLossAsync(this, restoreCtx, cancellationToken);
        }

        /// <inheritdoc />
        Task IBackupRestoreService.BeginCreateBackup(BackupOption backupOption)
        {
            return BackupRestoreServiceOperations.BeginCreateBackup(this, backupOption);
        }

        /// <inheritdoc />
        Task IBackupRestoreService.BeginRestoreBackup(BackupMetadata backupMetadata, DataLossMode dataLossMode)
        {
            return BackupRestoreServiceOperations.BeginRestoreBackup(this, backupMetadata, dataLossMode);

        }

        /// <inheritdoc />
        Task<IEnumerable<BackupMetadata>> IBackupRestoreService.ListBackups()
        {
            return BackupRestoreServiceOperations.ListBackups(this);

        }

        /// <inheritdoc />
        Task<IEnumerable<BackupMetadata>> IBackupRestoreService.ListAllBackups()
        {
            return BackupRestoreServiceOperations.ListAllBackups(this);
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

    public interface IMyStatefulService : IBackupRestoreService
	{
		/// <summary>
		/// Save state
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		Task SetState(string value);

		/// <summary>
		/// Query state
		/// </summary>
		/// <returns></returns>
		Task<string> GetState();
	}
}
