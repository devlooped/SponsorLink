```shell
USAGE:
    sponsor [OPTIONS] <COMMAND>

OPTIONS:
    -h, --help    Prints help information

COMMANDS:
    config           Manages sponsorlink configuration                          
    init             Initializes a sponsorable JWT manifest and signing keys    
    list             Lists current user and organization sponsorships leveraging
                     the GitHub CLI                                             
    remove           Removes manifests and notifies issuers to remove backend   
                     data too                                                   
    sync             Synchronizes sponsorship manifests                         
    view             Validates and displays the active sponsor manifests, if any
    nuget            Emits the nuget.json manifest with all contributors to     
                     active nuget packages                                      
    check <TOKEN>    Checks the validity of a GitHub token                      
```
