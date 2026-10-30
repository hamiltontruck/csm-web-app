const {chromium}=require('playwright');
const http=require('http');const {spawn}=require('child_process');const fs=require('fs');
(async()=>{

 const permissions=['Projects_View','Procurement_MaterialRequest_View','Procurement_Inventory_View','Users_View','HRM_Employee_View','Finance_Budget_View','Compliance_View','Reports_View','Notifications_View'];
 const companyId='11111111-1111-1111-1111-111111111111';
 // Local fixtures only: this server never contacts the production API.
 const api=http.createServer((req,res)=>{let data=[];req.on('data',b=>data.push(b));req.on('end',()=>{
 let obj=[];const p=req.url.toLowerCase();
 if(p.includes('auth/login'))obj={accessToken:'local-qa-only',refreshToken:'local-qa-only',email:'qa@example.test',fullName:'Local QA',roles:['Admin'],companyId};
 else if(p.includes('auth/permissions'))obj=permissions;
 else if(p.includes('auth/me'))obj={roles:['Admin'],companyId};
 else if(p.includes('unread-count'))obj=2;
 else if(p.includes('dashboard/executive'))obj={activeProjects:12,completedProjects:7,totalEmployees:34,openDelays:2,totalBudget:123456789.25,totalExpenses:1250000,budgetUtilization:50,openSafetyIncidents:0,lowStockItems:2,generatedAtUtc:'2026-10-30T00:00:00Z'};
 else if(p==='/users')obj=[{id:companyId,email:'operations.team.long.email@example.test',fullName:'Construction Operations Manager',phoneNumber:'+251911111111',jobTitle:'Regional Supervisor',isActive:true,roles:['Admin'],siteNames:['Adama Project Site'],siteAccesses:[companyId]}];
 res.writeHead(200,{'Content-Type':'application/json'});res.end(JSON.stringify(obj));});});
 await new Promise(r=>api.listen(5056,'127.0.0.1',r));
 const app=spawn(process.env.CSM_DOTNET || 'dotnet',['run','--project','CSM.Web/CSM.Web.csproj','--configuration','Release','--no-build','--no-launch-profile'],{cwd:require('path').resolve(__dirname,'../..'),env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',ASPNETCORE_URLS:'http://127.0.0.1:5057',ApiSettings__BaseUrl:'http://127.0.0.1:5056/'},stdio:['ignore','ignore','inherit']});
 let browser;
 try{
  for(let n=0;n<50;n++){try{await fetch('http://127.0.0.1:5057/login');break}catch(e){await new Promise(r=>setTimeout(r,250))}}
  browser=await chromium.launch({executablePath:process.env.CSM_CHROMIUM_PATH || undefined,args:['--no-sandbox'],headless:true});
  const page=await browser.newPage({viewport:{width:1440,height:1000}});let errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto('http://127.0.0.1:5057/login');await page.locator('#csm-email').fill('qa@example.test');await page.locator('#csm-password').fill('Local-qa-only-123!');await page.locator('button[type=submit]').click();await page.getByRole('heading',{name:'Executive Overview'}).waitFor({timeout:30000});
  for(const width of [1440,992,768,390,320]){
   await page.setViewportSize({width,height:950});
   for(const [route,heading] of [['dashboard','Executive Overview'],['equipment','Equipment'],['users','User Management'],['users/create','Create User'],['procurement','Procurement']]){
    await page.goto('http://127.0.0.1:5057/'+route);await page.getByRole('heading',{name:heading,exact:true}).waitFor({timeout:20000});
    await page.waitForTimeout(300);
    const dims=await page.evaluate(()=>({viewport:innerWidth,width:document.documentElement.scrollWidth,theme:!!document.querySelector('link[href*="csm-theme"]')}));
    if(dims.width>width)throw Error('Overflow '+width+' '+route+' '+JSON.stringify(dims));
    if(!dims.theme)throw Error('Theme missing');
    const brand=await page.locator('.csm-workspace').evaluate(e=>getComputedStyle(e).getPropertyValue('--csm-primary').trim());if(brand!=='#173d76')throw Error('Brand token missing');
    if(process.env.CSM_SCREENSHOT_DIR && (width===390||width===1440)){fs.mkdirSync(process.env.CSM_SCREENSHOT_DIR,{recursive:true});await page.screenshot({path:require('path').join(process.env.CSM_SCREENSHOT_DIR,route.replace('/','-')+'-'+width+'.png'),fullPage:true});}
    console.log('PASS',width,route);
   }
   if(width<992){await page.getByRole('button',{name:'Open navigation'}).click();await page.getByRole('button',{name:'Sign Out',exact:true}).scrollIntoViewIfNeeded();await page.getByRole('link',{name:'Overview',exact:true}).click();await page.getByRole('heading',{name:'Executive Overview'}).waitFor();if(await page.locator('#workspace-navigation').isVisible())throw Error('menu stays open');console.log('PASS menu',width);}
  }
  await page.setViewportSize({width:390,height:950});await page.goto('http://127.0.0.1:5057/users/create');await page.getByRole('heading',{name:'Create User'}).waitFor();
  const input=page.locator('.form-control').first();await input.focus();const style=await input.evaluate(e=>({font:getComputedStyle(e).fontSize,height:e.getBoundingClientRect().height}));if(style.font!=='16px'||style.height<44)throw Error('mobile input sizing');console.log('PASS mobile form targets',style);
  if(errors.length)throw Error('page errors '+errors.join(';'));
  console.log('PASS no browser errors');
 }finally{if(browser)await browser.close();app.kill();api.close();}
})().catch(e=>{console.error(e);process.exit(1)});
