using System;
using Microsoft.Azure.Cosmos.Table;

namespace OpenIddict.Ats.Models
{
    public interface ICloudTableClient
    {
        CloudTable GetTableReference(string tableName);
    }

    public class CloudTableClient : Microsoft.Azure.Cosmos.Table.CloudTableClient, ICloudTableClient
    {
        public CloudTableClient(StorageUri storageUri, StorageCredentials credentials) : base(storageUri, credentials)
        {
        }

        public CloudTableClient(Uri baseUri, StorageCredentials credentials, TableClientConfiguration configuration) : base(baseUri, credentials, configuration)
        {
        }

        public CloudTableClient(StorageUri storageUri, StorageCredentials credentials, TableClientConfiguration configuration) : base(storageUri, credentials, configuration)
        {
        }

        CloudTable ICloudTableClient.GetTableReference(string tableName)
        { 
            return GetTableReference(tableName);
        }
    }
}
