Write-Host "========================================="
Write-Host "   AI Museum - Artifact Push Tool"
Write-Host "========================================="
Write-Host ""
$IP = Read-Host "Enter server IP or Tailscale IP"
if (-not $IP) {
    Write-Host "No IP entered. Exiting."
    Read-Host "Press Enter to exit"
    exit 1
}
$SERVER = "http://" + $IP + ":3001"
$SECRET = "your-secret-here"
$FOLDER = Split-Path -Parent $MyInvocation.MyCommand.Path
Write-Host ""
Write-Host "Connecting to $SERVER..."
Write-Host ""
try {
    $status = Invoke-RestMethod -Uri ($SERVER + "/status") -Headers @{"x-ingest-secret"=$SECRET}
    Write-Host ("Status  : " + $status.status)
    Write-Host ("Indexed : " + $status.files + " files")
} catch {
    Write-Host "Cannot reach server"
    Write-Host "Make sure you are on LAN or Tailscale."
    Read-Host "Press Enter to exit"
    exit 1
}
Write-Host ""
Write-Host ("Pushing files from: " + $FOLDER)
Write-Host ""
$docs = @()
Get-ChildItem ($FOLDER + "\*.json") | Where-Object { $_.Name -ne "index.json" } | ForEach-Object {
    $doc = Get-Content $_.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    if ((-not $doc.id) -or (-not $doc.content)) {
        Write-Host ("SKIP " + $_.Name + " - missing id or content")
    } else {
        $t = $doc.title
        if (-not $t) { $t = $doc.id }
        Write-Host ("Queued: " + $t)
        $docs += $doc
    }
}
if ($docs.Count -eq 0) {
    Write-Host "No valid .json files found."
    Read-Host "Press Enter to exit"
    exit 1
}
Write-Host ""
Write-Host ("Pushing " + $docs.Count + " file(s)...")
$payload = ConvertTo-Json @($docs) -Depth 10 -Compress
$bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
try {
    $result = Invoke-RestMethod -Uri ($SERVER + "/upload") -Method Post -Headers @{"x-ingest-secret"=$SECRET} -ContentType "application/json; charset=utf-8" -Body $bytes
    Write-Host ""
    Write-Host ("Saved       : " + ($result.saved -join ", "))
    Write-Host ("Errors      : " + $result.errors.Count)
    Write-Host ("Total in DB : " + $result.total_in_db)
} catch {
    Write-Host ("Upload failed: " + $_)
}
Write-Host ""
Read-Host "Press Enter to exit"
