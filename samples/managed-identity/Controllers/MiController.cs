// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;

namespace mi_webapp.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MiController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetAsync()
        {
            var storageAccountName = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_NAME");
            var storageContainerName = Environment.GetEnvironmentVariable("STORAGE_CONTAINER_NAME");
            var managedIdentityClientId = Environment.GetEnvironmentVariable("MI_CLIENT_ID");

            if (string.IsNullOrWhiteSpace(storageAccountName))
            {
                throw new ArgumentNullException(nameof(storageAccountName));
            }

            if (string.IsNullOrWhiteSpace(storageContainerName))
            {
                throw new ArgumentNullException(nameof(storageContainerName));
            }

            if (string.IsNullOrWhiteSpace(managedIdentityClientId))
            {
                throw new ArgumentNullException(nameof(managedIdentityClientId));
            }

            Console.WriteLine("Inside GetAsync");
            var result = await CreateBlockBlobAsync(storageAccountName, storageContainerName, managedIdentityClientId);
            return Ok(result ? "It worked" : "Did not work :(");
        }

        private static async Task<bool> CreateBlockBlobAsync(string accountName, string containerName, string miClientId)
        {
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = miClientId, ExcludeSharedTokenCacheCredential = true });
            Console.WriteLine("created credential");
            var containerClient = new BlobContainerClient(new Uri($"https://{accountName}.blob.core.windows.net/{containerName}"), credential);
            Console.WriteLine("created blob client");
            var random = new Random();
            bool uploadResult = false, containerExistsResult = false;

            try
            {
                var blobName = "blob" + random.Next();
                // Upload text to a new block blob.
                string blobContents = "This is a block blob.";
                byte[] byteArray = Encoding.ASCII.GetBytes(blobContents);

                using (MemoryStream stream = new MemoryStream(byteArray))
                {
                    await containerClient.UploadBlobAsync(blobName, stream);
                }
                Console.WriteLine("upload done to " + blobName);
            }
            catch (Exception e)
            {
                uploadResult = true;
                // This is expected because the MI does not have permission to upload to the container
                Console.WriteLine("Expected error in uploading blob : " + e.Message);
            }

            try
            {
                Console.WriteLine(DateTime.Now + " : Container exists? : " + (await containerClient.ExistsAsync()).Value);
                containerExistsResult = true;
            }
            catch (Exception e)
            {
                // This is unexpected because MI should have permission to do this task
                Console.WriteLine("Unexpected error in checking if container exists : " + e.Message);
            }

            return uploadResult && containerExistsResult;
        }
    }
}