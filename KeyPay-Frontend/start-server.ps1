# ValPay Frontend Server Startup Script
# This script ensures Node.js is in PATH and starts the development server

Write-Host "🚀 Starting ValPay Frontend Server..." -ForegroundColor Green

# Add Node.js to PATH
$env:PATH += ";C:\Program Files\nodejs"

# Verify Node.js is available
try {
    $nodeVersion = & node --version
    $npmVersion = & npm --version
    Write-Host "✅ Node.js version: $nodeVersion" -ForegroundColor Green
    Write-Host "✅ npm version: $npmVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ Node.js not found. Please install Node.js first." -ForegroundColor Red
    exit 1
}

# Navigate to project directory
$projectPath = "C:\Users\EliasSadaka\OneDrive - Valsoft Corporation\Desktop\ValPay-Project\ValPay-Frontend"
Set-Location $projectPath

# Verify we're in the correct directory
if (-not (Test-Path "package.json")) {
    Write-Host "❌ package.json not found. Please run this script from the project root." -ForegroundColor Red
    exit 1
}

Write-Host "📁 Working directory: $(Get-Location)" -ForegroundColor Yellow

# Check if node_modules exists
if (-not (Test-Path "node_modules")) {
    Write-Host "📦 Installing dependencies..." -ForegroundColor Yellow
    npm install
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to install dependencies" -ForegroundColor Red
        exit 1
    }
}

# Start the development server
Write-Host "🌐 Starting development server..." -ForegroundColor Yellow
Write-Host "Server will be available at: http://localhost:3000" -ForegroundColor Cyan
Write-Host "Press Ctrl+C to stop the server" -ForegroundColor Gray
Write-Host ""

npm run dev
