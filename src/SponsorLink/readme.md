Domain events:

Source: SourceLink
- App.Installed(Id, Note)                                                // [account] by [sender]
- App.Removed(Id, Note)           
- Admin.Installed(Id, Note)       
- Admin.Removed(Id, Note)
- User.Authorized(Id, AccessToken, Note)
- User.Updated(Id, Emails[], Note)
- Sponsorship.Created(SponsorableId, SponsorId, Amount, ExpiresAt, Note) // [sponsor] > [sponsorable] : $1
- Sponsorship.Changed(SponsorableId, SponsorId, Amount, Note)            // [sponsor] > [sponsorable] : $1 > $2
- Sponsorship.Cancelled(SponsorableId, SponsorId, CancelAt, Note)        // [sponsor] x [sponsorable] on [date]
- Sponsorship.Expired(SponsorableId, SponsorId, Note)                    // [sponsor] x [sponsorable]  > cancelled+date expiration

