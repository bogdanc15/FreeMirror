[CmdletBinding()]
param([string]$Destination = (Join-Path $PSScriptRoot 'receiver'))
$ErrorActionPreference = 'Stop'
$destinationRoot = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $destinationRoot) {
    if (Test-Path -LiteralPath (Join-Path $destinationRoot 'uxplay-windows.exe')) {
        Write-Host 'A receiver folder already exists. It has not been changed.'
        return
    }
    throw 'Destination already exists without a receiver. Choose a new empty destination.'
}
$uri = 'https://github.com/leapbtw/uxplay-windows/releases/download/2.0.0.1736/uxplay-windows.zip'
$expected = '9D3A51C15FC9DB857351195E7EB7BBB21700D9AE25D936A54BCF8536B62CCA18'
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$work = [IO.Path]::GetFullPath((Join-Path $tempRoot ('FreeMirror-Setup-' + [guid]::NewGuid().ToString('N'))))
if (-not $work.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid temporary directory.' }
New-Item -ItemType Directory -Path $work | Out-Null
try {
    $archive = Join-Path $work 'receiver.zip'
    Write-Host 'Downloading the official uxplay-windows release from GitHub...'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -UseBasicParsing -Uri $uri -OutFile $archive
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expected) { throw 'Checksum mismatch. Nothing has been installed.' }
    $extracted = Join-Path $work 'extracted'
    Expand-Archive -LiteralPath $archive -DestinationPath $extracted
    if (-not (Test-Path -LiteralPath (Join-Path $extracted 'uxplay-windows.exe'))) { throw 'Receiver executable is missing from the download.' }
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destinationRoot)) -Force | Out-Null
    # Both paths are absolute: source is inside our checked temporary directory.
    Move-Item -LiteralPath $extracted -Destination $destinationRoot
    Write-Host 'Receiver prepared. Open FreeMirror.exe, then click Start receiving.'
}
finally {
    if ($work.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($work).StartsWith('FreeMirror-Setup-')) {
        Remove-Item -LiteralPath $work -Recurse -Force
    }
}
