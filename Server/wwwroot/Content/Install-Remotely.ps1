<#
.SYNOPSIS
   Installs the Remotely Client.
.DESCRIPTION
   Do not modify this script.  It was generated specifically for your account.
.EXAMPLE
   powershell.exe -f Install-Remotely.ps1
   powershell.exe -f Install-Remotely.ps1 -DeviceAlias "My Super Computer" -DeviceGroup "My Stuff"
#>

param (
	[string]$DeviceAlias,
	[string]$DeviceGroup,
	[string]$Path,
	[string]$OrganizationId,
	[string]$ServerUrl,
	[switch]$Uninstall,
	[switch]$Quiet
)

#region Set SecurityProtocol
# This will include all security protocols greater than or equal to TLS 1.2.
[System.Net.SecurityProtocolType]$SecurityProtocols = 0;
[System.Enum]::GetValues([System.Net.SecurityProtocolType]) | Where-Object {
	$_ -ge [System.Net.SecurityProtocolType]::Tls12
} | ForEach-Object {
	$SecurityProtocols = $SecurityProtocols -bor $_
}
[System.Net.ServicePointManager]::SecurityProtocol = $SecurityProtocols
#endregion

#region Set Variables
$LogPath = "$env:TEMP\Remotely_Install.txt"

[string]$HostName = $null
if ($ServerUrl) {
	$HostName = $ServerUrl
}

[string]$Organization = $null
if ($OrganizationId) {
	$Organization = $OrganizationId
}

$ConnectionInfo = $null

if ([System.Environment]::Is64BitOperatingSystem) {
	$Platform = "x64"
}
else {
	$Platform = "x86"
}

$InstallPath = "$env:ProgramFiles\Remotely"
#endregion

#region Functions
function Write-Log($Message) {
	if (!$Quiet) {
		Write-Host $Message
	}
	"$((Get-Date).ToString()) - $Message" | Out-File -FilePath $LogPath -Append
}
function Do-Exit() {
	Write-Log "Exiting..."
	Start-Sleep -Seconds 3
	exit
}
function Is-Administrator() {
	$Identity = [Security.Principal.WindowsIdentity]::GetCurrent()
	$Principal = New-Object System.Security.Principal.WindowsPrincipal -ArgumentList $Identity
	return $Principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Run-StartupChecks {

	if (!$HostName -or !$Organization) {
		Write-Log "Required parameters are missing.  Please try downloading the installer again."
		Do-Exit
	}

	if (!(Is-Administrator)) {
		Write-Log -Message "Install script requires elevation.  Attempting to self-elevate..."
		Start-Sleep -Seconds 3

		$Params = "-File `"$($MyInvocation.ScriptName)`"";
		if ($OrganizationId) {
			$Params += " -OrganizationId $OrganizationId"
		}
		if ($ServerUrl) {
			$Params += " -ServerUrl $ServerUrl"
		}
		if ($DeviceAlias) {
			$Params += " -DeviceAlias $DeviceAlias"
		}
		if ($DeviceGroup) {
			$Params += " -DeviceGroup $DeviceGroup"
		}
		if ($Path) {
			$Params += " -Path `"$Path`""
		}
		if ($Uninstall) {
			$Params += " -Uninstall"
		}
		if ($Quiet) {
			$Params += " -Quiet"
		}
		Start-Process -FilePath powershell.exe -ArgumentList $Params -Verb RunAs
		exit
	}
}

