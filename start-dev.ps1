$root = $PSScriptRoot

# Start backend in a new PowerShell window
Start-Process powershell -ArgumentList @(
    "-NoExit",
    "-Command",
    "Set-Location '$root'; dotnet run --project server/lan-box-party-pack.Server --urls http://localhost:5000"
)

# Start frontend in a new PowerShell window
Start-Process powershell -ArgumentList @(
    "-NoExit",
    "-Command",
    "Set-Location '$root/client'; npm run start -- --host 0.0.0.0"
)