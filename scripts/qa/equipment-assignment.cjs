// Real Blazor/local fixture API only. No production accounts or assignments.
const {chromium,expect}=require('playwright/test');const http=require('http'),{spawn}=require('child_process');
(async()=>{
 const company='11111111-1111-1111-1111-111111111111',site='22222222-2222-2222-2222-222222222222',equipment='33333333-3333-3333-3333-333333333333',id='44444444-4444-4444-4444-444444444444';let fail=true,writes=[],rows=[];
 const api=http.createServer((req,res)=>{const chunks=[];req.on('data',x=>chunks.push(x));req.on('end',()=>{
 const route=new URL(req.url,'http://fixture').pathname.toLowerCase(),body=chunks.length?JSON.parse(Buffer.concat(chunks)):null;let data,status=200;
 if(route==='/auth/login')data={accessToken:'local-assignment',refreshToken:'local-refresh',email:'admin@example.test',fullName:'Local SuperAdmin',roles:['SuperAdmin'],companyId:null};
 else if(route==='/auth/permissions')data=['Sites_View','Procurement_Inventory_View','Procurement_Inventory_Adjust'];
 else if(route==='/auth/me'||route==='/auth/me/profile')data={userId:id,name:'Local SuperAdmin',email:'admin@example.test',companyId:null,roles:['SuperAdmin']};
 else if(route==='/equipment')data=[{id:equipment,companyId:company,equipmentCode:'TEST-EQ-006',name:'Compactor',status:'Available',isActive:true,ownershipType:'Owned'}];
 else if(route==='/constructionsites'||route==='/sites')data=[{id:site,companyId:company,siteCode:'SITE',name:'Assignment site',isActive:true}];
 else if(route==='/equipmentassignments'&&req.method==='POST'){writes.push(body);if(fail){status=400;data={message:'Equipment and construction site must belong to the same company.'};}else{data={...body,id,companyId:company,assignmentNumber:'EA-LOCAL',equipmentName:'Compactor',equipmentCode:'TEST-EQ-006',constructionSiteName:'Assignment site',isActive:true};rows=[data];}}
 else if(route==='/equipmentassignments')data=rows;
 else{status=403;data={message:'Fixture denies unexpected endpoint'};}
 res.writeHead(status,{'Content-Type':'application/json'});res.end(JSON.stringify(data));});});await new Promise(r=>api.listen(5094,'127.0.0.1',r));
 const app=spawn('dotnet',['run','--project','CSM.Web/CSM.Web.csproj','-c','Release','--no-build','--no-launch-profile'],{env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',ASPNETCORE_URLS:'http://127.0.0.1:5095',ApiSettings__BaseUrl:'http://127.0.0.1:5094/'},stdio:['ignore','ignore','inherit']});let browser;
 try{for(let i=0;i<160;i++){try{await fetch('http://127.0.0.1:5095/login');break;}catch{await new Promise(r=>setTimeout(r,250));}}
 browser=await chromium.launch({headless:true,executablePath:process.env.CSM_CHROMIUM||undefined,args:['--no-sandbox']});for(const width of [390,1280]){
 fail=true;writes=[];rows=[];const context=await browser.newContext({viewport:{width,height:1000}}),page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('http://127.0.0.1:5095/login');await page.locator('#csm-email').fill('admin@example.test');await page.locator('#csm-password').fill('Local-Fixture-Password-123!');await page.locator('button[type=submit]').click();await page.waitForURL('**/dashboard');await page.goto(`http://127.0.0.1:5095/equipment/assignments/create/${site}`);
 await expect(page.getByRole('button',{name:'Save Assignment',exact:true})).toBeVisible();await page.locator('select').first().selectOption(equipment);await page.locator('input[type=number]').fill('2');await page.locator('textarea').fill('assignment test');await page.getByRole('button',{name:'Save Assignment',exact:true}).click();await expect(page.locator('[role=alert]')).toContainText('same company');await expect(page.locator('textarea')).toHaveValue('assignment test');await expect(page.locator('input[type=number]')).toHaveValue('2');
 fail=false;await page.getByRole('button',{name:'Save Assignment',exact:true}).click();await page.waitForURL(`**/equipment/assignments/site/${site}`);if(writes.length!==2||writes.some(x=>x.equipmentId!==equipment||x.constructionSiteId!==site||x.meterReadingAtAssignment!==2))throw Error('Incorrect assignment payload/duplicate POST');if(errors.length||await page.locator('#blazor-error-ui').isVisible())throw Error('Fatal circuit '+errors);console.log(`PASS companyless SuperAdmin assignment form, safe company validation, input preservation and successful retry at ${width}px`);await context.close();
 }}finally{await browser?.close();app.kill('SIGTERM');await new Promise(r=>api.close(r));}
})().catch(e=>{console.error(e);process.exitCode=1;});
