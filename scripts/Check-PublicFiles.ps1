[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$allowed = @('.gitignore','.gitattributes','LICENSE','README.md','THIRD-PARTY-NOTICES.md','SECURITY.md','CONTRIBUTING.md','CHANGELOG.md',
    'src/FreeMirror.csproj','src/Program.cs','src/FullScreenWindow.cs','src/app.manifest',
    'scripts/Build.ps1','scripts/Setup-Receiver.ps1','scripts/Check-PublicFiles.ps1','.github/workflows/build.yml')
$ignored = @('.git','artifacts','bin','obj','receiver','dist')
function Inspect-Directory([string]$directory) {
    foreach ($item in Get-ChildItem -LiteralPath $directory -Force) {
        if ($item.PSIsContainer) {
            if ($item.Name -notin $ignored) { Inspect-Directory $item.FullName }
            continue
        }
        $relative = $item.FullName.Substring($root.Length + 1).Replace('\','/')
        if ($relative -notin $allowed) { throw "File is not approved for publishing: $relative" }
        $content = [IO.File]::ReadAllText($item.FullName)
        $privatePath = '[A-Za-z]:[\\/](Users|Modding)[\\/]'
        $privateKey = '-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----'
        $accessToken = '(gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|AKIA[A-Z0-9]{16})'
        if ($content -match $privatePath -or $content -match $privateKey -or $content -match $accessToken) { throw "Potential private data in $relative" }
        foreach ($email in [regex]::Matches($content, '[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}')) {
            if ($email.Value -notlike '*@users.noreply.github.com') { throw "Unexpected email address in $relative" }
        }
    }
}
Inspect-Directory $root
Write-Host 'Public-file allowlist and private-data checks passed.'
