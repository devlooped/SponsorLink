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
            { "X-Sponsorable", sponsorable.Id } ,
            { "X-Sponsorable-Login", sponsorable.Login } ,
            { "X-Sponsor", sponsor.Id } ,
            { "X-Sponsor-Login", sponsor.Login } ,
        };

        foreach (var email in emails)
        {
            await container.GetBlobClient($"{sponsorable.Login}/{email}")
                .UploadAsync(new MemoryStream(), headers, tags);
        }
    }
}
