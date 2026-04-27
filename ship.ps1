# 1. Bereinigen und Restore (Sicherheit geht vor)
dotnet restore
dotnet build -c Release

if ($LASTEXITCODE -ne 0) { 
    Write-Host "Build fehlgeschlagen. Abbruch." -ForegroundColor Red
    exit 
}

# 2. Version automatisch aus der .csproj auslesen
$csproj = Get-ChildItem -Filter *.csproj -Recurse | Select-Object -First 1
$version = ([xml](Get-Content $csproj.FullName)).Project.PropertyGroup.Version
if (!$version) { $version = "1.0.1" } # Fallback

Write-Host "Versende Version v$version..." -ForegroundColor Cyan

# 3. Git Operationen
git add .
git commit -m "Release v$version"
git tag "v$version"

# 4. Push (Main und Tags)
git push origin main
git push origin "v$version"

Write-Host "Erfolgreich auf GitHub veröffentlicht!" -ForegroundColor Green