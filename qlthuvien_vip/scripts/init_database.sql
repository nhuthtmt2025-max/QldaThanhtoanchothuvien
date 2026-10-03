-- =========================================================================================
-- SCRIPT KHỞI TẠO HỆ THỐNG CƠ SỞ DỮ LIỆU SQL SERVER - BOOKSTORE MANAGEMENT SYSTEM
-- Hỗ trợ: Phân quyền (Admin, Manager, Staff, Customer), Tồn kho an toàn, Khóa tạm, E-Invoice, Yêu thích
-- =========================================================================================

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = N'LibraryStoreDB')
BEGIN
    CREATE DATABASE [LibraryStoreDB];
END
GO

USE [LibraryStoreDB];
GO

-- 1. BẢNG TÀI KHOẢN (CUSTOMERS / USERS)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'customers')
BEGIN
    CREATE TABLE [customers] (
        [customer_id] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [full_name] NVARCHAR(255) NOT NULL,
        [email] VARCHAR(255) NOT NULL UNIQUE,
        [phone_number] VARCHAR(20) NOT NULL DEFAULT '',
        [password_hash] VARCHAR(255) NOT NULL,
        [role] VARCHAR(50) NOT NULL DEFAULT 'Customer', -- Admin, Manager, Staff, Customer
        [is_active] BIT NOT NULL DEFAULT 1,
        [created_at] DATETIME NOT NULL DEFAULT GETDATE()
    );
END
GO

-- 2. BẢNG ĐỊA CHỈ GIAO HÀNG (CUSTOMER_ADDRESSES)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'customer_addresses')
BEGIN
    CREATE TABLE [customer_addresses] (
        [address_id] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [customer_id] BIGINT NOT NULL,
        [recipient_name] NVARCHAR(255) NOT NULL,
        [recipient_phone] VARCHAR(20) NOT NULL,
        [province] NVARCHAR(100) NOT NULL,
        [district] NVARCHAR(100) NOT NULL,
        [ward] NVARCHAR(100) NOT NULL,
        [detailed_address] NVARCHAR(500) NULL,
        [is_default] BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_addresses_customers FOREIGN KEY ([customer_id]) REFERENCES [customers]([customer_id]) ON DELETE CASCADE
    );
END
GO

-- 3. BẢNG SẢN PHẨM SÁCH (PRODUCTS)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'products')
BEGIN
    CREATE TABLE [products] (
        [product_id] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [title] NVARCHAR(255) NOT NULL,
        [author] NVARCHAR(255) NULL,
        [publisher] NVARCHAR(255) NULL,
        [isbn] VARCHAR(50) NULL,
        [category] NVARCHAR(100) NULL,
        [price] DECIMAL(15, 2) NOT NULL DEFAULT 0,
        [stock_quantity] INT NOT NULL DEFAULT 0,
        [hold_quantity] INT NOT NULL DEFAULT 0, -- Số lượng đang bị khóa tạm chờ thanh toán
        [description] NVARCHAR(MAX) NULL,
        [image_url] VARCHAR(500) NULL,
        [is_active] BIT NOT NULL DEFAULT 1,
        [created_at] DATETIME NOT NULL DEFAULT GETDATE(),
        [updated_at] DATETIME NOT NULL DEFAULT GETDATE()
    );
END
GO

-- 4. BẢNG GIỎ HÀNG (CARTS)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'carts')
BEGIN
    CREATE TABLE [carts] (
        [cart_id] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [customer_id] BIGINT NOT NULL UNIQUE,
        [updated_at] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_carts_customers FOREIGN KEY ([customer_id]) REFERENCES [customers]([customer_id]) ON DELETE CASCADE
    );
END
GO

-- 5. BẢNG MỤC SẢN PHẨM TRONG GIỎ (CART_ITEMS)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'cart_items')
BEGIN
    CREATE TABLE [cart_items] (
        [cart_item_id] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [cart_id] BIGINT NOT NULL,
        [product_id] BIGINT NOT NULL,
        [quantity] INT NOT NULL DEFAULT 1,
        [created_at] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_cartitems_carts FOREIGN KEY ([cart_id]) REFERENCES [carts]([cart_id]) ON DELETE CASCADE,
        CONSTRAINT FK_cartitems_products FOREIGN KEY ([product_id]) REFERENCES [products]([product_id]),
        CONSTRAINT UQ_cart_product UNIQUE ([cart_id], [product_id])
    );
END
GO

