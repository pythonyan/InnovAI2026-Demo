# start-demo.ps1
# Avvia l'API InnovAI e ngrok per la sessione demo

$ApiProject = Join-Path $PSScriptRoot "InnovAI.Demo.Api"
$ApiUrl     = "https://localhost:7100"
$SwaggerUrl = "$ApiUrl/swagger"
$NgrokUrl   = "https://YOUR-NGROK-URL.ngrok-free.app/api/similarity"

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  InnovAI Demo - Avvio sessione" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# --- 0. Avvia container Docker necessari ---
function Start-DockerContainer {
    param(
        [string]$Name,
        [string]$Image,
        [string]$RunArgs
    )

    $existing = docker ps -a --filter "name=^$Name$" --format "{{.Names}}" 2>$null
    if ($existing -eq $Name) {
        $running = docker ps --filter "name=^$Name$" --format "{{.Names}}" 2>$null
        if ($running -eq $Name) {
            Write-Host "      $Name gia' in esecuzione." -ForegroundColor DarkGray
        } else {
            Write-Host "      $Name esiste ma e' fermo — riavvio..." -ForegroundColor DarkGray
            docker start $Name | Out-Null
            Write-Host "      $Name avviato." -ForegroundColor Green
        }
    } else {
        $imagePresent = docker images --format "{{.Repository}}:{{.Tag}}" 2>$null |
            Where-Object { $_ -eq $Image -or $_.StartsWith(($Image -split ':')[0] + ':') }
        if (-not $imagePresent) {
            Write-Host "      Immagine $Image non trovata — pull in corso..." -ForegroundColor DarkGray
        }
        Invoke-Expression "docker run -d --name $Name $RunArgs $Image" | Out-Null
        Write-Host "      $Name creato e avviato." -ForegroundColor Green
    }
}

Write-Host "[0/3] Verifica container Docker..." -ForegroundColor Yellow

Start-DockerContainer `
    -Name "aspire-dashboard" `
    -Image "mcr.microsoft.com/dotnet/aspire-dashboard:latest" `
    -RunArgs "-p 18888:18888 -p 4317:18889 -e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true"

Start-DockerContainer `
    -Name "smtp4dev" `
    -Image "rnwood/smtp4dev" `
    -RunArgs "-p 3025:25 -p 5025:80"

Write-Host "      Dashboard telemetria : http://localhost:18888" -ForegroundColor DarkGray
Write-Host "      Casella email demo   : http://localhost:5025" -ForegroundColor DarkGray

# --- 1. Avvia API ---
Write-Host "[1/3] Avvio API dotnet..." -ForegroundColor Yellow
$apiProcess = Start-Process -FilePath "dotnet" `
    -ArgumentList "run --project `"$ApiProject`"" `
    -PassThru -WindowStyle Normal

Write-Host "      PID API: $($apiProcess.Id)" -ForegroundColor DarkGray

# --- 2. Attendi che la porta risponda ---
Write-Host "[2/3] Attendo che l'API sia pronta sulla porta 7100..." -ForegroundColor Yellow
$maxWait = 30
$waited  = 0
do {
    Start-Sleep -Seconds 2
    $waited += 2
    $ready = Test-NetConnection -ComputerName localhost -Port 7100 -WarningAction SilentlyContinue -InformationLevel Quiet
} while (-not $ready -and $waited -lt $maxWait)

if (-not $ready) {
    Write-Host "ATTENZIONE: API non risponde dopo $maxWait secondi. Continuo comunque." -ForegroundColor Red
} else {
    Write-Host "      API pronta dopo $waited secondi." -ForegroundColor Green
}

# --- 3. Avvia ngrok ---
Write-Host "[3/3] Avvio ngrok..." -ForegroundColor Yellow
$ngrokProcess = Start-Process -FilePath "ngrok" `
    -ArgumentList "start --all" `
    -PassThru -WindowStyle Normal

Start-Sleep -Seconds 3

# --- Riepilogo ---
Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Tutto avviato" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Swagger locale : $SwaggerUrl" -ForegroundColor White
Write-Host "  Endpoint ngrok : $NgrokUrl" -ForegroundColor White
Write-Host ""
Write-Host "  Premi INVIO per aprire Swagger nel browser..."
Read-Host | Out-Null
Start-Process $SwaggerUrl

Write-Host ""
Write-Host "  Per fermare tutto chiudi le finestre di dotnet e ngrok," -ForegroundColor DarkGray
Write-Host "  oppure premi CTRL+C in ciascuna." -ForegroundColor DarkGray
Write-Host ""
