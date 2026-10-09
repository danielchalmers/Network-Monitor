<#
.SYNOPSIS
Installs, upgrades, launches, and uninstalls one architecture's release files, failing on anything a user would hit.

.DESCRIPTION
It only checks that the app starts and keeps running, never its readings, because CI machines may block ping.
It also sets the current user's "Start with Windows" entry, so only run it on a machine that's thrown away afterwards, like CI or Windows Sandbox.
Runs on Windows PowerShell 5.1 and PowerShell 7, so it can be run by hand in Windows Sandbox.
#>
param(
    [Parameter(Mandatory)]
    [ValidateSet('x64', 'arm64')]
    [string] $Arch,

    # The folder with the release files, as the build uploads them.
    [string] $Artifacts = 'artifacts',

    # Where installer logs are written, so a failed run can be looked into.
    [string] $Logs = 'smoke-logs',

    # The repository whose latest release is installed first, to check that the new installer upgrades it.
    [string] $Repository
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$UpgradeCode = '{46A5208D-49CB-499A-BC21-7B6993B1F3F4}'
$Machines = @{ x64 = 0x8664; arm64 = 0xAA64 }
$InstalledExe = Join-Path $env:LOCALAPPDATA 'Network Monitor\Network Monitor.exe'
$Shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Network Monitor\Network Monitor.lnk'
$RunKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$ApprovedKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run'
$StartupName = 'Network_Monitor'

New-Item -ItemType Directory -Force $Logs | Out-Null
$Logs = (Resolve-Path $Logs).Path
$Work = Join-Path ([IO.Path]::GetTempPath()) "network-monitor-smoke-$Arch"
Remove-Item $Work -Recurse -Force -ErrorAction Ignore
New-Item -ItemType Directory -Force $Work | Out-Null

function Step([string] $message) {
    Write-Host "--- $message"
}

function Get-ReleaseFile([string] $extension, [string] $arch = $Arch) {
    $files = @(Get-ChildItem $Artifacts -Filter "Network-Monitor-*-$arch.$extension")

    if ($files.Count -ne 1) {
        throw "Expected one $arch .$extension in $Artifacts but found $($files.Count)."
    }

    return $files[0].FullName
}

# Reads the machine type from the exe's PE header.
function Get-Machine([string] $path) {
    $bytes = [IO.File]::ReadAllBytes($path)
    $peHeader = [BitConverter]::ToInt32($bytes, 0x3C)
    return [BitConverter]::ToUInt16($bytes, $peHeader + 4)
}

function Assert-Machine([string] $path) {
    $machine = Get-Machine $path

    if ($machine -ne $Machines[$Arch]) {
        throw ('{0} is built for machine 0x{1:X4}, not {2} (0x{3:X4}).' -f $path, $machine, $Arch, $Machines[$Arch])
    }
}

function Invoke-Msiexec([string] $action, [string] $package, [string] $logName) {
    $log = Join-Path $Logs "$logName.log"
    $process = Start-Process msiexec.exe -ArgumentList "$action `"$package`" /qn /l*v `"$log`"" -Wait -PassThru
    return $process.ExitCode
}

function Assert-Msiexec([string] $action, [string] $package, [string] $logName) {
    $exitCode = Invoke-Msiexec $action $package $logName

    if ($exitCode -ne 0) {
        throw "msiexec $action $package failed with exit code $exitCode; see $logName.log."
    }
}

# Returns the installed copies of Network Monitor, from any installer that shares its upgrade code.
function Get-InstalledProducts {
    $installer = New-Object -ComObject WindowsInstaller.Installer
    $installer.RelatedProducts($UpgradeCode) | ForEach-Object { $_ }
}

function Get-UninstallEntries {
    $keys = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'

    Get-ItemProperty $keys -ErrorAction Ignore | Where-Object { $_.PSObject.Properties['DisplayName'] -and $_.DisplayName -eq 'Network Monitor' }
}

function Get-PackageVersion([string] $package) {
    $installer = New-Object -ComObject WindowsInstaller.Installer
    $database = $installer.OpenDatabase($package, 0)
    $view = $database.OpenView("SELECT Value FROM Property WHERE Property = 'ProductVersion'")
    $null = $view.Execute()
    $version = $view.Fetch().StringData(1)
    $null = $view.Close()
    return $version
}

# Sets the startup entry the way the app's "Start with Windows" does, plus the record Task Manager keeps of it being enabled.
function Set-Startup([string] $exe) {
    Set-ItemProperty $RunKey $StartupName "`"$exe`""

    # A fresh profile may not have the key yet, and -Force on one that exists would empty it.
    if (-not (Test-Path $ApprovedKey)) {
        New-Item $ApprovedKey -Force | Out-Null
    }

    Set-ItemProperty $ApprovedKey $StartupName ([byte[]](2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)) -Type Binary
}

# Returns nothing when the value is missing, where Get-ItemPropertyValue would throw whatever -ErrorAction says.
function Get-StartupValue([string] $key) {
    $values = Get-ItemProperty $key -ErrorAction Ignore

    if ($values -and $values.PSObject.Properties[$StartupName]) {
        return $values.PSObject.Properties[$StartupName].Value
    }
}

function Assert-Startup([string] $exe) {
    if ((Get-StartupValue $RunKey) -ne "`"$exe`"" -or -not (Get-StartupValue $ApprovedKey)) {
        throw "The startup entry for $exe is gone or changed: $(Get-StartupValue $RunKey)."
    }
}

