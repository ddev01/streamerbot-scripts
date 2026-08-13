# Regenerates docs/cph-api.md and docs/fluentconfig-api.md from local DLLs.
# FluentConfig surface includes Fc runtime helpers, Grid/Row/RepeatFor/Size, Comparator, and Runtime.* types.
# After rebuilding FluentConfig.dll, pass -FluentConfigDll to the new build (or redeploy into Streamer.bot dlls/).
param(
  [string]$StreamerBotPath = $env:STREAMERBOT_PATH,
  [string]$FluentConfigDll = '',
  [string]$OutDir = ''
)

$ErrorActionPreference = 'Stop'

# Streamer.bot ships .NET Framework DLLs with obfuscated nested types in Common.dll.
# Reflection only works under Windows PowerShell 5.1 (.NET Framework), not PowerShell 7+.
if ($PSVersionTable.PSEdition -eq 'Core') {
  $windowsPs = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
  if (-not (Test-Path -LiteralPath $windowsPs)) {
    throw 'Generate-ApiDocs.ps1 requires Windows PowerShell 5.1. Streamer.bot DLLs cannot be reflected under PowerShell 7.'
  }
  $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath)
  if ($StreamerBotPath) { $argList += '-StreamerBotPath'; $argList += $StreamerBotPath }
  if ($FluentConfigDll) { $argList += '-FluentConfigDll'; $argList += $FluentConfigDll }
  if ($OutDir) { $argList += '-OutDir'; $argList += $OutDir }
  & $windowsPs @argList
  if ($LASTEXITCODE) { exit $LASTEXITCODE }
  return
}

$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $StreamerBotPath) {
  throw 'Set STREAMERBOT_PATH or pass -StreamerBotPath to your Streamer.bot install directory.'
}
if (-not $OutDir) { $OutDir = Join-Path $repoRoot 'docs' }
if (-not $FluentConfigDll) { $FluentConfigDll = Join-Path $StreamerBotPath 'dlls\FluentConfig.dll' }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function Format-TypeName([Type]$t) {
  if ($null -eq $t -or $t -eq [void]) { return 'void' }
  if ($t.IsByRef) { return (Format-TypeName $t.GetElementType()) + '&' }
  if ($t.IsGenericType) {
    $name = $t.Name
    $tick = $name.IndexOf('`')
    if ($tick -ge 0) { $name = $name.Substring(0, $tick) }
    $args = ($t.GetGenericArguments() | ForEach-Object { Format-TypeName $_ }) -join ', '
    return "$name<$args>"
  }
  if ($t.IsArray) { return "$(Format-TypeName $t.GetElementType())[]" }
  switch ($t.FullName) {
    'System.String' { return 'string' }
    'System.Int32' { return 'int' }
    'System.Int64' { return 'long' }
    'System.Boolean' { return 'bool' }
    'System.Double' { return 'double' }
    'System.Single' { return 'float' }
    'System.Object' { return 'object' }
    'System.Void' { return 'void' }
    default {
      if ($t.Namespace -like 'Streamer.bot*' -or $t.Namespace -like 'FluentConfig*') { return $t.Name }
      return $t.Name
    }
  }
}

function Get-MethodSignatures([Type]$type, [bool]$declaredOnly = $false) {
  $flags = [Reflection.BindingFlags]::Public -bor [Reflection.BindingFlags]::Instance -bor [Reflection.BindingFlags]::Static
  if ($declaredOnly) { $flags = $flags -bor [Reflection.BindingFlags]::DeclaredOnly }
  $list = New-Object System.Collections.Generic.List[string]
  foreach ($m in $type.GetMethods($flags)) {
    if ($m.IsSpecialName) { continue }
    if ($m.Name -match '^(get_|set_|add_|remove_)') { continue }
    if ($m.Name -in @('Equals', 'GetHashCode', 'GetType', 'ToString', 'Finalize', 'MemberwiseClone')) { continue }
    if ($m.DeclaringType.Namespace -like 'System*') { continue }
    $ret = Format-TypeName $m.ReturnType
    $pars = ($m.GetParameters() | ForEach-Object {
      $pType = Format-TypeName $_.ParameterType
      $opt = if ($_.IsOptional) { ' = ...' } else { '' }
      "$pType $($_.Name)$opt"
    }) -join ', '
    $static = if ($m.IsStatic) { 'static ' } else { '' }
    $list.Add("${static}${ret} $($m.Name)($pars)") | Out-Null
  }
  $list | Sort-Object -Unique
}

