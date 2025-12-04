# Build script for Spreading Devastation mod

# Check if VINTAGE_STORY environment variable is set
if (-not $env:VINTAGE_STORY) {
    Write-Host "ERROR: VINTAGE_STORY environment variable is not set!" -ForegroundColor Red
    Write-Host "Please set it to your Vintage Story installation directory, e.g.:" -ForegroundColor Yellow
    Write-Host '  $env:VINTAGE_STORY = "C:\Program Files\Vintage Story"' -ForegroundColor Yellow
    exit 1
}

Write-Host "Building Spreading Devastation mod..." -ForegroundColor Green
Write-Host "Vintage Story path: $env:VINTAGE_STORY" -ForegroundColor Cyan

# Build the project
dotnet build -c Release

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nBuild successful!" -ForegroundColor Green
    
    # Find the output zip
    $zipPath = Get-ChildItem -Path "bin\Release" -Recurse -Filter "SpreadingDevastation.zip" | Select-Object -First 1
    
    if ($zipPath) {
        Write-Host "Mod package created at: $($zipPath.FullName)" -ForegroundColor Cyan
        
        # Optionally copy to mods folder
        $modsFolder = Join-Path $env:VINTAGE_STORY "Mods"
        if (Test-Path $modsFolder) {
            $response = Read-Host "`nCopy to Vintage Story Mods folder? (y/n)"
            if ($response -eq 'y') {
                Copy-Item $zipPath.FullName -Destination $modsFolder -Force
                Write-Host "Copied to: $modsFolder" -ForegroundColor Green
            }
        }
    }
} else {
    Write-Host "`nBuild failed!" -ForegroundColor Red
    exit 1
}

