using System;

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


		public FileBackupMetadata(string relativeFolder, Guid originalServicePartitionId, DateTime timeStampUtc)
			: base(originalServicePartitionId, timeStampUtc)
		{
			if (string.IsNullOrWhiteSpace(relativeFolder))
				throw new ArgumentException("Value cannot be null or whitespace.", nameof(relativeFolder));
			RelativeFolder = relativeFolder;
		}

		public FileBackupMetadata(string relativeFolder, Guid originalServicePartitionId, DateTime timeStampUtc, Guid backupId)
			: base(originalServicePartitionId, timeStampUtc, backupId)
		{
			if (string.IsNullOrWhiteSpace(relativeFolder))
				throw new ArgumentException("Value cannot be null or whitespace.", nameof(relativeFolder));
			RelativeFolder = relativeFolder; 
		}
	}
}