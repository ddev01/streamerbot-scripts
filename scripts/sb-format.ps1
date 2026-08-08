# Formats C# files with the Streamer.bot-compatible Roslyn formatter (SbFormat).
# Usage:
#   .\scripts\sb-format.ps1 -Path 'path\to\file.cs','other.cs'
#   .\scripts\sb-format.ps1 'path\to\file.cs'
#   $code | .\scripts\sb-format.ps1 -Stdin
#   Get-Content file.cs -Raw | .\scripts\sb-format.ps1 -Stdin
#   .\scripts\sb-format.ps1 -Check -Path 'path\to\file.cs'

[CmdletBinding()]
param(
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [string[]] $Path,

    [Parameter(ValueFromPipeline = $true)]
    [AllowNull()]
    [string] $PipelineInput,

    [switch] $Stdin,
    [switch] $Check,
    [switch] $Build
)

begin {
    $ErrorActionPreference = 'Stop'
    $repoRoot = Split-Path -Parent $PSScriptRoot
    $project = Join-Path $repoRoot 'tools\SbFormat\SbFormat.csproj'
    $dll = Join-Path $repoRoot 'tools\SbFormat\bin\Release\net8.0\SbFormat.dll'
    $stdinChunks = [System.Collections.Generic.List[string]]::new()

    function Ensure-Built {
        if (-not $Build -and (Test-Path -LiteralPath $dll)) { return }
        Write-Host 'Building SbFormat...'
        & dotnet build $project -c Release --nologo -v q
        if ($LASTEXITCODE -ne 0) { throw "SbFormat build failed (exit $LASTEXITCODE)" }
    }

    Ensure-Built
}

process {
    if ($Stdin -and $null -ne $PipelineInput) {
        $stdinChunks.Add($PipelineInput)
    }
}

end {
    if ($Stdin) {
        $text =
            if ($stdinChunks.Count -gt 0) { $stdinChunks -join "`n" }
            else { [Console]::In.ReadToEnd() }

        $stdinArgs = @('exec', $dll, '--stdin')
        $text | & dotnet @stdinArgs
        exit $LASTEXITCODE
    }

    if (-not $Path -or $Path.Count -eq 0) {
        Write-Error "Pass one or more .cs paths via -Path, or use -Stdin."
        exit 1
    }

    $resolved = foreach ($p in $Path) {
        if (-not (Test-Path -LiteralPath $p)) {
            Write-Error "File not found: $p"
            exit 2
        }
        (Resolve-Path -LiteralPath $p).Path
    }

    $dotnetArgs = [System.Collections.Generic.List[string]]::new()
    $dotnetArgs.Add('exec')
    $dotnetArgs.Add($dll)
    if ($Check) { $dotnetArgs.Add('--check') }
    foreach ($r in $resolved) { $dotnetArgs.Add($r) }

    & dotnet @dotnetArgs
    exit $LASTEXITCODE
}
