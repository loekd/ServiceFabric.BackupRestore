using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Query;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Services.Client;
using Newtonsoft.Json.Linq;
using ServiceFabric.BackupRestore.Web.Models;

namespace ServiceFabric.BackupRestore.Web.Controllers
{
	[Route("[controller]")]
	public class ServiceDiscoveryController : Controller
	{
		private readonly ServicePartitionResolver _servicePartitionResolver;
		private readonly FabricClient _fabricClient;

		public ServiceDiscoveryController(FabricClient fabricClient)
		{
			_servicePartitionResolver = ServicePartitionResolver.GetDefault();
			_fabricClient = fabricClient ?? new FabricClient();
		}

		/// <summary>
		/// Queries all applications for backup/restore enabled services.
		/// </summary>
		/// <returns></returns>
		[HttpGet]
		public async Task<IActionResult> Get()
		{
			var backupEnabledServices = await QueryBackupRestoreServices();
			return View("View", backupEnabledServices);
		}

		/// <summary>
		/// Begins the process of creating a backup.
		/// </summary>
		/// <returns></returns>
		[HttpPost("/createbackup")]
		public async Task<IActionResult> CreateBackup([FromBody]BackupEnabledServiceReference service)
		{
			await Task.Delay(1);
			return View("View", service);
		}

		/// <summary>
		/// Begins the process of restoring a backup.
		/// </summary>
		/// <returns></returns>
		[HttpPost("/restorebackup")]
		public async Task<IActionResult> RestoreBackup([FromBody]BackupEnabledServiceReference service)
		{
			await Task.Delay(1);
			return View("View", service);
		}

		/// <summary>
		/// Query filtered by application, service and partition, for backup/restore enabled services.
		/// </summary>
		/// <param name="application">Query specific application</param>
		/// <param name="service">Query specific servicename, without application prefix. Optional, but requires application to be specified as well.</param>
		/// <param name="partitionId">Query specific partitionId, optional</param>
		/// <returns></returns>
		[HttpGet("{application}/{service?}/{partitionId?}")]
		public async Task<IActionResult> Get(string application, string service, Guid? partitionId = null)
		{
			var applicationName = string.IsNullOrWhiteSpace(application) ? null : new Uri(new Uri("fabric:/"), application);
			var serviceName = applicationName != null && !string.IsNullOrWhiteSpace(service) ? new Uri(applicationName.AbsoluteUri + "/" + service.Trim('/','\\')) : null;

			var backupEnabledServices = await QueryBackupRestoreServices(applicationName, serviceName, partitionId);
			return Ok(backupEnabledServices);
		}

		/// <summary>
		/// Queries all applications for backup/restore enabled services.
		/// </summary>
		/// <param name="application">Query specific application</param>
		/// <param name="service">Query specific service</param>
		/// <param name="partitionId">Query specific partition</param>
		/// <returns></returns>
		private async Task<List<BackupEnabledServiceReference>> QueryBackupRestoreServices(Uri application = null, Uri service = null, Guid? partitionId = null)
		{
			var backupEnabledServices = new List<BackupEnabledServiceReference>();
			string token;
			do
			{
				var apps = await _fabricClient.QueryManager.GetApplicationListAsync(application).ConfigureAwait(true);
				foreach (var app in apps)
				{
					await QueryApplicationServices(backupEnabledServices, app, service, partitionId);
				}
				token = apps.ContinuationToken;
			} while (token != null);

			return backupEnabledServices;
		}

		private async Task QueryApplicationServices(List<BackupEnabledServiceReference> backupEnabledServices, Application app, Uri serviceName = null, Guid? partitionId = null)
		{
			string token;
			do
			{
				var services = await _fabricClient.QueryManager.GetServiceListAsync(app.ApplicationName, serviceName).ConfigureAwait(true);
				foreach (var service in services)
				{
					await QueryServicePartitions(backupEnabledServices, app, service, partitionId);
				}
				token = services.ContinuationToken;
			} while (token != null);
		}

		private async Task QueryServicePartitions(List<BackupEnabledServiceReference> backupEnabledServices, Application app, Service service, Guid? partitionId)
		{
			string token;
			do
			{
				var partitions = await _fabricClient.QueryManager.GetPartitionListAsync(service.ServiceName, partitionId).ConfigureAwait(true);
				foreach (var partition in partitions)
				{
					ServicePartitionKey key;
					switch (partition.PartitionInformation.Kind)
					{
						//only int64 partitions are supported at this time
						case ServicePartitionKind.Int64Range:
							var longKey = (Int64RangePartitionInformation)partition.PartitionInformation;
							key = new ServicePartitionKey(longKey.LowKey);
							break;
						default:
							continue;
					}
					
					var resolved = await _servicePartitionResolver.ResolveAsync(service.ServiceName, key, CancellationToken.None).ConfigureAwait(true);
					foreach (var endpoint in resolved.Endpoints.Where(e => !string.IsNullOrWhiteSpace(e.Address)))
					{
						QueryPartitionEndpoints(backupEnabledServices, app, service, partition, endpoint);
					}
				}
				token = partitions.ContinuationToken;
			} while (token != null);
		}

		private void QueryPartitionEndpoints(List<BackupEnabledServiceReference> backupEnabledServices, Application app, Service service, Partition partition, ResolvedServiceEndpoint endpoint)
		{
			var endpointJson = JObject.Parse(endpoint.Address);
			var serviceEndpoint = endpointJson["Endpoints"][BackupRestoreService.BackupRestoreServiceEndpointName];
			if (serviceEndpoint != null)
			{
				string endpointAddress = serviceEndpoint.Value<string>();
				var serviceDescription = new BackupEnabledServiceReference
				{
					ApplicationName = app.ApplicationName,
					ServiceName = service.ServiceName,
					Int64RangePartitionGuid = partition.PartitionInformation.Id,
					Endpoint = new Uri(endpointAddress)
				};
				backupEnabledServices.Add(serviceDescription);
			}
		}
	}
}