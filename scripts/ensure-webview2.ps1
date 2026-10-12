$ErrorActionPreference = 'Stop'
$runtimeId = '{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}'
$locations = @("HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\$runtimeId", "HKLM:\SOFTWARE\Microsoft\EdgeUpdate\Clients\$runtimeId", "HKCU:\SOFTWARE\Microsoft\EdgeUpdate\Clients\$runtimeId")
function Get-RuntimeVersion {
    foreach ($location in $locations) {
        $value = Get-ItemProperty -LiteralPath $location -Name pv -ErrorAction SilentlyContinue
        if ($value.pv -and $value.pv -ne '0.0.0.0') { return $value.pv }
    }
    return $null
}
$runtimeVersion = Get-RuntimeVersion
Write-Output "Desktop runtime preflight: WebView2=$runtimeVersion; UserInteractive=$([Environment]::UserInteractive)"
if (-not $runtimeVersion) {
    $bootstrap = Join-Path $env:RUNNER_TEMP 'labinvoice-webview2-bootstrap.exe'
    Invoke-WebRequest -Uri 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' -OutFile $bootstrap
    $signature = Get-AuthenticodeSignature -LiteralPath $bootstrap
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Microsoft Corporation') { throw 'WebView2 bootstrap does not have a valid Microsoft signature' }
    $installer = Start-Process -FilePath $bootstrap -ArgumentList '/silent','/install' -WindowStyle Hidden -Wait -PassThru
    if ($installer.ExitCode -ne 0) { throw "WebView2 setup failed: $($installer.ExitCode)" }
    $runtimeVersion = Get-RuntimeVersion
    if (-not $runtimeVersion) { throw 'WebView2 runtime remains unavailable' }
    Write-Output "Installed official WebView2 runtime: $runtimeVersion"
}
