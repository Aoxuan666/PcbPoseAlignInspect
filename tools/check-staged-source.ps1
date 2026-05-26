param(
    [string[]] $Paths
)

$ErrorActionPreference = 'Stop'

$sourceExtensions = @(
    '.cs', '.csproj', '.sln', '.config', '.md', '.xml', '.json',
    '.resx', '.settings', '.props', '.targets'
)

$git = 'git'
if (-not (Get-Command $git -ErrorAction SilentlyContinue)) {
    $git = 'C:\Program Files\Git\cmd\git.exe'
}

$bad = New-Object System.Collections.Generic.List[string]

foreach ($path in $Paths) {
    if ([string]::IsNullOrWhiteSpace($path)) {
        continue
    }

    $extension = [System.IO.Path]::GetExtension($path).ToLowerInvariant()
    if ($sourceExtensions -notcontains $extension) {
        continue
    }

    $blob = & $git show ":$path" 2>$null
    if ($LASTEXITCODE -ne 0) {
        continue
    }

    $bytes = [System.Text.Encoding]::UTF8.GetBytes(($blob -join "`n"))
    if ($bytes.Length -eq 0) {
        continue
    }

    $sampleLength = [Math]::Min($bytes.Length, 4096)
    $controlCount = 0
    $nulCount = 0

    for ($i = 0; $i -lt $sampleLength; $i++) {
        $byte = $bytes[$i]
        if ($byte -eq 0) {
            $nulCount++
        }
        if ($byte -lt 32 -and $byte -ne 9 -and $byte -ne 10 -and $byte -ne 13) {
            $controlCount++
        }
    }

    $hasUtf16Bom = $bytes.Length -ge 2 -and (
        ($bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) -or
        ($bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF)
    )

    if (($nulCount -gt 0 -and -not $hasUtf16Bom) -or $controlCount -gt 20) {
        $bad.Add($path)
    }
}

if ($bad.Count -gt 0) {
    Write-Error ("Refusing to commit files that look encrypted or binary:`n" + ($bad -join "`n"))
    exit 1
}
