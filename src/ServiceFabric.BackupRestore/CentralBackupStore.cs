using System;
using Microsoft.ServiceFabric.Data;
using Microsoft.WindowsAzure.Storage;

namespace ServiceFabric.BackupRestore
{
    /// <summary>
    /// Metadata specifically for <see cref="FileStore"/>.
    /// </summary>
    internal class FileBackupMetadata : BackupMetadata
    {
        /// <summary>
        /// Specifies the relative remote folder where a backup is stored.
        /// </summary>
        public string RelativeFolder { get; set; }


        public FileBackupMetadata(string relativeFolder, Guid originalServicePartitionId, DateTime timeStampUtc, BackupOption backupOption)
            : base(originalServicePartitionId, timeStampUtc, backupOption)
        {
            if (string.IsNullOrWhiteSpace(relativeFolder))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(relativeFolder));
            RelativeFolder = relativeFolder;
        }

        public FileBackupMetadata(string relativeFolder, Guid originalServicePartitionId, DateTime timeStampUtc, BackupOption backupOption, Guid backupId)
            : base(originalServicePartitionId, timeStampUtc, backupOption, backupId)
        {
            if (string.IsNullOrWhiteSpace(relativeFolder))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(relativeFolder));
            RelativeFolder = relativeFolder;
        }
    }

    /// <summary>
	/// Metadata specifically for <see cref="BlobStore"/>.
	/// </summary>
	internal class BlobBackupMetadata : BackupMetadata
    {
        public BlobStorageUri BlockBlobUri { get; set; }

        public BlobBackupMetadata(BlobStorageUri blockBlobUri, Guid originalServicePartitionId, DateTime timeStampUtc, BackupOption backupOption)
            : base(originalServicePartitionId, timeStampUtc, backupOption)
        {
            BlockBlobUri = blockBlobUri ?? throw new ArgumentNullException(nameof(blockBlobUri));
        }

        public BlobBackupMetadata(BlobStorageUri blockBlobUri, Guid originalServicePartitionId, DateTime timeStampUtc, BackupOption backupOption, Guid backupId)
            : base(originalServicePartitionId, timeStampUtc, backupOption, backupId)
        {
            BlockBlobUri = blockBlobUri ?? throw new ArgumentNullException(nameof(blockBlobUri));
        }
    }

    public class BlobStorageUri
    {
        /// <summary>
        /// Initializes a new instance using the primary endpoint for the storage account.
        /// </summary>
        /// <param name="storageUri">The azure storage URI.</param>
        /// <exception cref="System.ArgumentNullException">storageUri</exception>

        public BlobStorageUri(StorageUri storageUri)
        {
            if (storageUri == null)
            {
                throw new ArgumentNullException(nameof(storageUri));
            }

            PrimaryUri = storageUri.PrimaryUri;
            SecondaryUri = storageUri.SecondaryUri;
        }

        /// <summary>
        /// Creates a new default instance.
        /// </summary>
        public BlobStorageUri()
        { }

        /// <summary>
        /// The endpoint for the primary location for the storage account.
        /// </summary>
        public Uri PrimaryUri { get; set; }
        /// <summary>
        /// The endpoint for the secondary location for the storage account.
        /// </summary>
        public Uri SecondaryUri { get; set; }
    }
    }