# Starts the exe and fails if it exits or writes a crash log within 10 seconds.
# The crash handler keeps the process open while it shows its message, so a running process alone doesn't prove it didn't crash.
function Assert-KeepsRunning([string] $exe) {
    $crashLogs = (Join-Path (Split-Path $exe) 'Network Monitor.log'), (Join-Path ([IO.Path]::GetTempPath()) 'Network Monitor.log')
    Remove-Item $crashLogs -ErrorAction Ignore

    $process = Start-Process $exe -PassThru

    # Holding the handle from the start keeps the exit code readable once it exits.
    $null = $process.Handle
    Start-Sleep -Seconds 10

    try {
        foreach ($crashLog in $crashLogs) {
            if (Test-Path $crashLog) {
                throw "$exe crashed:`n$(Get-Content $crashLog -Raw)"
            }
        }

        if ($process.HasExited) {
            throw "$exe exited with code $($process.ExitCode)."
        }
    }
    finally {
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
            $process.WaitForExit()
        }
    }
}

$msi = Get-ReleaseFile 'msi'
$zip = Get-ReleaseFile 'zip'
$version = Get-PackageVersion $msi

if (@(Get-InstalledProducts).Count) {
    throw 'Network Monitor is already installed, so the results would be meaningless.'
}

Step "Portable zip holds the $Arch exe"
Expand-Archive $zip (Join-Path $Work 'portable')
$portableExe = Join-Path $Work 'portable\Network Monitor.exe'
Assert-Machine $portableExe

if ($Arch -eq 'x64') {
    Step 'An x64 PC refuses the Arm64 installer'
    $exitCode = Invoke-Msiexec '/i' (Get-ReleaseFile 'msi' 'arm64') 'install-arm64-on-x64'

    # ERROR_INSTALL_PLATFORM_UNSUPPORTED
    if ($exitCode -ne 1633) {
        throw "Installing the Arm64 installer on x64 returned $exitCode instead of refusing with 1633."
    }

    if (@(Get-InstalledProducts).Count) {
        throw 'The Arm64 installer installed on x64.'
    }
}

$upgrading = $false

