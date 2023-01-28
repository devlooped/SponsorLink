﻿using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Devlooped.SponsorLink;

[Service]
public class SponsorsRegistry
{
    readonly BlobServiceClient blobService;
    readonly IEventStream events;
    
    public SponsorsRegistry(CloudStorageAccount storageAccount, IEventStream events)
        => (blobService, this.events)
        = (storageAccount.CreateBlobServiceClient(), events);

    public async Task RegisterAppAsync(AccountId account, IEnumerable<string> emails)
    {
        var container = blobService.GetBlobContainerClient("sponsorlink");
        await container.CreateIfNotExistsAsync(PublicAccessType.Blob);

        var headers = new BlobHttpHeaders { ContentType = "text/plain" };
        var tags = new Dictionary<string, string>
        {
            { "Id", account.Id } ,
            { "Login", account.Login } ,
        };

        foreach (var email in emails)
        {
            var blob = container.GetBlobClient($"apps/{email}");
            await blob.UploadAsync(new MemoryStream(), headers);
            await blob.SetTagsAsync(tags);
            await events.PushAsync(new AppRegistered(account.Id, account.Login, email));
        }
    }

    public async Task UnregisterAppAsync(AccountId account)
    {
        var container = blobService.GetBlobContainerClient("sponsorlink");

        await foreach (var blob in blobService.FindBlobsByTagsAsync($"@container='sponsorlink' AND Id='{account.Id}'"))
        {
            await container.DeleteBlobAsync(blob.BlobName);
            await events.PushAsync(new AppUnregistered(account.Id, account.Login, blob.BlobName[(blob.BlobName.LastIndexOf('/') + 1)..]));
        }
    }

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
            await events.PushAsync(new SponsorRegistered(sponsorable.Id, sponsor.Id, email));
        }
    }

    public async Task UnregisterSponsorAsync(AccountId sponsorable, AccountId sponsor)
    {        
        var container = blobService.GetBlobContainerClient("sponsorlink");

        await foreach (var blob in blobService.FindBlobsByTagsAsync($"@container='sponsorlink' AND Sponsorable='{sponsorable.Id}' AND Sponsor='{sponsor.Id}'"))
        {
            await container.DeleteBlobAsync(blob.BlobName);
            await events.PushAsync(new SponsorUnregistered(sponsorable.Id, sponsor.Id, blob.BlobName[(blob.BlobName.LastIndexOf('/')+1)..]));
        }
    }
}