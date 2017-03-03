using System;
using System.Runtime.Serialization;
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

		//maybe add file hashes?

		/// <summary>
		/// Creates a new instance using the provided arguments.
		/// </summary>
		/// <param name="originalServicePartitionId">Indicates the Stateful Service Parition Guid that the backup was created for.</param>
		/// <param name="timeStampUtc">Indicates when the backup was created.</param>
		public BackupMetadata(Guid originalServicePartitionId, DateTime timeStampUtc)
			 : this(originalServicePartitionId, timeStampUtc, Guid.NewGuid())
		{
		}

		/// <summary>
		/// Creates a new instance using the provided arguments.
		/// </summary>
		/// <param name="originalServicePartitionId">Indicates the Stateful Service Parition Guid that the backup was created for.</param>
		/// <param name="timeStampUtc">Indicates when the backup was created.</param>
		/// <param name="backupId">Unique identifier for a backup.</param>
		[JsonConstructor]
		public BackupMetadata(Guid originalServicePartitionId, DateTime timeStampUtc, Guid backupId)
		{
			BackupId = backupId;
			OriginalServicePartitionId = originalServicePartitionId;
			TimeStampUtc = timeStampUtc;
		}
	}
}