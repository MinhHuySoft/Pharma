# Pharma

Bản dựng local của [Trí Bình Pharma](https://tribinhpharma.com/) bằng Blazor Web App (.NET 10, Interactive Server). Project `Pharma` nằm cạnh `Pharmas`.

## Chạy trên máy local

Mở PowerShell trong thư mục chứa `Pharma.csproj`. Máy cần .NET 10 SDK, Python, SQL Server và `sqlcmd`.

```powershell
python -m pip install -r scripts/requirements.txt
python scripts/sync_catalog.py
dotnet run --launch-profile http
```

Mở [Pharma trên localhost](http://localhost:5169/). Lệnh import đọc snapshot và ảnh đã lưu trong project. Kết nối mặc định là SQL Server `lpc:.` qua Windows Authentication. Tài khoản Windows cần quyền tạo database `Pharma` ở lần đầu và quyền ghi các bảng danh mục khi nhập lại.

Nếu dùng SQL Server LocalDB, cấu hình script và ứng dụng trỏ tới cùng instance.

```powershell
$pharmaServer = '(localdb)\MSSQLLocalDB'
python scripts/sync_catalog.py --server $pharmaServer
$env:ConnectionStrings__Pharma = "Server=$pharmaServer;Database=Pharma;Integrated Security=true;TrustServerCertificate=true"
dotnet run --launch-profile http
```

## Crawl và database-first

```powershell
python scripts/sync_catalog.py --crawl
```

Crawler đọc sitemap và HTML công khai theo [robots.txt](https://tribinhpharma.com/robots.txt). Dùng `--crawl-only` để cập nhật snapshot và ảnh trước khi import. URL, ngày đăng và thông tin tác giả được lưu cùng nội dung. HTML đã được lọc bỏ mã thực thi, widget và thuộc tính trình bày của WordPress, rồi chuyển liên kết nội bộ đã biết sang URL tương ứng của bản local.

| Dữ liệu | Số bản ghi |
| --- | --- |
| Sản phẩm có mô tả đầy đủ | 23 |
| Danh mục chính | 5 |
| Nhóm theo nhu cầu và từ khóa | 30 |
| Vị trí ảnh trong gallery sản phẩm | 118 |
| Blog tư vấn | 62 |
| Tin tức và sự kiện | 2 |
| Chính sách | 3 |
| Trang tĩnh Giới thiệu và Liên hệ | 2 |
| Tài nguyên trang chủ | 14 |

Ảnh WebP được phục vụ từ `wwwroot/source-media`. [Snapshot](Database/catalog.json) ghi thời điểm thu thập và nguồn từng mục. Giá và trạng thái còn hàng phản ánh thời điểm đó. Các nhóm theo nhu cầu được dựng từ nhãn sản phẩm, kể cả nhóm có URL lưu trong sitemap nhưng trang nguồn trả về 404.

[01-schema.sql](Database/01-schema.sql) tạo hoặc mở rộng schema SQL Server. [02-import.sql](Database/02-import.sql) cập nhật catalog, nội dung và ảnh trong một transaction. Dữ liệu đơn hàng, tài khoản và biểu mẫu được lưu ở các bảng riêng. [PharmaContext](Data/PharmaContext.cs) và [model](Data/Models/) được scaffold từ database đã tạo.

Sau khi thay đổi schema, chạy lại lệnh scaffold từ thư mục project.

```powershell
dotnet ef dbcontext scaffold 'Server=lpc:.;Database=Pharma;Integrated Security=true;TrustServerCertificate=true' Microsoft.EntityFrameworkCore.SqlServer --output-dir Data/Models --context-dir Data --context PharmaContext --no-onconfiguring --no-pluralize --force
```

## Các luồng local

- **Giới thiệu và bài viết** có bố cục nội dung chính, mục lục thu gọn và cột Bài viết mới. Blog hỗ trợ tìm kiếm, phân trang và URL bài viết theo slug gốc.
- **Sản phẩm** có bộ ảnh, mô tả đầy đủ, quy cách, giá, tình trạng còn hàng, thẻ nhóm và bài viết liên quan. Shop hỗ trợ lọc và sắp xếp giá.
- **Giỏ hàng và đặt hàng** giữ giỏ trong trình duyệt, kiểm tra lại giá và tình trạng hàng khi chốt, rồi ghi đơn và các dòng đơn vào SQL Server. Người dùng có thể mua dưới dạng khách hoặc đăng nhập để xem lịch sử đơn.
- **Tài khoản** tạo mới và đăng nhập bằng cookie trong bản local. Mật khẩu được lưu dưới dạng hash. Trang hỗ trợ quên mật khẩu hướng người dùng đến quản trị viên local.
- **Liên hệ và đăng ký nhận tin** ghi các biểu mẫu vào SQL Server. Hotline, bản đồ và Zalo mở khi người dùng chọn liên kết.

Bản dựng phục vụ xem và thử luồng local. Đơn hàng được lưu tại máy, còn thanh toán online, giao hàng và gửi email phụ thuộc vào dịch vụ triển khai. Thương hiệu, ảnh và nội dung lấy từ trang nguồn, cần quyền sử dụng phù hợp khi phát hành.

## Đối chiếu

Nguồn thuộc mức L2 theo skill web-clone. Frontend công khai dùng WordPress, WooCommerce, Elementor và chủ đề Pure Gennex. Phần dựng lại giữ menu, thứ tự các khối và ảnh nguồn. Giao diện dùng tông xanh đậm, thẻ có độ tương phản rõ hơn, phần đọc chia cột desktop và xếp dọc trên điện thoại. Hiệu ứng CSS tôn trọng `prefers-reduced-motion`.

| Màn hình | Trang gốc | Pharma |
| --- | --- | --- |
| Trang chủ 1440 px | [Ảnh](RECON/screenshots/original-1440.png) | [Ảnh](RECON/screenshots/clone-1440.png) |
| Trang chủ 768 px | [Ảnh](RECON/screenshots/original-768.png) | [Ảnh](RECON/screenshots/clone-768.png) |
| Trang chủ 390 px | [Ảnh](RECON/screenshots/original-390.png) | [Ảnh](RECON/screenshots/clone-390.png) |
| Giới thiệu 1440 px | [Ảnh](RECON/screenshots/original-about-1440.png) | [Ảnh](RECON/screenshots/clone-about-1440.png) |
| Giới thiệu 390 px | [Ảnh](RECON/screenshots/original-about-390.png) | [Ảnh](RECON/screenshots/clone-about-390.png) |

Đã kiểm tra HTTP trên 138 URL local. Trình duyệt kiểm tra trang chủ, Giới thiệu, blog và sản phẩm ở 1440, 768 và 390 px, rồi chạy đăng ký, đăng nhập, tìm kiếm, phân trang, giỏ, đơn hàng và biểu mẫu. Các bản ghi thử nghiệm đã được đối chiếu và dọn khỏi SQL Server.

[Giỏ hàng](RECON/screenshots/clone-cart.png) · [Đặt hàng](RECON/screenshots/clone-checkout.png) · [Kết quả kiểm tra](RECON/browser-verification.json)

Điểm đối chiếu theo thang 5 của skill dựa trên HTML, ảnh chụp và luồng đã chạy.

| Hạng mục | Điểm | Căn cứ |
| --- | --- | --- |
| Bằng chứng nguồn | 4 | HTML, sitemap và ảnh trình duyệt |
| Cấu trúc | 5 | Trang chính, chính sách, archive, nội dung chi tiết và luồng cửa hàng local |
| Hình ảnh | 4 | Ảnh nguồn với bố cục, tông màu và kiểu thẻ đã chỉnh |
| Tương tác | 4 | Luồng duyệt và đặt hàng thử nghiệm hoạt động. Tích hợp dịch vụ triển khai là phần riêng |
| Responsive | 5 | Trang đại diện đã kiểm tra ở ba chiều rộng |

