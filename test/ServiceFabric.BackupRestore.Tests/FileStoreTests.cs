using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ServiceFabric.BackupRestore.Tests
{
	[TestClass]
	public class FileStoreTests
	{
		private static readonly string Remote = Path.Combine(Path.GetTempPath(), "FileStoreTests", "Remote");
		private static readonly string Local = Path.Combine(Path.GetTempPath(), "FileStoreTests", "Local");

		[ClassCleanup]
		public static void ClassCleanup()
		{
			Cleanup();
		}

		private static void Cleanup()
		{
			// ReSharper disable EmptyGeneralCatchClause

			try
			{
				Directory.Delete(Remote, true);
			}
			catch
			{

			}
			try
			{
				Directory.Delete(Local, true);
			}
			catch
			{

			}
		}

		[TestInitialize]
		public void ClassInitialize()
		{
			Cleanup();
		}

		[TestMethod]
		public void TestCtor()
		{
			var store = new FileStore(@"c:\temp");
			Assert.IsInstanceOfType(store, typeof(ICentralBackupStore));
		}

		[TestMethod]
		public void TestCtorFail()
		{
			// ReSharper disable UnusedVariable

			Assert.ThrowsException<ArgumentException>(() =>
			{
				var store = new FileStore(null);
			});

			Assert.ThrowsException<ArgumentException>(() =>
			{
				var store = new FileStore(string.Empty);
			});

			Assert.ThrowsException<ArgumentException>(() =>
			{
				var store = new FileStore(" ");
			});
		}


		[TestMethod]
		public async Task TestUploadBackupFolderAsync()
		{
			//setup
			const string content = "content";
			const string testTxt = "test.txt";
			const string subFolderName = "Sub";

			var partitionId = Guid.NewGuid();
			string partitionFolder = partitionId.ToString("n");

			//prepare a folder and a file in the local store:
			Directory.CreateDirectory(Local);
			File.WriteAllText(Path.Combine(Local, testTxt), content);

			string subFolder = Path.Combine(Local, subFolderName);
			Directory.CreateDirectory(subFolder);
			File.WriteAllText(Path.Combine(subFolder, testTxt), content);

			var store = new FileStore(Remote);

			//act
			var result = await store.UploadBackupFolderAsync(BackupOption.Full, partitionId, Local, CancellationToken.None);
			string dateTimeFolder = store.CreateDateTimeFolderName(partitionId, result.TimeStampUtc);

			//asserts
			Assert.IsInstanceOfType(result, typeof(BackupMetadata));
			Assert.AreEqual(partitionId, result.OriginalServicePartitionId);
			Assert.AreNotEqual(Guid.Empty, result.BackupId);

			Assert.IsTrue(Directory.Exists(Path.Combine(Remote)));
			Assert.IsTrue(File.Exists(Path.Combine(dateTimeFolder, testTxt)));
			Assert.IsTrue(Directory.Exists(Path.Combine(dateTimeFolder, subFolderName)));
			Assert.IsTrue(File.Exists(Path.Combine(dateTimeFolder, subFolderName, testTxt)));

			Assert.AreEqual(content, File.ReadAllText(Path.Combine(dateTimeFolder, testTxt)));
			Assert.AreEqual(content, File.ReadAllText(Path.Combine(dateTimeFolder, subFolderName, testTxt)));
		}


		[TestMethod]
		public async Task TestDownloadBackupFolderAsync()
		{
			//setup
			const string content = "content";
			const string testTxt = "test.txt";
			const string subFolderName = "Sub";

			Guid partitionId = Guid.NewGuid();
			Guid backupId = Guid.NewGuid();
			var timeStampUtc = DateTime.UtcNow;


			//prepare a remote folder and a file in the store:
			var store = new FileStore(Remote);

			Directory.CreateDirectory(Remote);
			string backupFolder = store.CreateDateTimeFolderName(partitionId, timeStampUtc);
			Directory.CreateDirectory(backupFolder);
			File.WriteAllText(Path.Combine(backupFolder, testTxt), content);

			string subFolder = Path.Combine(backupFolder, subFolderName);
			Directory.CreateDirectory(subFolder);
			File.WriteAllText(Path.Combine(subFolder, testTxt), content);

			

			//save backup metadata
			await store.StoreBackupMetadataAsync(backupFolder, new BackupMetadata(partitionId, timeStampUtc, BackupOption.Full, backupId));

			//fake SF backup metadata
			File.WriteAllText(Path.Combine(backupFolder, FileStore.BackupMetadataFileName), "");

			//act
			await store.DownloadBackupFolderAsync(backupId, Local, CancellationToken.None);

			//asserts

			Assert.IsTrue(Directory.Exists(Path.Combine(Local)));
			Assert.IsTrue(File.Exists(Path.Combine(Local, testTxt)));
			Assert.IsTrue(Directory.Exists(Path.Combine(Local, subFolderName)));
			Assert.IsTrue(File.Exists(Path.Combine(Local, subFolderName, testTxt)));

			Assert.AreEqual(content, File.ReadAllText(Path.Combine(Local, testTxt)));
			Assert.AreEqual(content, File.ReadAllText(Path.Combine(Local, subFolderName, testTxt)));
		}
	}
}
