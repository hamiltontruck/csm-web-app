// Isolated browser regression: all API traffic goes to this local fixture.
const {chromium, expect} = require('playwright/test');
const http = require('http');
const {spawn} = require('child_process');
const fs = require('fs');
const path = require('path');
(async () => {
 const site='11111111-1111-1111-1111-111111111111', vendor='22222222-2222-2222-2222-222222222222', material='33333333-3333-3333-3333-333333333333';
 const permissions=['Sites_View','Procurement_Vendor_View','Procurement_Inventory_View','Procurement_PurchaseOrder_View','Procurement_PurchaseOrder_Manage','Notifications_View'];
 const saved=[];
 const api=http.createServer((req,res) => {
  const chunks=[]; req.on('data',b=>chunks.push(b)); req.on('end',()=>{
   const route=req.url.split('?')[0].toLowerCase(); let data=[];
   if(route==='/auth/login') data={accessToken:'local-browser-fixture-only',refreshToken:'local-fixture-only',email:'qa@example.test',fullName:'Local Officer',roles:['ProcurementOfficer'],companyId:site};
   else if(route==='/auth/me') data={companyId:site,roles:['ProcurementOfficer']};
   else if(route==='/auth/permissions') data=permissions;
   else if(route.includes('unread-count')) data=0;
   else if(route==='/materials') data=[{id:material,name:'Cement',materialCode:'MAT',category:'General',unitOfMeasure:'Bag',materialType:'Bulk',isActive:true}];
   else if(route==='/vendors') data=[{id:vendor,name:'Supplier',vendorCode:'VEN',isActive:true}];
   else if(route==='/purchaseorders' && req.method==='POST') { saved.push(JSON.parse(Buffer.concat(chunks).toString())); data={}; }
   else if(route.includes('dashboard')) data={generatedAtUtc:new Date().toISOString(),lowStockItems:0};
   res.writeHead(200,{'Content-Type':'application/json'}); res.end(JSON.stringify(data));
  });
 });
 await new Promise(r=>api.listen(5068,'127.0.0.1',r));
 const app=spawn('dotnet',['run','--project','CSM.Web/CSM.Web.csproj','--configuration','Release','--no-build','--no-launch-profile'],{
  env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',ASPNETCORE_URLS:'http://127.0.0.1:5069',ApiSettings__BaseUrl:'http://127.0.0.1:5068/'},stdio:['ignore','ignore','inherit']});
 let browser;
 try {
  for(let i=0;i<80;i++){try {await fetch('http://127.0.0.1:5069/login');break;}catch {await new Promise(r=>setTimeout(r,250));}}
  browser=await chromium.launch({headless:true,args:['--no-sandbox']});
  for(const width of [1280,390]) {
   const context=await browser.newContext({viewport:{width,height:1000},isMobile:width===390,hasTouch:width===390});
   const page=await context.newPage();
   await page.goto('http://127.0.0.1:5069/login');
   await page.locator('#csm-email').fill('qa@example.test'); await page.locator('#csm-password').fill('Local-fixture-only-123!');
   await page.locator('button[type=submit]').click(); await page.waitForURL('**/dashboard');
   await page.goto('http://127.0.0.1:5069/purchase-orders/create/'+site);
   await page.getByRole('heading',{name:'New Purchase Order',exact:true}).waitFor();
   const form=page.locator('form'); await form.waitFor();
   await form.locator('input:not([type]), input[type=text]').first().fill('PO-BROWSER-'+width);
   await form.locator('select').first().selectOption(vendor); await form.locator('select').nth(1).selectOption(material);
   await form.locator('input[type=number]').first().fill('1');
   await form.locator('textarea').first().fill('Procurement Officer purchase order test.');
   const price=form.locator('input[type=number]').nth(1);
   await price.focus(); await price.press('Control+A'); await price.press('Backspace'); await price.pressSequentially('100.25');
   await expect(price).toHaveValue('100.25');
   const before=saved.length;
   await form.locator('button[type=submit]').click();
   await expect.poll(()=>saved.length,{timeout:15000}).toBe(before+1);
   const payload=saved[before];
   if(payload.notes!=='Procurement Officer purchase order test.' || payload.lines[0].unitPrice!==100.25 || payload.lines[0].orderedQuantity!==1)
    throw Error('Wrong saved API payload: '+JSON.stringify(payload));
   if(process.env.CSM_SCREENSHOT_DIR) {fs.mkdirSync(process.env.CSM_SCREENSHOT_DIR,{recursive:true});await page.screenshot({path:path.join(process.env.CSM_SCREENSHOT_DIR,'po-saved-'+width+'.png'),fullPage:true});}
   console.log('PASS keyboard typing and saved price/notes',width);
   await context.close();
  }
 } finally {if(browser) await browser.close();app.kill();api.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
