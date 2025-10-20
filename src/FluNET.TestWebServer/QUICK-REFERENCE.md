# FluNET Test Server - Quick Reference Card

## 🚀 Start Server
```powershell
cd src/FluNET.TestWebServer
.\start-server.ps1
```
**Server URL:** http://localhost:8765

## ✅ Validate Server
```powershell
.\test-server.ps1
```
**Expected:** 10/10 tests passing

## 🧪 Run Tests
```powershell
# All DOWNLOAD tests (28 tests)
dotnet test --filter "FullyQualifiedName~Download"

# Integration tests only (10 tests)
dotnet test --filter "FullyQualifiedName~DownloadIntegrationTests"

# Complete test suite (252 tests)
dotnet test
```

## 📝 Quick Test URLs
```
http://localhost:8765/api/testfiles/testfile.txt
http://localhost:8765/api/testfiles/data.json
http://localhost:8765/api/testfiles/image.png
http://localhost:8765/api/testfiles/health
```

## 💻 FluNET Examples
```
DOWNLOAD [data] FROM {http://localhost:8765/api/testfiles/data.json} TO {output.json} .
PULL [file] FROM {http://localhost:8765/api/testfiles/testfile.txt} .
GRAB [image] FROM {http://localhost:8765/api/testfiles/image.png} TO {image.png} .
```

## 📚 Documentation
- **Testing Guide:** `DOWNLOAD-TESTING.md` (root)
- **Server Docs:** `src/FluNET.TestWebServer/README.md`
- **Implementation:** `src/FluNET.TestWebServer/IMPLEMENTATION-SUMMARY.md`
- **Success Summary:** `src/FluNET.TestWebServer/SUCCESS-SUMMARY.md`

## 📊 Current Status
✅ **242/252 tests passing (96.0%)**  
✅ **28/28 DOWNLOAD tests passing (100%)**  
✅ **10 endpoints validated**  
✅ **7+ file types supported**

## 🛑 Troubleshooting
```powershell
# Kill process on port 8765
Get-Process -Id (Get-NetTCPConnection -LocalPort 8765).OwningProcess | Stop-Process

# Test connectivity
curl http://localhost:8765/api/testfiles/health

# Check logs
# (Logs appear in console where server is running)
```

## 🎯 Test Coverage
- ✅ JSON parsing
- ✅ Large files (233 KB)
- ✅ CSV format
- ✅ XML parsing
- ✅ Binary files
- ✅ Nested paths
- ✅ Variables
- ✅ Synonyms
- ✅ Error handling
- ✅ Filename extraction
