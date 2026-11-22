# The scheduler receives an encoded, self-contained copy of this script.
$ErrorActionPreference = 'Stop'
$Mode = '__MODE__'
$RootPath = '__ROOT__'
$UserSid = '__SID__'
$taskLog = Join-Path $RootPath 'maintenance.log'
try {
    if (-not (Test-Path -LiteralPath $RootPath -PathType Container)) { throw 'Managed backup folder is missing' }
    if ((Get-Item -LiteralPath $RootPath).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Reparse points are not allowed' }
    $ownerSid = (Get-Acl -LiteralPath $RootPath).GetOwner([Security.Principal.SecurityIdentifier]).Value
    if ($ownerSid -ne 'S-1-5-18' -and $ownerSid -ne 'S-1-5-32-544') { throw 'Managed folder owner is invalid' }
    if ($Mode -eq 'Registry') {
        $backupRoot = Join-Path $RootPath 'Registry'
        if (-not (Test-Path -LiteralPath $backupRoot)) { New-Item -ItemType Directory -Path $backupRoot | Out-Null }
        if ((Get-Item -LiteralPath $backupRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Invalid backup folder' }
        $backupFolder = Join-Path $backupRoot ((Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $backupFolder | Out-Null
        $regTool = Join-Path $env:SystemRoot 'System32\reg.exe'
        foreach ($hive in @('SYSTEM','SOFTWARE','SAM','SECURITY','DEFAULT')) {
            $registryKey = if ($hive -eq 'DEFAULT') { 'HKU\.DEFAULT' } else { 'HKLM\' + $hive }
            $hiveFile = Join-Path $backupFolder ($hive + '.hiv')
            & $regTool save $registryKey $hiveFile /y 2>&1 | Out-File -LiteralPath (Join-Path $backupFolder 'result.txt') -Append
            if ($LASTEXITCODE -ne 0) { throw ('Failed to save ' + $registryKey) }
        }
        if (Test-Path -LiteralPath ('Registry::HKEY_USERS\' + $UserSid)) {
            & $regTool save ('HKU\' + $UserSid) (Join-Path $backupFolder 'USER.hiv') /y 2>&1 | Out-File -LiteralPath (Join-Path $backupFolder 'result.txt') -Append
            if ($LASTEXITCODE -ne 0) { throw 'Failed to save user hive' }
        } else {
            ('User hive ' + $UserSid + ' was not loaded. Machine hives were saved; user backup is partial.') | Out-File -LiteralPath (Join-Path $backupFolder 'USER-NOT-LOADED.txt')
        }
        Get-ChildItem -LiteralPath $backupRoot | Where-Object { $_.PSIsContainer -and $_.Name -match '^\d{8}-\d{6}-[a-f0-9]{32}$' -and $_.LastWriteTime -lt (Get-Date).AddDays(-14) } | ForEach-Object {
            $candidate = [IO.Path]::GetFullPath($_.FullName)
            $expectedParent = [IO.Path]::GetFullPath($backupRoot).TrimEnd('\') + '\'
            if (-not $candidate.StartsWith($expectedParent,[StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid retention path' }
            if ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Retention encountered reparse point' }
            Remove-Item -LiteralPath $candidate -Recurse -Force
        }
        $message = 'Registry backup saved: ' + $backupFolder
    } elseif ($Mode -eq 'Restore') {
        $before = @(Get-ComputerRestorePoint -ErrorAction SilentlyContinue | Select-Object -ExpandProperty SequenceNumber)
        Checkpoint-Computer -Description ('Aurora daily ' + (Get-Date -Format 'yyyy-MM-dd HH:mm')) -RestorePointType MODIFY_SETTINGS -ErrorAction Stop
        $after = @(Get-ComputerRestorePoint -ErrorAction Stop | Select-Object -ExpandProperty SequenceNumber)
        if (@($after | Where-Object { $before -notcontains $_ }).Count -eq 0) { throw 'No new restore point. Check System Protection and the Windows 24-hour limit.' }
        $message = 'System restore point created'
    } else { throw 'Invalid maintenance mode' }
    ((Get-Date -Format s) + ' OK ' + $message) | Out-File -LiteralPath $taskLog -Append -Encoding UTF8
    Write-Output $message
    exit 0
} catch {
    $message = $_.Exception.Message
    try { ((Get-Date -Format s) + ' ERROR ' + $message) | Out-File -LiteralPath $taskLog -Append -Encoding UTF8 } catch { }
    Write-Output $message
    exit 1
}
