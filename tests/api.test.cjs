const test = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
function setup(mock=true, fetchStub) {
  const storage=new Map([['libra.session',JSON.stringify('demo')]]);
  const context=vm.createContext({window:{LIBRA_CONFIG:{useMock:mock,apiBaseUrl:'https://localhost:7001/api'}},localStorage:{getItem:k=>storage.get(k)||null,setItem:(k,v)=>storage.set(k,v)},fetch:fetchStub});
  for(const file of ['data.js','api.js'])vm.runInContext(fs.readFileSync('dist/'+file,'utf8'),context);
  return {api:context.window.LibraAPI,storage};
}
test('Demo accepts configured account and rejects incorrect credentials',async()=>{
 const {api,storage}=setup();
 assert.equal((await api.login({email:'demo@libra.vn',password:'Libra@123'})).name,'Minh Anh');
 await assert.rejects(api.login({email:'demo@libra.vn',password:'incorrect'}));
 assert.equal(storage.get('libra.session'),JSON.stringify('demo')); assert.equal([...storage.values()].join('').includes('Libra@123'),false);
});
test('Order calculates discount and shipping and excludes customer data',async()=>{
 const {api,storage}=setup();
 const order=await api.createOrder({items:[{bookId:1,quantity:1}],coupon:'LIBRA10',customer:{name:'Private Name',address:'Private Address'},paymentMethod:'cod'});
 assert.equal(order.subtotal,79000); assert.equal(order.discount,7900); assert.equal(order.shipping,25000); assert.equal(order.total,96100);
 assert.equal((await api.orders()).length,1);
 assert.equal(storage.get('libra.orders').includes('Private'),false);
});
test('Free shipping and promotion cap',async()=>{
 const {api}=setup();
 const order=await api.createOrder({items:[{bookId:1,quantity:10}],coupon:'LIBRA10',paymentMethod:'cod'});
 assert.equal(order.shipping,0);assert.equal(order.discount,50000);assert.equal(order.total,740000);
});
test('Rejects empty, fractional, out of stock and excess quantity orders',async()=>{
 const {api}=setup();
 for(const items of [[],[{bookId:1,quantity:1.5}],[{bookId:12,quantity:1}],[{bookId:1,quantity:49}],[{bookId:100,quantity:1}]])await assert.rejects(api.createOrder({items}));
});
test('Invalid stored order JSON is recovered',async()=>{
 const {api,storage}=setup();storage.set('libra.orders','broken');assert.equal((await api.orders()).length,0);
});
test('Live adapter posts only order contract with cookie credentials',async()=>{
 let call;
 const {api}=setup(false,async(url,options)=>{call={url,options};return {ok:true,status:200,json:async()=>({id:'API-1'})};});
 const payload={items:[{bookId:1,quantity:1}],coupon:'',paymentMethod:'cod'};
 assert.equal((await api.createOrder(payload)).id,'API-1');
 assert.equal(call.url,'https://localhost:7001/api/orders');assert.equal(call.options.credentials,'include');assert.equal(call.options.body,JSON.stringify(payload));
});
test('Live adapter exposes API failures',async()=>{
 const {api}=setup(false,async()=>({ok:false,status:409,json:async()=>({message:'Sách đã hết hàng'})}));
 await assert.rejects(api.books(),/Sách đã hết hàng/);
});

test('Customer cannot invoke admin API; admin can update stock',async()=>{
 const {api}=setup();await assert.rejects(api.adminOrders(),/quyền/);await assert.rejects(api.updateBook(1,{price:1,stock:1}),/quyền/);
 const admin=await api.login({email:'admin@libra.vn',password:'Admin@123'});assert.equal(admin.role,'admin');
 await api.updateBook(1,{price:80000,stock:5});assert.equal((await api.books())[0].stock,5);
 await api.logout();await assert.rejects(api.adminOrders(),/quyền/);
});
test('Orders are scoped by account while admin can view all',async()=>{
 const {api}=setup();await api.createOrder({items:[{bookId:1,quantity:1}],paymentMethod:'cod'});
 await api.login({email:'admin@libra.vn',password:'Admin@123'});assert.equal((await api.orders()).length,0);assert.equal((await api.adminOrders()).length,1);
 await api.logout();assert.equal((await api.orders()).length,0);
});
test('Staff can process orders but cannot edit books or accounts',async()=>{
 const {api}=setup();const order=await api.createOrder({items:[{bookId:1,quantity:1}],paymentMethod:'cod'});
 assert.equal((await api.login({email:'staff@libra.vn',password:'Staff@123'})).role,'staff');
 assert.equal((await api.workOrders()).length,1);await api.updateOrder(order.id,'Đang giao');
 await assert.rejects(api.updateBook(1,{price:100,stock:2}),/quyền/);await assert.rejects(api.accounts(),/quyền/);
});
test('Manager can edit inventory but cannot grant permissions',async()=>{
 const {api}=setup();await api.login({email:'manager@libra.vn',password:'Manager@123'});
 await api.updateBook(1,{price:81000,stock:3});assert.equal((await api.books())[0].price,81000);
 await assert.rejects(api.updateAccount('staff',{role:'admin',active:true}),/quyền/);
});
test('Admin can change roles and disable accounts but cannot lock itself',async()=>{
 const {api}=setup();await api.login({email:'admin@libra.vn',password:'Admin@123'});
 assert.equal((await api.accounts()).length,4);assert.ok((await api.accounts()).every(a=>!('password' in a)));
 await assert.rejects(api.updateAccount('admin',{role:'staff',active:false}));
 await api.updateAccount('staff',{role:'manager',active:true});assert.equal((await api.login({email:'staff@libra.vn',password:'Staff@123'})).role,'manager');
 await api.login({email:'admin@libra.vn',password:'Admin@123'});await api.updateAccount('staff',{role:'staff',active:false});
 await assert.rejects(api.login({email:'staff@libra.vn',password:'Staff@123'}));
});
