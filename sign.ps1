$ErrorActionPreference = "Continue"
$pass = "NaxiStudios2026"

$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -like "*Naxi Studios*" -and $_.HasPrivateKey } | Sort-Object NotAfter -Descending | Select-Object -First 1
if (-not $cert) {
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject "CN=Naxi Studios, O=Naxi Studios" -KeyUsage DigitalSignature -FriendlyName "Naxi Studios Code Signing" -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(3)
    "Created cert: $($cert.Thumbprint)"
} else {
    "Reusing cert: $($cert.Thumbprint)"
}

$pw = ConvertTo-SecureString -String $pass -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath "C:\Users\skazk\Desktop\NaxiBootstrap\signing.pfx" -Password $pw | Out-Null
Export-Certificate -Cert $cert -FilePath "C:\Users\skazk\Desktop\NaxiBootstrap\signing.cer" | Out-Null
"Exported signing.pfx / signing.cer"

foreach ($store in @("Cert:\CurrentUser\TrustedPublisher", "Cert:\CurrentUser\Root")) {
    $exists = Get-ChildItem $store | Where-Object { $_.Thumbprint -eq $cert.Thumbprint }
    if (-not $exists) {
        $storeName = $store.Split("\")[-1]
        Import-Certificate -FilePath "C:\Users\skazk\Desktop\NaxiBootstrap\signing.cer" -CertStoreLocation $store | Out-Null
        "Imported to $storeName"
    } else { "Already in $store" }
}
try {
    Import-Certificate -FilePath "C:\Users\skazk\Desktop\NaxiBootstrap\signing.cer" -CertStoreLocation "Cert:\LocalMachine\Root" | Out-Null
    Import-Certificate -FilePath "C:\Users\skazk\Desktop\NaxiBootstrap\signing.cer" -CertStoreLocation "Cert:\LocalMachine\TrustedPublisher" | Out-Null
    "Imported to LocalMachine stores (admin)"
} catch { "LocalMachine import skipped (need admin): $($_.Exception.Message)" }

$exe = "C:\Users\skazk\Desktop\NaxiBootstrap\bin\Release\net8.0-windows\win-x64\publish\NaxiBootstrap.exe"
$sig = Set-AuthenticodeSignature -FilePath $exe -Certificate $cert -TimeStampServer "http://timestamp.digicert.com"
"Signature status: $($sig.Status), signer: $($sig.SignerCertificate.Subject)"
