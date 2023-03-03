# How to create the nuget package for Cite.Api.Client
1. cd ../Cite.Api
2. swagger tofile --output ../Cite.Api.Client/swagger.json bin/Debug/net6.0/Cite.Api.dll v1
3. cd ../Cite.Api.Client
4. ./node_modules/.bin/nswag run /runtime:Net60
5. dotnet pack -c Release /p:version=0.1.2

