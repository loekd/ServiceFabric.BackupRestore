using System;

namespace ServiceFabric.BackupRestore.Web.Models
{
    public class BackupEnabledServiceReference
    {
	    public Uri ApplicationName { get; set; }

	    public Uri ServiceName { get; set; }

	    public Guid Int64RangePartitionGuid { get; set; }

	    public Uri Endpoint { get; set; }

    }
}
