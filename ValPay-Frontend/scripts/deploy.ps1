# ValPay Frontend Deployment Script for Windows
# This script builds and deploys the frontend to AWS S3 + CloudFront

param(
    [string]$BucketName = "your-valpay-frontend-bucket",
    [string]$DistributionId = "YOUR_DISTRIBUTION_ID",
    [string]$Region = "us-east-1"
)

# Stop on any error
$ErrorActionPreference = "Stop"

Write-Host "🚀 Starting ValPay Frontend Deployment..." -ForegroundColor Green

# Check if required environment variables are set
if (-not $env:VITE_API_BASE_URL) {
    Write-Host "❌ Error: VITE_API_BASE_URL environment variable is required" -ForegroundColor Red
    exit 1
}

if (-not $env:VITE_ADYEN_CLIENT_KEY) {
    Write-Host "❌ Error: VITE_ADYEN_CLIENT_KEY environment variable is required" -ForegroundColor Red
    exit 1
}

# Install dependencies
Write-Host "📦 Installing dependencies..." -ForegroundColor Blue
npm ci

# Run linting
Write-Host "🔍 Running linter..." -ForegroundColor Blue
npm run lint

# Run tests
Write-Host "🧪 Running tests..." -ForegroundColor Blue
npm run test

# Build the application
Write-Host "🏗️  Building application..." -ForegroundColor Blue
npm run build

# Check if build was successful
if (-not (Test-Path "dist")) {
    Write-Host "❌ Error: Build failed - dist directory not found" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Build completed successfully" -ForegroundColor Green

# Deploy to S3
Write-Host "☁️  Deploying to S3 bucket: $BucketName" -ForegroundColor Blue
aws s3 sync dist/ "s3://$BucketName" --delete --region $Region

# Set cache headers for static assets
Write-Host "📄 Setting cache headers..." -ForegroundColor Blue
aws s3 cp "s3://$BucketName" "s3://$BucketName" --recursive --metadata-directive REPLACE `
    --cache-control "public, max-age=31536000" `
    --exclude "*.html" `
    --exclude "*.json" `
    --region $Region

# Set cache headers for HTML files
aws s3 cp "s3://$BucketName" "s3://$BucketName" --recursive --metadata-directive REPLACE `
    --cache-control "public, max-age=0, must-revalidate" `
    --include "*.html" `
    --include "*.json" `
    --region $Region

# Invalidate CloudFront cache
if ($DistributionId -ne "YOUR_DISTRIBUTION_ID") {
    Write-Host "🔄 Invalidating CloudFront cache..." -ForegroundColor Blue
    aws cloudfront create-invalidation `
        --distribution-id $DistributionId `
        --paths "/*" `
        --region $Region
} else {
    Write-Host "⚠️  Warning: CloudFront distribution ID not set, skipping cache invalidation" -ForegroundColor Yellow
}

Write-Host "✅ Deployment completed successfully!" -ForegroundColor Green
Write-Host "🌐 Your application should be available at your CloudFront URL" -ForegroundColor Green
