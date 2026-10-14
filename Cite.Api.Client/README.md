# How to create the nuget package for Cite.Api.Client

1. cd ../Cite.Api
2. dotnet tool restore
3. dotnet build
4. dotnet swagger tofile --output ../Cite.Api.Client/swagger.json bin/Debug/net10.0/Cite.Api.dll v1
5. cd ../Cite.Api.Client
6. npm install
7. ./node_modules/.bin/nswag run /runtime:Net100
8. dotnet pack -c Release /p:version=<version>

*** NOTE: `dotnet tool restore` installs the Swashbuckle CLI (`dotnet swagger`) from the tool manifest in Cite.Api/.config/dotnet-tools.json. The version in the manifest must match the Swashbuckle.AspNetCore version in the Cite.Api.csproj file.

    `npm install` installs nswag from package.json in the Cite.Api.Client folder.

# Publishing

The manual "Build Client Package" workflow (.github/workflows/client.yml) runs these steps in Release with the version given as `clientVersion`, pushes the package to nuget.org as unlisted, and creates a GitHub release for it. The manual "List Package" workflow (.github/workflows/list-package.yml) lists a published version.
