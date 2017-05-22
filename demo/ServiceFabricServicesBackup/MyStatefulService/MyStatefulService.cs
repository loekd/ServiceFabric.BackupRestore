using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using ServiceFabric.BackupRestore;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;

namespace MyStatefulService
{
	/// <summary>
	/// Inherits from <see cref="BackupRestoreService"/>
	/// </summary>
	internal sealed class MyStatefulService : BackupRestoreService, IMyStatefulService
	{
		public MyStatefulService(StatefulServiceContext context, ICentralBackupStore centralBackupStore, Action<string> logCallback) 
			: base(context, centralBackupStore, logCallback)
		{
		}

		public MyStatefulService(StatefulServiceContext context, IReliableStateManagerReplica reliableStateManagerReplica, ICentralBackupStore centralBackupStore, Action<string> logCallback) 
			: base(context, reliableStateManagerReplica, centralBackupStore, logCallback)
		{
		}

		/// <summary>
		/// Returns a Service Remoting Listener that can be used to perform backup and restore operations on this replica. 
		/// </summary>
		/// <returns>A collection of listeners.</returns>
		protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
		{
			yield return new ServiceReplicaListener(this.CreateServiceRemotingListener, BackupRestoreServiceEndpointName);
		}

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