-- 6. BẢNG ĐƠN HÀNG (ORDERS)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'orders')
BEGIN
    CREATE TABLE [orders] (
        [order_id] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [customer_id] BIGINT NOT NULL,
        [recipient_name] NVARCHAR(255) NOT NULL,
        [recipient_phone] VARCHAR(20) NOT NULL,
        [shipping_address] NVARCHAR(MAX) NOT NULL,
        [subtotal] DECIMAL(15, 2) NULL DEFAULT 0,
        [shipping_fee] DECIMAL(15, 2) NULL DEFAULT 0,
        [discount_amount] DECIMAL(15, 2) NULL DEFAULT 0,
        [discount_code] VARCHAR(50) NULL,
        [total_amount] DECIMAL(15, 2) NOT NULL DEFAULT 0,
        [payment_method] VARCHAR(50) NOT NULL DEFAULT 'COD',
        [order_status] VARCHAR(50) NOT NULL DEFAULT 'PENDING_PAYMENT',
        [created_at] DATETIME NOT NULL DEFAULT GETDATE(),
        [updated_at] DATETIME NOT NULL DEFAULT GETDATE(),
        [expires_at] DATETIME NULL,
        CONSTRAINT FK_orders_customers FOREIGN KEY ([customer_id]) REFERENCES [customers]([customer_id])
    );
END
GO

-- 7. BẢNG CHI TIẾT ĐƠN HÀNG (ORDER_ITEMS)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'order_items')
BEGIN
    CREATE TABLE [order_items] (
        [order_item_id] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [order_id] BIGINT NOT NULL,
        [product_id] BIGINT NOT NULL,
        [unit_price] DECIMAL(15, 2) NOT NULL,
        [quantity] INT NOT NULL,
        [subtotal] DECIMAL(15, 2) NOT NULL,
        CONSTRAINT FK_orderitems_orders FOREIGN KEY ([order_id]) REFERENCES [orders]([order_id]) ON DELETE CASCADE,
        CONSTRAINT FK_orderitems_products FOREIGN KEY ([product_id]) REFERENCES [products]([product_id])
    );
END
GO

-- 8. BẢNG GIAO DỊCH THANH TOÁN (PAYMENT_TRANSACTIONS)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'payment_transactions')
BEGIN
    CREATE TABLE [payment_transactions] (
        [payment_id] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [order_id] BIGINT NOT NULL,
        [payment_method] VARCHAR(50) NOT NULL, -- COD, QR_TRANSFER, ONLINE, VNPAY
        [amount] DECIMAL(15, 2) NOT NULL,
        [payment_status] VARCHAR(50) NOT NULL DEFAULT 'PENDING',
        [transaction_code] VARCHAR(100) NULL,
        [response_code] VARCHAR(50) NULL,
        [error_message] NVARCHAR(500) NULL,
        [created_at] DATETIME NOT NULL DEFAULT GETDATE(),
        [completed_at] DATETIME NULL,
        CONSTRAINT FK_payments_orders FOREIGN KEY ([order_id]) REFERENCES [orders]([order_id])
    );
END
GO

-- 9. BẢNG HÓA ĐƠN ĐIỆN TỬ (INVOICES)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'invoices')
BEGIN
    CREATE TABLE [invoices] (
        [invoice_id] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [order_id] BIGINT NOT NULL UNIQUE,
        [payment_id] BIGINT NOT NULL,
        [invoice_number] VARCHAR(100) NOT NULL UNIQUE,
        [total_amount] DECIMAL(15, 2) NOT NULL,
        [tax_amount] DECIMAL(15, 2) NOT NULL DEFAULT 0,
        [status] VARCHAR(50) NOT NULL DEFAULT 'ISSUED',
        [pdf_url] VARCHAR(500) NULL,
        [invoice_date] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_invoices_orders FOREIGN KEY ([order_id]) REFERENCES [orders]([order_id]),
        CONSTRAINT FK_invoices_payments FOREIGN KEY ([payment_id]) REFERENCES [payment_transactions]([payment_id])
    );
END
GO

-- 10. BẢNG SÁCH YÊU THÍCH (FAVORITES / WISHLIST)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'favorites')
BEGIN
    CREATE TABLE [favorites] (
        [favorite_id] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [customer_id] BIGINT NOT NULL,
        [product_id] BIGINT NOT NULL,
        [created_at] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_favorites_customers FOREIGN KEY ([customer_id]) REFERENCES [customers]([customer_id]) ON DELETE CASCADE,
        CONSTRAINT FK_favorites_products FOREIGN KEY ([product_id]) REFERENCES [products]([product_id]) ON DELETE CASCADE,
        CONSTRAINT UQ_favorites_customer_product UNIQUE ([customer_id], [product_id])
    );
END
GO

