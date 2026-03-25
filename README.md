# Inventory KPI System

Ứng dụng console .NET 8 theo dõi file hóa đơn và đơn mua hàng theo thời gian thực, xử lý dữ liệu tăng dần và tính toán các chỉ số KPI tồn kho.

---

## Các KPI được tính

| # | KPI | Công thức |
|---|-----|-----------|
| 1 | **Total SKUs** | Số lượng Product ID duy nhất trong toàn bộ dữ liệu |
| 2 | **Cost of Inventory** | Tổng (Số lượng tồn x Giá vốn bình quân) theo từng SKU |
| 3 | **Out-of-Stock Items** | Số SKU có (Số mua - Số bán) <= 0 |
| 4 | **Average Daily Sales** | Tổng số lượng đã bán / (Ngày bán cuối - Ngày bán đầu + 1) |
| 5 | **Average Inventory Age** | Trung bình (Ngày mới nhất trong dataset - Ngày nhập hàng sớm nhất) của các SKU còn tồn |

> **Lưu ý KPI 5:** "Ngày hiện tại" được xác định là ngày invoice mới nhất trong dataset thay vì `DateTime.Now`, đảm bảo kết quả nhất quán dù chạy vào ngày nào.

---

## Yêu cầu hệ thống

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows / Linux / macOS

---

## Cài đặt & Chạy chương trình

**1. Clone repository**
```bash
git clone <repository-url>
cd InventoryKPI
```

**2. Đặt file dữ liệu**

Đặt các file invoice/purchase order định dạng `.txt` (nội dung JSON) vào:
```
InventoryKPI/Data/Invoices/          <- File hóa đơn (ACCREC + ACCPAY)
InventoryKPI/Data/PurchaseOrders/    <- File đơn mua hàng (tùy chọn)
```

**3. Build và chạy**
```bash
dotnet run --project InventoryKPI
```

Hoặc build trước rồi chạy binary:
```bash
dotnet build
cd InventoryKPI/bin/Debug/net8.0
./InventoryKPI
```

---

## Hướng dẫn sử dụng

Khi khởi động, hệ thống tự động nạp toàn bộ file có sẵn, hiển thị thanh tiến trình, sau đó in báo cáo KPI và menu điều hướng.

```
=== Inventory KPI System ===
Starting...

[INFO] Found 885 files to load (885 invoices + 0 purchase orders)
  [########################################] 100% (885/885)

[DONE] Loaded 885 files -- OK: 873  WARN: 12  ERROR: 0

========================================
  KPI REPORT -- 2026-03-25 14:15:53
========================================
  Total SKUs              : 1398
  Cost of Inventory       : $798,098.84
  Out-of-Stock Items      : 600
  Average Daily Sales     : 159.06 units/day
  Average Inventory Age   : 2,860.1 days
========================================

+-----------------------------+
|        MENU OPTIONS         |
+-----------------------------+
|  [1]  KPI Summary           |
|  [2]  Top 10 Out-of-Stock   |
|  [3]  Top 10 Highest Value  |
|  [R]  Recalculate           |
|  [Q]  Quit                  |
+-----------------------------+
```

| Phím | Chức năng |
|------|-----------|
| `1` | In báo cáo KPI tổng hợp |
| `2` | Hiển thị 10 SKU hết hàng nhiều nhất |
| `3` | Hiển thị 10 SKU có giá trị tồn kho cao nhất |
| `R` | Tính lại và in báo cáo KPI |
| `Q` | Thoát chương trình |

**File mới** được thả vào thư mục đang theo dõi sẽ được tự động phát hiện và xử lý mà không cần khởi động lại.

---

## Cấu trúc project

```
InventoryKPI/
├── Data/
│   ├── Invoices/               # Đầu vào: file hóa đơn (.txt, định dạng JSON)
│   ├── PurchaseOrders/         # Đầu vào: file đơn mua hàng (.txt, định dạng JSON)
│   └── product.txt             # Danh mục sản phẩm (tham khảo)
│
├── Models/
│   ├── Invoice.cs              # Model Invoice, LineItem, LineItemRef
│   ├── Product.cs              # Model Product, PriceDetail
│   └── KpiResult.cs            # Model kết quả KPI
│
├── Services/
│   ├── FileProcessor.cs        # Đọc và deserialize JSON
│   ├── FileWatcherService.cs   # Theo dõi thư mục, quản lý hàng đợi, retry
│   ├── KpiCalculator.cs        # Tính KPI tăng dần, thread-safe
│   └── KpiReportWriter.cs      # Ghi kết quả ra kpi_report.json
│
├── Program.cs                  # Entry point, menu tương tác
└── InventoryKPI.csproj
```

**File đầu ra:** `kpi_report.json` được ghi vào thư mục làm việc sau mỗi lần tính toán.

---

## Các quyết định thiết kế

### 1. Xử lý tăng dần (Incremental Processing)
KPI được duy trì dưới dạng các biến tổng hợp trong bộ nhớ (`KpiCalculator`). Mỗi file mới chỉ cộng thêm dữ liệu vào — không bao giờ xử lý lại file cũ. Điều này giữ cho bộ nhớ và CPU ổn định theo thời gian.

### 2. Producer-Consumer với Channel
`FileWatcherService` dùng `System.Threading.Channels` để tách biệt việc phát hiện file và xử lý file. `FileSystemWatcher` ghi đường dẫn file vào channel (producer), trong khi một background task đọc và xử lý tuần tự (consumer). Cách này tránh race condition và giữ thread chính luôn sẵn sàng.

### 3. Chống trùng lặp bằng Checksum
Mỗi file được nhận diện qua `tên file + MD5 checksum`. File đã xử lý được ghi vào `processed_files.json`. Nếu cùng một file được thả vào lần nữa (kể cả đổi tên), hệ thống sẽ bỏ qua. Registry này được giữ lại qua các lần khởi động lại.

### 4. Retry Logic
File bị lỗi khi đọc sẽ được thử lại tối đa 3 lần với độ trễ tăng dần (500ms, 1000ms, 1500ms). Xử lý trường hợp file vẫn đang được ghi khi được phát hiện lần đầu.

### 5. Thread Safety
Toàn bộ trạng thái KPI trong `KpiCalculator` được bảo vệ bằng `lock`. Nhiều background task có thể gọi `AddInvoices()` đồng thời mà không gây lỗi dữ liệu.

### 6. KPI 5 nhất quán (Reproducible)
Average Inventory Age dùng ngày invoice mới nhất trong dataset làm "ngày hiện tại" thay vì `DateTime.Now`. Đảm bảo kết quả không thay đổi dù chạy vào ngày khác nhau.

### 7. Mapping dữ liệu theo Xero API
File đầu vào theo schema Xero Accounting API:
- Invoice `ACCPAY` = đơn mua hàng (nhập hàng từ nhà cung cấp)
- Invoice `ACCREC` = hóa đơn bán hàng (bán hàng cho khách)
- Chỉ các line item có `Item.ItemID` hợp lệ mới được tính vào KPI, đảm bảo mỗi dòng ánh xạ đúng sang một SKU trong hệ thống.