if ($Repository) {
    Step "Install the newest release of $Repository with an installer for $Arch"
    $headers = @{}

    if ($env:GH_TOKEN) {
        $headers.Authorization = "Bearer $env:GH_TOKEN"
    }

    $release = $null
    $asset = $null

    # A release that was just published has no files until the deploy workflow attaches them, so the one before it is used meanwhile.
    foreach ($candidate in (Invoke-RestMethod "https://api.github.com/repos/$Repository/releases?per_page=10" -Headers $headers)) {
        if ($candidate.draft -or $candidate.prerelease) {
            continue
        }

        $installers = @($candidate.assets | Where-Object name -Like '*.msi')

        # Releases before 5.0 had a single installer for every PC, without an architecture in its name.
        $asset = @($installers | Where-Object name -Like "*-$Arch.msi") + @($installers | Where-Object name -NotMatch '-(x64|arm64)\.msi$') | Select-Object -First 1

        if ($asset) {
            $release = $candidate
            break
        }
    }

    if ($asset) {
        $previousMsi = Join-Path $Work $asset.name
        Invoke-WebRequest $asset.browser_download_url -OutFile $previousMsi -Headers $headers -UseBasicParsing
        Assert-Msiexec '/i' $previousMsi 'install-previous'
        Write-Host "Installed $($release.tag_name) ($($asset.name))."
        $upgrading = $true
    }
    else {
        Write-Warning "No release of $Repository has an installer for $Arch, so upgrading from one isn't tested."
    }
}

# Arm PCs can also install the x64 build, so upgrading from it tests an upgrade from a version that cleans up when it's uninstalled.
if ($Arch -eq 'arm64') {
    Step 'Install the x64 build, as someone on Arm who picked the wrong file would'
    Assert-Msiexec '/i' (Get-ReleaseFile 'msi' 'x64') 'install-x64-on-arm64'
    $upgrading = $true
}

if ($upgrading) {
    Set-Startup $InstalledExe
}

Step "Install $(Split-Path $msi -Leaf)"
Assert-Msiexec '/i' $msi 'install'

$products = @(Get-InstalledProducts)

if ($products.Count -ne 1) {
    throw "Expected one installed copy but found $($products.Count): $($products -join ', ')."
}

$entries = @(Get-UninstallEntries)

if ($entries.Count -ne 1 -or $entries[0].DisplayVersion -ne $version) {
    throw "Expected one installed apps entry for $version but found: $(($entries | ForEach-Object DisplayVersion) -join ', ')."
}

if (-not (Test-Path $Shortcut)) {
    throw "The Start menu shortcut is missing: $Shortcut."
}

Assert-Machine $InstalledExe

if ($upgrading) {
    Step 'Upgrading kept the startup entry'
    Assert-Startup $InstalledExe
}

Step 'Installed app starts and keeps running'
Assert-KeepsRunning $InstalledExe

Step 'Uninstalling removes the startup entry and crash log'
Set-Startup $InstalledExe
Set-Content (Join-Path (Split-Path $InstalledExe) 'Network Monitor.log') 'An old crash'
Assert-Msiexec '/x' $msi 'uninstall'

if (@(Get-InstalledProducts).Count) {
    throw 'Network Monitor is still installed after uninstalling.'
}

if (@(Get-UninstallEntries).Count) {
    throw 'Network Monitor is still in installed apps after uninstalling.'
}

foreach ($path in (Split-Path $InstalledExe), $Shortcut) {
    if (Test-Path $path) {
        throw "Uninstalling left $path behind."
    }
}

if ((Get-StartupValue $RunKey) -or (Get-StartupValue $ApprovedKey)) {
    throw 'Uninstalling left the startup entry behind.'
}

Step "Uninstalling leaves another copy's startup entry alone"
Assert-Msiexec '/i' $msi 'reinstall'
Set-Startup $portableExe
Assert-Msiexec '/x' $msi 'uninstall-again'
Assert-Startup $portableExe
Remove-ItemProperty $RunKey $StartupName
Remove-ItemProperty $ApprovedKey $StartupName

Step 'Portable app starts and keeps running'
Assert-KeepsRunning $portableExe

Step "Network Monitor $version for $Arch passed"
