/* Live workflows. No mock account, price calculation or order persistence. */
let liveQuote=null,quoteError='',quotePending=false,quoteVersion=0,liveAddresses=[],liveBusy=false;
function mergeBooks(incoming){for(const b of incoming){const index=books.findIndex(x=>x.id===b.id);if(index<0)books.push(b);else books[index]=b;}}
function liveSelected(){return cart.filter(i=>i.selected!==false).map(i=>({productId:i.bookId,quantity:i.quantity}));}
function selectionKey(){return 'libra.selection.'+(user?.id||'guest');}
function saveSelection(){sessionStorage.setItem(selectionKey(),JSON.stringify(cart.filter(i=>i.selected===false).map(i=>i.bookId)));}
async function syncLiveCart(){
 if(!user){cart=[];return;}
 const data=await LibraAPI.cart();let excluded=[];try{excluded=JSON.parse(sessionStorage.getItem(selectionKey())||'[]');}catch{}
 cart=data.items.map(i=>({bookId:i.productId,cartItemId:i.cartItemId,quantity:i.quantity,selected:!excluded.includes(i.productId)}));
 for(const item of data.items){let b=books.find(b=>b.id===item.productId);if(!b){b=await LibraAPI.product(item.productId);mergeBooks([b]);}b.price=item.productPrice;}
 header();
}
async function syncLiveFavorites(){if(!user){favorites=[];return;}const rows=await LibraAPI.favorites();favorites=rows.map(b=>b.productId);for(const r of rows)if(!books.some(b=>b.id===r.productId))mergeBooks([await LibraAPI.product(r.productId)]);}
function liveSummary(checkout=false){
 const q=liveQuote;return `<aside class="order-summary"><h2>Tóm tắt đơn hàng</h2>${quotePending?'<p role="status">Đang tính tiền từ nhà sách…</p>':quoteError?`<p class="form-error" role="alert">${esc(quoteError)}</p><button type="button" class="text-button" data-quote-retry>Thử tính lại</button>`:q?`<div><span>Tiền sách</span><b>${money(q.subtotal)}</b></div><div><span>Vận chuyển</span><b>${money(q.shippingFee)}</b></div><div><span>Giảm giá</span><b>−${money(q.discountAmount)}</b></div><div class="total"><span>Tổng cộng</span><b>${money(q.totalAmount)}</b></div>${q.discountMessage?`<p class="hint">${esc(q.discountMessage)}</p>`:''}`:'<p>Chọn sách để xem tổng tiền.</p>'}${!checkout?`<form id="coupon-form"><label for="coupon">Mã ưu đãi</label><div class="coupon-row"><input id="coupon" value="${esc(coupon)}" placeholder="GIAM10"><button ${quotePending?'disabled':''}>Áp dụng</button></div></form>${q&&!quoteError&&!quotePending&&liveSelected().length?'<a class="button full" href="#/thanh-toan">Thanh toán sách đã chọn →</a>':'<button class="button full" disabled>Chọn sách và kiểm tra tổng tiền</button>'}`:''}</aside>`;
}
function drawLiveQuote(){
 const y=window.scrollY,x=window.scrollX;const node=main.querySelector('.order-summary');const draft=main.querySelector('#coupon')?.value;if(node)node.outerHTML=liveSummary(location.hash.startsWith('#/thanh-toan'));if(draft!==undefined&&main.querySelector('#coupon'))main.querySelector('#coupon').value=draft;
 const submit=main.querySelector('#live-checkout button[type=submit]');if(submit){submit.disabled=quotePending||!!quoteError||!liveQuote||liveBusy;submit.textContent='Xác nhận đặt hàng'+(liveQuote?' · '+money(liveQuote.totalAmount):'');}
 window.scrollTo({left:x,top:y,behavior:'instant'});
}
async function refreshLiveQuote(){
 const version=++quoteVersion;liveQuote=null;quoteError='';quotePending=liveSelected().length>0;
 if(!quotePending){drawLiveQuote();return;}
 drawLiveQuote();try{const quote=await LibraAPI.preview({items:liveSelected(),discountCode:coupon||null});if(version===quoteVersion)liveQuote=quote;}catch(error){if(version===quoteVersion)quoteError=error.message;}finally{if(version===quoteVersion){quotePending=false;drawLiveQuote();}}
}
function updateLiveCartView(){refreshCartTotals();saveSelection();refreshLiveQuote();}
function addressFields(a={}){return `<div class="form-grid">${[['recipientName','Họ tên người nhận'],['recipientPhone','Số điện thoại'],['province','Tỉnh / Thành phố'],['district','Quận / Huyện'],['ward','Phường / Xã'],['detailedAddress','Số nhà, tên đường']].map(([key,label])=>`<label class="field">${label}<input name="${key}" value="${esc(a[key]||'')}" ${key==='detailedAddress'?'':'required'} maxlength="${key==='recipientPhone'?20:key==='detailedAddress'?500:100}" ${key==='recipientPhone'?'type="tel"':'type="text"'}></label>`).join('')}</div><label class="check"><input type="checkbox" name="isDefault" ${a.isDefault?'checked':''}> Địa chỉ mặc định</label>`;}
function addressPayload(form){const d=Object.fromEntries(new FormData(form));return {recipientName:d.recipientName.trim(),recipientPhone:d.recipientPhone.trim(),province:d.province.trim(),district:d.district.trim(),ward:d.ward.trim(),detailedAddress:d.detailedAddress.trim(),isDefault:!!d.isDefault};}
function liveCheckout(){
 return `<section class="wrap section">${heading('Thông tin giao hàng','Kiểm tra địa chỉ và số tiền trước khi xác nhận.')}<div class="checkout-layout"><form id="live-checkout" class="panel"><h2>Địa chỉ nhận hàng</h2><label class="field">Chọn địa chỉ<select id="address-choice" name="addressId"><option value="new">Nhập địa chỉ mới</option>${liveAddresses.map(a=>`<option value="${a.addressId}" ${a.isDefault?'selected':''}>${esc(a.recipientName+' — '+a.fullAddressString)}</option>`).join('')}</select></label><fieldset id="new-address" ${liveAddresses.some(a=>a.isDefault)?'hidden disabled':''}>${addressFields()}</fieldset><h2>Phương thức thanh toán</h2><label class="payment"><input type="radio" name="paymentMethod" value="COD" checked> Thanh toán khi nhận hàng (COD)</label>${LIBRA_CONFIG.onlinePaymentsEnabled?'<label class="payment"><input type="radio" name="paymentMethod" value="QR_TRANSFER"> Chuyển khoản QR</label><label class="payment"><input type="radio" name="paymentMethod" value="VNPAY"> VNPay</label>':''}<p class="hint">Đặt đơn COD chưa có nghĩa là đã thanh toán tiền.</p><label class="check"><input type="checkbox" required> Tôi đã kiểm tra thông tin nhận hàng.</label><p class="form-error" role="alert"></p><button class="button full" type="submit" disabled>Xác nhận đặt hàng</button></form><div>${liveSummary(true)}<div class="panel"><h3>Sách được chọn</h3>${selectedItems().map(i=>`<p>${esc(i.book.title)} × ${i.quantity}</p>`).join('')}</div></div></div></section>`;
}
async function liveOrders(version){
 const all=await LibraAPI.orders();if(version!==routeVersion)return;const mine=all.filter(o=>o.userId===user.id);
 main.innerHTML=`<section class="wrap section">${heading('Đơn hàng của tôi','Trạng thái được cập nhật từ nhà sách.')} ${mine.length?mine.map(o=>`<article class="panel order"><div class="order-heading"><h3>Đơn #${o.id}</h3><span class="status">${esc(o.status)}</span></div><p>${new Date(o.createdAt).toLocaleString('vi-VN')} · ${esc(o.paymentMethod)}</p>${o.items.map(i=>`<div class="order-line"><span>${esc(i.title)} × ${i.quantity}</span><b>${money(i.price*i.quantity)}</b></div>`).join('')}<div class="order-total"><span>Ship ${money(o.shipping)} · Giảm ${money(o.discount)}</span><b>${money(o.total)}</b></div><p>${esc(o.recipientName)} · ${esc(o.shippingAddress)}</p>${['PENDING_PAYMENT','PENDING_CONFIRMATION'].includes(o.statusCode)?`<button class="text-button" data-cancel-order="${o.id}">Hủy đơn hàng</button>`:''}${o.statusCode==='PENDING_PAYMENT'&&LIBRA_CONFIG.onlinePaymentsEnabled?` <a class="button" href="#/thanh-toan-don/${o.id}">Tiếp tục thanh toán</a>`:''}</article>`).join(''):empty('Chưa có đơn hàng','Đơn của bạn sẽ xuất hiện tại đây.')}</section>`;
}
async function paymentPage(id,version){
 const order=await LibraAPI.order(id);const payment=await LibraAPI.paymentStatus(id);if(version!==routeVersion)return;
 const pending=order.statusCode==='PENDING_PAYMENT';const p=typeof payment==='object'&&payment?payment:{};const url=safeLiveUrl(p.paymentUrl),qr=safeLiveUrl(p.qrCodeUrl);
 main.innerHTML=`<section class="wrap section">${heading('Thanh toán đơn #'+id,esc(order.status))}<div class="panel"><h2>${money(order.total)}</h2><p>Trạng thái giao dịch: ${esc(p.paymentStatus||'Chưa khởi tạo')}</p><p>${esc(p.note||'Chỉ coi là đã thanh toán khi nhà sách xác nhận.')}</p>${pending&&p.paymentMethod==='QR_TRANSFER'&&qr?`<img class="payment-qr" src="${esc(qr)}" alt="Mã QR thanh toán"><p>${esc(p.bankInfo)}</p>`:''}${pending&&url&&p.paymentMethod!=='QR_TRANSFER'?`<a class="button" href="${esc(url)}" target="_blank" rel="noopener noreferrer">Mở cổng thanh toán ↗</a>`:''}${pending&&LIBRA_CONFIG.onlinePaymentsEnabled?`<button class="button secondary" data-init-payment="${id}" data-method="${['QR_TRANSFER','VNPAY','ONLINE'].includes(order.paymentMethod)?order.paymentMethod:'VNPAY'}">${p.paymentId?'Tạo lại liên kết thanh toán':'Khởi tạo thanh toán'}</button>`:''}<button class="button secondary" data-payment-refresh="${id}">Cập nhật trạng thái</button><p class="form-error" id="payment-error" role="alert"></p><p><a href="#/don-hang">← Về đơn hàng của tôi</a></p></div></section>`;
}
function safeLiveUrl(value){try{const url=new URL(value,LIBRA_CONFIG.apiBaseUrl);return ['http:','https:'].includes(url.protocol)?url.href:'';}catch{return '';}}
const baseRender=render;
render=async function(){
 if(LIBRA_CONFIG.useMock)return baseRender();
 const version=++routeVersion;const [path,query='']=(location.hash.slice(1)||'/').split('?');const params=new URLSearchParams(query);header();
 try{
 if(['/gio-hang','/thanh-toan','/yeu-thich','/don-hang','/dia-chi','/quan-tri','/quan-ly','/nhan-vien'].includes(path)||path.startsWith('/thanh-toan-don/')){if(!user){main.innerHTML=empty('Vui lòng đăng nhập','Đăng nhập để sử dụng tính năng này.','#/dang-nhap','Đăng nhập');return;}}
 if(['/quan-tri','/quan-ly','/nhan-vien'].includes(path)){user=await LibraAPI.me();header();return renderAdmin(version,path);}
 if(path==='/sach'){
  main.innerHTML='<div class="empty" role="status">Đang tìm sách…</div>';const data=await LibraAPI.catalog(params);if(version!==routeVersion)return;mergeBooks(data.items);
  main.innerHTML=catalog(params);const content=main.querySelector('.catalog-layout > div');content.innerHTML=`<div class="results-bar"><span>${data.totalItems} cuốn sách · Trang ${data.page}/${Math.max(1,data.totalPages)}</span><span>Mới nhất</span></div>${data.items.length?`<div class="book-grid catalog-grid">${data.items.map(card).join('')}</div>`:empty('Không có sách phù hợp','Thử tìm kiếm hoặc danh mục khác.')}<div class="pagination">${data.page>1?`<button data-page="${data.page-1}">←</button>`:''}<button aria-current="page">${data.page}</button>${data.page<data.totalPages?`<button data-page="${data.page+1}">→</button>`:''}</div>`;
  main.querySelectorAll('#filter-form .check').forEach(el=>el.remove());document.querySelector('#search').value=params.get('q')||'';
 }else if(path==='/gio-hang'){
  await syncLiveCart();if(version!==routeVersion)return;main.innerHTML=cartPage();await refreshLiveQuote();
 }else if(path==='/thanh-toan'){
  await syncLiveCart();liveAddresses=await LibraAPI.addresses();if(version!==routeVersion)return;if(!liveSelected().length){main.innerHTML=empty('Chưa chọn sách','Chọn sách trong giỏ để tiếp tục.','#/gio-hang','Về giỏ');return;}main.innerHTML=liveCheckout();await refreshLiveQuote();
 }else if(path==='/dia-chi'){
  liveAddresses=await LibraAPI.addresses();if(version!==routeVersion)return;main.innerHTML=`<section class="wrap section">${heading('Sổ địa chỉ')}<div class="admin-books">${liveAddresses.map(a=>`<form class="panel" data-live-address="${a.addressId}"><h3>${esc(a.recipientName)}</h3>${addressFields(a)}<button class="button" type="submit">Lưu địa chỉ</button> <button type="button" class="text-button" data-delete-address="${a.addressId}">Xóa</button><p class="form-error" role="alert"></p></form>`).join('')}<form class="panel" data-live-address="new"><h3>Thêm địa chỉ mới</h3>${addressFields()}<button class="button" type="submit">Thêm địa chỉ</button><p class="form-error" role="alert"></p></form></div></section>`;
 }else if(path==='/don-hang'){await liveOrders(version);
 }else if(/^\/thanh-toan-don\/\d+$/.test(path)){await paymentPage(Number(path.split('/')[2]),version);
 }else{
  if(/^\/sach\/\d+$/.test(path)){mergeBooks([await LibraAPI.product(Number(path.split('/')[2]))]);if(version!==routeVersion)return;}
  if(path==='/yeu-thich'){await syncLiveFavorites();if(version!==routeVersion)return;}
  await baseRender();
  if(path==='/tai-khoan'&&user){const link=document.createElement('a');link.href='#/dia-chi';link.className='button secondary';link.textContent='Sổ địa chỉ';main.querySelector('.account-panel')?.append(link);}
  main.querySelectorAll('.rating').forEach(el=>el.remove());
  if(path==='/'){main.querySelectorAll('a[href*="sort=popular"]').forEach(a=>{a.href='#/sach';a.textContent='Khám phá sách →';});main.querySelectorAll('a[href*="sale=1"]').forEach(a=>a.href='#/sach');}
  if(path==='/chinh-sach')main.innerHTML=`<section class="wrap section prose">${heading('Giao hàng & thanh toán')}<h2>Phí giao hàng</h2><p>Phí vận chuyển 30.000₫, miễn phí cho tiền sách từ 300.000₫. Tổng tiền chính xác và điều kiện khuyến mãi được nhà sách xác nhận ở bước thanh toán.</p><h2>Ưu đãi</h2><p>Nhập mã GIAM10, SALE10 hoặc VIP50K tại giỏ hàng để kiểm tra điều kiện áp dụng.</p><h2>Thanh toán</h2><p>Với COD, bạn thanh toán khi nhận sách. Đơn hàng được ghi nhận trước khi giao, không đồng nghĩa đã thu tiền.</p><h2>Đổi trả</h2><p>Liên hệ nhà sách để xác nhận điều kiện đổi trả trước khi mua.</p></section>`;
 
 }

 document.title=(main.querySelector('h1')?.textContent||'Nhà sách')+' | Libra';
 window.scrollTo(0,0);
 }catch(error){if(version!==routeVersion)return;main.innerHTML=`<section class="wrap section">${heading('Chưa thể tải nội dung')}<p role="alert">${esc(error.message)}</p><button class="button" data-live-retry>Thử lại</button> <a href="#/dang-nhap">Đăng nhập</a></section>`;}
};
async function liveInit(){
 cart=[];favorites=[];user=null;loaded=true;header();document.querySelectorAll('header a[href*="sort=popular"]').forEach(a=>{a.href='#/sach';a.textContent='Khám phá sách';});document.querySelectorAll('header a[href*="sale=1"]').forEach(a=>{a.href='#/sach';a.textContent='Chọn sách';});main.innerHTML='<div class="empty" role="status">Đang kết nối nhà sách…</div>';
 try{user=await LibraAPI.me();books=await LibraAPI.books();const availableCategories=[...new Set(books.map(b=>b.category))];if(availableCategories.length)categories.splice(0,categories.length,...availableCategories);if(user)await Promise.all([syncLiveCart(),syncLiveFavorites()]);await render();}catch(error){main.innerHTML=`<div class="empty"><h1>Chưa kết nối được nhà sách</h1><p>${esc(error.message)}</p><button class="button" data-live-init>Thử kết nối lại</button><p><a href="#/dang-nhap">Đi đến đăng nhập</a></p></div>`;}
}
function intercept(event){event.preventDefault();event.stopImmediatePropagation();}
async function liveAction(button,action){if(liveBusy)return;liveBusy=true;if(button)button.disabled=true;try{await action();}catch(error){toast(error.message);}finally{liveBusy=false;if(button)button.disabled=false;}}
window.addEventListener('libra:unauthorized',()=>{if(LIBRA_CONFIG.useMock)return;user=null;cart=[];favorites=[];liveQuote=null;header();});
document.addEventListener('click',event=>{
 if(LIBRA_CONFIG.useMock)return;const b=event.target.closest('button');if(!b)return;
 const id=Number(b.dataset.add||b.dataset.favorite||b.dataset.remove);
 if(b.dataset.add||b.dataset.favorite||b.dataset.remove){intercept(event);if(!user){location.hash='/dang-nhap';toast('Đăng nhập để lưu giỏ hàng và yêu thích.');return;}liveAction(b,async()=>{
  if(b.dataset.add){await LibraAPI.addCart(id,b.dataset.detail?Number(document.querySelector('#quantity').value):1);await syncLiveCart();toast('Đã thêm sách vào giỏ.');}
  if(b.dataset.favorite){const on=!favorites.includes(id);await LibraAPI.favorite(id,on);await syncLiveFavorites();b.textContent=on?'♥':'♡';b.setAttribute('aria-pressed',String(on));b.classList.toggle('liked',on);if(location.hash.startsWith('#/yeu-thich'))await render();}
  if(b.dataset.remove){const item=cart.find(i=>i.bookId===id);await LibraAPI.removeCart(item.cartItemId);const y=window.scrollY;await render();window.scrollTo({top:y,behavior:'instant'});toast('Đã xóa sách khỏi giỏ.');}
 });return;}
 if(b.id==='logout'){intercept(event);liveAction(b,async()=>{try{await LibraAPI.logout();}finally{user=null;cart=[];favorites=[];coupon='';header();location.hash='/dang-nhap';}});return;}
 if(b.hasAttribute('data-quote-retry')){intercept(event);refreshLiveQuote();return;}
 if(b.hasAttribute('data-live-init')){intercept(event);liveInit();return;}
 if(b.hasAttribute('data-live-retry')){intercept(event);render();return;}
 if(b.dataset.cancelOrder||b.dataset.deleteAddress||b.dataset.initPayment||b.dataset.paymentRefresh){intercept(event);liveAction(b,async()=>{
  if(b.dataset.cancelOrder){await LibraAPI.cancelOrder(b.dataset.cancelOrder);await render();}
  if(b.dataset.deleteAddress){await LibraAPI.removeAddress(b.dataset.deleteAddress);await render();}
  if(b.dataset.initPayment){await LibraAPI.initiate(Number(b.dataset.initPayment),b.dataset.method);await render();}
  if(b.dataset.paymentRefresh)await render();
 });}
},true);
document.addEventListener('change',event=>{
 if(LIBRA_CONFIG.useMock)return;const el=event.target;
 if(el.id==='address-choice'){const box=main.querySelector('#new-address');box.hidden=box.disabled=el.value!=='new';return;}
 if(el.dataset.select||el.id==='select-all'){intercept(event);if(el.id==='select-all')cart.forEach(i=>i.selected=el.checked);else{const item=cart.find(i=>i.bookId===Number(el.dataset.select));if(item)item.selected=el.checked;}updateLiveCartView();return;}
 if(el.dataset.quantity){intercept(event);const item=cart.find(i=>i.bookId===Number(el.dataset.quantity)),n=Number(el.value);if(liveBusy){el.value=item.quantity;return;}if(!Number.isInteger(n)||n<1){el.value=item.quantity;toast('Số lượng phải là số nguyên từ 1.');return;}
 liveAction(el,async()=>{try{await LibraAPI.changeCart(item.cartItemId,n);await syncLiveCart();updateLiveCartView();}catch(error){el.value=item.quantity;throw error;}});}
},true);
document.addEventListener('submit',async event=>{
 if(LIBRA_CONFIG.useMock)return;const form=event.target;
 if(!['auth-form','coupon-form','live-checkout'].includes(form.id)&&!form.dataset.liveAddress)return;intercept(event);
 const button=form.querySelector('button[type=submit]')||form.querySelector('button');if(liveBusy)return;
 liveBusy=true;if(button)button.disabled=true;const errorBox=form.querySelector('.form-error');if(errorBox)errorBox.textContent='';
 try{
 const data=Object.fromEntries(new FormData(form));
 if(form.id==='auth-form'){
  if(form.dataset.register==='true'){if(data.password!==data.confirm)throw new Error('Mật khẩu xác nhận chưa khớp.');await LibraAPI.register({name:data.name.trim(),email:data.email.trim(),password:data.password});location.hash='/dang-nhap';toast('Đăng ký thành công. Bạn hãy đăng nhập.');}
  else{user=await LibraAPI.login({email:data.email.trim(),password:data.password});coupon='';await Promise.all([syncLiveCart(),syncLiveFavorites()]);location.hash=roleHome(user.role);header();}
 }else if(form.id==='coupon-form'){coupon=form.querySelector('#coupon').value.trim().toUpperCase();await refreshLiveQuote();
 }else if(form.dataset.liveAddress){await LibraAPI.saveAddress(addressPayload(form),form.dataset.liveAddress==='new'?null:form.dataset.liveAddress);await render();toast('Đã lưu địa chỉ.');
 }else{
  if(quotePending||!liveQuote||quoteError||!liveSelected().length)throw new Error('Vui lòng đợi nhà sách xác nhận tổng tiền.');
  const items=liveSelected();const quote=await LibraAPI.preview({items,discountCode:coupon||null});if(quote.totalAmount!==liveQuote.totalAmount){liveQuote=quote;drawLiveQuote();throw new Error('Tổng tiền vừa thay đổi. Vui lòng kiểm tra rồi xác nhận lại.');}
  let addressId=Number(data.addressId);if(data.addressId==='new'){const address=await LibraAPI.saveAddress(addressPayload(form));addressId=address.addressId;liveAddresses.push(address);const option=new Option(address.recipientName,addressId,true,true);form.querySelector('#address-choice').add(option);form.querySelector('#new-address').disabled=true;}
  const order=await LibraAPI.createOrder({addressId,items,discountCode:coupon||null,paymentMethod:data.paymentMethod});
  coupon='';liveQuote=null;cart=cart.filter(i=>!items.some(p=>p.productId===i.bookId));saveSelection();header();
  if(data.paymentMethod==='COD'){location.hash='/don-hang';toast('Đã tạo đơn COD. Bạn thanh toán khi nhận hàng.');}
  else{location.hash='/thanh-toan-don/'+order.id;try{await LibraAPI.initiate(order.id,data.paymentMethod);if(location.hash==='#/thanh-toan-don/'+order.id)await render();}catch(error){toast('Đơn đã được tạo, chưa khởi tạo được thanh toán: '+error.message);}}
 }
 }catch(error){if(errorBox)errorBox.textContent=error.message;else toast(error.message);}finally{liveBusy=false;if(button)button.disabled=false;drawLiveQuote();}
},true);
