﻿using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Azure.Storage.Blobs;
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

        var headers = new BlobHttpHeaders
        {
            ContentType = "text/plain",
            // Allow caching up to 12hrs to optimize via a CDN
            CacheControl = "max-age=43200"
        };
        var tags = new Dictionary<string, string>
        {
            { "Id", account.Id } ,
            { "Login", account.Login } ,
        };

        foreach (var email in emails)
        {
            var data = SHA256.HashData(Encoding.UTF8.GetBytes(email));
            var hash = Base62.Encode(BigInteger.Abs(new BigInteger(data)));

            var blob = container.GetBlobClient($"apps/{hash}");
            await blob.UploadAsync(new MemoryStream(), headers);
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

    /// <summary>
    /// Creates a blob storage entry for the given sponsorable account, with the given sponsor and emails.
    /// </summary>
    public async Task RegisterSponsorAsync(AccountId sponsorable, AccountId sponsor, IEnumerable<string> emails, bool member = false)
    {
        var container = blobService.GetBlobContainerClient("sponsorlink");
        await container.CreateIfNotExistsAsync(PublicAccessType.Blob);

        var headers = new BlobHttpHeaders
        {
            ContentType = "text/plain",
            // Allow caching up to 12hrs to optimize via a CDN
            CacheControl = "max-age=43200"
        };
        var tags = new Dictionary<string, string>
        {
            { "Sponsorable", sponsorable.Id } ,
            { "SponsorableLogin", sponsorable.Login } ,
            { "Sponsor", sponsor.Id } ,
            { "SponsorLogin", sponsor.Login } ,
        };

        if (member)
            tags.Add("SponsorableMember", "true");

        foreach (var email in emails)
        {
            // TODO: we store the blobs using the email hash as the blob name. 
            // This might not be enough to preserve user' privacy, so we'll likey 
            // need to find a better alternative that involves more user interaction 
            // on the client side. Kinda annoying vs having things "Just Work" but 
            // it seems it will be necessary anyway.
            var data = SHA256.HashData(Encoding.UTF8.GetBytes(email));
            var hash = Base62.Encode(BigInteger.Abs(new BigInteger(data)));

            var blob = container.GetBlobClient($"{sponsorable.Login}/{hash}");
            // The blob will contain a single byte if the sponsor is a member, not a sponsor.
            // See SponsorLink.CheckSponsorAsync on how the check is done.
            await blob.UploadAsync(member ? new MemoryStream(new[] { (byte)1 }) : new MemoryStream(), headers);
            await events.PushAsync(new SponsorRegistered(sponsorable.Id, sponsor.Id, email));
        }
    }

    public async Task UnregisterSponsorAsync(AccountId sponsorable, AccountId sponsor)
    {
        var container = blobService.GetBlobContainerClient("sponsorlink");

        await foreach (var blob in blobService.FindBlobsByTagsAsync($"@container='sponsorlink' AND Sponsorable='{sponsorable.Id}' AND Sponsor='{sponsor.Id}'"))
        {
            await container.DeleteBlobAsync(blob.BlobName);
            await events.PushAsync(new SponsorUnregistered(sponsorable.Id, sponsor.Id, blob.BlobName[(blob.BlobName.LastIndexOf('/') + 1)..]));
        }
    }
}
