# ServiceFabric.BackupRestore
ServiceFabric.BackupRestore simplifies creating and restoring backups for Reliable Stateful Service replicas. Store your backups safely outside of the cluster by using / implementing ICentralBackupStore.
Just implement an interface!

## Change log
- 3.1.3
	- Upgraded nuget packages (SF 3.0.480) 

- 3.1.1
	- Upgraded nuget packages (SF 2.8.232)

- 3.1.0
	- Upgraded nuget packages (SF 2.8.211)

- 3.0.0
	- Added BlobStore that saves backup data in an Azure Storage Account
	- No longer needed to inherit Stateful Services, just implement one or two interfaces. (added sample code below)

- 2.3.1 
	- Upgraded nuget packages (SF 2.7.198)

- 2.3.0
	- Upgraded nuget packages (SF 2.6.220)

- 2.2.0
	- Merged PR by bitTobiasMeier to fix issue in Actor backups
	- Merged PR by ypom to fix issue with multiple full backups
	- Fixed more issues in restoring backups 

- 2.1.0
	- Upgraded nuget packages (SF 2.6.210)

- 2.0.0
	- Upgraded sln to VS2017
	- Upgraded nuget packages (SF 2.6.204)

- 1.2.0
	- Upgraded nuget packages (SF 2.5.216, JSON 10.0.1)

- 1.1.0 
	- Added support for DataLossMode when restoring backups.
	- Added support for all partition types.

- 1.0.0 
	- Added support for incremental backups.
	- Added support for ActorServices using `BackupRestoreActorService`

- 0.9.2 First version. 
  - This version has only 1 central store implementation: FileStore.
  - It works for Stateful Services only.
  - It takes full backups, and performs forced restores.
  - Make sure to store your backup files outside of your cluster. 
  - Contributions are more than welcome! 

## Demo
Run the demo app on your local dev cluster, to see how it works.
https://github.com/loekd/ServiceFabric.BackupRestore/tree/master/demo
Change the code in https://github.com/loekd/ServiceFabric.BackupRestore/blob/master/demo/MyStatefulService/Program.cs use the Azure Blob based store, or the File System based store.

## Enable your Stateful Service for Backup & Restore:

1. Add the nuget package https://www.nuget.org/packages/ServiceFabric.BackupRestore/
2. Have your Stateful Service implement ```IBackupRestoreServiceOperations``` 
 Inject an instance of a type that implements `ICentralBackupStore`, for example `IBlobStore` or `IFileStore`. You can also implement your own types.
 To implement ```IBackupRestoreServiceOperations```, delegate most of the work to `BackupRestoreServiceOperations`.

  ``` csharp
  internal sealed class MyStatefulService : StatefulService, IBackupRestoreServiceOperations, IMyStatefulService
  {
		public MyStatefulService(StatefulServiceContext context, ICentralBackupStore centralBackupStore, Action<string> logCallback) 
				: base(context)
		{
			_centralBackupStore = centralBackupStore ?? throw new ArgumentNullException(nameof(centralBackupStore));
		}

		//////NOT IN INTERFACE:
		
		/// <inheritdoc />
        protected sealed override Task<bool> OnDataLossAsync(RestoreContext restoreCtx, CancellationToken cancellationToken)
        {
            //after data loss, we'll restore a backup here:
            return BackupRestoreServiceOperations.OnDataLossAsync(this, restoreCtx, cancellationToken);
        }

		
		/////IN INTERFACE: 

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
  ```
3. Change Program.Main to provide an implementation of ```ICentralBackupStore``` e.g. the `BlobStore` or `FileStore`:

``` csharp
    ServiceRuntime.RegisterServiceAsync("MyStatefulServiceType", context =>
    {       
          //Use the blob store, combined with an Azure Storage Account, or the Storage Emulator for testing.	 
      	  var centralBackupStore = new BlobStore("UseDevelopmentStorage=true", serviceName);
	  //Or the file store:
	  string serviceName = context.ServiceName.AbsoluteUri.Replace(":", string.Empty).Replace("/", "-");
          string remoteFolderName = Path.Combine(@"E:\sfbackups", serviceName);
          //The E drive is a mapped network share to a File Server outside of the cluster here.
          //make sure the account running this service has R/W access to that location.
          var centralBackupStore = new FileStore(remoteFolderName);
          return new MyStatefulService(context, centralBackupStore, log => ServiceEventSource.Current.ServiceMessage(context, log)); 
    }).GetAwaiter().GetResult();
```  
   
4. Optionally, enable communication with your service, for instance using SF Remoting by implementing the interface `IBackupRestoreService`.
Again, delegate the most of the work of the operations, to `BackupRestoreServiceOperations`.

   ``` csharp
   internal sealed class MyStatefulService : StatefulService, IBackupRestoreServiceOperations, IMyStatefulService, IBackupRestoreService
    {
		//////NOT IN INTERFACE:

		[..]
  		protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
		{
			//Enable interaction, to allow external callers to trigger backups and restores, by using Service Remoting through IBackupRestoreService
			yield return new ServiceReplicaListener(this.CreateServiceRemotingListener, BackupRestoreService.BackupRestoreServiceEndpointName);
		}



		//////IN INTERFACE:

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
    }
```

``` csharp
```

### Inheritance is optional

You can also implement the required interfaces by inheriting from `ServiceFabric.BackupRestore.BackupRestoreService` or `ServiceFabric.BackupRestore.BackupRestoreActorService`.

  
## Optional calling application

1. Create an application that calls your Service to perform Backup & Restore operations
2. Add the nuget package to your calling application too:  https://www.nuget.org/packages/ServiceFabric.BackupRestore/
3. Add a reference to the Stateful Service project
4. Create a full backup asynchronously

  ``` csharp
  var proxy = ServiceProxy.Create<IMyStatefulService>(ServiceUri, servicePartitionKey);
  await proxy.BeginCreateBackup(BackupOption.Full);  //use BackupOption.Incremental for incremental backup
  ```
5. List all central backups
 
  ``` csharp
  var proxy = ServiceProxy.Create<IMyStatefulService>(ServiceUri, servicePartitionKey);
  var list = await proxy.ListAllBackups();
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
