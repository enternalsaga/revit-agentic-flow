# Hướng dẫn Cài đặt, Sử dụng và Cập nhật Revit MCP

Tài liệu này hướng dẫn cách thiết lập và sử dụng bộ công cụ **Revit MCP** (Model Context Protocol) để kết nối các trợ lý AI (như Claude, Cursor, Cline) trực tiếp với Autodesk Revit.

---

## 1. Cài đặt (Installation)

### Yêu cầu hệ thống
- **Autodesk Revit** (Hỗ trợ bản 2023, 2024, 2025, 2026 - ưu tiên 2024 theo thiết lập mặc định).
- **Node.js 18+** (Để chạy phần MCP Server bằng TypeScript).
- **.NET 8.0 SDK** (Yêu cầu cài đặt để có thể biên dịch mã nguồn C# của Plugin).
  - *Cách 1 (Nếu có quyền Admin)*: Tải và cài đặt file `.exe` từ [trang chủ Microsoft](https://dotnet.microsoft.com/download/dotnet/8.0).
  - *Cách 2 (Nếu KHÔNG có quyền Admin)*: Mở PowerShell và chạy lần lượt các lệnh sau để cài đặt cục bộ vào thư mục User Profile:
    ```powershell
    Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile "dotnet-install.ps1"
    .\dotnet-install.ps1 -Channel 8.0 -InstallDir "$env:USERPROFILE\.dotnet"
    Remove-Item -Path .\dotnet-install.ps1 -Force
    ```
    Sau đó, khi chạy build/install, cần đưa đường dẫn trên vào môi trường hiện tại bằng lệnh:
    ```powershell
    $env:Path = "$env:USERPROFILE\.dotnet;" + $env:Path
    ```

### Các bước cài đặt Plugin vào Revit
Dự án cung cấp sẵn hai file script tự động hóa:
- `build_and_install.bat`: Tự động biên dịch (build) mã nguồn C# và gọi script cài đặt (Khuyên dùng cho lần cài đặt đầu tiên hoặc sau khi sửa code C#).
- `install.bat`: Chỉ copy các file đã được biên dịch sẵn vào thư mục Addins của Revit.

**Cách thực hiện:**

1. Đảm bảo rằng bạn đã **đóng toàn bộ phiên làm việc của phần mềm Revit**.
2. Mở thư mục gốc của dự án `MCP_Revit`.
3. Chạy file `build_and_install.bat` bằng cách nhấp đúp hoặc chạy qua Terminal:
   ```powershell
   # Dành cho người dùng có quyền Admin (hoặc đã cài .NET SDK hệ thống)
   .\build_and_install.bat

   # Dành cho người dùng KHÔNG có quyền Admin (đã cài đặt .NET SDK cục bộ)
   $env:Path = "$env:USERPROFILE\.dotnet;" + $env:Path
   .\build_and_install.bat
   ```
4. Script sẽ hỏi phiên bản Revit bạn muốn cài (mặc định là `2024`). Nhập phiên bản tương ứng và nhấn Enter.
5. Script sẽ tự động chạy lệnh `dotnet build` để biên dịch project, sau đó gọi `install.bat` để copy file `.addin` và thư mục `revit_mcp_plugin` (bao gồm các file DLL) vào thư mục `%AppData%\Autodesk\Revit\Addins\<Phiên_bản>\` của bạn và Unblock các file DLL.

*(Lưu ý: Nếu bạn chạy script với quyền Administrator, plugin sẽ được cài cho toàn bộ người dùng trên máy tính tại thư mục `%ProgramData%` thay vì `%AppData%`).*

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
1. **Tắt hẳn Revit** (điều này bắt buộc vì nếu Revit đang mở, các file DLL sẽ bị khóa và không thể chép đè).
2. Bạn có hai cách để build và cài đặt lại plugin:
   - **Cách 1 (Nhanh nhất):** Chạy script `build_and_install.bat` ở thư mục gốc của dự án. Script này sẽ tự động chạy lệnh `dotnet build` để biên dịch rồi copy file vào thư mục Addins của Revit.
   - **Cách 2 (Visual Studio):** Mở file `mcp-servers-for-revit.sln` bằng Visual Studio. Chọn đúng cấu hình Build (Configuration) cho phiên bản Revit bạn đang dùng (ví dụ: `Debug R24` cho Revit 2024), tiến hành Build solution (F6), sau đó chạy `install.bat` để copy file.
3. Mở lại Revit. Plugin mới nhất đã được áp dụng.

---

## 4. Revit Self-Improve Harness
 
Bộ harness giúp bạn **chẩn đoán, kiểm thử, và cải tiến** hệ thống Revit MCP một cách có hệ thống. Thay vì phải tự debug kết nối, so sánh command bằng tay, hay ghi nhớ lỗi trong đầu — harness tự động hóa toàn bộ quy trình này qua một CLI hợp nhất.
 
### Harness giải quyết vấn đề gì?
 
| Vấn đề thường gặp | Lệnh CLI | Giải pháp |
|---|---|---|
| Không biết Revit plugin đã kết nối chưa | `.\harness check` | Kiểm tra transport và báo cáo trạng thái |
| Command có trong code nhưng runtime không thấy | `.\harness registry` | Phát hiện drift giữa 4 layer |
| Gọi lệnh Revit bị lỗi JSON do shell quoting | `.\harness invoke <tên_lệnh>` | Tự xử lý params qua file JSON |
| Lỗi lặp lại nhưng không ai nhớ pattern | `.\harness classify <file>` | Phân loại lỗi và gợi ý cách sửa |
| Thiếu command cho thao tác phổ biến | `.\harness gap <subcommand>` | Phát hiện, đề xuất và tạo code mẫu (scaffold) |
 
### Kiểm tra kết nối trước khi làm việc
 
Chạy lệnh này **mỗi khi mở Revit** để xác nhận mọi thứ sẵn sàng:
 
```batch
.\harness check
```
 
Kết quả cho bạn biết:
- ✅ Transport nào đang hoạt động (Named Pipe / JSON-RPC)
- 📊 Số lượng command ở từng layer (manifest, TypeScript, C#, commandset)
- ⚠️ Command nào bị drift (có trong source nhưng runtime thiếu)
- 💡 Hướng dẫn xử lý cụ thể cho session hiện tại
 
### Kiểm tra command coverage
 
Muốn biết command nào đã có đầy đủ ở cả 4 layer, command nào còn thiếu wrapper:
 
```batch
.\harness registry
```
 
Lệnh này so sánh: `command.json` ↔ TypeScript tools ↔ C# MCP wrappers ↔ Commandset implementations, và liệt kê chính xác chỗ nào còn gap.
 
### Gọi lệnh Revit an toàn
 
Thay vì tự viết JSON-RPC call và bị lỗi quoting, dùng CLI wrapper:
 
```batch
# Gọi đơn giản
.\harness invoke get_project_info
 
# Gọi với params phức tạp (truyền qua file)
.\harness invoke create_level --params-file .\params.json --timeout 60

```

Kết quả luôn trả về dạng chuẩn — có `success`, `durationMs`, `error.categoryHint` nếu lỗi — giúp bạn debug nhanh hơn.
 
### Phân loại lỗi
 
Khi command trả lỗi, chạy classifier để biết nguyên nhân thuộc loại nào:
 
```batch
.\harness classify .\error-output.json
```
 
Hệ thống phân loại thành các nhóm rõ ràng: `json_quoting` (lỗi quoting), `missing_family` (thiếu family type), `view_missing` (thiếu view), `command_not_registered` (command chưa đăng ký)... kèm gợi ý cách khắc phục.
 
### Chạy bộ kiểm thử (Evals)
 
Sau khi sửa code plugin hoặc thêm command mới, chạy eval để đảm bảo không gì bị hỏng:
 
```batch
# Kiểm thử offline (không cần mở Revit)
.\harness evals
 
# Kiểm thử đầy đủ (tự bỏ qua nếu Revit chưa mở)
.\harness evals --live
```
 
- **Offline evals**: kiểm tra bootstrap, registry, invoke-command, classifier, trace writer hoạt động đúng.
- **Live evals**: tạo thử level/grid, wall/floor, 3D view, chụp snapshot — chỉ chạy khi Revit đang mở.
 
### Đề xuất command mới (Command Gap Resolver)
 
Khi bạn phát hiện một thao tác Revit phổ biến nhưng chưa có command chuyên dụng, quy trình 3 bước:
 
**Bước 1 — Phát hiện gap:**
```batch
.\harness gap detect .revit-harness\runs\<run_id>\trace.json
```
 
**Bước 2 — Tạo proposal để review:**
```batch
.\harness gap propose .revit-harness\command-gaps\<gap_id>.json
```
 
Proposal chứa: tên command, input/output schema, file cần tạo, eval plan. Bạn review và duyệt trước khi tiếp.
 
**Bước 3 — Scaffold code (sau khi duyệt):**
```batch
# Xem trước sẽ tạo file gì (không chạm source)
.\harness gap scaffold .revit-harness\command-proposals\<proposal_id>.json --dry-run
 
# Tạo source files thật
.\harness gap scaffold .revit-harness\command-proposals\<proposal_id>.json
```
 
> ⚠️ Harness **không bao giờ** tự build, deploy, restart Revit, hay commit code. Mọi thay đổi source đều cần bạn review và duyệt.

---

## 5. Quy Trình Tự Động Hóa Với Trợ Lý AI (Agentic Workflows & Skills)

Để giúp bạn tối ưu hóa hiệu suất làm việc, dự án tích hợp sẵn bộ quy trình và kỹ năng tự động hóa chuyên biệt dành cho các trợ lý AI (như Claude Code, Cursor, Cline) khi tương tác với Revit.

### 🧭 Các Lệnh Điều Phối Nhanh (Workflows)
Khi giao tiếp với AI trong cửa sổ chat, bạn có thể sử dụng các lệnh sau để yêu cầu AI tự động thực hiện các tác vụ phức tạp:
* **`/start`**: Yêu cầu AI khởi động phiên làm việc. AI sẽ tự động chạy kiểm tra kết nối với Revit (Named Pipe / JSON-RPC) và nạp tri thức/quy tắc thiết kế của dự án.
* **`/harness`**: Yêu cầu AI tự động chạy các công cụ chẩn đoán sức khỏe hệ thống, kiểm tra lỗi và tự động phát hiện các câu lệnh Revit còn thiếu.

### 🛠️ Bộ Kỹ Năng Định Hướng Hành Vi (Skills)
Trợ lý AI sẽ tự động nhận diện và áp dụng các kỹ năng chuyên sâu được lưu trữ tại thư mục `.agents/skills/` để hỗ trợ bạn:
* **Dựng hình chuẩn xác (`run-revit-mcp`):** Khi bạn ra lệnh dựng hình, AI sẽ tự động tuân thủ quy trình 4 bước (*Chẩn đoán ➔ Lập kế hoạch ➔ Thực thi ➔ Xác thực*), ưu tiên sử dụng cấu kiện BIM gốc của Revit và bắt buộc chụp ảnh snapshot thực tế của dự án để bạn nghiệm thu trực quan.
* **Tạo câu lệnh mới (`revit-mcp-command`):** Hỗ trợ lập trình viên sinh mã nguồn mẫu cho các command Revit mới và tự động triển khai lên máy chủ MCP.

---

**Chúc bạn có trải nghiệm tự động hóa tuyệt vời với Revit MCP!**
