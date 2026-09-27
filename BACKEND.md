# Hợp đồng kết nối — backend ZIP mới

Tài liệu này thay thế hợp đồng dự kiến ở các bản demo trước. Base URL mặc định: https://localhost:7193/api.

## Định dạng và phiên

Mọi kết quả có `{success,message,data}`. FE giải bọc data, hiển thị message hoặc lỗi validation ASP.NET. Token lấy từ `data.token`, người dùng từ `data.userInfo`. Gửi `Authorization: Bearer <token>` cho API đã đăng nhập. Token lưu sessionStorage, không ghi mật khẩu.

| Nhóm | API FE đang gọi |
|---|---|
| Tài khoản | POST /Auth/register, POST /Auth/login, GET /Auth/me, POST /Auth/logout |
| Sách | GET /Products?search=&category=&maxPrice=&page=&pageSize=, GET /Products/{id}, PUT /Products/{id} |
| Giỏ | GET /Cart, POST /Cart/add, PUT /Cart/item/{cartItemId}, DELETE /Cart/item/{cartItemId} |
| Yêu thích | GET /Favorites, POST /Favorites/{productId}, DELETE /Favorites/{productId} |
| Địa chỉ | GET /Addresses, POST /Addresses, PUT /Addresses/{id}, DELETE /Addresses/{id} |
| Đơn | POST /Orders/preview-cost, POST /Orders/checkout, GET /Orders, GET /Orders/{id}, PUT /Orders/{id}/status, POST /Orders/{id}/cancel |
| Quản trị | GET /Users?page=&pageSize=, GET /Users/{id}, PUT /Users/{id}/role, PUT /Users/{id}/status |
| Thanh toán | POST /Payment/initiate, GET /Payment/status/{orderId} |

## Quy tắc quan trọng

- Products/Users trả data dạng PagedResult `{items,totalItems,page,pageSize,totalPages}`.
- Role từ BE: Customer, Staff, Manager, Admin; FE ánh xạ chữ thường.
- FE PUT sách giữ title/author/publisher/isbn/category/description/imageUrl/isActive, chỉ đổi price và stockQuantity theo form.
- FE gửi role và isActive bằng hai API riêng. Nếu lần hai lỗi, thông báo rõ quyền có thể đã lưu để người dùng kiểm tra lại.
- AddToCart gửi productId, quantity; backend tự xác định customerId từ JWT.
- Địa chỉ gửi recipientName, recipientPhone, province, district, ward, detailedAddress, isDefault.
- Checkout gửi addressId, paymentMethod, discountCode, items:[{productId,quantity}]. Không gửi giá/tổng tiền làm căn cứ thanh toán.
- Preview-cost là nguồn tổng tiền chính thức. Trước tạo đơn FE hỏi lại preview; nếu tổng đổi thì yêu cầu người dùng kiểm tra và xác nhận lại.
- COD không gọi Payment/initiate thêm vì Checkout đã xử lý PENDING_CONFIRMATION.
- Online: tạo đơn trước, khởi tạo thanh toán sau. Nếu khởi tạo thất bại, giữ mã đơn để thử tiếp, không tạo lại đơn. Không coi việc mở cổng thanh toán là đã trả tiền.
- FE không gọi generate-test-signature/webhook để hoàn tất giao dịch.

## Việc vận hành còn cần phía backend

Chạy SQL Server, nạp schema/dữ liệu, cấu hình chuỗi kết nối, HTTPS và CORS cho origin frontend. Thiết lập tài khoản có role phù hợp trong database; không dùng tài khoản mẫu FE làm bằng chứng đăng nhập thật.

Trước bật thanh toán online cần xác minh cấu hình VNPay/ngân hàng/callback. Cần rà soát API tạo chữ ký thử và quyền truy cập theo môi trường; FE không sử dụng API đó. Quy tắc FREESHIP trong mã nguồn hiện giảm cả shippingFee và discountAmount, cần xác nhận có đúng nghiệp vụ hay không.

Đã xác minh hợp đồng bằng kiểm thử tự động và trình duyệt fixture; chưa xác minh với backend/SQL thật do dịch vụ chưa chạy trên máy ở thời điểm triển khai.
