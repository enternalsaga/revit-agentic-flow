# Hướng dẫn Cài đặt, Sử dụng và Cập nhật Revit MCP

Tài liệu này hướng dẫn cách thiết lập và sử dụng bộ công cụ **Revit MCP** (Model Context Protocol) để kết nối các trợ lý AI (như Claude, Cursor, Cline) trực tiếp với Autodesk Revit.

---

## 1. Cài đặt (Installation)

### Yêu cầu hệ thống
- **Autodesk Revit** (Hỗ trợ bản 2023, 2024, 2025, 2026 - ưu tiên 2024 theo thiết lập mặc định).
- **Node.js 18+** (Để chạy phần MCP Server bằng TypeScript).

### Các bước cài đặt Plugin vào Revit
Dự án đã cung cấp sẵn một file script (`install.bat`) để tự động hóa quá trình cài đặt.

1. Đảm bảo rằng bạn đã **đóng toàn bộ phiên làm việc của phần mềm Revit**.
2. Mở thư mục gốc của dự án `MCP_Revit` (thư mục chứa file `install.bat`).
3. Nhấp đúp (hoặc chạy qua Terminal) file `install.bat`.
4. Script sẽ hỏi phiên bản Revit bạn muốn cài (mặc định là `2024`). Nhập phiên bản tương ứng và nhấn Enter.
5. Script sẽ tự động copy file `.addin` và thư mục `revit_mcp_plugin` (bao gồm các file DLL) vào thư mục `%AppData%\Autodesk\Revit\Addins\<Phiên_bản>\` của bạn và Unblock các file DLL.

*(Lưu ý: Nếu bạn chạy `install.bat` với quyền Administrator, plugin sẽ được cài cho toàn bộ người dùng trên máy tính tại thư mục `%ProgramData%` thay vì `%AppData%`).*

### Thiết lập MCP Server cho AI (Claude Desktop / Cursor / Claude Code)

Để AI có thể nhận diện và giao tiếp được với Revit, bạn cần cấu hình khai báo máy chủ MCP.

**Đối với Claude Desktop / Cursor:**
Thêm cấu hình sau vào file thiết lập MCP (ví dụ `claude_desktop_config.json` hoặc trong phần cài đặt MCP của Cursor):

```json
{
  "mcpServers": {
    "mcp-server-for-revit": {
      "command": "node",
      "args": [
        "H:\\OneDrive\\Work\\AI\\Work\\MCP_Revit\\mcp-servers-for-revit\\server\\build\\index.js"
      ]
    }
  }
}
```
*(Nếu bạn đổi vị trí thư mục làm việc, hãy nhớ cập nhật lại đường dẫn tuyệt đối cho đúng).*

**Đối với Claude Code (CLI):**
Mở terminal và chạy lệnh:
```bash
claude mcp add revit-mcp -- node H:\OneDrive\Work\AI\Work\MCP_Revit\mcp-servers-for-revit\server\build\index.js
```

---

## 2. Sử dụng (Usage)

1. **Khởi động Revit:** Mở phiên bản Revit bạn vừa cài đặt plugin (vd: Revit 2024).
2. **Xác nhận Load Plugin:** Nếu Revit hiện cảnh báo về add-in không xác định (Unknown Publisher), hãy chọn **"Always Load"**.
3. **Cấu hình trong Revit:** Trên thanh Ribbon của Revit, tìm tab **mcp-servers-for-revit**, bấm nút **Settings**. Tại đây, đảm bảo các chức năng/lệnh (tools) bạn muốn AI sử dụng đều đã được đánh dấu tích (enable), sau đó bấm **Save**.
4. **Sử dụng AI:** Mở AI Client (Claude/Cursor) đã được kết nối MCP ở bước trên. Bạn có thể bắt đầu ra lệnh cho AI bằng ngôn ngữ tự nhiên.
   - *Ví dụ 1:* "Hãy lấy thông tin các phòng trong model hiện tại."
   - *Ví dụ 2:* "Tạo một hệ lưới trục (grid) 5x5 khoảng cách 4000mm."
   - *Ví dụ 3:* "Thống kê số lượng tường và vật liệu trong dự án."

---

## 3. Cập nhật (Update)

Trong quá trình phát triển, khi có cập nhật code C# (plugin) hoặc TypeScript (server), bạn cần thực hiện theo các bước sau để làm mới hệ thống:

### Cập nhật MCP Server (Phần Node.js/TypeScript)
Nếu phần định nghĩa công cụ (trong thư mục `server/`) có sự thay đổi:
1. Mở Terminal, di chuyển vào thư mục `mcp-servers-for-revit/server`.
2. Chạy lệnh để cài đặt thư viện và build lại mã nguồn:
   ```bash
   npm install
   npm run build
   ```
3. Khởi động lại AI Client (Claude/Cursor) để nó kết nối lại và nhận diện danh sách các tools mới.

### Cập nhật Plugin Revit (Phần C# DLL)
Nếu mã nguồn C# (trong `plugin/` hoặc `commandset/`) được cập nhật:
1. Mở file `mcp-servers-for-revit.sln` bằng Visual Studio. Chọn đúng cấu hình Build (Configuration) cho phiên bản Revit bạn đang dùng (ví dụ: `Debug R24` cho Revit 2024) và tiến hành Build solution (F6).
2. **Tắt hẳn Revit** (điều này bắt buộc vì nếu Revit đang mở, các file DLL sẽ bị khóa và không thể chép đè).
3. Chạy lại file `install.bat` ở thư mục gốc của dự án. Script sẽ thực hiện việc ghi đè các file DLL mới vào thư mục Addins của Revit.
4. Mở lại Revit. Plugin mới nhất đã được áp dụng.

---

**Chúc bạn có trải nghiệm tự động hóa tuyệt vời với Revit MCP!**