function Get-PropertySignatures([Type]$type) {
  $flags = [Reflection.BindingFlags]::Public -bor [Reflection.BindingFlags]::Instance -bor [Reflection.BindingFlags]::Static -bor [Reflection.BindingFlags]::DeclaredOnly
  foreach ($p in ($type.GetProperties($flags) | Sort-Object Name)) {
    $access = @()
    if ($p.CanRead) { $access += 'get' }
    if ($p.CanWrite) { $access += 'set' }
    $static = if ($p.GetMethod -and $p.GetMethod.IsStatic) { 'static ' } else { '' }
    "${static}$(Format-TypeName $p.PropertyType) $($p.Name) { $($access -join '; ') }"
  }
}

function Get-EnumMembers([Type]$type) {
  [Enum]::GetNames($type) | Sort-Object
}

# Load Streamer.bot managed deps (skip native DLLs in the install root).
Get-ChildItem (Join-Path $StreamerBotPath 'Streamer.bot*.dll') | ForEach-Object {
  try { [void][Reflection.Assembly]::LoadFrom($_.FullName) } catch {}
}
$cphAsm = [Reflection.Assembly]::LoadFrom((Join-Path $StreamerBotPath 'Streamer.bot.Plugin.Interface.dll'))
$proxy = $cphAsm.GetType('Streamer.bot.Plugin.Interface.IInlineInvokeProxy')
$base = $cphAsm.GetType('Streamer.bot.Plugin.Interface.CPHInlineBase')

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('# CPH API Reference (generated)')
[void]$sb.AppendLine('')
[void]$sb.AppendLine("Source: ``Streamer.bot.Plugin.Interface.dll`` under ``$StreamerBotPath``")
[void]$sb.AppendLine("Generated: $(Get-Date -Format 'yyyy-MM-dd')")
[void]$sb.AppendLine('')
[void]$sb.AppendLine('In Streamer.bot C# actions, `CPH` is an `IInlineInvokeProxy` instance on `CPHInlineBase`.')
[void]$sb.AppendLine('Prefer this file over guessing method names/signatures.')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('## CPHInlineBase')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('```csharp')
foreach ($line in (Get-PropertySignatures $base)) { [void]$sb.AppendLine($line) }
foreach ($line in (Get-MethodSignatures $base -declaredOnly:$true)) { [void]$sb.AppendLine($line) }
[void]$sb.AppendLine('```')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('## IInlineInvokeProxy (CPH.*)')
[void]$sb.AppendLine('')

$buckets = [ordered]@{}
foreach ($sig in (Get-MethodSignatures $proxy -declaredOnly:$true)) {
  if ($sig -notmatch '(?:static )?\S+ (\w+)\(') { continue }
  $name = $Matches[1]
  $prefix = 'Core'
  switch -Regex ($name) {
    '^(Twitch)' { $prefix = 'Twitch'; break }
    '^(YouTube|Youtube)' { $prefix = 'YouTube'; break }
    '^(Obs|OBS)' { $prefix = 'OBS'; break }
    '^(Kick)' { $prefix = 'Kick'; break }
    '^(Trovo)' { $prefix = 'Trovo'; break }
    '^(Elgato)' { $prefix = 'Elgato'; break }
    '^(Speakerbot|SpeakerBot)' { $prefix = 'Speakerbot'; break }
    '^(Streamlabs|Slobs)' { $prefix = 'Streamlabs'; break }
    '^(Meld)' { $prefix = 'Meld Studio'; break }
    '^(VTube)' { $prefix = 'VTube Studio'; break }
    '^(Midi|MIDI)' { $prefix = 'MIDI'; break }
    '^(Websocket|WebSocket)' { $prefix = 'WebSocket'; break }
    '^(Http|HTTP)' { $prefix = 'HTTP'; break }
    '^(Discord)' { $prefix = 'Discord'; break }
    '^(Lumia)' { $prefix = 'Lumia'; break }
    default { $prefix = 'Core' }
  }
  if (-not $buckets.Contains($prefix)) { $buckets[$prefix] = New-Object System.Collections.Generic.List[string] }
  $buckets[$prefix].Add($sig)
}

foreach ($key in ($buckets.Keys | Sort-Object)) {
  [void]$sb.AppendLine("### $key")
  [void]$sb.AppendLine('')
  [void]$sb.AppendLine('```csharp')
  foreach ($sig in ($buckets[$key] | Sort-Object -Unique)) { [void]$sb.AppendLine($sig) }
  [void]$sb.AppendLine('```')
  [void]$sb.AppendLine('')
}

