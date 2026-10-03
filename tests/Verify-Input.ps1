[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Executable,
    [string]$ScratchDirectory
)
$ErrorActionPreference = 'Stop'
if (-not $ScratchDirectory) {
    $ScratchDirectory = & 'C:\E\kp\aaBenimProgramlarim\NewTemp\NewTemp.exe'
    if ($LASTEXITCODE -ne 0) { throw 'NewTemp could not create a temporary folder.' }
}
$testRoot = Join-Path $ScratchDirectory 'input-tests'
$appRoot = Join-Path $testRoot 'app'
$storageRoot = Join-Path $testRoot 'storage'
[IO.Directory]::CreateDirectory($appRoot) | Out-Null
Copy-Item -LiteralPath $Executable -Destination (Join-Path $appRoot 'NewTemp.exe') -Force
$testExe = Join-Path $appRoot 'NewTemp.exe'
$configPath = Join-Path $appRoot 'NewTemp.settings.txt'
[IO.File]::WriteAllText($configPath, "RootDirectory = $storageRoot`nMaximumIndex = 10000`nLanguage = tr`n")
$script:completed = 0

function Invoke-Test([string[]]$Arguments = @(), [string]$RawArguments = '', [switch]$RedirectInput, [byte[]]$Bytes = @()) {
    $start = [Diagnostics.ProcessStartInfo]::new($testExe)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WorkingDirectory = $testRoot
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.RedirectStandardInput = [bool]$RedirectInput
    $start.StandardOutputEncoding = [Text.UTF8Encoding]::new($false)
    $start.StandardErrorEncoding = [Text.UTF8Encoding]::new($false)
    $start.Environment['DOTNET_BUNDLE_EXTRACT_BASE_DIR'] = (Join-Path $testRoot 'bundle-cache')
    if ($RawArguments) { $start.Arguments = $RawArguments }
    else { foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) } }
    $process = [Diagnostics.Process]::Start($start)
    $output = $process.StandardOutput.ReadToEndAsync()
    $errors = $process.StandardError.ReadToEndAsync()
    if ($RedirectInput) {
        $process.StandardInput.BaseStream.Write($Bytes, 0, $Bytes.Length)
        $process.StandardInput.Close()
    }
    $process.WaitForExit()
    $result = [pscustomobject]@{ Code = $process.ExitCode; Path = $output.GetAwaiter().GetResult().TrimEnd("`r", "`n"); Error = $errors.GetAwaiter().GetResult() }
    $process.Dispose()
    return $result
}

function Verify-File($Result, [string]$FileName, [byte[]]$ExpectedBytes, [string]$Scenario) {
    if ($Result.Code -ne 0 -or $Result.Error.Length -ne 0) { throw "$Scenario failed: $($Result.Error)" }
    if ([IO.Path]::GetFileName($Result.Path) -cne $FileName) { throw "$Scenario returned the wrong filename: $($Result.Path)" }
    if ([Convert]::ToHexString([IO.File]::ReadAllBytes($Result.Path)) -cne [Convert]::ToHexString($ExpectedBytes)) { throw "$Scenario changed the file bytes." }
    $script:completed++
    Write-Output "PASS: $Scenario"
}

$utf8 = [Text.UTF8Encoding]::new($false)
Verify-File (Invoke-Test -RawArguments '"test.txt" buraya yazılacak içerik') 'test.txt' ($utf8.GetBytes('buraya yazılacak içerik')) 'Plain quoted filename and text'
Verify-File (Invoke-Test -RawArguments '"dosya adı.mp3"  raw   content "quoted"') 'dosya adı.mp3' ($utf8.GetBytes(' raw   content "quoted"')) 'Repeated spaces and content quotes'
Verify-File (Invoke-Test -Arguments @('"legacy.txt"literal content')) 'legacy.txt' ($utf8.GetBytes('literal content')) 'Existing literal-argument syntax'

[byte[]]$bytes = (0..255) * 4
Verify-File (Invoke-Test -Arguments @('direct stream.mp3') -RedirectInput -Bytes $bytes) 'direct stream.mp3' $bytes 'Binary standard input including zero bytes'
Verify-File (Invoke-Test -Arguments @('empty.bin') -RedirectInput) 'empty.bin' ([byte[]]@()) 'Empty standard input'

$existingPath = Join-Path $testRoot 'existing.bin'
[IO.File]::WriteAllBytes($existingPath, $bytes)
Verify-File (Invoke-Test -Arguments @('existing.bin') -RedirectInput -Bytes ([byte[]](9,8,7))) 'existing.bin' ([byte[]](9,8,7)) 'Stream filename does not move a matching source file'
if ([Convert]::ToHexString([IO.File]::ReadAllBytes($existingPath)) -cne [Convert]::ToHexString($bytes)) { throw 'Stream input modified the existing source file.' }

$noArguments = Invoke-Test -RedirectInput
if ($noArguments.Code -ne 0 -or -not (Test-Path -LiteralPath $noArguments.Path -PathType Container)) { throw 'No-argument directory allocation failed.' }
$script:completed++
Write-Output 'PASS: No-argument directory allocation with a closed input pipe'

$env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = Join-Path $testRoot 'bundle-cache'
$resultPath = & $testExe "normal.txt" buraya yazılacak içerik
Verify-File ([pscustomobject]@{Code=$LASTEXITCODE;Path=$resultPath;Error=''}) 'normal.txt' ($utf8.GetBytes('buraya yazılacak içerik')) 'Normal PowerShell command without outer single quotes'
$typeCommand = 'type "' + $existingPath + '"'
$resultPath = cmd.exe /d /c $typeCommand | & $testExe "native stream.mp3"
Verify-File ([pscustomobject]@{Code=$LASTEXITCODE;Path=$resultPath;Error=''}) 'native stream.mp3' $bytes 'Native PowerShell binary pipeline'

$blockedRoot = Join-Path $testRoot 'blocked-root'
[IO.File]::WriteAllText($blockedRoot, 'A file occupies the configured root.')
[IO.File]::WriteAllText($configPath, "RootDirectory = $blockedRoot`nMaximumIndex = 10000`nLanguage = tr`n")
$failed = Invoke-Test -Arguments @('failed.bin') -RedirectInput -Bytes ([byte[]](1,2,3))
if ($failed.Code -eq 0 -or $failed.Path.Length -ne 0 -or $failed.Error.Length -eq 0) { throw 'Stream failure was reported as success.' }
$script:completed++
Write-Output 'PASS: Stream destination failure returns an error without a success path'
Write-Output "Verified $script:completed input scenarios. Temporary files: $testRoot"
