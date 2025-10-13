# Start Microsoft Edge with debugging enabled for ValPay Frontend

Write-Host "🌐 Starting Microsoft Edge with debugging enabled..." -ForegroundColor Cyan

# Check if Edge is installed
$edgePath = "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe"
if (-not (Test-Path $edgePath)) {
    $edgePath = "${env:ProgramFiles}\Microsoft\Edge\Application\msedge.exe"
}

if (-not (Test-Path $edgePath)) {
    Write-Host "❌ Microsoft Edge not found. Please install Edge or check the installation path." -ForegroundColor Red
    Write-Host "Expected paths:" -ForegroundColor Yellow
    Write-Host "  - ${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe" -ForegroundColor Gray
    Write-Host "  - ${env:ProgramFiles}\Microsoft\Edge\Application\msedge.exe" -ForegroundColor Gray
    exit 1
}

Write-Host "✅ Found Edge at: $edgePath" -ForegroundColor Green

# Start Edge with debugging enabled
Write-Host "🚀 Launching Edge with debugging on port 9222..." -ForegroundColor Yellow
Write-Host "📱 Navigate to: http://localhost:3000" -ForegroundColor Cyan
Write-Host "🔧 In VS Code, select 'Attach to Edge' to start debugging" -ForegroundColor Green
Write-Host ""

Start-Process -FilePath $edgePath -ArgumentList @(
    "--remote-debugging-port=9222",
    "--disable-web-security",
    "--disable-features=VizDisplayCompositor",
    "--user-data-dir=$env:TEMP\edge-debug-profile",
    "http://localhost:3000"
)

Write-Host "✅ Edge launched with debugging enabled!" -ForegroundColor Green
