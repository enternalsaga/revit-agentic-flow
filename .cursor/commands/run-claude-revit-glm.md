# run-claude-revit-glm

Khởi chạy Claude Code với Revit MCP và cấu hình Z.AI GLM.

Workflow này chạy `claude.exe` với **mcp-server-for-revit** và biến môi trường GLM (Z.AI), thông qua `CLAUDE-REVIT-GLM.bat`.

**Batch:** `%USERPROFILE%\.claude\commands\scripts\CLAUDE-REVIT-GLM.bat`

1. Mở terminal tại **thư mục gốc của project đang mở** (workspace root).
2. Chạy (PowerShell):

```powershell
# Chạy từ thư mục gốc workspace (repo đang mở), ví dụ:
Set-Location "h:\OneDrive\Work\AI\Work\MCP_Revit"
& "$env:USERPROFILE\.claude\commands\scripts\CLAUDE-REVIT-GLM.bat"
```

3. Batch sẽ **mở cửa sổ CMD mới** chạy Claude (TUI tương tác); cửa sổ gốc chỉ log bước cấu hình MCP. Khi agent chạy lệnh, dùng `block_until_ms` khoảng **5000** để kịp thấy log MCP trước khi chuyển nền.

**Ghi chú:** Trong `CLAUDE-REVIT-GLM.bat` cần có `ZAI_API_KEY` hoặc biến môi trường `ANTHROPIC_AUTH_TOKEN` (Z.AI). Có thể đồng bộ thêm `ANTHROPIC_DEFAULT_*_MODEL` như trong `CLAUDE-GLM.bat` nếu cần.
