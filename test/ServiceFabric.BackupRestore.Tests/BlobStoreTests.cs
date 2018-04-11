using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Microsoft.WindowsAzure.Storage.Blob;

namespace ServiceFabric.BackupRestore.Tests
{
    [TestClass]
    public class BlobStoreTests
    {
        private static readonly string Remote = Path.Combine(Path.GetTempPath(), "BlobStoreTests", "Remote");
        private static readonly string Local = Path.Combine(Path.GetTempPath(), "BlobStoreTests", "Local");
        private const string ContainerName = "servicefabricbackups";

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
                var store = new BlobStore("UseDevelopmentStorage=true", ContainerName);
                store.InitializeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                store.DeleteContainer();
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
            var store = new BlobStore("UseDevelopmentStorage=true", ContainerName);
            Assert.IsInstanceOfType(store, typeof(ICentralBackupStore));
        }

        [TestMethod]
        public void TestCtorFail()
        {
            // ReSharper disable UnusedVariable

            Assert.ThrowsException<ArgumentException>(() =>
            {
                var store = new BlobStore((string)null, ContainerName);
            });

            Assert.ThrowsException<ArgumentNullException>(() =>
            {
                var store = new BlobStore((CloudBlobClient)null, ContainerName);
            });

            Assert.ThrowsException<ArgumentException>(() =>
            {
                var store = new BlobStore(string.Empty, ContainerName);
            });

            Assert.ThrowsException<ArgumentException>(() =>
            {
                var store = new BlobStore(" ", ContainerName);
            });
        }


        [TestMethod]
        public async Task TestUploadBackupFolderAsync()
        {
            if (!CanOperate())
            {
                Assert.Inconclusive("Can't run at this machine!");
            }

            //setup
            const string content = "content";
            const string testTxt = "test.txt";
            const string subFolderName = "Sub";

            var partitionId = Guid.Parse("{92DA7CA2-CE18-497C-84DB-428B8C476994}");
            string partitionFolder = partitionId.ToString("n");

            //prepare a folder and a file in the local store:
            Directory.CreateDirectory(Local);
            File.WriteAllText(Path.Combine(Local, testTxt), content);

            string subFolder = Path.Combine(Local, subFolderName);
            Directory.CreateDirectory(subFolder);
            File.WriteAllText(Path.Combine(subFolder, testTxt), content);

            BlobStore store = new BlobStore("UseDevelopmentStorage=true", ContainerName);
            await store.InitializeAsync();
            //act
            var result = await store.UploadBackupFolderAsync(BackupOption.Full, partitionId, Local, CancellationToken.None);

            //asserts
            Assert.IsInstanceOfType(result, typeof(BackupMetadata));
            Assert.AreEqual(partitionId, result.OriginalServicePartitionId);
            Assert.AreNotEqual(Guid.Empty, result.BackupId);

            var container = store.BlobClient.GetContainerReference(ContainerName);
            Assert.IsTrue(container.ExistsAsync().ConfigureAwait(false).GetAwaiter().GetResult());

            var rootFolder = container.GetDirectoryReference(BlobStore.RootFolder);

            var list = rootFolder.ListBlobsSegmentedAsync(true, BlobListingDetails.All, 10, new BlobContinuationToken(), null, null)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult()
                .Results
                .ToList();
            Assert.AreEqual(2, list.Count(file => file.Uri.AbsoluteUri.EndsWith(testTxt)));
            Assert.AreEqual(1, list.Count(file => file.Uri.AbsoluteUri.EndsWith($"{subFolderName}/{testTxt}")));
            Assert.AreEqual(1, list.Count(file => file.Uri.AbsoluteUri.EndsWith(FileStore.ServiceFabricBackupRestoreMetadataFileName)));            
        }


        [TestMethod]
        public async Task TestDownloadBackupFolderAsync()
        {
            if (!CanOperate())
            {
                Assert.Inconclusive("Can't run at this machine!");
            }

            //setup
            const string content = "content";
            const string testTxt = "test.txt";
            const string subFolderName = "Sub";

            var partitionId = Guid.Parse("{92DA7CA2-CE18-497C-84DB-428B8C476994}");
            string partitionFolder = partitionId.ToString("n");
            
            //prepare a folder and a file in the local store:
            Directory.CreateDirectory(Local);
            File.WriteAllText(Path.Combine(Local, testTxt), content);

            string subFolder = Path.Combine(Local, subFolderName);
            Directory.CreateDirectory(subFolder);
            File.WriteAllText(Path.Combine(subFolder, testTxt), content);

            BlobStore store = new BlobStore("UseDevelopmentStorage=true", ContainerName);
            await store.InitializeAsync();
            //act
            var result = await store.UploadBackupFolderAsync(BackupOption.Full, partitionId, Local, CancellationToken.None);
            Guid backupId = result.BackupId;
            await store.DownloadBackupFolderAsync(backupId, Local, CancellationToken.None);

            //asserts

            Assert.IsTrue(Directory.Exists(Path.Combine(Local)));
            Assert.IsTrue(File.Exists(Path.Combine(Local, testTxt)));
            Assert.IsTrue(Directory.Exists(Path.Combine(Local, subFolderName)));
            Assert.IsTrue(File.Exists(Path.Combine(Local, subFolderName, testTxt)));

            Assert.AreEqual(content, File.ReadAllText(Path.Combine(Local, testTxt)));
            Assert.AreEqual(content, File.ReadAllText(Path.Combine(Local, subFolderName, testTxt)));
        }

        private bool CanOperate()
        {
            try
            {
                return Directory.Exists(@"C:\Program Files (x86)\Microsoft SDKs\Azure\Storage Emulator");
            }
            catch
            {
                return false;
            }
        }
    }
}
