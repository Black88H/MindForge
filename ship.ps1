# 1. Version aus .csproj auslesen
$csproj = Get-ChildItem -Filter *.csproj -Recurse | Select-Object -First 1
[xml]$xml = Get-Content $csproj.FullName
$currentVersion = [version]$xml.Project.PropertyGroup.Version

# 2. Version um eins erhöhen (z.B. 1.0.1 -> 1.0.2)
$newVersion = "{0}.{1}.{2}" -f $currentVersion.Major, $currentVersion.Minor, ($currentVersion.Build + 1)
$xml.Project.PropertyGroup.Version = $newVersion
$xml.Project.PropertyGroup.AssemblyVersion = "$newVersion.0"
$xml.Project.PropertyGroup.FileVersion = "$newVersion.0"
$xml.Save($csproj.FullName)

Write-Host "Upgrade auf Version v$newVersion..." -ForegroundColor Cyan

# 3. Build (Release)
dotnet restore
dotnet build -c Release

if ($LASTEXITCODE -ne 0) { 
    Write-Host "Build fehlgeschlagen!" -ForegroundColor Red
    exit 
}

# 4. Git Automatisierung
git add .
git commit -m "Release v$newVersion"
git tag -f "v$newVersion"
git push origin main
git push origin "v$newVersion" --force

Write-Host "MindForge v$newVersion ist jetzt live auf GitHub!" -ForegroundColor Green