function Stop-Remotely {
	$Service = Get-Service -Name "Remotely_Service" -ErrorAction SilentlyContinue
	$ServiceExists = $null -ne $Service
	if ($Service) {
		if ($Service.Status -ne [System.ServiceProcess.ServiceControllerStatus]::Stopped) {
			Write-Log "Stopping Remotely_Service..."
			Stop-Service -Name "Remotely_Service" -Force -ErrorAction SilentlyContinue
			try {
				$Service.WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Stopped, [TimeSpan]::FromSeconds(30))
			}
			catch {
				# Force-stop the processes below if the service did not exit cleanly.
			}
		}
	}

	# Kill the agent FIRST and wait for it to really exit before touching the
	# desktop. The agent's supervisor relaunches the desktop when it is
	# terminated, so killing the desktop while the agent is still alive
	# guarantees the "process came back" failure below.
	$AgentProcesses = @(Get-Process -Name "Remotely_Agent" -ErrorAction SilentlyContinue)
	foreach ($Process in $AgentProcesses) {
		Stop-Process -Id $Process.Id -Force -ErrorAction SilentlyContinue
	}
	foreach ($Process in $AgentProcesses) {
		try {
			Wait-Process -Id $Process.Id -Timeout 30 -ErrorAction Stop
		}
		catch {
			# Wait-Process also throws when a process has already exited.
		}
	}

	# The agent is gone, so its supervisor can no longer respawn the desktop.
	# Keep trying the desktop until it stays dead; a desktop that survives is
	# removed with taskkill which takes the forceful path.
	for ($Attempt = 1; $Attempt -le 10; $Attempt++) {
		$DesktopProcesses = @(Get-Process -Name "Remotely_Desktop" -ErrorAction SilentlyContinue)
		if ($DesktopProcesses.Count -eq 0) {
			break
		}
		foreach ($Process in $DesktopProcesses) {
			Stop-Process -Id $Process.Id -Force -ErrorAction SilentlyContinue
		}
		Start-Sleep -Seconds 2
	}

	$RemainingDesktop = @(Get-Process -Name "Remotely_Desktop" -ErrorAction SilentlyContinue)
	if ($RemainingDesktop.Count -gt 0) {
		foreach ($Process in $RemainingDesktop) {
			& taskkill /PID $Process.Id /F *> $null
		}
		Start-Sleep -Seconds 2
	}

	$ServiceStopped = $true
	if ($ServiceExists) {
		$Service.Refresh()
		$ServiceStopped = $Service.Status -eq [System.ServiceProcess.ServiceControllerStatus]::Stopped
		$Service.Close()
	}
	if (!$ServiceStopped) {
		throw "Remotely_Service is still running. The old client files were not removed."
	}

	$RemainingProcesses = @(Get-Process -Name "Remotely_Agent", "Remotely_Desktop" -ErrorAction SilentlyContinue)
	if ($RemainingProcesses.Count -gt 0) {
		$ProcessNames = ($RemainingProcesses | ForEach-Object { $_.ProcessName }) -join ", "
		throw "Could not stop Remotely processes ($ProcessNames). Close them and retry the installation."
	}

	if ($ServiceExists) {
		& sc.exe delete "Remotely_Service" *> $null
		$DeleteExitCode = $LASTEXITCODE
		if (($DeleteExitCode -ne 0) -and ($DeleteExitCode -ne 1060) -and ($DeleteExitCode -ne 1072)) {
			throw "Failed to remove Remotely_Service (sc.exe exit code $DeleteExitCode)."
		}

		$ServiceRemoved = $false
		for ($Attempt = 0; $Attempt -lt 30; $Attempt++) {
			& sc.exe query "Remotely_Service" *> $null
			if ($LASTEXITCODE -eq 1060) {
				$ServiceRemoved = $true
				break
			}
			Start-Sleep -Seconds 1
		}
		if (!$ServiceRemoved) {
			throw "Remotely_Service is still being removed. Retry the installation in a few seconds."
		}
	}
}

function Remove-InstallFiles {
	param ([switch]$RemoveInstallDirectory)

	if (!(Test-Path -LiteralPath $InstallPath)) {
		return
	}

	if ($RemoveInstallDirectory) {
		$Targets = @($InstallPath)
	}
	else {
		$Targets = @(Get-ChildItem -LiteralPath $InstallPath -Force | Where-Object {
			$_.Name -ne "ConnectionInfo.json"
		} | ForEach-Object {
			$_.FullName
		})
	}

	foreach ($Target in $Targets) {
		for ($Attempt = 1; $Attempt -le 5; $Attempt++) {
			try {
				if (Test-Path -LiteralPath $Target) {
					Remove-Item -LiteralPath $Target -Recurse -Force -ErrorAction Stop
				}
				break
			}
			catch {
				if ($Attempt -eq 5) {
					throw "Could not remove '$Target'. The client was not updated. $($_.Exception.Message)"
				}
				Write-Log "Waiting for a locked install file to be released (attempt $Attempt of 5)..."
				Start-Sleep -Seconds 2
			}
		}
	}
}

