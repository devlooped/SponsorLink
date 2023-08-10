# SponsorLink

[![Version](https://img.shields.io/nuget/vpre/Devlooped.SponsorLink.svg?color=royalblue)](https://www.nuget.org/packages/Devlooped.SponsorLink)
[![Downloads](https://img.shields.io/nuget/dt/Devlooped.SponsorLink.svg?color=green)](https://www.nuget.org/packages/Devlooped.SponsorLink)

![Monthly Active Users](https://img.shields.io/endpoint.svg?url=https://sponsorlink.devlooped.com/stats/users&label=users&color=brightgreen)
![Total Projects](https://img.shields.io/endpoint.svg?url=https://sponsorlink.devlooped.com/stats/projects&label=projects&color=blue)
![Total Accounts](https://img.shields.io/endpoint.svg?url=https://sponsorlink.devlooped.com/stats/accounts&label=accounts&color=FF69B4)

Required web app settings:

* GitHub:WebhookSecret = [a shared secret initially configured for the webhook of both sponsorlink and admin apps]
* GitHub:Sponsorable:RedirectUri = https://[HOST]/authorize/sponsorable
* GitHub:Sponsorable:ClientSecret =  [from https://github.com/organizations/devlooped/settings/apps/sponsorlink-admin]
* GitHub:Sponsorable:ClientId =  [from https://github.com/organizations/devlooped/settings/apps/sponsorlink-admin]
* GitHub:Sponsorable:AppId =  [from https://github.com/organizations/devlooped/settings/apps/sponsorlink-admin]
* GitHub:Sponsor:RedirectUri = https://[HOST]/authorize/sponsor
* GitHub:Sponsor:ClientSecret = [from https://github.com/organizations/devlooped/settings/apps/sponsorlink]
* GitHub:Sponsor:ClientId = [from https://github.com/organizations/devlooped/settings/apps/sponsorlink]
* GitHub:Sponsor:AppId = [from https://github.com/organizations/devlooped/settings/apps/sponsorlink]
* EventGrid:Domain = [from Event Grid Domain overview]
* EventGrid:AccessKey = [from Event Grid Domain access keys]


Devlooped org needs to have the [SponsorLink Admin](https://github.com/apps/sponsorlink-admin) app 
installed. After the installation, find the Sponsorable record in storage for the devlooped organization 
and make sure the `Secret` column has the same value as the `GitHub:WebhookSecret`. Both should always be in sync. 
If the admin app is ever re-installed, the webhook secret will need updating too (or the Secret column).

## Provisioning a new Sponsorable

Users and organizations that want to use SponsorLink need to start by installing the 
[SponsorLink Admin](https://github.com/apps/sponsorlink-admin) GitHub app, and then 
adding a webhook to their sponsor account with the URL `https://sponsorlink.devlooped.com/sponsor/[YOUR_SPONSOR_LOGIN]`.

SponsorLink secures these webhooks with a shared secret that you need to obtain from us after 
the admin app is installed. Please contact us at hello@devlooped.com to get the secret to use. 
Until this secret is in place, your sponsor payloads will not be processed.

### Registering existing sponsors

Since GH [doesn't provide a way for an app to retrieve your existing sponsors](https://github.com/orgs/community/discussions/44226), 
we need to resort to a manual workaround for the time being. 

Open the GraphQL explorer and sign-in with your GH credentials, then run the following query, which 
will retrieve your currently active sponsors:

```graphql
   query { 
     organization(login: "[SPONSORABLE]") {
       id
       login
       sponsorshipsAsMaintainer(first: 100, orderBy: {field: CREATED_AT, direction: ASC}, includePrivate: true) {
         nodes {
           createdAt
           isOneTimePayment
           sponsorEntity {
             ... on Organization {
               id
               login
             }
             ... on User {
               id
               login
             }
           }
           tier {
             monthlyPriceInDollars
           }
         }
       }
     }
   }
```

> NOTE: Replace `organization(login: ...)` with `user(login: ...)` if your sponsor account is a user rather than 
> an organization.

Then send the resulting JSON response to hello@devlooped.com and we'll register your existing sponsors for you.
Please make sure you have installed the SponsorLink Admin app beforehand.

You MUST do this BEFORE you instruct users to install the [SponsorLink GitHub App](https://github.com/apps/sponsorlink) 
so that the system can properly attribute their existing sponsorship to your sponsorable account.
