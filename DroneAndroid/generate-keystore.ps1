# Android Keystore Generation Script
# Run this to generate the keystore for signing APKs

# Set environment variable for password
$env:NIGHTFRAME_KEYSTORE_PASS = "NightFrame2024!"

# Generate keystore using keytool (requires JDK)
# keytool -genkeypair -v -keystore nightframe.keystore -alias nightframe -keyalg RSA -keysize 2048 -validity 10000 -storepass $env:NIGHTFRAME_KEYSTORE_PASS -keypass $env:NIGHTFRAME_KEYSTORE_PASS -dname "CN=NIGHTFRAME, OU=Drone, O=NIGHTFRAME, L=Global, ST=Mesh, C=WW"

Write-Host "Keystore configuration:"
Write-Host "  File: nightframe.keystore"
Write-Host "  Alias: nightframe"
Write-Host "  Password: (set in NIGHTFRAME_KEYSTORE_PASS env var)"
Write-Host ""
Write-Host "To generate the actual keystore, run:"
Write-Host '  keytool -genkeypair -v -keystore nightframe.keystore -alias nightframe -keyalg RSA -keysize 2048 -validity 10000'
