// Isolated UI integration fixture. Does not contact SQL Server or the real API.
const http=require('node:http'),fs=require('node:fs'),path=require('node:path');
const product=(id,title)=>({productId:id,title,author:'Tác giả mẫu',category:'Văn học',price:id===1?79000:88000,stockQuantity:50,isActive:true});
const products=[product(1,'Nhà giả kim'),product(2,'Đi tìm lẽ sống')];let cart=[{cartItemId:11,productId:1,productTitle:'Nhà giả kim',productPrice:79000,quantity:1},{cartItemId:12,productId:2,productTitle:'Đi tìm lẽ sống',productPrice:88000,quantity:2}],addresses=[],orders=[];
const user={customerId:1,fullName:'Khách kiểm thử',email:'test@example.com',role:'Customer',isActive:true};
http.createServer(async(req,res)=>{let body='';for await(const part of req)body+=part;const data=body?JSON.parse(body):{};const url=new URL(req.url,'http://localhost'),p=url.pathname;const reply=(data,status=200)=>{res.writeHead(status,{'Content-Type':'application/json'});res.end(JSON.stringify({success:status<400,data,message:status<400?'OK':'Lỗi kiểm thử'}));};
if(p==='/config.js'){res.setHeader('Content-Type','text/javascript');res.end("window.LIBRA_CONFIG={useMock:false,apiBaseUrl:'http://127.0.0.1:4188/api',onlinePaymentsEnabled:false};");return;}
if(!p.startsWith('/api/')){const name=p==='/'?'index.html':p.slice(1);if(!['index.html','app.js','api.js','api-live.js','live.js','admin.js','data.js','styles.css'].includes(name)){res.writeHead(404);return res.end();}res.setHeader('Content-Type',name.endsWith('.js')?'text/javascript':name.endsWith('.css')?'text/css':'text/html');return res.end(fs.readFileSync(path.join(__dirname,'../dist',name)));}
if(p==='/api/Products')return reply({items:products,totalItems:2,page:1,pageSize:8,totalPages:1});
if(/^\/api\/Products\/\d+$/.test(p))return reply(products.find(b=>b.productId===Number(p.split('/').pop())));
if(p==='/api/Auth/login')return reply({token:'fixture-token',userInfo:user});
if(p==='/api/Auth/me')return reply(user);
if(!req.headers.authorization)return reply(null,401);
if(p==='/api/Favorites')return reply([]);
if(p==='/api/Cart')return reply({items:cart});
if(p==='/api/Cart/add'){const item=cart.find(i=>i.productId===data.productId);if(item)item.quantity+=data.quantity;else{const b=products.find(b=>b.productId===data.productId);cart.push({cartItemId:data.productId+10,productId:b.productId,productTitle:b.title,productPrice:b.price,quantity:data.quantity});}return reply({items:cart});}
if(p.startsWith('/api/Cart/item/')){const id=Number(p.split('/').pop());if(req.method==='DELETE')cart=cart.filter(i=>i.cartItemId!==id);else cart.find(i=>i.cartItemId===id).quantity=data.quantity;return reply({items:cart});}
if(p==='/api/Addresses'){if(req.method==='POST'){const a={...data,addressId:addresses.length+1,fullAddressString:data.detailedAddress+', '+data.ward+', '+data.district+', '+data.province};addresses.push(a);return reply(a);}return reply(addresses);}
if(p==='/api/Orders/preview-cost'||p==='/api/Orders/checkout'){const subtotal=data.items.reduce((s,i)=>s+products.find(b=>b.productId===i.productId).price*i.quantity,0),shippingFee=subtotal>=300000?0:30000,discountAmount=data.discountCode==='GIAM10'?Math.round(subtotal*.1):0;const result={subtotal,shippingFee,discountAmount,totalAmount:subtotal+shippingFee-discountAmount};if(p.endsWith('/preview-cost'))return reply(result);const order={...result,orderId:orders.length+1,customerId:1,orderStatus:'PENDING_CONFIRMATION',paymentMethod:'COD',createdAt:new Date().toISOString(),items:data.items.map(i=>({...i,productTitle:products.find(b=>b.productId===i.productId).title,unitPrice:products.find(b=>b.productId===i.productId).price})),recipientName:'Khách kiểm thử',shippingAddress:'Địa chỉ kiểm thử'};orders.push(order);cart=cart.filter(i=>!data.items.some(p=>p.productId===i.productId));return reply(order);}
if(p==='/api/Orders')return reply(orders);
return reply(null,404);
}).listen(4188,'127.0.0.1',()=>console.log('Isolated UI fixture: http://127.0.0.1:4188'));