-- 11. BẢNG THÔNG BÁO (NOTIFICATIONS)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'notifications')
BEGIN
    CREATE TABLE [notifications] (
        [notification_id] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [customer_id] BIGINT NULL,
        [title] NVARCHAR(255) NOT NULL,
        [message] NVARCHAR(MAX) NOT NULL,
        [type] VARCHAR(50) NULL, -- 'order', 'product', 'payment', etc.
        [is_read] BIT NOT NULL DEFAULT 0,
        [created_at] DATETIME NOT NULL DEFAULT GETDATE(),
        [read_at] DATETIME NULL,
        CONSTRAINT FK_notifications_customers FOREIGN KEY ([customer_id]) REFERENCES [customers]([customer_id]) ON DELETE CASCADE
    );
END
GO

-- =========================================================================================
-- DỮ LIỆU MẪU BAN ĐẦU (SEED DATA)
-- Mật khẩu mặc định của tất cả tài khoản mẫu: 123456
-- BCrypt Hash của 123456: $2a$11$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi
-- =========================================================================================

-- Seed Accounts: Admin, Manager, Staff, Customer
IF NOT EXISTS (SELECT * FROM [customers] WHERE [email] = 'admin@bookstore.vn')
BEGIN
    INSERT INTO [customers] ([full_name], [email], [phone_number], [password_hash], [role], [is_active], [created_at])
    VALUES (N'Quản trị viên Hệ thống', 'admin@bookstore.vn', '0901234567', '$2a$11$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi', 'Admin', 1, GETDATE());
END

IF NOT EXISTS (SELECT * FROM [customers] WHERE [email] = 'manager@bookstore.vn')
BEGIN
    INSERT INTO [customers] ([full_name], [email], [phone_number], [password_hash], [role], [is_active], [created_at])
    VALUES (N'Quản lý Cửa hàng', 'manager@bookstore.vn', '0902345678', '$2a$11$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi', 'Manager', 1, GETDATE());
END

IF NOT EXISTS (SELECT * FROM [customers] WHERE [email] = 'staff@bookstore.vn')
BEGIN
    INSERT INTO [customers] ([full_name], [email], [phone_number], [password_hash], [role], [is_active], [created_at])
    VALUES (N'Nhân viên Vận hành', 'staff@bookstore.vn', '0903456789', '$2a$11$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi', 'Staff', 1, GETDATE());
END

IF NOT EXISTS (SELECT * FROM [customers] WHERE [email] = 'customer@bookstore.vn')
BEGIN
    INSERT INTO [customers] ([full_name], [email], [phone_number], [password_hash], [role], [is_active], [created_at])
    VALUES (N'Nguyễn Văn Khách Hàng', 'customer@bookstore.vn', '0904567890', '$2a$11$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi', 'Customer', 1, GETDATE());
END

-- Seed Products
IF NOT EXISTS (SELECT * FROM [products] WHERE [isbn] = '978-604-1-00001-1')
BEGIN
    INSERT INTO [products] ([title], [author], [publisher], [isbn], [category], [price], [stock_quantity], [hold_quantity], [description], [image_url], [is_active], [created_at], [updated_at])
    VALUES 
    (N'Nhà Giả Kim (The Alchemist)', N'Paulo Coelho', N'NXB Hội Nhà Văn', '978-604-1-00001-1', N'Văn học', 79000, 50, 0, N'Cuốn sách kinh điển về hành trình theo đuổi ước mơ và lắng nghe tiếng gọi trái tim.', 'https://images.unsplash.com/photo-1544716278-ca5e3f4abd8c?w=500', 1, GETDATE(), GETDATE()),
    (N'Đắc Nhân Tâm', N'Dale Carnegie', N'NXB Trẻ', '978-604-1-00002-2', N'Kỹ năng sống', 86000, 80, 0, N'Nghệ thuật thu phục lòng người và xây dựng các mối quan hệ bền vững.', 'https://images.unsplash.com/photo-1512820790803-83ca734da794?w=500', 1, GETDATE(), GETDATE()),
    (N'Clean Code - Mã Sạch', N'Robert C. Martin', N'NXB Thông Tin & Truyền Thông', '978-604-1-00003-3', N'Công nghệ thông tin', 245000, 30, 0, N'Cẩm nang kinh điển cho mọi lập trình viên muốn viết code chuyên nghiệp, dễ bảo trì.', 'https://images.unsplash.com/photo-1532012164546-f432f2e3777f?w=500', 1, GETDATE(), GETDATE()),
    (N'Cha Giàu Cha Nghèo (Tập 1)', N'Robert Kiyosaki', N'NXB Trẻ', '978-604-1-00004-4', N'Kinh tế', 110000, 45, 0, N'Để không có tiền vẫn tạo ra tiền - bài học tài chính từ hai người cha.', 'https://images.unsplash.com/photo-1589829085413-56de8ae18c73?w=500', 1, GETDATE(), GETDATE());
END
GO
