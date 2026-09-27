/* Adapter for qlthuvien_vip.zip. Demo API remains available when useMock=true. */
(function(){
 if(window.LIBRA_CONFIG.useMock)return;
 const base=window.LIBRA_CONFIG.apiBaseUrl.replace(/\/$/,'');
 let token=sessionStorage.getItem('libra.jwt')||'';
 const statuses={PENDING_PAYMENT:'Chờ thanh toán',PENDING_CONFIRMATION:'Chờ xác nhận',PAID:'Đã thanh toán',CONFIRMED:'Đã xác nhận',PROCESSING:'Đang chuẩn bị',SHIPPING:'Đang giao',DELIVERED:'Hoàn tất',CANCELLED:'Đã hủy',RETURNED:'Đã hoàn trả',EXPIRED:'Hết hạn',REFUND_REQUIRED:'Cần hoàn tiền'};
 const profile=p=>({id:p.customerId,name:p.fullName,email:p.email,phone:p.phoneNumber,role:(p.role||'Customer').toLowerCase(),active:p.isActive!==false});
 const product=p=>({...p,id:p.productId,stock:p.isActive===false?0:p.stockQuantity,originalPrice:p.price,color:['#b56a36','#345d56','#c5a578','#71865b','#324e63','#739bb0','#a06466','#873f36','#657b88','#6c7945','#954e3e','#343c66'][(Number(p.productId)-1)%12],description:p.description||'',author:p.author||'Chưa cập nhật',category:p.category||'Khác',pages:'—',year:'—',language:'—',rating:null});
 const order=o=>({...o,id:o.orderId,userId:o.customerId,statusCode:o.orderStatus,status:statuses[o.orderStatus]||o.orderStatus,shipping:o.shippingFee,discount:o.discountAmount,total:o.totalAmount,items:o.items.map(i=>({...i,bookId:i.productId,title:i.productTitle,price:i.unitPrice}))});
 async function request(path,options={}){
  const controller=new AbortController();const timer=setTimeout(()=>controller.abort(),15000);
  try{
   const response=await fetch(base+path,{...options,signal:controller.signal,headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{}),...options.headers}});
   const body=response.status===204?null:await response.json().catch(()=>null);
   if(!response.ok||body?.success===false){
    if(response.status===401){token='';sessionStorage.removeItem('libra.jwt');window.dispatchEvent(new Event('libra:unauthorized'));}
    const message=body?.message||Object.values(body?.errors||{}).flat().join(' ')||(response.status===403?'Bạn không có quyền thực hiện thao tác này.':response.status===401?'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.':`Yêu cầu thất bại (${response.status}).`);
    const error=new Error(message);error.status=response.status;throw error;
   }
   return body&&Object.hasOwn(body,'data')?body.data:body;
  }catch(error){if(error.name==='AbortError')throw new Error('Máy chủ phản hồi quá lâu. Vui lòng thử lại.');if(error instanceof TypeError)throw new Error('Không kết nối được backend. Kiểm tra Visual Studio đang chạy, địa chỉ API, chứng chỉ HTTPS và CORS.');throw error;}finally{clearTimeout(timer);}
 }
 const send=(path,method,data)=>request(path,{method,...(data===undefined?{}:{body:JSON.stringify(data)})});
 async function allPages(path){let result=[],page=1;do{const data=await request(path+(path.includes('?')?'&':'?')+`page=${page}&pageSize=100`);result.push(...data.items);if(page>=data.totalPages)break;page++;}while(page<10000);return result;}
 const api={
  statuses,request,hasSession:()=>!!token,
  product:async id=>product(await request('/Products/'+id)),
  books:async()=> (await allPages('/Products')).map(product),
  catalog:async params=>{const q=new URLSearchParams({page:params.get('page')||'1',pageSize:'8'});for(const [from,to] of [['q','search'],['category','category'],['max','maxPrice']])if(params.get(from))q.set(to,params.get(from));const data=await request('/Products?'+q);return {...data,items:data.items.map(product)};},
  login:async data=>{const result=await send('/Auth/login','POST',{email:data.email,password:data.password});token=result.token;sessionStorage.setItem('libra.jwt',token);return profile(result.userInfo);},
  register:data=>send('/Auth/register','POST',{fullName:data.name,email:data.email,password:data.password,phoneNumber:data.phone||''}),
  me:async()=>token?profile(await request('/Auth/me')):null,
  logout:async()=>{try{if(token)await send('/Auth/logout','POST');}finally{token='';sessionStorage.removeItem('libra.jwt');}},
  cart:()=>request('/Cart'),addCart:(id,quantity)=>send('/Cart/add','POST',{productId:id,quantity}),
  changeCart:(id,quantity)=>send('/Cart/item/'+id,'PUT',{quantity}),removeCart:id=>send('/Cart/item/'+id,'DELETE'),
  favorites:()=>request('/Favorites'),favorite:(id,on)=>send('/Favorites/'+id,on?'POST':'DELETE'),
  addresses:()=>request('/Addresses'),saveAddress:(data,id)=>send('/Addresses'+(id?'/'+id:''),id?'PUT':'POST',data),removeAddress:id=>send('/Addresses/'+id,'DELETE'),
  preview:items=>send('/Orders/preview-cost','POST',items),
  createOrder:async data=>order(await send('/Orders/checkout','POST',data)),
  orders:async()=> (await request('/Orders')).map(order),workOrders:async()=> (await request('/Orders')).map(order),
  order:async id=>order(await request('/Orders/'+id)),cancelOrder:id=>send('/Orders/'+id+'/cancel','POST'),
  updateOrder:(id,status)=>send('/Orders/'+id+'/status','PUT',{status}),
  createBook:async data=>product(await send('/Products','POST',data)),
  updateBook:async(id,values)=>{const b=await request('/Products/'+id);return send('/Products/'+id,'PUT',{title:b.title,author:b.author,publisher:b.publisher,isbn:b.isbn,category:b.category,description:b.description,imageUrl:b.imageUrl,isActive:b.isActive,...{price:values.price,stockQuantity:values.stock}});},
  accounts:async()=> (await allPages('/Users')).map(profile),
  updateAccount:async(id,values)=>{const old=await request('/Users/'+id);if(old.role.toLowerCase()!==values.role)await send('/Users/'+id+'/role','PUT',{role:values.role[0].toUpperCase()+values.role.slice(1)});try{if((old.isActive!==false)!==values.active)await send('/Users/'+id+'/status','PUT',{isActive:values.active});}catch(error){throw new Error('Quyền có thể đã được lưu, nhưng cập nhật trạng thái thất bại: '+error.message+' Hãy tải lại danh sách.');}},
  initiate:(orderId,paymentMethod)=>send('/Payment/initiate','POST',{orderId,paymentMethod}),paymentStatus:id=>request('/Payment/status/'+id)
 };
 window.LibraAPI=api;
})();
