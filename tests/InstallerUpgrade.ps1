# Only run on a disposable CI Windows runner: installs the old and new app.
$ErrorActionPreference='Stop'
if ($env:GITHUB_ACTIONS -ne 'true') { throw 'Installer smoke test requires a disposable GitHub runner.' }
$destination=Join-Path $env:RUNNER_TEMP 'GoatMeter-upgrade-smoke'
$previous=Join-Path $env:RUNNER_TEMP 'GoatMeter-previous-218.exe'
$url='https://github.com/BradyTheGoat94/GoatMeter/releases/download/goatmeter-beta-218'
Invoke-WebRequest "$url/GoatMeter-Setup.exe" -OutFile $previous
$manifestPath=Join-Path $env:RUNNER_TEMP 'GoatMeter-previous-SHA256SUMS.txt'
Invoke-WebRequest "$url/SHA256SUMS.txt" -OutFile $manifestPath
$lines=@(Get-Content $manifestPath | Where-Object {$_ -match '^[0-9a-fA-F]{64}  GoatMeter-Setup.exe\s*$'})
if ($lines.Count -ne 1) { throw 'Previous installer checksum entry missing.' }
$expected=$lines[0].Substring(0,64)
if ((Get-FileHash $previous -Algorithm SHA256).Hash -ne $expected) { throw 'Previous installer checksum failed.' }
function Install([string]$exe,[string]$label) {
  $log=Join-Path $env:RUNNER_TEMP "GoatMeter-install-$label.log"
  $process=Start-Process -FilePath $exe -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/DIR=`"$destination`"",'/TASKS=',"/LOG=`"$log`"") -PassThru -WindowStyle Hidden
  if (!$process.WaitForExit(120000)) { $process.Kill(); throw "Installer timed out: $label" }
  if ($process.ExitCode -ne 0) { Get-Content $log -Tail 35; throw "Installer failed: $label ($($process.ExitCode))" }
}
Install $previous 'previous'
$app=Join-Path $destination 'GoatMeter.exe'
if (!(Test-Path $app)) { throw 'Previous app was not installed.' }
if ((Get-Item $app).VersionInfo.FileVersion -notlike '1.0.218*') { throw 'Unexpected previous version.' }
$data=Join-Path $env:LOCALAPPDATA 'Aion2DPSPro'
New-Item $data -ItemType Directory -Force | Out-Null
$marker=Join-Path $data 'updater-smoke-preservation.txt'
'preserve settings and history directory' | Set-Content $marker
Install (Resolve-Path 'dist/GoatMeter-Setup.exe').Path 'new'
if ((Get-Item $app).VersionInfo.FileVersion -notlike "$env:GOATMETER_VERSION*") { throw 'Upgrade did not replace the app version.' }
if ((Get-Content $marker) -ne 'preserve settings and history directory') { throw 'Upgrade changed user data.' }
Write-Host "PASS: installed 1.0.218, upgraded to $env:GOATMETER_VERSION, preserved user data."
