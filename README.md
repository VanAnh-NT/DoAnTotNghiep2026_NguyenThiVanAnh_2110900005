# VAN ANH PERFUME

Website quản lý và bán hàng nước hoa trực tuyến **VAN ANH PERFUME**.

Dự án được xây dựng nhằm hỗ trợ khách hàng xem sản phẩm, tìm kiếm sản phẩm, xem chi tiết sản phẩm, thêm sản phẩm vào giỏ hàng, đặt hàng và theo dõi đơn hàng. Ngoài ra, hệ thống có khu vực quản trị giúp Admin quản lý sản phẩm, danh mục, thương hiệu, biến thể, hình ảnh, tồn kho, đơn hàng, người dùng và các nội dung liên quan.

---

## 1. Thông tin dự án

- Tên dự án: **VAN ANH PERFUME**
- Loại dự án: Website bán hàng nước hoa
- Mục đích: Đồ án tốt nghiệp
- Cơ sở dữ liệu: SQL Server
- Kiến trúc: ASP.NET Core MVC

---

## 2. Chức năng chính

### 2.1. Phía khách hàng

- Xem trang chủ
- Xem danh sách sản phẩm
- Tìm kiếm sản phẩm
- Xem chi tiết sản phẩm
- Thêm sản phẩm vào giỏ hàng
- Cập nhật số lượng sản phẩm trong giỏ hàng
- Đặt hàng
- Đăng ký tài khoản
- Đăng nhập tài khoản
- Quản lý thông tin cá nhân
- Xem lịch sử đơn hàng
- Thanh toán qua VNPAY Sandbox/Test

### 2.2. Phía quản trị viên

- Đăng nhập trang quản trị
- Quản lý sản phẩm
- Quản lý danh mục
- Quản lý thương hiệu
- Quản lý biến thể sản phẩm
- Quản lý hình ảnh sản phẩm
- Quản lý tồn kho
- Quản lý đơn hàng
- Quản lý người dùng
- Quản lý bài viết hoặc tin tức
- Quản lý banner hoặc khuyến mãi

---

## 3. Công nghệ sử dụng

- ASP.NET Core MVC
- Entity Framework Core
- SQL Server
- HTML
- CSS
- JavaScript
- Bootstrap
- Cookie Authentication
- Session
- Gmail SMTP
- VNPAY Sandbox/Test
- Git và GitHub

---

## 4. Cấu trúc thư mục
ProjectCode/
├── VanAnhPrefume/
│   └── VanAnhPrefume/
│       ├── Areas/
│       ├── Controllers/
│       ├── Data/
│       ├── Filters/
│       ├── Helpers/
│       ├── Models/
│       ├── Properties/
│       ├── Repositories/
│       ├── Services/
│       ├── Views/
│       ├── wwwroot/
│       ├── appsettings.json
│       ├── appsettings.example.json
│       ├── Program.cs
│       └── VanAnhPerfume.csproj
│
├── Database/
│   ├── VANANHPERFUME_DBFN.sql
│   └── HuongDanImportDatabase.txt
│
├── Documents/
│   ├── HUONG_DAN_CAI_DAT_VA_CHAY_WEBSITE.md
│   ├── HuongDanCauHinhConnectionString.txt
│   ├── TaiKhoanDemo.txt
│   └── GhiChuBaoMat.txt
│
├── README.md
└── .gitignore