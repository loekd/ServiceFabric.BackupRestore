using System;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using MyStatefulService;

namespace TestConsole
{
	internal class Program
	{
		private static readonly Uri ServiceUri;

		static Program()
		{
			ServiceUri = new Uri("fabric:/ServiceFabric.BackupRestore.Demo/MyStatefulService");
		}

		private static void Main()
		{
			Task.WaitAll(Task.Run(async () =>
			{
				try
				{
					await MainAsync();
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex);
					Console.WriteLine("There was an error. Press any key to exit.");
					Console.ReadKey(true);
				}
			}));
		}

		private static async Task MainAsync()
		{
			Console.WriteLine("Waiting for services....");

			var proxyPartitionOne = await CreateProxyAsync(-1L);
			var proxyPartitionTwo = await CreateProxyAsync(1L);
			var proxy = proxyPartitionOne;

			Console.WriteLine("Waited for services..");


			while (true)
			{
				Console.WriteLine($"Press any key to continue");
				Console.ReadKey(true);
				Console.Clear();

				Console.WriteLine("Press 0 to select target partition");
				Console.WriteLine("Press 1 to get state");
				Console.WriteLine("Press 2 to set state");
				Console.WriteLine("Press 3 to create a backup");
				Console.WriteLine("Press 4 to restore a backup");
				Console.WriteLine("Press 5 to list all central backups");
				Console.WriteLine("Press 6 to list the current Service Partition Ids");
				Console.WriteLine("Other key to exit");

				var key = Console.ReadKey(true);
				string input;

				switch (key.Key)
				{
					case ConsoleKey.D0:
						Console.WriteLine("Type 1 for partition one, or 2 for partition two");
						key = Console.ReadKey(true);
						if (ConsoleKey.D2 == key.Key)
						{
							proxy = proxyPartitionTwo;
							Console.WriteLine("Using partition two.");
						}
						else
						{
							proxy = proxyPartitionOne;
							Console.WriteLine("Using partition one.");
						}
						break;

					case ConsoleKey.D1:
						string state = await proxy.GetState();
						Console.WriteLine($"State: '{state}'");
						break;
					case ConsoleKey.D2:
						Console.WriteLine("Enter string to store as state:");
						input = Console.ReadLine();
						await proxy.SetState(input ?? "");
						Console.WriteLine($"State saved: '{input}'");
						break;
					case ConsoleKey.D3:
						Console.WriteLine("Type 1 for full backup or 2 for incremental backup (incremental requires full backup to exist)");
						key = Console.ReadKey(true);
						if (ConsoleKey.D1 == key.Key)
						{
							Console.WriteLine("Creating a full backup asynchronously...");
							await proxy.BeginCreateBackup(BackupOption.Full);
						}
						else
						{
							Console.WriteLine("Creating an incremental backup asynchronously...");
							await proxy.BeginCreateBackup(BackupOption.Incremental);
						}
						
						break;
					case ConsoleKey.D4:
						Console.WriteLine($"Starting the restore of a backup");
						Console.WriteLine($"Enter central backup id (guid):");
						input = Console.ReadLine();

						var backups = (await proxy.ListBackups()).ToList();
						Guid index;
						if (Guid.TryParse(input, out index))
						{
							DataLossMode lossMode = DataLossMode.FullDataLoss;
							Console.WriteLine("Type 1 for full data loss or 2 for partial data loss.");

							key = Console.ReadKey(true);
							if (ConsoleKey.D1 == key.Key)
							{
								Console.WriteLine("Restoring backup with full data loss asynchronously...");
							}
							else
							{
								Console.WriteLine("Restoring backup with partial data loss asynchronously...");
								lossMode = DataLossMode.PartialDataLoss;
							}
							
							await proxy.BeginRestoreBackup(backups.Single(b => b.BackupId == index), lossMode);
							Console.WriteLine($"Restore is active. This will take some time. Check progress in SF explorer.");
						}

						break;
					case ConsoleKey.D5:
						Console.WriteLine($"List all central backups");
						var list = await proxy.ListBackups();
						Console.WriteLine($"Original partition\t\t\tBackup Id\t\t\t\tBackup Type");
						Console.WriteLine(string.Join(Environment.NewLine, list.Select(data => $"{data.BackupId}\t{data.OriginalServicePartitionId}\t{data.BackupOption}")));
						break;

					case ConsoleKey.D6:
						var resolver = ServicePartitionResolver.GetDefault();
						var resolved = await resolver.ResolveAsync(ServiceUri, new ServicePartitionKey(-1L), CancellationToken.None);
						Console.WriteLine($"Partition key -1L resolves to partition {resolved.Info.Id}");
						resolved = await resolver.ResolveAsync(ServiceUri, new ServicePartitionKey(1L), CancellationToken.None);
						Console.WriteLine($"Partition key 1L resolves to partition {resolved.Info.Id}");

						if (proxy == proxyPartitionOne)
						{
							Console.WriteLine("Using partition one (-1L)");
						}
						else
						{
							Console.WriteLine("Using partition two (1L)");
						}
						break;
					default:
						return;
				}
			}
		}

		private static async Task<IMyStatefulService> CreateProxyAsync(long partitionKey)
		{
			IMyStatefulService proxy = null;

			while (proxy == null)
			{
				try
				{
					var servicePartitionKey = new ServicePartitionKey(partitionKey);
					proxy = ServiceProxy.Create<IMyStatefulService>(ServiceUri, servicePartitionKey);
					var result = await proxy.ListBackups();
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
