---
description: Khởi động AI — Nạp tri thức và kiểm tra kết nối MCP/Revit. Chạy 1 lần khi mở cửa sổ mới.
---

# Khởi động MCP Servers for Revit

Workflow này giúp AI nạp lại toàn bộ tri thức cần thiết trước khi làm việc.
User chạy `/start` khi mở cửa sổ chat mới.

## Bước 1: Đọc tri thức
// turbo-all
Đọc **đồng thời** tất cả các file sau bằng `view_file`:

1. `.agents/skills/revit-mcp-command/SKILL.md` — Hướng dẫn tạo command mới
2. `.agents/lesson_learned.md` — Các lỗi đã gặp, tránh lặp lại
3. `mcp-servers-for-revit/command.json` — Danh sách commands hiện có

## Bước 2: Kiểm tra kết nối Revit
Gọi tool MCP `say_hello` để test kết nối:

```
Gọi MCP tool: say_hello (message: "Connection test")
```

- Nếu OK → thông báo: "✅ Đã sẵn sàng. Revit đang kết nối qua MCP."
- Nếu lỗi → thông báo: "⚠️ Đã sẵn sàng. Revit chưa kết nối — hãy mở Revit và đảm bảo plugin MCP đã load."

## Bước 2b: Kiểm tra Revit Harness
 
Chạy:
 
```batch
.\harness check
```
 
Báo cáo ngắn:

- Transport khả dụng.
- Số command manifest/tool wrapper.
- Drift hoặc stale session nếu có.

## Bước 3: Liệt kê khả năng
Trả lời user ngắn gọn:
```
✅ AI đã sẵn sàng!
- Tools: [số] MCP tools có sẵn
- Revit: [trạng thái kết nối]
- Skills: revit-mcp-command (tạo tool mới)

Các nhóm chức năng:
📊 Query & Analysis — Truy vấn elements, parameters, views, warnings
🏗️ Create — Tạo walls, floors, doors, grids, levels, rooms
🏷️ Annotate — Tag walls/rooms, dimensions
🎨 Visualize — Color elements, operate (select/hide/isolate)
🔧 Advanced — Send C# code, model statistics, material quantities

Bạn có thể đặt yêu cầu ngay bây giờ.
```

**KHÔNG cần scan project, KHÔNG cần list_dir, KHÔNG cần đọc source code C#.**
