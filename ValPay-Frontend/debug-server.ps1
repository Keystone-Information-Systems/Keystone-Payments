# ValPay Frontend Debug Server Script
# This script starts the development server with debugging enabled

Write-Host "🐛 Starting ValPay Frontend in Debug Mode..." -ForegroundColor Cyan

# Add Node.js to PATH
$env:PATH += ";C:\Program Files\nodejs"

# Navigate to project directory
$projectPath = "C:\Users\EliasSadaka\OneDrive - Valsoft Corporation\Desktop\ValPay-Project\ValPay-Frontend"
Set-Location $projectPath

# Verify we're in the correct directory
if (-not (Test-Path "package.json")) {
    Write-Host "❌ package.json not found. Please run this script from the project root." -ForegroundColor Red
    exit 1
}

Write-Host "📁 Working directory: $(Get-Location)" -ForegroundColor Yellow

# Set debug environment variables
$env:NODE_ENV = "development"
$env:DEBUG = "vite:*"

# Check if node_modules exists
if (-not (Test-Path "node_modules")) {
    Write-Host "📦 Installing dependencies..." -ForegroundColor Yellow
    npm install
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to install dependencies" -ForegroundColor Red
        exit 1
    }
}

# Start the development server with debug options
Write-Host "🌐 Starting development server with debugging enabled..." -ForegroundColor Yellow
Write-Host "Server will be available at: http://localhost:3000" -ForegroundColor Cyan
Write-Host "Debug configuration ready in VS Code!" -ForegroundColor Green
Write-Host "Press Ctrl+C to stop the server" -ForegroundColor Gray
Write-Host ""

# Start Vite with debug-friendly options
Write-Host "🔍 To debug with Edge:" -ForegroundColor Yellow
Write-Host "1. Start Edge with debugging: msedge --remote-debugging-port=9222" -ForegroundColor Cyan
Write-Host "2. Go to http://localhost:3000" -ForegroundColor Cyan
Write-Host "3. In VS Code, select 'Attach to Edge' and start debugging" -ForegroundColor Cyan
Write-Host ""

npm run dev
