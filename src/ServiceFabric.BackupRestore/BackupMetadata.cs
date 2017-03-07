using System;
using System.Runtime.Serialization;
using Microsoft.ServiceFabric.Data;
using Newtonsoft.Json;

namespace ServiceFabric.BackupRestore
{
	/// <summary>
	/// Metadata for ServiceFabric.BackupRestore backup.
	/// </summary>
	[DataContract]
	public class BackupMetadata
	{
		/// <summary>
		/// Unique identifier for a backup.
		/// </summary>
		[DataMember]
		public readonly Guid BackupId;

		/// <summary>
		/// Indicates the Stateful Service Parition Guid that the backup was created for.
		/// </summary>
		[DataMember]
		public readonly Guid OriginalServicePartitionId;

		/// <summary>
		/// Indicates when the backup was created.
		/// </summary>
		[DataMember]
		public readonly DateTime TimeStampUtc;

		/// <summary>
		/// Indicates the type of backup.
		/// </summary>
		[DataMember]
		public readonly BackupOption BackupOption;

		//maybe add file hashes?

		/// <summary>
		/// Creates a new instance using the provided arguments.
		/// </summary>
		/// <param name="originalServicePartitionId">Indicates the Stateful Service Parition Guid that the backup was created for.</param>
		/// <param name="timeStampUtc">Indicates when the backup was created.</param>
		/// <param name="backupOption">Indicates the type of backup.</param>
		public BackupMetadata(Guid originalServicePartitionId, DateTime timeStampUtc, BackupOption backupOption)
			 : this(originalServicePartitionId, timeStampUtc, backupOption, Guid.NewGuid())
		{
		}

		/// <summary>
		/// Creates a new instance using the provided arguments.
		/// </summary>
		/// <param name="originalServicePartitionId">Indicates the Stateful Service Parition Guid that the backup was created for.</param>
		/// <param name="timeStampUtc">Indicates when the backup was created.</param>
		/// <param name="backupOption">Indicates the type of backup.</param>
		/// <param name="backupId">Unique identifier for a backup.</param>
		[JsonConstructor]
		public BackupMetadata(Guid originalServicePartitionId, DateTime timeStampUtc, BackupOption backupOption, Guid backupId)
		{
			BackupId = backupId;
			OriginalServicePartitionId = originalServicePartitionId;
			TimeStampUtc = timeStampUtc;
			BackupOption = backupOption;
		}
	}
}