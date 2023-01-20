using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Devlooped.SponsorLink;

[Service]
public class SponsorsRegistry
{
    readonly BlobServiceClient blobService;

    public SponsorsRegistry(CloudStorageAccount storageAccount)
        => blobService = storageAccount.CreateBlobServiceClient();

    public async Task RegisterSponsorAsync(AccountId sponsorable, AccountId sponsor, IEnumerable<string> emails)
    {
        var container = blobService.GetBlobContainerClient("sponsorlink");
        await container.CreateIfNotExistsAsync(PublicAccessType.Blob);

        var headers = new BlobHttpHeaders { ContentType = "text/plain" };
        var tags = new Dictionary<string, string>
        {
            { "Sponsorable", sponsorable.Id } ,
            { "SponsorableLogin", sponsorable.Login } ,
            { "Sponsor", sponsor.Id } ,
            { "SponsorLogin", sponsor.Login } ,
        };

        foreach (var email in emails)
        {
            var blob = container.GetBlobClient($"{sponsorable.Login}/{email}");
            await blob.UploadAsync(new MemoryStream(), headers);
            await blob.SetTagsAsync(tags);
        }
    }

    public async Task UnregisterSponsorAsync(AccountId sponsorable, AccountId sponsor)
    {        
        var container = blobService.GetBlobContainerClient("sponsorlink");

        await foreach (var blob in blobService.FindBlobsByTagsAsync($"@container='sponsorlink' AND Sponsorable='{sponsorable.Id}' AND Sponsor='{sponsor.Id}'"))
        {
            await container.DeleteBlobAsync(blob.BlobName);
        }
    }
}
