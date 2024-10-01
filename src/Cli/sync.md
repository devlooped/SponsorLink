DESCRIPTION:
Synchronizes sponsorship manifests

USAGE:
    sponsor sync [account] [OPTIONS]

ARGUMENTS:
    [account]    Optional sponsored account(s) to synchronize

OPTIONS:
    -h, --help          Prints help information                                 
        --autosync      Enable or disable automatic synchronization of expired  
                        manifests                                               
    -l, --local         Sync only existing local manifests                      
    -f, --force         Force sync, regardless of expiration of manifests found 
                        locally                                                 
    -v, --validate      Validate local manifests using the issuer public key    
    -u, --unattended    Prevent interactive credentials refresh                 
        --with-token    Read GitHub authentication token from standard input for
                        sync                                                    
```
