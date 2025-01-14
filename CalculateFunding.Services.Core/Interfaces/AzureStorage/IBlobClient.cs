﻿using Microsoft.Azure.Storage.Blob;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Core.Interfaces.AzureStorage
{
    public interface IBlobClient
    {
        Task<(bool Ok, string Message)> IsHealthOk();

        string GetBlobSasUrl(string blobName, DateTimeOffset finish,
            SharedAccessBlobPermissions permissions);

        ICloudBlob GetBlockBlobReference(string blobName);

        Task<ICloudBlob> GetBlobReferenceFromServerAsync(string blobName);

        Task<bool> BlobExistsAsync(string blobName);

        Task<Stream> DownloadToStreamAsync(ICloudBlob blob);

        void Initialize();

        Task UploadAsync(ICloudBlob blob, string data);
    }
}
