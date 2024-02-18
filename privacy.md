# SponsorLink Privacy Statement

This privacy statement explains the personal data SponsorLink processes, how SponsorLink processes it, 
and for what purposes.

SponsorLink offers a mechanism for integrators to check whether a user is sponsoring a given account. 
References to SponsorLink products in this statement include SponsorLink backend services, websites, 
apps, extensions and other related software.

This statement applies to the interactions SponsorLink has with you and the SponsorLink products listed below, 
as well as other SponsorLink products that display this statement.

## Personal data we collect

SponsorLink does not collect any data unless you explicitly consent to it. You provide this data directly, 
by interacting with the SponsorLink GitHub CLI extension and indirectly afterward by interacting with 
integrators that use SponsorLink to check whether you are sponsoring a given account. 

The only personal data we collect outside of your local machine is your GitHub user identifier, as 
part of authenticating with SponsorLink for the purpose of signing a locally-generated manifest file.
Upon logging in for the first time you will be prompted to Authorize the GH Sponsors CLI with access to 
your GitHub profile. This authorization (required for manifest signing), alongside the signature 
verification of the manifest itself, is a sufficient indication to integrators that you have consented 
to the use of your personal data for the purpose of checking whether you are sponsoring a given account.

Your SponsorLink authentication token is stored locally as an environment variable to avoid having to 
authenticate through GitHub every time you synchronize your sponsorships. This token can only be used 
for reading your profile information (and invoking the signing endpoint), which is already visible for 
anyone on GitHub (i.e. open [Linus](https://api.github.com/users/torvalds)
or [microsoft](https://api.github.com/orgs/microsoft).

When we ask you to provide this personal data, you can decline. If you choose not to provide data necessary 
to provide you with a product or feature, you cannot use that product or feature. For example, if an 
integrator requires a sponsorship check to enable a certain feature, you will not be able to use that 
feature unless you provide the required data.

Where providing the data is optional (e.g. it is only used to avoid displaying a notice while using the 
product), and you choose not to share personal data, features like personalization that use such data will 
not work for you.

### Email

Your email is collected *locally* as part of the process of generating a manifest of your sponsorships, 
using the SponsorLink GitHub CLI extension. This is done by reading the email(s) associated with your 
GitHub account *locally* using the GitHub CLI itself. 

This manifest contains salted hashes of your email(s) and sponsored account and is signed with your GitHub 
user identifier on the backend. To perform this operation, you must authenticate with SponsorLink using 
your GitHub account via github.com, and authorize the SponsorLink GitHub CLI extension to access your 
GitHub profile.

Integrators can use the presence of the manifest (as an environment variable) as an indication of your 
consent to using the contained information to check whether you are sponsoring a given account. This 
is done *locally* and *offline* by the integrator, by usually running `git config --get user.email` 
and then hashing the result with the sponsorable account to check. If the manifest contains a matching 
hash, the integrator can be sure that you are sponsoring the given account.

## How we use personal data

SponsorLink uses the data we collect to provide integrators with a mechanism to enable proper attribution 
of your (or your organization's) sponsorships to improve the products you use. In particular, we use data to:

* Sign a locally generated manifest file with your GitHub user identifier and sponsorships to enable 
  integrators to verify that you are sponsoring a given account in a secure and offline way.
* Allow integrators to lookup (locally and offline) your email and check whether you are sponsoring a 
  given account (also locally and offline)

In carrying out these purposes, we gather data we collect from external tools (for example, we use 
git config to get your email address) and combine it with data we collect through SponsorLink products 
(e.g. with the random GUID used for salting hashes) to give you a more seamless, consistent, and personalized 
experience.

{: .info }
> NOTE: Your personal data (i.e. your email) is not used at all outside your local machine, and if the
> manifest is not present (which requires explicit consent for signing), nothing gets persisted or used
> for any purpose at any time.

## How to access and control your personal data

You can also make choices about the collection and use of your data by SponsorLink. You can control your personal 
data that SponsorLink has obtained, and exercise your data protection rights, by contacting SponsorLink or using 
the [GitHub CLI extension](https://github.com/devlooped/gh-sponsors) tool we provide. 

Using the tool, you can:

* View the data associated with your GitHub user identifier, both locally and remotely
* Delete the data associated with your GitHub user identifier, both locally and remotely

All personal data processed by SponsorLink can be accessed or controlled via this tool. 

## SponsorLink Account

With a SponsorLink account, you can sign your sponsorship manifest. Personal data associated with your SponsorLink 
account includes only your GitHub user identifier. Signing in to your SponsorLink account (using your GitHub account 
via github.com) enables the sponsorship check for integrators.

When you remove your data using the above tool, we delete all data associated with your SponsorLink account. 
Subsequent authentication with SponsorLink will create a new account, even if the same GitHub user identifier is used.
This will be manifest in the tool by a different GUID being used for salting hashes, as well as the authorization 
flow from github.com being triggered again.
