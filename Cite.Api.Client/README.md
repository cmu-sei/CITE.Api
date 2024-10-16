# How to create the nuget package for Cite.Api.Client
1. cd ../Cite.Api
2. dotnet swagger tofile --output ../Cite.Api.Client/swagger.json bin/Debug/net8.0/Cite.Api.dll v1
3. cd ../Cite.Api.Client
4. ./node_modules/.bin/nswag run /runtime:Net60
5. dotnet pack -c Release /p:version=0.1.2

*** NOTE: If dotnet sawgger is not recognized, in the Cite.Api folder run the following:
    dotnet new tool-manifest
    dotnet tool install --version 6.4.0 Swashbuckle.AspNetCore.Cli

    The version installed must match the version in Cite.Api.csproj file.

    Also, if nswag is not found, run npm install from Cite.Api.Client folder
