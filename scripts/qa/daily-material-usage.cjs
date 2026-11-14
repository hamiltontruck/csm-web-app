// Real Blazor with an isolated local API fixture; no production logs, usage or stock writes.
const { chromium, expect } = require('playwright/test');
const http = require('http'), { spawn } = require('child_process'), fs = require('fs'), assert = require('assert/strict');
(async () => {
  const company='11111111-1111-1111-1111-111111111111',site='22222222-2222-2222-2222-222222222222',project='33333333-3333-3333-3333-333333333333',log='44444444-4444-4444-4444-444444444444',material='55555555-5555-5555-5555-555555555555',other='66666666-6666-6666-6666-666666666666';
  const grants=[...fs.readFileSync('CSM.Domain/Enums/GranularPermission.cs','utf8').matchAll(/^\s*(\w+)(?:\s*=\s*\d+)?\s*,?\s*$/gm)].map(x=>x[1]).filter(x=>x.includes('_'));
  let historyFail=true,logsFail=true,createFail=true,saveFail=500,logPosts=[],usagePosts=[],dailyLogs=[],usages=[],unexpected=[];
  const api=http.createServer((req,res)=>{const chunks=[];req.on('data',x=>chunks.push(x));req.on('end',()=>{
    const url=new URL(req.url,'http://fixture'),path=url.pathname.toLowerCase();let status=200,data=[];
    if(path==='/auth/login')data={accessToken:'local-usage',refreshToken:'local-refresh',email:'admin@example.test',fullName:'Local SuperAdmin',companyId:null,roles:['SuperAdmin']};
    else if(path==='/auth/permissions')data=grants;
    else if(path==='/auth/me'||path==='/auth/me/profile')data={userId:company,companyId:null,name:'Local SuperAdmin',email:'admin@example.test',roles:['SuperAdmin']};
    else if(path==='/sites')data=[{id:site,companyId:company,name:'MORMOR',siteCode:'SITE-002',isActive:true}];
    else if(path==='/projects')data=[{id:project,constructionSiteId:site,projectCode:'FENCE',name:'Mor Fence'},{id:other,constructionSiteId:site,projectCode:'OTHER',name:'Other project'}];
    else if(path==='/stockbalances')data=[{materialId:material,constructionSiteId:site,materialCode:'MAT-001',materialName:'Cement',quantityOnHand:700,availableQuantity:700,unitOfMeasure:'Bag'}];
    else if(path==='/dailyprogresslogs'&&req.method==='POST'){
      const body=JSON.parse(Buffer.concat(chunks));logPosts.push(body);
      if(createFail){status=500;data={message:'private diagnostic'};}else{data={id:body.logDate==='2026-11-13'?other:log,projectId:body.projectId,logDate:body.logDate};dailyLogs=[data];}
    }else if(path==='/dailyprogresslogs'){
      if(logsFail){status=503;data={message:'private diagnostic'};}else data=dailyLogs.filter(x=>x.projectId===url.searchParams.get('projectId'));
    }else if(path==='/dailymaterialusages'&&req.method==='POST'){
      const body=JSON.parse(Buffer.concat(chunks));usagePosts.push(body);
      if(saveFail){status=saveFail;data={message:saveFail===400?'Usage date must match the daily progress log date.':'private diagnostic'};}else{data={id:log,...body,constructionSiteName:'MORMOR',materialCode:'MAT-001',materialName:'Cement',quantityVariance:50,requiresSupervisorReview:true};usages=[data];}
    }else if(path==='/dailymaterialusages'){
      if(historyFail){status=500;data={message:'private diagnostic'};}else data=[...usages,{id:other,constructionSiteId:other,materialName:'Foreign site material',date:'2026-11-14'}];
    }else if(req.method==='POST'){unexpected.push(path);status=500;data={message:'Unexpected fixture write'};}
    res.writeHead(status,{'Content-Type':'application/json'});res.end(JSON.stringify(data));
  });});await new Promise(r=>api.listen(5108,'127.0.0.1',r));
  const app=spawn('dotnet',['run','--project','CSM.Web/CSM.Web.csproj','-c','Release','--no-build','--no-launch-profile'],{env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',ASPNETCORE_URLS:'http://127.0.0.1:5109',ApiSettings__BaseUrl:'http://127.0.0.1:5108/'},stdio:['ignore','ignore','inherit']});let browser;
  try{
    for(let i=0;i<160;i++){try{await fetch('http://127.0.0.1:5109/login');break;}catch{await new Promise(r=>setTimeout(r,250));}}
    browser=await chromium.launch({headless:true,executablePath:process.env.CSM_CHROMIUM||undefined,args:['--no-sandbox']});
    for(const width of [390,1280]){
      historyFail=logsFail=createFail=true;saveFail=500;logPosts=[];usagePosts=[];dailyLogs=[];usages=[];unexpected=[];
      const context=await browser.newContext({viewport:{width,height:1000}}),page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
      await page.goto('http://127.0.0.1:5109/login');await page.locator('#csm-email').fill('admin@example.test');await page.locator('#csm-password').fill('Local-Fixture-Password-123!');await page.locator('button[type=submit]').click();await page.waitForURL('**/dashboard');
      await page.goto(`http://127.0.0.1:5109/daily-material-usage/site/${site}`);await expect(page.getByRole('alert')).toContainText('Unable to load daily material usage');await expect(page.getByText('No daily usage records')).toHaveCount(0);
      historyFail=false;await page.getByRole('button',{name:'Retry',exact:true}).click();await expect(page.getByRole('heading',{name:'MORMOR',exact:true})).toBeVisible();await page.getByRole('button',{name:'New Usage',exact:true}).click();await page.waitForURL(`**/daily-material-usage/create/${site}`);
      const field=name=>page.locator('label').filter({hasText:new RegExp(`^${name}$`)}).locator('..').locator('input,select,textarea').first();
      await expect(field('Construction Site')).toHaveValue(site);await field('Project').selectOption(project);await expect(page.getByRole('alert')).toContainText('Unable to load daily progress logs');await expect(page.locator('#create-usage-daily-log')).toHaveCount(0);
      logsFail=false;await page.locator('#reload-usage-daily-logs').click();await expect(page.getByRole('status')).toContainText('No daily progress logs');assert.equal(logPosts.length,0);assert.equal(usagePosts.length,0);
      await field('Material').selectOption(material);await field('Quantity Issued').fill('200');await field('Quantity Used').fill('100');await field('Quantity Returned').fill('50');await field('Notes').fill('Usage note');await field('Daily Log Date').fill('2026-11-14');
      await page.locator('#create-usage-daily-log').click();await expect(page.getByRole('alert')).toContainText('Unable to create daily log');await expect(field('Quantity Issued')).toHaveValue('200');
      createFail=false;await page.locator('#create-usage-daily-log').click();await expect(field('Daily Progress Log')).toHaveValue(log);await expect(field('Usage Date')).toHaveValue('2026-11-14');assert.equal(logPosts.length,2);assert.equal(logPosts[1].projectId,project);assert.equal(logPosts[1].taskId,'00000000-0000-0000-0000-000000000000');
      await page.locator('#save-daily-usage').click();await expect(page.getByRole('alert')).toContainText('Unable to save daily material usage');await expect(page.getByRole('alert')).not.toContainText('private diagnostic');await expect(field('Notes')).toHaveValue('Usage note');
      saveFail=400;await page.locator('#save-daily-usage').click();await expect(page.getByRole('alert')).toContainText('Usage date must match');await expect(field('Quantity Returned')).toHaveValue('50');
      saveFail=0;await page.locator('#save-daily-usage').click();await page.waitForURL(`**/daily-material-usage/site/${site}`);await expect(page.getByText('MAT-001 - Cement',{exact:true})).toBeVisible();await expect(page.getByText('Review',{exact:true})).toBeVisible();await expect(page.getByText('Foreign site material')).toHaveCount(0);
      assert.equal(usagePosts.length,3);const body=usagePosts[2];assert.equal(body.constructionSiteId,site);assert.equal(body.materialId,material);assert.equal(body.dailyProgressLogId,log);assert.equal(body.date,'2026-11-14');assert.equal(body.quantityIssued,200);assert.equal(body.quantityUsed,100);assert.equal(body.quantityReturned,50);assert.equal(body.unit,'Bag');
      const add=page.getByRole('button',{name:'New Usage',exact:true});const box=await add.boundingBox();assert(box&&box.x>=0&&box.x+box.width<=width+1);await add.click();await page.waitForURL(`**/daily-material-usage/create/${site}`);await field('Project').selectOption(project);await field('Daily Progress Log').selectOption(log);await field('Project').selectOption(other);await expect(field('Daily Progress Log')).toHaveValue('');await expect(page.locator('#save-daily-usage')).toBeDisabled();assert.equal(usagePosts.length,3);assert.equal(logPosts.length,2);await field('Project').selectOption(project);await expect(field('Daily Progress Log').locator('option[value="'+log+'"]')).toHaveCount(1);await field('Daily Log Date').fill('2026-11-13');await page.locator('#create-usage-daily-log').click();await expect(field('Daily Progress Log')).toHaveValue(other);await expect(field('Usage Date')).toHaveValue('2026-11-13');assert.equal(logPosts.length,3);assert.equal(usagePosts.length,3);assert.equal(unexpected.length,0);assert.equal(errors.length,0);await expect(page.locator('#blazor-error-ui')).not.toBeVisible();
      console.log(`PASS ${width}px Daily Material Usage log-load retry, explicit daily-log creation, bound site/date, 200/100/50 save retry, site history and stale-project guard`);await context.close();
    }
  }finally{await browser?.close();app.kill('SIGTERM');await new Promise(r=>api.close(r));}
})().catch(e=>{console.error(e);process.exitCode=1;});