$cphPath = Join-Path $OutDir 'cph-api.md'
[IO.File]::WriteAllText($cphPath, $sb.ToString())
Write-Host "Wrote $cphPath"

# FluentConfig authoring surface
$fcAsm = [Reflection.Assembly]::LoadFrom($FluentConfigDll)
$authorTypes = @(
  @{ Name = 'FluentConfig.FluentConfigUi'; DeclaredOnly = $true },
  @{ Name = 'FluentConfig.FluentConfig'; DeclaredOnly = $true },
  @{ Name = 'FluentConfig.Fc'; DeclaredOnly = $true },
  @{ Name = 'FluentConfig.SectionBuilder'; DeclaredOnly = $false },
  @{ Name = 'FluentConfig.PanelBuilder'; DeclaredOnly = $false },
  @{ Name = 'FluentConfig.IControlOptions'; DeclaredOnly = $true },
  @{ Name = 'FluentConfig.Comparator'; DeclaredOnly = $true },
  @{ Name = 'FluentConfig.UiContext'; DeclaredOnly = $true },
  @{ Name = 'FluentConfig.CallbackContext'; DeclaredOnly = $true },
  @{ Name = 'FluentConfig.KnownBots'; DeclaredOnly = $true },
  @{ Name = 'FluentConfig.Runtime.ExtensionLogger'; DeclaredOnly = $true },
  @{ Name = 'FluentConfig.Runtime.EventContext'; DeclaredOnly = $true },
  @{ Name = 'FluentConfig.Runtime.MessageTemplates'; DeclaredOnly = $true },
  @{ Name = 'FluentConfig.Runtime.SettingsRedaction'; DeclaredOnly = $true },
  @{ Name = 'FluentConfig.Updater.GitHubUpdater'; DeclaredOnly = $true }
)

$fb = New-Object System.Text.StringBuilder
[void]$fb.AppendLine('# FluentConfig API Reference (generated)')
[void]$fb.AppendLine('')
[void]$fb.AppendLine("Source: ``$FluentConfigDll``")
[void]$fb.AppendLine("Generated: $(Get-Date -Format 'yyyy-MM-dd')")
[void]$fb.AppendLine('Author source/docs: `F:\Dev\SB-FluentConfig`')
[void]$fb.AppendLine('')
[void]$fb.AppendLine('Authoring surface only. Prefer `Fc.*` in action scripts. Control factories are on `SectionBuilder` / `PanelBuilder`; option methods chain via `IControlOptions`.')
[void]$fb.AppendLine('Layout: `Grid`/`Row`/`Size` use 1:1 Tailwind class names (see [fluentconfig-guide.md](fluentconfig-guide.md) and `F:\Dev\SB-FluentConfig\docs\guides\LAYOUT.md`).')
[void]$fb.AppendLine('Runtime helpers (`LoadSettings`/`SetSetting`/`LoadData`/`Logger`/`CaptureEvent`/`ApplyTemplate`): [fluentconfig-guide.md](fluentconfig-guide.md) and `F:\Dev\SB-FluentConfig\docs\guides\DIALOGS_AND_RUNTIME_VALUES.md`.')
[void]$fb.AppendLine('Usage patterns: [fluentconfig-guide.md](fluentconfig-guide.md).')
[void]$fb.AppendLine('')

foreach ($entry in $authorTypes) {
  $t = $fcAsm.GetType($entry.Name)
  if (-not $t) {
    [void]$fb.AppendLine("## MISSING $($entry.Name)")
    [void]$fb.AppendLine('')
    continue
  }
  $kind = if ($t.IsEnum) { 'enum' } elseif ($t.IsInterface) { 'interface' } else { 'class' }
  [void]$fb.AppendLine("## $kind $($entry.Name)")
  [void]$fb.AppendLine('')
  [void]$fb.AppendLine('```csharp')
  if ($t.IsEnum) {
    foreach ($line in (Get-EnumMembers $t)) {
      [void]$fb.AppendLine($line)
    }
  } else {
    foreach ($line in (Get-PropertySignatures $t)) {
      [void]$fb.AppendLine($line)
    }
    foreach ($line in (Get-MethodSignatures $t -declaredOnly:([bool]$entry.DeclaredOnly))) {
      [void]$fb.AppendLine($line)
    }
  }
  [void]$fb.AppendLine('```')
  [void]$fb.AppendLine('')
}

$fcPath = Join-Path $OutDir 'fluentconfig-api.md'
[IO.File]::WriteAllText($fcPath, $fb.ToString())
Write-Host "Wrote $fcPath"
