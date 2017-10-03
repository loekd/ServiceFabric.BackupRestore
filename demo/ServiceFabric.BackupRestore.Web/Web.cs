using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Runtime;
using MyStatefulService;

namespace ServiceFabric.BackupRestore.Web
{
    /// <summary>
    /// The FabricRuntime creates an instance of this class for each service type instance. 
    /// </summary>
    internal sealed class Web : StatelessService
    {
        public Web(StatelessServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Optional override to create listeners (like tcp, http) for this service instance.
        /// </summary>
        /// <returns>The collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new ServiceInstanceListener[]
            {
                new ServiceInstanceListener(serviceContext =>
                    new KestrelCommunicationListener(serviceContext, "ServiceEndpoint", (url, listener) =>
                    {
                        ServiceEventSource.Current.ServiceMessage(serviceContext, $"Starting Kestrel on {url}");

                        return new WebHostBuilder()
                                    .UseKestrel()
                                    .ConfigureServices(
                                        services => services
                                            .AddSingleton<StatelessServiceContext>(serviceContext))
                                    .UseContentRoot(Directory.GetCurrentDirectory())
                                    .UseStartup<Startup>()
                                    .UseServiceFabricIntegration(listener, ServiceFabricIntegrationOptions.None)
                                    .UseUrls(url)
                                    .Build();
                    }))
            };
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            await CreateProxyAsync(-1L);
           // return base.RunAsync(cancellationToken);
        }

        private static readonly Uri ServiceUri = new Uri("fabric:/ServiceFabric.BackupRestore.Demo/MyStatefulService");


        private static async Task<IMyStatefulService> CreateProxyAsync(long partitionKey)
        {
            IMyStatefulService proxy = null;

            while (proxy == null)
            {
                try
                {
                    var servicePartitionKey = new ServicePartitionKey(partitionKey);
                    proxy = ServiceProxy.Create<IMyStatefulService>(ServiceUri, servicePartitionKey, TargetReplicaSelector.Default, BackupRestoreService.BackupRestoreServiceEndpointName);
                    var result = await proxy.ListBackups().ConfigureAwait(false);
                    if (result != null)
                    {
                        break;
                    }

                }
                catch
                {
                    proxy = null;
                    Console.Write(".");
                    await Task.Delay(200);
                }
            }
            return proxy;
        }
    }
}
