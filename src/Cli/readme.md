SponsorLink manifest synchronization dotnet global tool.

Example:

```bash
sponsor sync devlooped
```

Sync options:

```bash
USAGE:
    sponsor sync [account] [OPTIONS]

ARGUMENTS:
    [account]    Optional sponsored account(s) to synchronize

OPTIONS:
    -h, --help          Prints help information
        --autosync      Enable or disable automatic synchronization of expired manifests
    -l, --local         Sync only existing local manifests
    -f, --force         Force sync, regardless of expiration of manifests found locally
    -v, --validate      Whether to always validate local manifests using the issuer public key
    -u, --unattended    Whether to prevent interactive credentials refresh
        --with-token    Read GitHub authentication token from standard input for sync
```

Other commands:

```bash
USAGE:
    sponsor [OPTIONS] <COMMAND>

OPTIONS:
    -h, --help    Prints help information

COMMANDS:
    config    Manages sponsorlink configuration
    init      Initializes a sponsorable JWT manifest and signing keys
    list      Lists current user and organization sponsorships leveraging the GitHub CLI
    remove    Removes all manifests and notifies issuers to remove backend data too
    sync      Synchronizes sponsorship manifests
    view      Validates and displays the active sponsor manifests, if any
```

Learn more [about the tool](https://github.com/devlooped/SponsorLink/blob/main/docs/github.md#sponsor-manifest-sync) 
and related [telemetry](https://github.com/devlooped/SponsorLink/blob/main/docs/github.md#telemetry).