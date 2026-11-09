param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist')
)

$ErrorActionPreference = 'Stop'

$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw '.NET Framework compiler was not found.'
}

$output = (New-Item -ItemType Directory -Path $OutputDirectory -Force).FullName
$icon = Join-Path $PSScriptRoot 'assets\SaveSync.ico'
$sources = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -File
& $compiler /nologo /target:winexe /optimize+ /warnaserror+ /codepage:65001 `
    /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll `
    "/win32icon:$icon" "/out:$output\SaveSync.exe" $sources.FullName
if ($LASTEXITCODE -ne 0) {
    throw 'SaveSync build failed.'
}

Get-Item -LiteralPath (Join-Path $output 'SaveSync.exe')
