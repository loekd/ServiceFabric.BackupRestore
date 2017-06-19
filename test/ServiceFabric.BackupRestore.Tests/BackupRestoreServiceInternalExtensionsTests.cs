using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data;
using System.Fabric;
using System.Threading;
using Moq;

namespace ServiceFabric.BackupRestore.Tests
{
    [TestClass]
    public class BackupRestoreServiceInternalExtensionsTests
    {

        [TestMethod]
        public async Task TestGetBackupMetadataAsync_OneFullBackup_Present()
        {
            //arrange
            Guid partitionId = Guid.NewGuid();
            DateTime now = DateTime.UtcNow;
            BackupMetadata backupMetadata = new BackupMetadata(partitionId, now, BackupOption.Full);
                           
            var mockBackupStoreObject = new Moq.Mock<ICentralBackupStore>();            
            var mockServiceObject = new Moq.Mock<IBackupRestoreServiceInternal>();
            mockServiceObject.Setup(service => service.CentralBackupStore).Returns(mockBackupStoreObject.Object);
            mockServiceObject.Setup(service => service.Context).Returns(Mocks.MockStatefulServiceContextFactory.Default);

            //act
            var result = await BackupRestoreServiceInternalExtensions.GetBackupMetadataAsync(mockServiceObject.Object, backupMetadata);

            //assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(backupMetadata, result[0]);
        }



        [TestMethod]
        public async Task TestGetBackupMetadataAsync_OneFullBackup_AndOneIncrementalBackup_Present()
        {
            //arrange
            Guid partitionId = Guid.NewGuid();
            DateTime now = DateTime.UtcNow;

            BackupMetadata backupMetadata = new BackupMetadata(partitionId, now, BackupOption.Full);
            BackupMetadata incrementalMetadata = new BackupMetadata(partitionId, now.AddHours(1), BackupOption.Incremental);
                       
            var mockBackupStoreObject = new Moq.Mock<ICentralBackupStore>();
            mockBackupStoreObject
                .Setup(store => store.RetrieveScheduledBackupAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult(incrementalMetadata));

            mockBackupStoreObject
                .Setup(store => store.GetBackupMetadataAsync(null, It.IsAny<Guid>()))
                .Returns(Task.FromResult<IEnumerable<BackupMetadata>>(new[] { backupMetadata, incrementalMetadata }));
                        
            var mockServiceObject = new Moq.Mock<IBackupRestoreServiceInternal>();
            mockServiceObject.Setup(service => service.CentralBackupStore).Returns(mockBackupStoreObject.Object);
            mockServiceObject.Setup(service => service.Context).Returns(Mocks.MockStatefulServiceContextFactory.Default);

            //act
            var result = await BackupRestoreServiceInternalExtensions.GetBackupMetadataAsync(mockServiceObject.Object, incrementalMetadata);

            //assert
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(backupMetadata, result[0]);
            Assert.AreEqual(incrementalMetadata, result[1]);
        }

        [TestMethod]
        public async Task TestGetBackupMetadataAsync_OneFullBackup_AndTwoIncrementalBackup_Present()
        {
            //arrange
            Guid partitionId = Guid.NewGuid();
            DateTime now = DateTime.UtcNow;

            BackupMetadata backupMetadata = new BackupMetadata(partitionId, now, BackupOption.Full);
            BackupMetadata incrementalMetadata = new BackupMetadata(partitionId, now.AddHours(1), BackupOption.Incremental);
            BackupMetadata incrementalMetadataTwo = new BackupMetadata(partitionId, now.AddHours(2), BackupOption.Incremental);

            var mockBackupStoreObject = new Moq.Mock<ICentralBackupStore>();
            mockBackupStoreObject
                .Setup(store => store.RetrieveScheduledBackupAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult(incrementalMetadata));

            mockBackupStoreObject
                .Setup(store => store.GetBackupMetadataAsync(null, It.IsAny<Guid>()))
                .Returns(Task.FromResult<IEnumerable<BackupMetadata>>(new[] { backupMetadata, incrementalMetadata, incrementalMetadataTwo }));

            var mockServiceObject = new Moq.Mock<IBackupRestoreServiceInternal>();
            mockServiceObject.Setup(service => service.CentralBackupStore).Returns(mockBackupStoreObject.Object);
            mockServiceObject.Setup(service => service.Context).Returns(Mocks.MockStatefulServiceContextFactory.Default);

            //act
            var result = await BackupRestoreServiceInternalExtensions.GetBackupMetadataAsync(mockServiceObject.Object, incrementalMetadataTwo);

            //assert
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(backupMetadata, result[0]);
            Assert.AreEqual(incrementalMetadata, result[1]);
            Assert.AreEqual(incrementalMetadataTwo, result[2]);
        }


        [TestMethod]
        public async Task TestGetBackupMetadataAsync_TwoFullBackups_AndFourIncrementalBackup_Present()
        {
            //arrange
            Guid partitionId = Guid.NewGuid();
            DateTime now = DateTime.UtcNow;

            BackupMetadata backupMetadata = new BackupMetadata(partitionId, now, BackupOption.Full);
            BackupMetadata incrementalMetadata = new BackupMetadata(partitionId, now.AddHours(1), BackupOption.Incremental);
            BackupMetadata incrementalMetadataTwo = new BackupMetadata(partitionId, now.AddHours(2), BackupOption.Incremental);

            BackupMetadata backupMetadataTwo = new BackupMetadata(partitionId, now.AddDays(1), BackupOption.Full);
            BackupMetadata incrementalMetadataThree = new BackupMetadata(partitionId, now.AddDays(1).AddHours(1), BackupOption.Incremental);
            BackupMetadata incrementalMetadataFour = new BackupMetadata(partitionId, now.AddDays(1).AddHours(2), BackupOption.Incremental);


            var mockBackupStoreObject = new Moq.Mock<ICentralBackupStore>();
            mockBackupStoreObject
                .Setup(store => store.RetrieveScheduledBackupAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult(incrementalMetadata));

            mockBackupStoreObject
                .Setup(store => store.GetBackupMetadataAsync(null, It.IsAny<Guid>()))
                .Returns(Task.FromResult<IEnumerable<BackupMetadata>>(new[] 
                {
                    backupMetadata,
                    incrementalMetadata,
                    incrementalMetadataTwo,
                    backupMetadataTwo,
                    incrementalMetadataThree,
                    incrementalMetadataFour
                }));

            var mockServiceObject = new Moq.Mock<IBackupRestoreServiceInternal>();
            mockServiceObject.Setup(service => service.CentralBackupStore).Returns(mockBackupStoreObject.Object);
            mockServiceObject.Setup(service => service.Context).Returns(Mocks.MockStatefulServiceContextFactory.Default);

            //act
            var result = await BackupRestoreServiceInternalExtensions.GetBackupMetadataAsync(mockServiceObject.Object, incrementalMetadataTwo);

            //assert
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(backupMetadata, result[0]);
            Assert.AreEqual(incrementalMetadata, result[1]);
            Assert.AreEqual(incrementalMetadataTwo, result[2]);

            //act
            result = await BackupRestoreServiceInternalExtensions.GetBackupMetadataAsync(mockServiceObject.Object, incrementalMetadataFour);

            //assert
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(backupMetadataTwo, result[0]);
            Assert.AreEqual(incrementalMetadataThree, result[1]);
            Assert.AreEqual(incrementalMetadataFour, result[2]);
        }

        public void DeleteMeAfterDatalossIssueIsResolved()
        {
            Assert.Fail("Block release of nuget package.");
        }
    }
   

}
