# SponsorLink Development/Deployment

Notes on the reference implementation of the SponsorLink backend API,
GH CLI extension and run-time helpers for verifying manifests.

## GH Auth

Create a GH OAuth app with a redirect URL pointing to 
`https://[host]/.auth/login/github/callback`. 

I recommend using `ngrok` to get a stable secure channel.

When the app runs and requires GH authorization, you will see a message 
like the following in the function host running locally:

```
[2024-06-18T22:35:56.007Z] Navigate to https://github.com/login/device
[2024-06-18T22:35:56.015Z] Then enter code: DCA7-D797
```

Until you enter the code, the web request will not complete, and 
periodically render a message like the following:

```
[2024-06-18T22:36:03.354Z] authorization_pending: The authorization request is still pending.
```

Once authorized, the app will have 