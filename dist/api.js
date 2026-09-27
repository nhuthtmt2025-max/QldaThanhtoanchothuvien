(function () {
  const config = window.LIBRA_CONFIG;
  function mockOrders() {
    try {
      const value = JSON.parse(localStorage.getItem("libra.orders") || "[]");
      return Array.isArray(value) ? value : [];
    } catch {
      return [];
    }
  }
  const accounts = [
    {
      id: "demo",
      name: "Minh Anh",
      email: "demo@libra.vn",
      role: "customer",
      password: "Libra@123",
    },
    {
      id: "manager",
      name: "Quản lý Libra",
      email: "manager@libra.vn",
      role: "manager",
      password: "Manager@123",
    },
    {
      id: "staff",
      name: "Nhân viên Libra",
      email: "staff@libra.vn",
      role: "staff",
      password: "Staff@123",
    },
    {
      id: "admin",
      name: "Quản trị Libra",
      email: "admin@libra.vn",
      role: "admin",
      password: "Admin@123",
    },
  ];
  function profiles() {
    let edits = {};
    try {
      edits = JSON.parse(localStorage.getItem("libra.accountEdits") || "{}");
    } catch {}
    return accounts.map((a) => ({ ...a, ...edits[a.id] }));
  }
  function requireRole(roles) {
    if (!roles.includes(session()?.role))
      throw new Error("Bạn không có quyền thực hiện thao tác này.");
  }
  function session() {
    try {
      return profiles().find(
        (a) =>
          a.active !== false &&
          a.id === JSON.parse(localStorage.getItem("libra.session") || "null"),
      );
    } catch {
      return null;
    }
  }
  function requireAdmin() {
    if (session()?.role !== "admin")
      throw new Error("Bạn không có quyền quản trị.");
  }
  function mockBooks() {
    try {
      const edits = JSON.parse(localStorage.getItem("libra.bookEdits") || "{}");
      return window.LIBRA_BOOKS.map((b) => ({ ...b, ...edits[b.id] }));
    } catch {
      return window.LIBRA_BOOKS;
    }
  }
  async function request(path, options = {}) {
    const response = await fetch(config.apiBaseUrl.replace(/\/$/, "") + path, {
      credentials: "include",
      ...options,
      headers: { "Content-Type": "application/json", ...options.headers },
    });
    if (!response.ok) {
      const body = await response.json().catch(() => ({}));
      throw new Error(
        body.message ||
          `Yêu cầu thất bại (${response.status}). Vui lòng thử lại.`,
      );
    }
    return response.status === 204 ? null : response.json();
  }
  window.LibraAPI = {
    books: () =>
      config.useMock ? Promise.resolve(mockBooks()) : request("/books"),
    login: async (data) => {
      if (!config.useMock)
        return request("/auth/login", {
          method: "POST",
          body: JSON.stringify(data),
        });
      const account = profiles().find(
        (a) =>
          a.active !== false &&
          a.email === data.email.trim().toLowerCase() &&
          a.password === data.password,
      );
      if (!account) throw new Error("Email hoặc mật khẩu chưa đúng.");
      localStorage.setItem("libra.session", JSON.stringify(account.id));
      const { password, ...profile } = account;
      return profile;
    },
    register: async (data) => {
      if (!config.useMock)
        return request("/auth/register", {
          method: "POST",
          body: JSON.stringify(data),
        });
      throw new Error(
        "Bản demo chưa tạo tài khoản mới. Hãy đăng nhập bằng demo@libra.vn / Libra@123.",
      );
    },
    me: async () => {
      if (!config.useMock) return request("/auth/me");
      const account = session();
      if (!account) return null;
      const { password, ...profile } = account;
      return profile;
    },
    logout: async () => {
      if (!config.useMock) return request("/auth/logout", { method: "POST" });
      localStorage.setItem("libra.session", "null");
    },
    workOrders: async () => {
      if (!config.useMock) return request("/operations/orders");
      requireRole(["admin", "manager", "staff"]);
      return mockOrders();
    },
    accounts: async () => {
      if (!config.useMock) return request("/admin/accounts");
      requireAdmin();
      return profiles().map(({ password, ...profile }) => ({
        ...profile,
        active: profile.active !== false,
      }));
    },
    updateAccount: async (id, values) => {
      if (!config.useMock)
        return request("/admin/accounts/" + encodeURIComponent(id), {
          method: "PATCH",
          body: JSON.stringify(values),
        });
      requireAdmin();
      if (id === session().id)
        throw new Error(
          "Không thể đổi quyền hoặc khóa chính tài khoản đang đăng nhập.",
        );
      if (
        !accounts.some((a) => a.id === id) ||
        !["customer", "staff", "manager", "admin"].includes(values.role) ||
        typeof values.active !== "boolean"
      )
        throw new Error("Thông tin tài khoản không hợp lệ.");
      let edits = {};
      try {
        edits = JSON.parse(localStorage.getItem("libra.accountEdits") || "{}");
      } catch {}
      edits[id] = { role: values.role, active: values.active };
      localStorage.setItem("libra.accountEdits", JSON.stringify(edits));
    },
    adminOrders: async () => {
      if (!config.useMock) return request("/admin/orders");
      requireAdmin();
      return mockOrders();
    },
    updateBook: async (id, values) => {
      if (!config.useMock)
        return request("/management/books/" + id, {
          method: "PATCH",
          body: JSON.stringify(values),
        });
      requireRole(["admin", "manager"]);
      if (
        !Number.isInteger(values.stock) ||
        values.stock < 0 ||
        !Number.isFinite(values.price) ||
        values.price <= 0
      )
        throw new Error("Giá hoặc tồn kho không hợp lệ.");
      const edits = JSON.parse(localStorage.getItem("libra.bookEdits") || "{}");
      edits[id] = { price: values.price, stock: values.stock };
      localStorage.setItem("libra.bookEdits", JSON.stringify(edits));
    },
    updateOrder: async (id, status) => {
      if (!config.useMock)
        return request("/operations/orders/" + encodeURIComponent(id), {
          method: "PATCH",
          body: JSON.stringify({ status }),
        });
      requireRole(["admin", "manager", "staff"]);
      if (!["Đã tiếp nhận", "Đang giao", "Hoàn tất", "Đã hủy"].includes(status))
        throw new Error("Trạng thái không hợp lệ.");
      const orders = mockOrders();
      const order = orders.find((o) => o.id === id);
      if (!order) throw new Error("Không tìm thấy đơn.");
      order.status = status;
      localStorage.setItem("libra.orders", JSON.stringify(orders));
    },
    orders: () =>
      config.useMock
        ? Promise.resolve(
            mockOrders().filter((o) => session() && o.userId === session().id),
          )
        : request("/orders"),
    createOrder: async (data) => {
      if (!config.useMock)
        return request("/orders", {
          method: "POST",
          body: JSON.stringify(data),
        });
      if (!Array.isArray(data.items) || !data.items.length)
        throw new Error("Giỏ hàng đang trống.");
      const items = data.items.map((item) => {
        const book = mockBooks().find((book) => book.id === item.bookId);
        if (
          !book ||
          !Number.isInteger(item.quantity) ||
          item.quantity < 1 ||
          item.quantity > book.stock
        )
          throw new Error("Số lượng sách không hợp lệ.");
        return {
          bookId: book.id,
          title: book.title,
          price: book.price,
          quantity: item.quantity,
        };
      });
      const subtotal = items.reduce(
        (sum, item) => sum + item.price * item.quantity,
        0,
      );
      const discount =
        data.coupon === "LIBRA10"
          ? Math.min(Math.round(subtotal * 0.1), 50000)
          : 0;
      const shipping = subtotal >= 299000 ? 0 : 25000;
      const order = {
        userId: session()?.id || "guest",
        id: "LB-" + Date.now(),
        createdAt: new Date().toISOString(),
        status: "Đã tiếp nhận (demo)",
        items,
        subtotal,
        discount,
        shipping,
        total: subtotal - discount + shipping,
        paymentMethod: data.paymentMethod,
      };
      const orders = mockOrders();
      localStorage.setItem("libra.orders", JSON.stringify([order, ...orders]));
      return order;
    },
  };
})();
