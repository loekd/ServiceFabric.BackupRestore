# ServiceFabric.BackupRestore
ServiceFabric.BackupRestore simplifies creating and restoring backups for Reliable Stateful Service replicas.

## Change log
- 1.0.0 
	Added support for incremental backups.
	Added support for ActorServices using `BackupRestoreActorService`

- 0.9.2 First version. 
  
  Has 1 central store implementation: FileStore.
  Make sure to store your backup files outside of your cluster. 
  Contributions of implementations of ICentralBackupStore more than welcome! 

## Enable your Stateful Service for Backup & Restore:

1. Add the nuget package https://www.nuget.org/packages/ServiceFabric.BackupRestore/
2. Have your Stateful Service inherit from ```BackupRestoreService```

  ``` csharp
  internal sealed class MyStatefulService : BackupRestoreService, IMyStatefulService
  {
    public MyStatefulService(StatefulServiceContext context, ICentralBackupStore centralBackupStore, Action<string> logCallback) 
			: base(context, centralBackupStore, logCallback)
		{
		}
  }
  ```
3. Change Program.Main to provide an implementation of ```ICentralBackupStore``` e.g. the ```FileStore```:

	``` csharp
  //Register the service with a FileStore.
  ServiceRuntime.RegisterServiceAsync("MyStatefulServiceType",
    context =>
    {
      string serviceName = context.ServiceName.AbsoluteUri.Replace(":", string.Empty).Replace("/", "-");
      string remoteFolderName = Path.Combine(@"E:\sfbackups", serviceName);
      //The E drive is a mapped network share to a File Server outside of the cluster here.
      //make sure the account running this service has R/W access to that location.
      var centralBackupStore = new FileStore(remoteFolderName);

      return new MyStatefulService(context, centralBackupStore, log => ServiceEventSource.Current.ServiceMessage(context, log));

    }).GetAwaiter().GetResult();
  ```  
   
4. Enable communication with your service, for instance using SF Remoting

   ``` csharp
   internal sealed class MyStatefulService : BackupRestoreService, IMyStatefulService
    {
      [..]
  	  protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
		  {
			  yield return new ServiceReplicaListener(this.CreateServiceRemotingListener, BackupRestoreServiceEndpointName);
		  }
    }
  ```
  
## Calling application
1. Create an application that calls your Service to perform Backup & Restore operations
2. Add the nuget package to your calling application too:  https://www.nuget.org/packages/ServiceFabric.BackupRestore/
3. Add a reference to the Stateful Service project
4. Create a backup asynchronously

  ``` csharp
  var proxy = ServiceProxy.Create<IMyStatefulService>(ServiceUri, servicePartitionKey);
  await proxy.BeginCreateBackup();
  ```
5. List all central backups
 
  ``` csharp
  var proxy = ServiceProxy.Create<IMyStatefulService>(ServiceUri, servicePartitionKey);
  var list = await proxy.ListBackups();
						Console.WriteLine($"Backup Id\t\t\t\tOriginal partition");
						Console.WriteLine(string.Join(Environment.NewLine, list.Select(data => $"             {data.BackupId}\t{data.OriginalServicePartitionId}")));
  ```
6. Restore a backup asynchronously
 
  ``` csharp
  var proxy = ServiceProxy.Create<IMyStatefulService>(ServiceUri, servicePartitionKey);
  var backups = (await proxy.ListBackups()).ToList();
  int index = 0; //or any other list item!
  await proxy.BeginRestoreBackup(backups[index]);
  ```