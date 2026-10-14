[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
& (Join-Path $PSScriptRoot 'Check-PublicFiles.ps1')
$version = '0.1.0'
$artifacts = Join-Path $root 'artifacts'
$stage = Join-Path $artifacts ('build-' + [guid]::NewGuid().ToString('N'))
$package = Join-Path $stage 'FreeMirror'
New-Item -ItemType Directory -Path $package -Force | Out-Null
dotnet publish (Join-Path $root 'src/FreeMirror.csproj') -c Release -r win-x64 --self-contained true -o $package -p:PublishSingleFile=true "-p:PathMap=$root=/_/FreeMirror"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$test = Start-Process -FilePath (Join-Path $package 'FreeMirror.exe') -ArgumentList '--self-test' -PassThru -WindowStyle Hidden
if (-not $test.WaitForExit(30000)) { $test.Kill(); throw 'Self-check timed out.' }
if ($test.ExitCode -ne 0) { throw 'Self-check failed. Review the local test-result.txt in the build output.' }
Write-Host 'Controller self-checks passed.'
foreach ($name in 'test-result.txt','live-window-test.txt','preview.png') {
    $file = Join-Path $package $name
    if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
}
if (Get-ChildItem -LiteralPath $package -Recurse -Filter '*.pdb') { throw 'Debug symbols must not be distributed.' }
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination (Join-Path $package 'README.txt')
Copy-Item -LiteralPath (Join-Path $root 'LICENSE'),(Join-Path $root 'THIRD-PARTY-NOTICES.md'),(Join-Path $root 'SECURITY.md') -Destination $package
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Setup-Receiver.ps1') -Destination $package
$source = Join-Path $package 'source'
New-Item -ItemType Directory -Path (Join-Path $source 'src'),(Join-Path $source 'scripts') -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $root 'src') -File | Copy-Item -Destination (Join-Path $source 'src')
Get-ChildItem -LiteralPath $PSScriptRoot -File | Copy-Item -Destination (Join-Path $source 'scripts')
Copy-Item -LiteralPath (Join-Path $root 'LICENSE'),(Join-Path $root 'README.md'),(Join-Path $root 'THIRD-PARTY-NOTICES.md'),(Join-Path $root 'SECURITY.md'),(Join-Path $root 'CONTRIBUTING.md'),(Join-Path $root 'CHANGELOG.md'),(Join-Path $root '.gitignore') -Destination $source
$packageRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
$licenses = Join-Path $package 'licenses'
New-Item -ItemType Directory -Path $licenses | Out-Null
Copy-Item -LiteralPath (Join-Path $packageRoot 'microsoft.netcore.app.runtime.win-x64/8.0.31/LICENSE.TXT') -Destination (Join-Path $licenses 'DOTNET-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $packageRoot 'microsoft.netcore.app.runtime.win-x64/8.0.31/THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $licenses 'DOTNET-THIRD-PARTY-NOTICES.txt')
Copy-Item -LiteralPath (Join-Path $packageRoot 'microsoft.windowsdesktop.app.runtime.win-x64/8.0.31/LICENSE') -Destination (Join-Path $licenses 'WINDOWS-DESKTOP-LICENSE.txt')
if (Test-Path -LiteralPath (Join-Path $package 'receiver')) { throw 'The upstream receiver must not be redistributed by this build.' }
$zip = Join-Path $artifacts "FreeMirror-v$version-win-x64.zip"
Compress-Archive -Path (Join-Path $package '*') -DestinationPath $zip -CompressionLevel Optimal -Force
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath ($zip + '.sha256') -Value "$hash  $([IO.Path]::GetFileName($zip))" -Encoding ascii
Write-Host "Built FreeMirror-v$version-win-x64.zip and its SHA-256 checksum."
