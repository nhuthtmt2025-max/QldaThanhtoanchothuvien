const main = document.querySelector('#main');
const homeTemplate = document.querySelector('#home-template');
document.querySelector('.skip').addEventListener('click', event => {
  event.preventDefault();
  main.focus();
  main.scrollIntoView();
});

function field(id, label, type, autocomplete, hint = '') {
  const password = type === 'password';
  return `<div class="field"><label for="${id}">${label}</label>${password ? '<div class="password-wrap">' : ''}<input id="${id}" name="${id}" type="${type}" autocomplete="${autocomplete}" required ${password ? 'minlength="8" maxlength="128"' : 'maxlength="120"'} ${hint ? `aria-describedby="${id}-hint"` : ''}>${password ? `<button class="toggle-password" type="button" aria-label="Hiện ${label.toLowerCase()}" aria-pressed="false" data-for="${id}">Hiện</button></div>` : ''}${hint ? `<p class="hint" id="${id}-hint">${hint}</p>` : ''}</div>`;
}

function render() {
  const route = location.hash || '#/';
  const register = route === '#/dang-ky';
  const login = route === '#/dang-nhap';
  document.querySelector('.home-link').classList.toggle('active', !register && !login);
  if (!register && !login) {
    main.replaceChildren(homeTemplate.content.cloneNode(true));
    document.title = 'Libra | Thư viện & Nhà sách';
    if (route === '#/gioi-thieu') document.querySelector('#gioi-thieu').scrollIntoView();
    else window.scrollTo(0, 0);
    return;
  }
  const title = register ? 'Tạo tài khoản' : 'Chào mừng trở lại';
  document.title = `${register ? 'Đăng ký' : 'Đăng nhập'} | Libra`;
  main.innerHTML = `<section class="wrap auth-layout"><aside class="auth-intro"><div class="eyebrow">CÙNG LIBRA, ĐỌC NHIỀU HƠN</div><h2>${register ? 'Một khởi đầu mới.<br><span>Vạn trang sách hay.</span>' : 'Hành trình đọc sách<br><span>vẫn đang chờ bạn.</span>'}</h2><p>Kết nối với thư viện và bắt đầu hành trình khám phá tri thức của riêng bạn.</p></aside><div class="auth-form"><a class="back" href="#/">← Về trang chủ</a><h1>${title}</h1><p class="subtitle">${register ? 'Điền thông tin để đăng ký thành viên Libra.' : 'Đăng nhập bằng email và mật khẩu của bạn.'}</p><form>${register ? field('name', 'Họ và tên', 'text', 'name') : ''}${field('email', 'Email', 'email', 'email')}${field('password', 'Mật khẩu', 'password', register ? 'new-password' : 'current-password', register ? 'Ít nhất 8 ký tự.' : '')}${register ? field('confirm-password', 'Xác nhận mật khẩu', 'password', 'new-password') : ''}<div class="message" role="status" aria-live="polite"></div><button type="submit" class="button submit">${register ? 'Đăng ký tài khoản' : 'Đăng nhập'} <span aria-hidden="true">↗</span></button></form><p class="switch">${register ? 'Đã có tài khoản? <a href="#/dang-nhap">Đăng nhập</a>' : 'Chưa có tài khoản? <a href="#/dang-ky">Đăng ký ngay</a>'}</p><p class="notice">Bản giao diện thử nghiệm. Chưa kết nối cơ sở dữ liệu; thông tin tài khoản sẽ không được lưu hoặc gửi đi.</p></div></section>`;
  const form = main.querySelector('form');
  main.querySelectorAll('.toggle-password').forEach(button => {
    button.addEventListener('click', () => {
      const input = document.getElementById(button.dataset.for);
      const show = input.type === 'password';
      input.type = show ? 'text' : 'password';
      button.textContent = show ? 'Ẩn' : 'Hiện';
      button.setAttribute('aria-pressed', String(show));
      button.setAttribute('aria-label', `${show ? 'Ẩn' : 'Hiện'} ${input.id === 'confirm-password' ? 'xác nhận mật khẩu' : 'mật khẩu'}`);
    });
  });
  form.addEventListener('input', () => {
    form.querySelectorAll('input').forEach(input => input.setCustomValidity(''));
    form.querySelector('.message').textContent = '';
  });
  form.addEventListener('submit', event => {
    event.preventDefault();
    if (register) {
      const name = form.elements.namedItem('name');
      const confirm = form.elements.namedItem('confirm-password');
      if (!name.value.trim()) { name.setCustomValidity('Vui lòng nhập họ và tên.'); name.reportValidity(); return; }
      if (confirm.value !== form.elements.namedItem('password').value) { confirm.setCustomValidity('Mật khẩu xác nhận chưa khớp.'); confirm.reportValidity(); return; }
    }
    form.querySelector('.message').textContent = 'Thông tin đã hợp lệ về định dạng. Cần kết nối máy chủ để hoàn tất ' + (register ? 'đăng ký tài khoản.' : 'xác thực đăng nhập.');
  });
  window.scrollTo(0, 0);
}
window.addEventListener('hashchange', render);
render();