function Uninstall-Remotely {
	Stop-Remotely
	Remove-InstallFiles -RemoveInstallDirectory
	Remove-NetFirewallRule -Name "Remotely Desktop Unattended" -ErrorAction SilentlyContinue
}

function Set-ServiceHardening {
	# The default security descriptor of a Windows service leaves the right to stop and
	# reconfigure it with more than just administrators, which means the agent can be
	# taken down from a user's session.  Restrict those rights to administrators and
	# SYSTEM, and leave read-only access for everyone else.
	$Sddl = "D:(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCLCSWLORC;;;AU)S:(AU;FA;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;WD)"

	& sc.exe sdset "Remotely_Service" $Sddl | Out-Null
	if ($LASTEXITCODE -ne 0) {
		Write-Log "Failed to set the security descriptor of Remotely_Service."
	}

	# Restart the service if it is killed, and give it more than one try before the
	# recovery options run out.
	& sc.exe failureflag "Remotely_Service" 1 | Out-Null
	& sc.exe failure "Remotely_Service" reset=86400 actions=restart/1000/restart/5000/restart/15000 | Out-Null
	if ($LASTEXITCODE -ne 0) {
		Write-Log "Failed to set the recovery options of Remotely_Service."
	}
}

function Install-Remotely {
	$HeadResponse = Invoke-WebRequest -Uri "$HostName/Content/Remotely-Win-$Platform.zip" -Method Head -UseBasicParsing
	$ETag = $HeadResponse.Headers["ETag"]
	if (!$Etag) {
		Write-Log "Failed to get ETag from server.  Aborting install."
	}

	if ((Test-Path -Path "$InstallPath") -and (Test-Path -Path "$InstallPath\ConnectionInfo.json")) {
		$ConnectionInfo = Get-Content -Path "$InstallPath\ConnectionInfo.json" | ConvertFrom-Json
		if ($ConnectionInfo) {
			$ConnectionInfo.Host = $HostName
			$ConnectionInfo.OrganizationID = $Organization
			$ConnectionInfo.ServerVerificationToken = ""
		}
	}
	else {
		New-Item -ItemType Directory -Path "$InstallPath" -Force
	}

	if (!$ConnectionInfo) {
		# The running agent may live in a different directory (a manual or
		# dev install, an AgentApp run). Reuse its identity instead of
		# minting a new DeviceID, which would register a duplicate device.
		foreach ($Process in @(Get-Process -Name "Remotely_Agent" -ErrorAction SilentlyContinue)) {
			try {
				$CandidatePath = Join-Path (Split-Path -Parent $Process.Path) "ConnectionInfo.json"
				if (!(Test-Path -LiteralPath $CandidatePath)) {
					continue
				}
				$Candidate = Get-Content -Path $CandidatePath -Raw | ConvertFrom-Json
				if ($Candidate -and $Candidate.DeviceID) {
					$ConnectionInfo = $Candidate
					$ConnectionInfo.Host = $HostName
					$ConnectionInfo.OrganizationID = $Organization
					$ConnectionInfo.ServerVerificationToken = ""
					Write-Log "Found existing agent install; reusing its DeviceID."
					break
				}
			}
			catch {
				# A process we cannot inspect carries no usable identity here.
			}
		}
	}

	if (!$ConnectionInfo) {
		$NewDeviceId = [System.Guid]::NewGuid().ToString();
		$ConnectionInfo = @{
			DeviceID                = $NewDeviceId;
			Host                    = $HostName;
			OrganizationID          = $Organization;
			ServerVerificationToken = "";
		}
	}

	if ($HostName.EndsWith("/")) {
		$HostName = $HostName.Substring(0, $HostName.LastIndexOf("/"))
	}

	if ($Path) {
		Write-Log "Copying install files..."
		Copy-Item -Path $Path -Destination "$env:TEMP\Remotely-Win-$Platform.zip"

	}
	else {
		$ProgressPreference = 'SilentlyContinue'
		Write-Log "Downloading client..."
		Invoke-WebRequest -Uri "$HostName/Content/Remotely-Win-$Platform.zip" -OutFile "$env:TEMP\Remotely-Win-$Platform.zip" -UseBasicParsing
		$ProgressPreference = 'Continue'
	}

	if (!(Test-Path -Path "$env:TEMP\Remotely-Win-$Platform.zip")) {
		Write-Log "Client files failed to download."
		Do-Exit
	}

	# Non-destructive preflight: extract and verify the new package in
	# isolation FIRST. A corrupt, partial, or wrong-platform zip must not
	# stop and delete the running client, because that leaves the device
	# offline until someone visits it.
	$StagingPath = "$env:TEMP\RemotelyUpdate-Staging-$([System.Guid]::NewGuid().ToString('N'))"
	try {
		New-Item -ItemType Directory -Path "$StagingPath" -Force | Out-Null
		Expand-Archive -Path "$env:TEMP\Remotely-Win-$Platform.zip" -DestinationPath "$StagingPath" -Force -ErrorAction Stop
		if (!(Test-Path -LiteralPath "$StagingPath\Remotely_Agent.exe")) {
			throw "Staged extraction did not produce Remotely_Agent.exe. The current install was left untouched."
		}
	}
	catch {
		Write-Log "Staged preflight failed: $($_.Exception.Message)"
		throw
	}

	# Tell the running agent's desktop supervisor not to relaunch the
	# desktop while it is being replaced. The flag is removed after the new
	# service starts; a stale flag is ignored after ten minutes.
	$SuppressFlagPath = "$InstallPath\SuppressDesktopRestart.flag"
	New-Item -ItemType File -Path "$SuppressFlagPath" -Value (Get-Date).ToString("o") -Force | Out-Null

	try {
		Stop-Remotely
		Remove-InstallFiles

		Copy-Item -Path "$StagingPath\*" -Destination "$InstallPath" -Recurse -Force -ErrorAction Stop
		if (!(Test-Path -LiteralPath "$InstallPath\Remotely_Agent.exe")) {
			throw "Client extraction did not produce Remotely_Agent.exe. The service was not created."
		}
	}
	finally {
		if (Test-Path -LiteralPath $StagingPath) {
			Remove-Item -LiteralPath $StagingPath -Recurse -Force -ErrorAction SilentlyContinue
		}
	}

	New-Item -ItemType File -Path "$InstallPath\ConnectionInfo.json" -Value (ConvertTo-Json -InputObject $ConnectionInfo) -Force

	New-Item -ItemType File -Path "$InstallPath\etag.txt" -Value $ETag -Force

	if ($DeviceAlias -or $DeviceGroup) {
		$DeviceSetupOptions = @{
			DeviceAlias     = $DeviceAlias;
			DeviceGroupName = $DeviceGroup;
			OrganizationID  = $Organization;
			DeviceID        = $ConnectionInfo.DeviceID;
		}

		$Body = $DeviceSetupOptions | ConvertTo-Json
		Invoke-RestMethod -Method Post -ContentType "application/json" -Uri "$HostName/api/devices" -Body $Body
	}

	New-Service -Name "Remotely_Service" -BinaryPathName "`"$InstallPath\Remotely_Agent.exe`"" -DisplayName "Remotely Service" -StartupType Automatic -Description "Background service that maintains a connection to the Remotely server.  The service is used for remote support and maintenance by this computer's administrators."
	Set-ServiceHardening
	Start-Service -Name Remotely_Service

	if (Test-Path -LiteralPath "$InstallPath\SuppressDesktopRestart.flag") {
		Remove-Item -LiteralPath "$InstallPath\SuppressDesktopRestart.flag" -Force -ErrorAction SilentlyContinue
	}
}

#endregion

#region Main

try {
	Run-StartupChecks

	Write-Log "Install/uninstall logs are being written to `"$LogPath`""

	if ($Uninstall) {
		Write-Log "Uninstall started."
		Uninstall-Remotely
		Write-Log "Uninstall completed."
		exit
	}
	else {
		Write-Log "Install started."
		Install-Remotely
		Write-Log "Install completed."
		exit
	}
}
catch {
	Write-Log -Message "Error occurred: $($Error[0].InvocationInfo.PositionMessage)"
	throw $Error[0]
	Do-Exit
}
#endregion
