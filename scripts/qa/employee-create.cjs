// Real Blazor, local fixture API only. Never uses production accounts or employee data.
const {chromium,expect}=require('playwright/test');
const http=require('http'),{spawn}=require('child_process');
(async()=>{
 const alpha='11111111-1111-1111-1111-111111111111',beta='22222222-2222-2222-2222-222222222222',id='33333333-3333-3333-3333-333333333333';
 let fail=true,writes=[];
 const api=http.createServer((req,res)=>{let chunks=[];req.on('data',x=>chunks.push(x));req.on('end',()=>{
 const route=new URL(req.url,'http://fixture').pathname.toLowerCase(),body=chunks.length?JSON.parse(Buffer.concat(chunks)):null;let data,status=200;
 if(route==='/auth/login') data={accessToken:'local-employee',refreshToken:'local-refresh',email:'admin@example.test',fullName:'Local SuperAdmin',roles:['SuperAdmin'],companyId:null};
 else if(route==='/auth/permissions')data=['HRM_Employee_View','HRM_Employee_Manage'];
 else if(route==='/auth/me'||route==='/auth/me/profile')data={userId:id,name:'Local SuperAdmin',email:'admin@example.test',companyId:null,roles:['SuperAdmin']};
 else if(route==='/employees/create-options')data={canSelectCompany:true,companyId:null,companies:[{id:alpha,code:'A',name:'Alpha'},{id:beta,code:'B',name:'Beta'}]};
 else if(route==='/employees'&&req.method==='POST'){writes.push(body);if(fail){status=400;data={message:'Employee number already exists.'};}else data={id,...body,status:'Active',employeeType:'Permanent',wageType:'Monthly'};}
 else if(route==='/employees')data=[];
 else {status=403;data={message:'Fixture denies unexpected endpoint'};}
 res.writeHead(status,{'Content-Type':'application/json'});res.end(JSON.stringify(data));});});
 await new Promise(r=>api.listen(5092,'127.0.0.1',r));
 const app=spawn('dotnet',['run','--project','CSM.Web/CSM.Web.csproj','-c','Release','--no-build','--no-launch-profile'],{env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',ASPNETCORE_URLS:'http://127.0.0.1:5093',ApiSettings__BaseUrl:'http://127.0.0.1:5092/'},stdio:['ignore','ignore','inherit']});
 let browser;
 try{
 for(let i=0;i<160;i++){try{await fetch('http://127.0.0.1:5093/login');break;}catch{await new Promise(r=>setTimeout(r,250));}}
 browser=await chromium.launch({headless:true,executablePath:process.env.CSM_CHROMIUM||undefined,args:['--no-sandbox']});
 for(const width of [390,1280]){
 fail=true;writes=[];const context=await browser.newContext({viewport:{width,height:1000}}),page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('http://127.0.0.1:5093/login');await page.locator('#csm-email').fill('admin@example.test');await page.locator('#csm-password').fill('Local-Fixture-Password-123!');await page.locator('button[type=submit]').click();await page.waitForURL('**/dashboard');
 await page.goto('http://127.0.0.1:5093/hr/employees/create');await expect(page.locator('#employee-company')).toBeVisible();await expect(page.locator('#employee-company')).toHaveValue('');
 const field=label=>page.locator('label').filter({hasText:label}).locator('..').locator('input,select').first();
 await field('Employee Number *').fill('EMP-LOCAL');await field('First Name *').fill('Comp');await field('Last Name *').fill('Level');await field('Employee Type *').selectOption('Permanent');await field('Employee Scope *').selectOption('Company');await field('Wage Type *').selectOption('Monthly');await field('Base Wage *').fill('1000.25');
 await page.getByRole('button',{name:'Create Employee',exact:true}).click();await expect(page.locator('[role=alert]')).toContainText('Select a company');if(writes.length)throw Error('Missing-company request was posted');
 await page.locator('#employee-company').selectOption(beta);await page.getByRole('button',{name:'Create Employee',exact:true}).click();await expect(page.locator('[role=alert]')).toContainText('Employee number already exists.');await expect(field('Employee Number *')).toHaveValue('EMP-LOCAL');await expect(field('First Name *')).toHaveValue('Comp');await expect(field('Base Wage *')).toHaveValue('1000.25');await expect(page.getByRole('button',{name:'Create Employee',exact:true})).toBeEnabled();
 fail=false;await page.getByRole('button',{name:'Create Employee',exact:true}).click();await page.waitForURL('**/hr/employees');if(writes.length!==2||writes.some(x=>x.companyId!==beta||x.baseWage!==1000.25||x.employeeScope!=='Company'))throw Error('Incorrect company/wage/scope or duplicate POST');
 if(errors.length||await page.locator('#blazor-error-ui').isVisible())throw Error('Unhandled circuit failure '+errors);
 console.log(`PASS SuperAdmin employee company selection, validation, safe failure, input preservation and retry at ${width}px`);await context.close();
 }
 }finally{await browser?.close();app.kill('SIGTERM');await new Promise(r=>api.close(r));}
})().catch(e=>{console.error(e);process.exitCode=1;});
