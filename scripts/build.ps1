# Builds and tests the server, publishes it into client/server, and compiles the TS client.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

dotnet test "$root/CSharpSense.slnx"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet publish "$root/src/Server/CSharpSense.Server.csproj" -c Release -o "$root/client/server"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Push-Location "$root/client"
try {
    npm install --no-audit --no-fund
    npm run compile
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} finally {
    Pop-Location
}
