[CmdletBinding()]
param(
    [string]$Executable = 'C:\E\kp\aaBenimProgramlarim\NewTemp\NewTemp.exe',
    [string]$ScratchDirectory
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $ScratchDirectory) {
    $ScratchDirectory = & 'C:\E\kp\aaBenimProgramlarim\NewTemp\NewTemp.exe'
    if ($LASTEXITCODE -ne 0) { throw 'NewTemp could not create a temporary folder.' }
}
$testRoot = Join-Path $ScratchDirectory ('tests\' + [Guid]::NewGuid().ToString('N'))
$appRoot = Join-Path $testRoot 'portable'
[IO.Directory]::CreateDirectory($appRoot) | Out-Null
Copy-Item -LiteralPath $Executable -Destination (Join-Path $appRoot 'NewTemp.exe')
$script:testExe = Join-Path $appRoot 'NewTemp.exe'
$script:caseCount = 0
$script:assertions = 0

function Assert([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:assertions++
}

function New-Case([string]$Limit = '10000', [string]$Language = 'tr') {
    $script:caseCount++
    $script:storage = Join-Path $testRoot ('storage-' + $script:caseCount)
    [IO.Directory]::CreateDirectory($script:storage) | Out-Null
    [IO.File]::WriteAllText((Join-Path $appRoot 'NewTemp.settings.txt'), "RootDirectory = $script:storage`nMaximumIndex = $Limit`nLanguage = $Language`n")
}

function Start-Request([string[]]$Arguments = @(), [string]$RawArguments = '') {
    $info = [Diagnostics.ProcessStartInfo]::new($script:testExe)
    $info.WorkingDirectory = $projectRoot
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.Environment['DOTNET_BUNDLE_EXTRACT_BASE_DIR'] = (Join-Path $testRoot 'bundle-cache')
    $info.StandardOutputEncoding = [Text.UTF8Encoding]::new($false)
    $info.StandardErrorEncoding = [Text.UTF8Encoding]::new($false)
    if ($RawArguments) { $info.Arguments = $RawArguments }
    else { foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) } }
    $process = [Diagnostics.Process]::Start($info)
    $output = $process.StandardOutput.ReadToEndAsync()
    $errorOutput = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    $result = [pscustomobject]@{ ExitCode = $process.ExitCode; Path = $output.GetAwaiter().GetResult().TrimEnd("`r", "`n"); Error = $errorOutput.GetAwaiter().GetResult() }
    $process.Dispose()
    return $result
}

function Assert-Success($Result, [string]$ExpectedPath) {
    Assert ($Result.ExitCode -eq 0) "Expected success: $($Result.Error)"
    Assert ($Result.Path -ceq $ExpectedPath) "Unexpected path: [$($Result.Path)] expected [$ExpectedPath]"
    Assert ($Result.Error.Length -eq 0) "Unexpected stderr: $($Result.Error)"
    Assert (Test-Path -LiteralPath $ExpectedPath) "Result path does not exist: $ExpectedPath"
}

New-Case
Assert-Success (Start-Request) (Join-Path $storage 'a')
Assert-Success (Start-Request) (Join-Path $storage 'b')
[IO.Directory]::CreateDirectory((Join-Path $storage 'C')) | Out-Null
[IO.File]::WriteAllText((Join-Path $storage 'd'), 'occupied by a file')
Assert-Success (Start-Request) (Join-Path $storage 'e')

New-Case '2'
[IO.Directory]::CreateDirectory((Join-Path $storage 'a')) | Out-Null
[IO.Directory]::CreateDirectory((Join-Path $storage 'b')) | Out-Null
$full = Start-Request
Assert ($full.ExitCode -eq 3 -and $full.Path.Length -eq 0 -and $full.Error.Contains('bazı klasörleri silmeniz')) 'Allocation limit was not reported.'
Assert-Success (Start-Request @('named sandbox')) (Join-Path $storage 'named sandbox')
Assert-Success (Start-Request @('named sandbox')) (Join-Path $storage 'named sandbox-a')

foreach ($limit in @('1','0','-5','invalid','2147483648','')) {
    New-Case $limit
    [IO.Directory]::CreateDirectory((Join-Path $storage 'a')) | Out-Null
    [IO.Directory]::CreateDirectory((Join-Path $storage 'b')) | Out-Null
    Assert-Success (Start-Request) (Join-Path $storage 'c')
}
New-Case
[IO.File]::WriteAllText((Join-Path $appRoot 'NewTemp.settings.txt'), "RootDirectory = $storage`nLanguage = tr`n")
Assert-Success (Start-Request) (Join-Path $storage 'a')

