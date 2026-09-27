# Libra — Frontend kết nối qlthuvien_vip

Giao diện navy–cam đã tích hợp theo mã nguồn trong `qlthuvien_vip.zip` (bản backend mới). Bản backend giải nén nằm trong `backend-current`; cấu hình Development đã trỏ SQL Server localhost / LibraryStoreDB.

## Chạy với backend thật

1. Chạy file start-backend.ps1 bằng PowerShell trong thư mục dự án (máy cần .NET SDK và SQL Server).
2. Backend chạy tại http://localhost:5161; Swagger ở /swagger.
3. Chạy frontend bằng node server.cjs.
4. Mở http://127.0.0.1:4173 và dùng tài khoản trong database thật.

Cấu hình hiện tại trong `dist/config.js`:

```js
window.LIBRA_CONFIG = {
  useMock: false,
  apiBaseUrl: 'http://localhost:5161/api',
  onlinePaymentsEnabled: false
};
```

Nếu backend chạy cổng khác, sửa apiBaseUrl. Nếu phục vụ frontend cùng backend qua wwwroot, dùng apiBaseUrl `/api`. Cần backend phục vụ file tĩnh khi dùng wwwroot. Không mở index.html trực tiếp bằng file:// khi dùng API thật.

## Chức năng đã nối

- Đăng ký đúng DTO FullName; đăng ký xong chuyển về đăng nhập.
- Đăng nhập JWT; token lưu sessionStorage (riêng từng tab), gửi Bearer header; xử lý 401/403, lấy lại người dùng qua Auth/me.
- Bốn vai trò và khu vực tương ứng: customer, staff, manager, admin.
- Danh mục sách: tìm kiếm, lọc danh mục/giá, phân trang từ Products API. Ảnh lấy từ imageUrl nếu có; bìa CSS là phương án dự phòng.
- Giỏ hàng theo tài khoản: thêm/xóa/đổi số lượng bằng cartItemId; chọn sách cần mua. Tăng giảm số lượng/chọn sách không cuộn về đầu trang.
- Yêu thích và sổ địa chỉ đồng bộ với backend; thêm/sửa/xóa địa chỉ, chọn địa chỉ khi checkout.
- Tính tiền bằng Orders/preview-cost. Checkout gửi addressId, items được chọn, discountCode, paymentMethod.
- Đơn COD ở trạng thái chờ xác nhận; lịch sử, hủy đơn đủ điều kiện. Giữ lại sách chưa mua.
- Nhân viên/quản lý xử lý đơn theo các bước chuyển trạng thái BE cho phép. Không có nút tự đánh dấu giao dịch đã thanh toán.
- Quản lý sửa giá/tồn kho bằng PUT đầy đủ, giữ nguyên thông tin sách khác.
- Admin xem tài khoản, đổi role và khóa/mở bằng hai endpoint riêng.

## Thanh toán online

Mã FE đã có khởi tạo QR/VNPay, trang thanh toán riêng, mở liên kết do backend trả và kiểm tra trạng thái. Mặc định tắt bằng `onlinePaymentsEnabled: false` vì chưa xác minh cấu hình ngân hàng/VNPay thực tế. Chỉ bật sau khi BE đã cấu hình đúng tài khoản thụ hưởng, callback và đối soát. FE không gọi webhook hay API tạo chữ ký thử để giả lập thanh toán thành công.

## Kiểm tra đã thực hiện

- 23 bài kiểm tra tự động đạt: adapter API thật và luồng demo cũ.
- Trình duyệt chạy FE với API fixture độc lập đúng DTO BE: đăng nhập JWT, giỏ 2 sách, chọn một sách, tăng số lượng giữ nguyên vị trí cuộn, GIAM10, tạo địa chỉ, checkout COD và hiển thị đơn chờ xác nhận tổng 172.200đ.
- Fixture là `tests/ui-fixture.cjs`, chạy riêng cổng 4188; không kết nối database thật và không đổi cấu hình chính.
- Chưa kiểm thử đầu-cuối với SQL Server thật: thời điểm triển khai không có backend lắng nghe cổng 7193/5160. Cần chạy backend và database để nghiệm thu cuối cùng.

## Giới hạn của API hiện tại

Products API chưa hỗ trợ sắp xếp giá/đánh giá, lọc còn hàng hoặc giá khuyến mãi riêng. Trang danh mục dùng thứ tự mới nhất do BE trả, không giả vờ áp các bộ lọc này. Trang chủ và trang quản lý hiện tải các trang sản phẩm theo lô 100; với kho rất lớn nên bổ sung API tổng quan riêng.

Backend đang tính FREESHIP vừa đặt shippingFee=0 vừa trừ discountAmount bằng phí ship cũ. FE hiển thị nguyên kết quả BE và không tự sửa số tiền; cần phía BE xác nhận quy tắc này. Các tài khoản demo không tự tồn tại trong database thật.

## File cần bàn giao

Toàn bộ thư mục `dist`, `server.cjs`, `package.json`, README.md và BACKEND.md. Không cần các thư mục backend-review/backend-current để chạy FE.

- `api-live.js`: hợp đồng API thật, ánh xạ dữ liệu và JWT.
- `live.js`: luồng giao diện thật.
- `api.js`, `data.js`: chỉ dùng khi bật useMock để xem demo.
- `app.js`, `admin.js`, `styles.css`: giao diện dùng chung.

Kiểm tra: `npm run check`, `npm test` (hoặc `node --test tests/api.test.cjs tests/live-api.test.cjs`).

