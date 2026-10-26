$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$refs = @('PresentationCore','PresentationFramework','WindowsBase') | ForEach-Object { '/r:' + (Join-Path $framework ('WPF\' + $_ + '.dll')) }
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ '/r:System.Xaml.dll' '/r:System.Drawing.dll' '/r:System.Windows.Forms.dll' @refs ('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')) ('/resource:' + (Join-Path $root 'assets\sit.png') + ',assets.sit.png') ('/resource:' + (Join-Path $root 'assets\relax.png') + ',assets.relax.png') ('/resource:' + (Join-Path $root 'assets\sleep.png') + ',assets.sleep.png') ('/out:' + (Join-Path $root 'DiandianPet.exe')) (Join-Path $PSScriptRoot 'DiandianPet.cs')
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Output 'Build OK'
