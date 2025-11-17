#!/bin/bash

# ValPay Frontend Deployment Script
# This script builds and deploys the frontend to AWS S3 + CloudFront

set -e

# Configuration
BUCKET_NAME=${BUCKET_NAME:-"your-valpay-frontend-bucket"}
DISTRIBUTION_ID=${DISTRIBUTION_ID:-"YOUR_DISTRIBUTION_ID"}
REGION=${AWS_REGION:-"us-east-1"}

echo "🚀 Starting ValPay Frontend Deployment..."

# Check if required environment variables are set
if [ -z "$VITE_API_BASE_URL" ]; then
    echo "❌ Error: VITE_API_BASE_URL environment variable is required"
    exit 1
fi

# Install dependencies
echo "📦 Installing dependencies..."
npm ci

# Run linting
echo "🔍 Running linter..."
npm run lint

# Run tests
echo "🧪 Running tests..."
npm run test

# Build the application
echo "🏗️  Building application..."
npm run build

# Check if build was successful
if [ ! -d "dist" ]; then
    echo "❌ Error: Build failed - dist directory not found"
    exit 1
fi

echo "✅ Build completed successfully"

# Deploy to S3
echo "☁️  Deploying to S3 bucket: $BUCKET_NAME"
aws s3 sync dist/ s3://$BUCKET_NAME --delete --region $REGION

# Set cache headers for static assets
echo "📄 Setting cache headers..."
aws s3 cp s3://$BUCKET_NAME s3://$BUCKET_NAME --recursive --metadata-directive REPLACE \
    --cache-control "public, max-age=31536000" \
    --exclude "*.html" \
    --exclude "*.json" \
    --region $REGION

# Set cache headers for HTML files
aws s3 cp s3://$BUCKET_NAME s3://$BUCKET_NAME --recursive --metadata-directive REPLACE \
    --cache-control "public, max-age=0, must-revalidate" \
    --include "*.html" \
    --include "*.json" \
    --region $REGION

# Invalidate CloudFront cache
if [ "$DISTRIBUTION_ID" != "YOUR_DISTRIBUTION_ID" ]; then
    echo "🔄 Invalidating CloudFront cache..."
    aws cloudfront create-invalidation \
        --distribution-id $DISTRIBUTION_ID \
        --paths "/*" \
        --region $REGION
else
    echo "⚠️  Warning: CloudFront distribution ID not set, skipping cache invalidation"
fi

echo "✅ Deployment completed successfully!"
echo "🌐 Your application should be available at your CloudFront URL"
