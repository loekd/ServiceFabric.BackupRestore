using System;
using System.Diagnostics;
using System.Fabric;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Runtime;
using ServiceFabric.BackupRestore;
using System.Text.RegularExpressions;

namespace MyStatefulService
{
    internal static class Program
    {
        /// <summary>
        /// This is the entry point of the service host process.
        /// </summary>
        private static void Main()
        {
            try
            {
                //Register the service with a FileStore.
                ServiceRuntime.RegisterServiceAsync("MyStatefulServiceType",
                    context =>
                    {
                        Regex regEx = new Regex(@"[^a-zA-Z0-9]");
                        string serviceName = regEx.Replace(context.ServiceName.AbsoluteUri, "-").Replace("--", "-").ToLowerInvariant();
                        
                        //enable this line to use the file based central store
                        //var centralBackupStore = CreateFileStore(serviceName);

                        //enable this line to use the azure blob storage based central store
                        var centralBackupStore = CreateBlobStore(serviceName);

                        return new MyStatefulService(context, centralBackupStore, log => ServiceEventSource.Current.ServiceMessage(context, log));

                    }).GetAwaiter().GetResult();

                ServiceEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof(MyStatefulService).Name);

                // Prevents this host process from terminating so services keep running.
                Thread.Sleep(Timeout.Infinite);
            }
            catch (Exception e)
            {
                ServiceEventSource.Current.ServiceHostInitializationFailed(e.ToString());
                throw;
            }
        }

        private static ICentralBackupStore CreateBlobStore(string serviceName)
        {            
            #warning This blob store is configured to use the Azure Storage Emulator. Replace the connection string with an actual storage account connectionstring for production scenarios.
            //see: https://docs.microsoft.com/en-us/azure/storage/common/storage-use-emulator
            var blobStore = new BlobStore("UseDevelopmentStorage=true", serviceName);
            return blobStore;
        }

        private static ICentralBackupStore CreateFileStore(string serviceName)
        {            
            string remoteFolderName = Path.Combine(@"c:\temp", serviceName);
            #warning change this folder in your own project!
            //this should not point to C:\ in production, instead use a mapped network share that stores data outside the cluster.
            //make sure the account running this service has R/W access to the location.
            Directory.CreateDirectory(remoteFolderName);
            var centralBackupStore = new FileStore(remoteFolderName);
            return centralBackupStore;
        }
    }
}
