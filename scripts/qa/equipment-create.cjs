// Local fixture API only; no production accounts or records.
const {chromium,expect}=require('playwright/test');
const http=require('http'),{spawn}=require('child_process');
(async()=>{
 const alpha='11111111-1111-1111-1111-111111111111',beta='22222222-2222-2222-2222-222222222222',id='33333333-3333-3333-3333-333333333333';
 let fail=true,writes=[];
 const api=http.createServer((req,res)=>{let chunks=[];req.on('data',x=>chunks.push(x));req.on('end',()=>{
 const route=new URL(req.url,'http://fixture').pathname.toLowerCase(),body=chunks.length?JSON.parse(Buffer.concat(chunks)):null;let data,status=200;
 if(route==='/auth/login') data={accessToken:'local-equipment',refreshToken:'local-refresh',email:'admin@example.test',fullName:'Local SuperAdmin',roles:['SuperAdmin'],companyId:null};
 else if(route==='/auth/permissions')data=['Procurement_Inventory_View','Procurement_Inventory_Adjust'];
 else if(route==='/auth/me'||route==='/auth/me/profile')data={userId:id,name:'Local SuperAdmin',email:'admin@example.test',companyId:null,roles:['SuperAdmin']};
 else if(route==='/equipment/create-options')data={canSelectCompany:true,companyId:null,companies:[{id:alpha,code:'A',name:'Alpha'},{id:beta,code:'B',name:'Beta'}]};
 else if(route==='/equipment'&&req.method==='POST'){writes.push(body);if(fail){status=400;data={message:'Equipment code already exists.'};}else data={id,...body,status:'Available',ownershipType:'Owned'};}
 else if(route===`/equipment/${id}`)data={id,companyId:beta,equipmentCode:'EQ-005',name:'Compactor',status:'Available',ownershipType:'Owned',meterUnit:'Hours',isActive:true};
 else {status=403;data={message:'Fixture denies unexpected endpoint'};}
 res.writeHead(status,{'Content-Type':'application/json'});res.end(JSON.stringify(data));});});
 await new Promise(r=>api.listen(5090,'127.0.0.1',r));
 const app=spawn('dotnet',['run','--project','CSM.Web/CSM.Web.csproj','-c','Release','--no-build','--no-launch-profile'],{env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',ASPNETCORE_URLS:'http://127.0.0.1:5091',ApiSettings__BaseUrl:'http://127.0.0.1:5090/'},stdio:['ignore','ignore','inherit']});
 let browser;
 try{
 for(let i=0;i<160;i++){try{await fetch('http://127.0.0.1:5091/login');break;}catch{await new Promise(r=>setTimeout(r,250));}}
 browser=await chromium.launch({headless:true,executablePath:process.env.CSM_CHROMIUM||undefined,args:['--no-sandbox']});
 for(const width of [390,1280]){
 fail=true;writes=[];const context=await browser.newContext({viewport:{width,height:1000}}),page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('http://127.0.0.1:5091/login');await page.locator('#csm-email').fill('admin@example.test');await page.locator('#csm-password').fill('Local-Fixture-Password-123!');await page.locator('button[type=submit]').click();await page.waitForURL('**/dashboard');
 await page.goto('http://127.0.0.1:5091/equipment/create');await expect(page.locator('#equipment-company')).toBeVisible();await expect(page.locator('#equipment-company')).toHaveValue('');
 const field=label=>page.locator('label').filter({hasText:label}).locator('..').locator('input').first();
 await field('Equipment Code').fill('EQ-005');await field('Name').fill('Compactor');await field('Category').fill('compactor');await field('Make').fill('CAT');
 await page.getByRole('button',{name:'Create Equipment',exact:true}).click();await expect(page.locator('#equipment-save-error')).toContainText('Select a company');if(writes.length)throw Error('Missing-company request was posted');
 await page.locator('#equipment-company').selectOption(beta);await page.getByRole('button',{name:'Create Equipment',exact:true}).click();await expect(page.locator('#equipment-save-error')).toContainText('Equipment code already exists.');await expect(field('Equipment Code')).toHaveValue('EQ-005');await expect(field('Name')).toHaveValue('Compactor');await expect(page.getByRole('button',{name:'Create Equipment',exact:true})).toBeEnabled();
 fail=false;await page.getByRole('button',{name:'Create Equipment',exact:true}).click();await page.waitForURL(`**/equipment/${id}`);if(writes.length!==2||writes.some(x=>x.companyId!==beta))throw Error('Incorrect company or duplicate POST');
 if(errors.length||await page.locator('#blazor-error-ui').isVisible())throw Error('Unhandled circuit failure '+errors);
 console.log(`PASS SuperAdmin company selection, validation, safe failure and retry at ${width}px`);await context.close();
 }
 }finally{await browser?.close();app.kill('SIGTERM');await new Promise(r=>api.close(r));}
})().catch(e=>{console.error(e);process.exitCode=1;});
