$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $framework 'csc.exe'
$wpf = Join-Path $framework 'WPF'
if (!(Test-Path $csc)) { throw '.NET Framework compiler not found.' }
& $csc /nologo "/win32icon:$root\PC-Studio.ico" /target:winexe /platform:anycpu /codepage:65001 "/out:$root\PC-Studio.exe" "/resource:$root\MainWindow.xaml,MainWindow.xaml" /reference:System.Management.dll /reference:System.Xaml.dll "/reference:$wpf\WindowsBase.dll" "/reference:$wpf\PresentationCore.dll" "/reference:$wpf\PresentationFramework.dll" "$root\Program.cs"
if ($LASTEXITCODE -ne 0) { throw "Compiler exited with $LASTEXITCODE" }
Write-Host 'Built PC-Studio.exe'
