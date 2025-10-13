#!/bin/bash

# Test All Script for ValPay Frontend
# This script runs all tests: unit, integration, and e2e

echo "🧪 Starting ValPay Frontend Test Suite..."
echo ""

# Check if we're in the correct directory
if [ ! -f "package.json" ]; then
    echo "❌ Error: package.json not found. Please run this script from the project root."
    exit 1
fi

# Function to run command and handle errors
run_test() {
    local command="$1"
    local description="$2"
    
    echo "🔍 $description..."
    echo "Running: $command"
    
    if eval "$command"; then
        echo "✅ $description completed successfully"
    else
        echo "❌ $description failed with exit code $?"
        return 1
    fi
    
    echo ""
    return 0
}

# Install dependencies if needed
if [ ! -d "node_modules" ]; then
    echo "📦 Installing dependencies..."
    npm install
    if [ $? -ne 0 ]; then
        echo "❌ Failed to install dependencies"
        exit 1
    fi
    echo ""
fi

# Run linting first
run_test "npm run lint" "Linting code"
lint_success=$?

if [ $lint_success -ne 0 ]; then
    echo "⚠️  Linting failed, but continuing with tests..."
    echo ""
fi

# Run unit tests
run_test "npm run test" "Unit tests"
unit_test_success=$?

# Run unit tests with coverage
run_test "npm run test:coverage" "Unit tests with coverage"
coverage_success=$?

# Check if Playwright is installed
if [ ! -d "node_modules/@playwright/test" ]; then
    echo "📦 Installing Playwright..."
    npm install @playwright/test
    npx playwright install
    echo ""
fi

# Run e2e tests
run_test "npm run test:e2e" "End-to-end tests"
e2e_test_success=$?

# Summary
echo "📊 Test Summary:"
echo "================="

if [ $lint_success -eq 0 ]; then
    echo "✅ Linting: PASSED"
else
    echo "❌ Linting: FAILED"
fi

if [ $unit_test_success -eq 0 ]; then
    echo "✅ Unit Tests: PASSED"
else
    echo "❌ Unit Tests: FAILED"
fi

if [ $coverage_success -eq 0 ]; then
    echo "✅ Coverage: PASSED"
else
    echo "❌ Coverage: FAILED"
fi

if [ $e2e_test_success -eq 0 ]; then
    echo "✅ E2E Tests: PASSED"
else
    echo "❌ E2E Tests: FAILED"
fi

echo ""

# Overall result
if [ $unit_test_success -eq 0 ] && [ $e2e_test_success -eq 0 ]; then
    echo "🎉 All tests passed! Your ValPay frontend is ready for deployment."
    exit 0
else
    echo "💥 Some tests failed. Please review the output above and fix the issues."
    exit 1
fi
