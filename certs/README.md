# Build-time corporate root CAs

Drop PEM-encoded root CA certificates (`.crt`) here to make the Docker **build**
trust an SSL-inspecting proxy (e.g. Zscaler) when it fetches NuGet packages and
the Synthea JAR over HTTPS.

- Files here are git-ignored (see `.gitignore`) — **never commit a corp CA**.
- In a clean network (GitHub Actions / public CI) this directory has no `.crt`,
  so the Dockerfile's `update-ca-certificates` step is a harmless no-op.
- These CAs are injected only into the **build** stages, never into the published
  runtime image.

Export the Zscaler root CA on Windows, for example:

```powershell
$c = Get-Item Cert:\LocalMachine\Root\<thumbprint>
$b64 = [Convert]::ToBase64String($c.RawData, 'InsertLineBreaks')
"-----BEGIN CERTIFICATE-----`r`n$b64`r`n-----END CERTIFICATE-----" |
    Set-Content certs\zscaler-root.crt -Encoding ascii
```