New-Case
$source = Join-Path $testRoot 'source file.bin'
$bytes = [byte[]](0,1,2,255,10,13,128)
[IO.File]::WriteAllBytes($source, $bytes)
$target = Join-Path $storage 'a\source file.bin'
Assert-Success (Start-Request @($source)) $target
Assert (-not (Test-Path -LiteralPath $source)) 'File source still exists after move.'
Assert ([Convert]::ToHexString([IO.File]::ReadAllBytes($target)) -ceq [Convert]::ToHexString($bytes)) 'File bytes changed.'

New-Case
$source = Join-Path $testRoot 'source folder'
[IO.Directory]::CreateDirectory((Join-Path $source 'nested')) | Out-Null
[IO.File]::WriteAllText((Join-Path $source 'nested\sample.txt'), 'nested content')
$target = Join-Path $storage 'a\source folder'
Assert-Success (Start-Request @($source)) $target
Assert (-not (Test-Path -LiteralPath $source)) 'Directory source still exists after move.'
Assert ([IO.File]::ReadAllText((Join-Path $target 'nested\sample.txt')) -ceq 'nested content') 'Nested file content changed.'

New-Case
$body = "`t two  spaces`r`nTürkçe içerik `"kept`"`n"
$target = Join-Path $storage 'a\dosya adı.mp3'
Assert-Success (Start-Request @(('"dosya adı.mp3"' + $body))) $target
Assert ([IO.File]::ReadAllText($target) -ceq $body) 'Literal named content was changed.'
Assert-Success (Start-Request @('"empty.mp3"')) (Join-Path $storage 'b\empty.mp3')
Assert ((Get-Item -LiteralPath (Join-Path $storage 'b\empty.mp3')).Length -eq 0) 'Empty content is not empty.'

New-Case
$target = Join-Path $storage 'a\dosya adı.mp3'
Assert-Success (Start-Request -RawArguments '"dosya adı.mp3"  raw   content "quoted"') $target
Assert ([IO.File]::ReadAllText($target) -ceq ' raw   content "quoted"') 'Raw argument tail was changed after the separator.'

New-Case
$text = "hello: invalid/name`nsecond line"
$target = Join-Path $storage 'a\a.txt'
Assert-Success (Start-Request @($text)) $target
Assert ([IO.File]::ReadAllText($target) -ceq $text) 'Plain text was changed.'
Assert-Success (Start-Request @('CON')) (Join-Path $storage 'b\a.txt')
Assert-Success (Start-Request @('Türkçe test')) (Join-Path $storage 'Türkçe test')

New-Case '2' 'en'
[IO.Directory]::CreateDirectory((Join-Path $storage 'a')) | Out-Null
[IO.Directory]::CreateDirectory((Join-Path $storage 'b')) | Out-Null
$full = Start-Request
Assert ($full.ExitCode -eq 3 -and $full.Error.Contains('delete some folders')) 'English limit text was not applied.'
New-Case '2' 'tr'
[IO.Directory]::CreateDirectory((Join-Path $storage 'a')) | Out-Null
[IO.Directory]::CreateDirectory((Join-Path $storage 'b')) | Out-Null
Assert ((Start-Request).Error.Contains('bazı klasörleri silmeniz')) 'Turkish was not restored.'

New-Case
$blocked = Start-Request @($storage)
Assert ($blocked.ExitCode -eq 2 -and $blocked.Error.Contains('kendi içine')) 'Moving the storage root was not rejected.'
Assert ((Get-ChildItem -LiteralPath $storage).Count -eq 0) 'Root rejection created a sandbox.'

New-Case
$config = [IO.File]::ReadAllText((Join-Path $appRoot 'NewTemp.settings.txt'))
$movedApp = Join-Path $testRoot 'moved portable'
[IO.Directory]::Move($appRoot, $movedApp)
$script:testExe = Join-Path $movedApp 'NewTemp.exe'
Assert-Success (Start-Request) (Join-Path $storage 'a')
Assert ([IO.File]::ReadAllText((Join-Path $movedApp 'NewTemp.settings.txt')) -ceq $config) 'Portable settings were overwritten.'
Assert (Test-Path -LiteralPath (Join-Path $movedApp 'UserData\lang.en.xml')) 'Portable language files are missing.'
Assert (Test-Path -LiteralPath (Join-Path $movedApp 'AppCache\Logs')) 'Portable logs are missing.'

Write-Output "PASS: $assertions assertions across $caseCount isolated scenarios. Test files: $testRoot"
