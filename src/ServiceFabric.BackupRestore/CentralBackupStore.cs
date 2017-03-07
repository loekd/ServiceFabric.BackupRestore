using System;
using Microsoft.ServiceFabric.Data;

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

		public FileBackupMetadata(string relativeFolder, Guid originalServicePartitionId, DateTime timeStampUtc, BackupOption backupOption,  Guid backupId)
			: base(originalServicePartitionId, timeStampUtc, backupOption, backupId)
		{
			if (string.IsNullOrWhiteSpace(relativeFolder))
				throw new ArgumentException("Value cannot be null or whitespace.", nameof(relativeFolder));
			RelativeFolder = relativeFolder; 
		}
	}
}