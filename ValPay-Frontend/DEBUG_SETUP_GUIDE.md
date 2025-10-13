# ValPay Frontend Debug Setup Guide

## 🚨 Current Issue
The error "Could not connect to debug target at http://localhost:9222" occurs because Edge isn't running with debugging enabled.

## ✅ Solutions (Try in Order)

### Option 1: Simple Edge Launch (Recommended)
1. **Make sure your dev server is running:**
   ```powershell
   npm run dev
   ```

2. **In VS Code:**
   - Press `F5` or go to Run and Debug panel
   - Select **"Launch Edge (Simple)"** from dropdown
   - Click the green play button

### Option 2: Manual Edge with Debugging
1. **Start your dev server:**
   ```powershell
   npm run dev
   ```

2. **Open Edge with debugging:**
   ```powershell
   msedge --remote-debugging-port=9222 --disable-web-security
   ```

3. **Navigate to:** `http://localhost:3000`

4. **In VS Code:**
   - Select **"Attach to Edge"** from debug dropdown
   - Click the green play button

### Option 3: Use the Edge Debug Script
1. **Run the Edge debug script:**
   ```powershell
   .\start-edge-debug.ps1
   ```

2. **In VS Code:**
   - Select **"Attach to Edge"** from debug dropdown
   - Click the green play button

## 🔧 Troubleshooting

### If Edge still won't connect:
1. **Check if Edge is running with debugging:**
   - Go to `http://localhost:9222` in any browser
   - You should see a JSON response with Edge targets

2. **Kill any existing Edge processes:**
   ```powershell
   taskkill /F /IM msedge.exe
   ```

3. **Try the simple launch option first** - it doesn't require remote debugging

### If you get "Edge not found" error:
1. **Check Edge installation path:**
   - `C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe`
   - `C:\Program Files\Microsoft\Edge\Application\msedge.exe`

2. **Update the path in `start-edge-debug.ps1` if needed**

## 🎯 Available Debug Configurations

- **"Launch Edge (Simple)"** - Easiest option, no remote debugging needed
- **"Launch ValPay Frontend (Edge)"** - Full debugging with remote debugging
- **"Attach to Edge"** - Attach to manually launched Edge
- **"Debug Vite Dev Server"** - Debug the Vite server itself

## 🚀 Quick Start
1. Run `npm run dev`
2. Press `F5` in VS Code
3. Select "Launch Edge (Simple)"
4. Start debugging!
