# Test All Script for ValPay Frontend
# This script runs all tests: unit, integration, and e2e

Write-Host "🧪 Starting ValPay Frontend Test Suite..." -ForegroundColor Green
Write-Host ""

# Check if we're in the correct directory
if (-not (Test-Path "package.json")) {
    Write-Host "❌ Error: package.json not found. Please run this script from the project root." -ForegroundColor Red
    exit 1
}

# Function to run command and handle errors
function Run-Test {
    param(
        [string]$Command,
        [string]$Description
    )
    
    Write-Host "🔍 $Description..." -ForegroundColor Yellow
    Write-Host "Running: $Command" -ForegroundColor Gray
    
    try {
        Invoke-Expression $Command
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ $Description completed successfully" -ForegroundColor Green
        } else {
            Write-Host "❌ $Description failed with exit code $LASTEXITCODE" -ForegroundColor Red
            return $false
        }
    } catch {
        Write-Host "❌ $Description failed with error: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
    
    Write-Host ""
    return $true
}

# Install dependencies if needed
if (-not (Test-Path "node_modules")) {
    Write-Host "📦 Installing dependencies..." -ForegroundColor Yellow
    npm install
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to install dependencies" -ForegroundColor Red
        exit 1
    }
    Write-Host ""
}

# Run linting first
$lintSuccess = Run-Test "npm run lint" "Linting code"

if (-not $lintSuccess) {
    Write-Host "⚠️  Linting failed, but continuing with tests..." -ForegroundColor Yellow
    Write-Host ""
}

# Run unit tests
$unitTestSuccess = Run-Test "npm run test" "Unit tests"

# Run unit tests with coverage
$coverageSuccess = Run-Test "npm run test:coverage" "Unit tests with coverage"

# Check if Playwright is installed
$playwrightInstalled = Test-Path "node_modules/@playwright/test"
if (-not $playwrightInstalled) {
    Write-Host "📦 Installing Playwright..." -ForegroundColor Yellow
    npm install @playwright/test
    npx playwright install
    Write-Host ""
}

# Run e2e tests
$e2eTestSuccess = Run-Test "npm run test:e2e" "End-to-end tests"

# Summary
Write-Host "📊 Test Summary:" -ForegroundColor Cyan
Write-Host "=================" -ForegroundColor Cyan

if ($lintSuccess) {
    Write-Host "✅ Linting: PASSED" -ForegroundColor Green
} else {
    Write-Host "❌ Linting: FAILED" -ForegroundColor Red
}

if ($unitTestSuccess) {
    Write-Host "✅ Unit Tests: PASSED" -ForegroundColor Green
} else {
    Write-Host "❌ Unit Tests: FAILED" -ForegroundColor Red
}

if ($coverageSuccess) {
    Write-Host "✅ Coverage: PASSED" -ForegroundColor Green
} else {
    Write-Host "❌ Coverage: FAILED" -ForegroundColor Red
}

if ($e2eTestSuccess) {
    Write-Host "✅ E2E Tests: PASSED" -ForegroundColor Green
} else {
    Write-Host "❌ E2E Tests: FAILED" -ForegroundColor Red
}

Write-Host ""

# Overall result
$allPassed = $unitTestSuccess -and $e2eTestSuccess

if ($allPassed) {
    Write-Host "🎉 All tests passed! Your ValPay frontend is ready for deployment." -ForegroundColor Green
    exit 0
} else {
    Write-Host "💥 Some tests failed. Please review the output above and fix the issues." -ForegroundColor Red
    exit 1
}
