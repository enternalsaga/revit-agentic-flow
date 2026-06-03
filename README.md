# Hướng dẫn Cài đặt, Sử dụng và Cập nhật Revit MCP

Tài liệu này hướng dẫn cách thiết lập và sử dụng bộ công cụ **Revit MCP** (Model Context Protocol) để kết nối các trợ lý AI (như Claude, Cursor, Cline) trực tiếp với Autodesk Revit.

---

## 1. Cài đặt (Installation)

### Yêu cầu hệ thống
- **Autodesk Revit** (Hỗ trợ bản 2024, 2025).
- **.NET 8.0 SDK** (Yêu cầu cài đặt để có thể biên dịch mã nguồn C# của MCP Server và Plugin).
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
    "revit-mcp": {
      "type": "stdio",
      "command": "C:\\Users\\baoanh.nguyen\\OneDrive - The Design Lab\\Work\\MCP_Revit\\src\\RevitMcpServer\\bin\\Release\\net8.0-windows\\win-x64\\publish\\RevitMcpServer.exe"
    }
  }
}
```
*(Nếu bạn đổi vị trí thư mục làm việc, hãy nhớ cập nhật lại đường dẫn tuyệt đối cho đúng).*

**Đối với Claude Code (CLI):**
Mở terminal và chạy lệnh:
```bash
claude mcp add revit-mcp -- "C:\Users\baoanh.nguyen\OneDrive - The Design Lab\Work\MCP_Revit\src\RevitMcpServer\bin\Release\net8.0-windows\win-x64\publish\RevitMcpServer.exe"
```

---

## 2. Sử dụng (Usage)

1. **Khởi động Revit:** Mở phiên bản Revit bạn vừa cài đặt plugin (vd: Revit 2024).
2. **Xác nhận Load Plugin:** Nếu Revit hiện cảnh báo về add-in không xác định (Unknown Publisher), hãy chọn **"Always Load"**.
3. **Cấu hình trong Revit:** Trên thanh Ribbon của Revit, tìm tab **Revit MCP**, bấm nút **Settings**. Tại đây, đảm bảo các chức năng/lệnh (tools) bạn muốn AI sử dụng đều đã được đánh dấu tích (enable), sau đó bấm **Save**.
4. **Sử dụng AI:** Mở AI Client (Claude/Cursor) đã được kết nối MCP ở bước trên. Bạn có thể bắt đầu ra lệnh cho AI bằng ngôn ngữ tự nhiên.
   - *Ví dụ 1:* "Hãy lấy thông tin các phòng trong model hiện tại."
   - *Ví dụ 2:* "Tạo một hệ lưới trục (grid) 5x5 khoảng cách 4000mm."
   - *Ví dụ 3:* "Thống kê số lượng tường và vật liệu trong dự án."

### 2.1 Quy trình Dựng hình Thực tế (Dành cho Người dùng - Common User)

Khi bạn muốn AI tự động dựng một công trình cụ thể (ví dụ: Nhà kho 45x48x10m), hãy làm theo các bước sau:

#### Bước 1: Chuẩn bị trong Revit
* Mở một dự án mới. Nên dùng Metric Template (như *Default Metric*) để đơn vị mặc định là **milimet (mm)**.
* Đảm bảo các Family cơ bản đã được load sẵn (cột thép H/I, loại tường gạch/tôn, cửa cuốn, loại mái...).

#### Bước 2: Gửi yêu cầu bằng ngôn ngữ tự nhiên
* Copy toàn bộ đề bài/mô tả hoặc đính kèm ảnh vẽ của bạn vào ô chat với AI.
* *Ví dụ:* `"Dựng cho tôi nhà kho kiểu 1: kích thước 45mx48mx10m, bước cột 8m, 3 gian 15m..."`

#### Bước 3: Duyệt kế hoạch (Review Plan)
* AI sẽ tự động phân tích đề bài và xuất ra một **Kế hoạch thực thi (Execution Plan)** dưới dạng danh sách việc cần làm (Tạo level -> Tạo Grid -> Dựng cột -> Vẽ tường...).
* **Hành động của bạn:** 
  * Gõ **`OK`** hoặc **`Tiến hành đi`** để đồng ý.
  * Hoặc gõ yêu cầu điều chỉnh nếu AI hiểu sai ý: `"Chỉnh lại chiều cao tường gạch thành 1.5m nhé"`.

#### Bước 4: Quan sát AI tự vẽ
* AI sẽ tự động gọi các API của Revit để dựng mô hình từng bước một. Bạn có thể nhìn thấy các cấu kiện xuất hiện trực tiếp trên màn hình Revit của mình.

#### Bước 5: Nghiệm thu và yêu cầu chỉnh sửa (Feedback to Fix)
* Sau khi vẽ xong, AI sẽ chụp ảnh màn hình Revit gửi vào chat để bạn nghiệm thu trực quan.
* Nếu phát hiện lỗi hoặc muốn đổi ý, bạn chỉ cần chat trực tiếp:
  * `"Cột ở trục X2 bị lệch, chỉnh lại giúp tôi."`
  * `"Đổi màu tôn mái sang màu xanh lá."`
* AI sẽ tự tìm phần tử đó trong Revit để cập nhật/sửa lỗi và chụp ảnh báo cáo lại.

---

### 2.2 Cơ chế Tự sửa lỗi và Nâng cấp của AI (Auto-fix & Self-Improvement)

Hệ thống MCP tích hợp cơ chế tự học để hạn chế phiền hà cho người dùng:

* **Tự động sửa lỗi khi vẽ (Auto-fix):** Nếu một lệnh vẽ bị lỗi (lệch tọa độ, thiếu tham số...), AI sẽ tự đọc mã lỗi từ Revit, tự chẩn đoán nguyên nhân (qua hệ thống Harness Classify) và tự thay đổi thông số để vẽ lại mà không cần hỏi bạn.
* **Tự đề xuất nâng cấp tính năng (Command Gap Propose):** Khi gặp cấu cấu phức tạp chưa có sẵn công cụ vẽ tự động (ví dụ: *Nóc gió*), AI sẽ tự động lập trình ra một đoạn code nâng cấp mới.
  * **Hành động của bạn:** AI sẽ hỏi: *"Tôi phát hiện thiếu công cụ vẽ X và đã tự viết code để bổ sung, bạn có đồng ý cài đặt không?"*. Bạn chỉ cần gõ **`Yes`** để đồng ý, AI sẽ tự động cài đặt và cập nhật hệ thống để lần sau không bao giờ mắc lại lỗi thiếu tính năng này.

---

## 3. Cập nhật (Update)

Trong quá trình phát triển, khi có cập nhật code C# (MCP server, plugin, hoặc commandset), bạn cần thực hiện theo các bước sau để làm mới hệ thống:

### Cập nhật MCP Server và Plugin (Phần C#)
Nếu mã nguồn C# (trong `src/RevitMcpServer/`, `src/RevitMcpPlugin/`, hoặc `src/RevitMcpCommandSet/`) được cập nhật:
1. **Tắt hẳn Revit** (điều này bắt buộc vì nếu Revit đang mở, các file DLL sẽ bị khóa và không thể chép đè).
2. Build MCP server, commandset, và deploy plugin:
   ```powershell
   dotnet build .\src\RevitMcpServer.sln -c Release
   dotnet publish .\src\RevitMcpServer\RevitMcpServer.csproj -c Release -r win-x64 --self-contained
   .\.scripts\deploy-phase1.ps1 -RevitVersion 2024
   ```
   (Thay `2024` bằng `2025` tùy phiên bản Revit bạn đang dùng.)
3. Khởi động lại AI Client (Claude/Cursor) để nó kết nối lại và nhận diện danh sách các tools mới.
4. Mở lại Revit. Plugin mới nhất đã được áp dụng.

---

## 4. Quy Trình Tự Động Hóa Với Trợ Lý AI (Agentic Workflows & Skills)

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

## 5. Revit Self-Improve Harness
 
Bộ harness là CLI chẩn đoán và kiểm thử cho Revit MCP. Khi bắt đầu làm việc hoặc sau khi mở lại Revit, chạy:

```batch
.\harness check
```

Các thao tác thường dùng:

| Mục đích | Lệnh |
|---|---|
| Kiểm tra kết nối Revit/plugin | `.\harness check` |
| Kiểm tra command coverage/drift | `.\harness registry` |
| Gọi command Revit an toàn qua JSON params | `.\harness invoke <tên_lệnh>` |
| Phân loại lỗi từ output | `.\harness classify <file>` |
| Phát hiện và đề xuất command còn thiếu | `.\harness gap <subcommand>` |
| Chạy bộ kiểm thử harness | `.\harness evals` hoặc `.\harness evals --live` |

Với trợ lý AI, dùng command **`/harness`** để tự động chạy workflow chẩn đoán. Chi tiết workflow nằm trong `.claude/commands/harness.md`; quy trình phát hiện command thiếu nằm trong skill reference `.claude/skills/run-revit-mcp/references/command-gap-workflow.md`.

---

**Chúc bạn có trải nghiệm tự động hóa tuyệt vời với Revit MCP!**
