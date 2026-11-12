// Real Blazor UI, local fixture API only. Never uses production accounts or business data.
const { chromium, expect } = require('playwright/test');
const http = require('http');
const { spawn } = require('child_process');
const fs = require('fs');
const path = require('path');
(async () => {
  const id='11111111-1111-1111-1111-111111111111', site='22222222-2222-2222-2222-222222222222';
  const project='33333333-3333-3333-3333-333333333333', code='44444444-4444-4444-4444-444444444444', invoiceId='55555555-5555-5555-5555-555555555555', paymentId='66666666-6666-6666-6666-666666666666', expenseId='77777777-7777-7777-7777-777777777777',budgetId='88888888-8888-8888-8888-888888888888';
  const roles=['FinanceOfficer'], permissions=['Sites_View','Finance_CostCode_View','Finance_Budget_View','Finance_Budget_Manage','Finance_Expense_View','Finance_Expense_Submit','Finance_Expense_Approve','Finance_Invoice_View','Finance_Payment_View','Finance_Payment_Manage','Finance_PettyCash_View','Finance_PettyCash_Manage','Reports_View','Reports_FinancialSummary_View'];
  const constructionSite={id:site,companyId:id,siteCode:'ASSIGNED',name:'Assigned finance site',status:'Planning',isActive:true,country:'Ethiopia'};
  const options={siteId:site,companyId:id,siteCode:'ASSIGNED',siteName:constructionSite.name,projects:[{id:project,projectCode:'P-1',name:'Assigned project',constructionSiteId:site}],costCodes:[{id:code,code:'SHARED',name:'Shared code',isActive:true}],vendors:[]};
  const invoice={id:invoiceId,companyId:id,constructionSiteId:site,vendorId:project,invoiceNumber:'INV-LOCAL',type:'VendorBill',status:'Draft',invoiceDate:'2026-11-11',subtotal:1,taxAmount:0,totalAmount:1,paidAmount:0,currencyCode:'ETB',exchangeRate:1};
  let reads=[],writes=[],payment,expense,budget,adminMode=false,mixedMode=false;
  function reset(){reads=[];writes=[];budget={id:budgetId,companyId:id,constructionSiteId:site,budgetNumber:'LOCAL-BUDGET',name:'Site budget',constructionSiteName:constructionSite.name,totalAmount:10,currencyCode:'ETB',effectiveFrom:'2026-11-11',status:'Draft'};payment={id:paymentId,companyId:id,constructionSiteId:site,paymentNumber:'LOCAL-PAY',paymentDate:'2026-11-11',amount:1,currencyCode:'ETB',exchangeRate:1,direction:'Outgoing',status:'Pending',paymentMethod:'Cash'};expense={id:expenseId,companyId:id,constructionSiteId:site,costCodeId:code,expenseNumber:'LOCAL-EXPENSE',expenseDate:'2026-11-11',description:'Local expense',amount:1,taxAmount:0,currencyCode:'ETB',exchangeRate:1,status:'Draft',createdAtUtc:'2026-11-11T00:00:00Z'};}
  reset();
  const api=http.createServer((req,res)=>{const chunks=[];req.on('data',b=>chunks.push(b));req.on('end',()=>{
    const route=new URL(req.url,'http://fixture').pathname.toLowerCase();const body=chunks.length?JSON.parse(Buffer.concat(chunks).toString()):null;
    if(req.method==='GET') reads.push(route);else writes.push({route,method:req.method,body});
    let data,status=200;
    if(route==='/auth/login'){adminMode=body.email==='admin@example.test';mixedMode=body.email==='mixed@example.test';data={accessToken:'local-finance-fixture',refreshToken:'local-refresh-fixture',email:'finance@example.test',fullName:adminMode?'Local Administrator':'Local Finance Officer',roles:adminMode?['CompanyAdmin']:mixedMode?['FinanceOfficer','Accountant']:roles,companyId:id};}
    else if(route==='/auth/permissions') data=adminMode?['Users_Create','Sites_View']:mixedMode?[...permissions,'HRM_Payroll_View']:permissions;
    else if(route==='/finance/action-capabilities') data={includesCompany:false,canUsePayroll:false};
    else if(route==='/users/role-options') data=['Accountant','FinanceOfficer','Storekeeper'];
    else if(route==='/auth/me'||route==='/auth/me/profile') data={userId:id,name:'Local Finance Officer',email:'finance@example.test',companyId:id,roles:adminMode?['CompanyAdmin']:mixedMode?['FinanceOfficer','Accountant']:roles};
    else if(route==='/user/preferences') data={userId:id,theme:'System',language:'en',timeZone:'Africa/Addis_Ababa',dateFormat:'dd/MM/yyyy',timeFormat:'24h',numberFormat:'en-US',confirmActions:true,rememberNavigation:true};
    else if(route.startsWith('/reports/')&&route.includes('executive-dashboard')) data={totalSites:1,totalProjects:1,sitesByStatus:{Planning:1},projectsByStatus:{Active:1}};
    else if(route==='/reports/financial-summary/all') {const financial={totalBudget:1000,totalExpenses:1050,totalExpenditure:160,totalCompletedPayments:200,balance:40,cashBalance:40,budgetRemaining:-50,netCashFlow:40,pettyCashBalance:40,totalPettyCashOut:160,activePettyCashAccounts:1};data={company:null,total:financial,sites:[{siteId:site,siteCode:'ASSIGNED',siteName:constructionSite.name,financial}]};}
    else if(route===`/reports/site/${site}/financial-summary`) data={totalBudget:1000,totalExpenses:1050,totalExpenditure:160,totalCompletedPayments:200,balance:40,cashBalance:40,budgetRemaining:-50,netCashFlow:40,pettyCashBalance:40,totalPettyCashOut:160,activePettyCashAccounts:1};
    else if(route.includes('dashboard')) data={generatedAtUtc:new Date().toISOString(),today:'2026-11-11',currencyCode:'ETB',totalBudget:10,totalExpenses:1};
    else if(route==='/sites') data=[constructionSite];
    else if(route===`/sites/${site}`) data=constructionSite;
    else if(route===`/finance/sites/${site}/options`) data=options;
    else if(route===`/constructionsites/${site}/cost-codes`) data=[{id:code,companyId:id,constructionSiteId:site,code:'HBG-BUD-001',name:'General Construction',isActive:true},{id:project,companyId:id,constructionSiteId:site,code:'INACTIVE',name:'Inactive code',isActive:false}];
    else if(route===`/budgets/${budgetId}/status`){budget.status=({1:'Draft',2:'PendingApproval',3:'Approved',4:'Active',5:'Closed',6:'Cancelled'})[body.status];data=budget;}
    else if(route===`/budgets/${budgetId}`) data=budget;
    else if(route===`/budgetlines/budget/${budgetId}`) data=[];
    else if(route===`/invoices/${invoiceId}`) data=invoice;
    else if(route==='/invoices/vendor-options') data=[{id:project,vendorCode:'VEN-001',name:'Local invoice vendor'}];
    else if(route===`/invoicelines/invoice/${invoiceId}`) data=[];
    else if(route==='/invoices') data=[invoice];
    else if(route==='/payments'&&req.method==='POST'){payment={...payment,...body,id:paymentId,status:'Pending',direction:body.direction===1?'Incoming':'Outgoing'};data=payment;}
    else if(route===`/payments/${paymentId}/status`){payment.status=body.status===2?'Completed':'Pending';data=payment;}
    else if(route===`/payments/${paymentId}/reverse`){payment.status='Reversed';data=payment;}
    else if(route===`/payments/${paymentId}`) data=payment;
    else if(route==='/payments'||route.includes(`/payments/site/${site}`)) data=[payment];
    else if(route===`/expenses/${expenseId}/status`){expense.status=({2:'Submitted',3:'Approved',5:'Paid'})[body.status];data=expense;}
    else if(route===`/expenses/${expenseId}`) data=expense;
    else if(route===`/expenses/site/${site}`) data=[expense];
    else {status=403;data={message:'Fixture denies unexpected endpoint'};}
    res.writeHead(status,{'Content-Type':'application/json'});res.end(JSON.stringify(data));
  });});
  await new Promise(resolve=>api.listen(5080,'127.0.0.1',resolve));
  const app=spawn('dotnet',['run','--project','CSM.Web/CSM.Web.csproj','--configuration','Release','--no-build','--no-launch-profile'],{env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',ASPNETCORE_URLS:'http://127.0.0.1:5081',ApiSettings__BaseUrl:'http://127.0.0.1:5080/'},stdio:['ignore','ignore','inherit']});
  let browser, activePage;
  try {
    for(let i=0;i<160;i++){try{await fetch('http://127.0.0.1:5081/login');break;}catch{await new Promise(r=>setTimeout(r,250));}}
    browser=await chromium.launch({headless:true,executablePath:process.env.CSM_CHROMIUM||undefined,args:['--no-sandbox']});
    const admin=await browser.newContext({viewport:{width:390,height:1000}}),adminPage=await admin.newPage();activePage=adminPage;
    await adminPage.goto('http://127.0.0.1:5081/login');await adminPage.locator('#csm-email').fill('admin@example.test');await adminPage.locator('#csm-password').fill('Local-Fixture-Password-123!');await adminPage.locator('button[type=submit]').click();await adminPage.waitForURL('**/dashboard');
    await adminPage.goto('http://127.0.0.1:5081/users/create');
    await expect(adminPage.locator('.form-check').filter({hasText:'FinanceOfficer'})).toBeVisible();
    await expect(adminPage.getByText('EquipmentManager',{exact:true})).toHaveCount(0);
    await adminPage.locator('.form-check').filter({hasText:'FinanceOfficer'}).locator('input').check();
    if(!reads.includes('/users/role-options')||reads.includes('/roles')) throw Error('Create User did not use its authorized live role-options endpoint');
    if(writes.some(x=>x.route==='/users')) throw Error('Role-options browser check unexpectedly created an account');
    console.log('PASS Create User live FinanceOfficer role options with Users_Create and no Users_View');await admin.close();adminMode=false;
    for(const [width,mixed] of [[320,false],[390,false],[1280,false],[390,true]]){
      reset();const context=await browser.newContext({viewport:{width,height:1000},isMobile:width<500,hasTouch:width<500});const page=await context.newPage(),errors=[];activePage=page;page.on('pageerror',e=>errors.push(e.message));
      const visit=async route=>{await page.goto('http://127.0.0.1:5081'+route);};
      const geometry=async()=>{const g=await page.evaluate(()=>({viewport:innerWidth,width:document.documentElement.scrollWidth,error:document.querySelector('#blazor-error-ui')?.getBoundingClientRect().height||0}));if(g.width>width+1||g.error||errors.length) throw Error('Layout/circuit failure '+JSON.stringify({g,errors}));};
      const field=label=>page.locator('label').filter({hasText:new RegExp('^'+label+'(?: \\*)?$')}).locator('..').locator('input,select,textarea').first();
      await visit('/login');await page.locator('#csm-email').fill(mixed?'mixed@example.test':'finance@example.test');await page.locator('#csm-password').fill('Local-Fixture-Password-123!');await page.locator('button[type=submit]').click();await page.waitForURL('**/dashboard');
      await visit('/finance');await expect(page.getByRole('link',{name:'Open assigned sites',exact:true})).toBeVisible();await expect(page.getByText('Finance Overview',{exact:true})).toHaveCount(0);await geometry();
      await page.getByRole('link',{name:'Open assigned sites',exact:true}).click();await expect(page.getByText(constructionSite.name,{exact:true})).toBeVisible();
      await visit(`/finance/sites/${site}/cost-codes`);await expect(page.getByRole('cell',{name:'General Construction',exact:true})).toBeVisible();await expect(page.getByRole('button',{name:/^(Activate|Deactivate|Edit|Delete|New Cost Code)$/})).toHaveCount(0);
      for(const form of ['budgets','invoices','expenses']){await visit(`/finance/sites/${site}/${form}/create`);await expect(page.locator('select').filter({hasText:'Assigned project'})).toHaveCount(1);await geometry();}
      await visit(`/finance/sites/${site}/invoices/${invoiceId}`);await expect(page.getByText('Local invoice vendor',{exact:true})).toBeVisible();await expect(page.getByRole('button',{name:'Issue Invoice',exact:true})).toBeVisible();await expect(page.getByRole('button',{name:'+ Add Line',exact:true})).toBeVisible();await geometry();
      await visit(`/finance/sites/${site}/budgets/${budgetId}`);for(const name of ['Submit for Approval','Approve','Activate']) await page.getByRole('button',{name,exact:true}).click();await expect(page.getByText('Active',{exact:true})).toBeVisible();await geometry();
      await visit(`/finance/sites/${site}/payments/create`);await expect(page.getByText('Loading payment information...',{exact:true})).toHaveCount(0);await expect(page.locator('option[value=Payroll]')).toHaveCount(0);
      await field('Payment Number').fill('LOCAL-PAY-'+width);await field('Amount').fill('1');await page.getByRole('button',{name:/Save Payment|Record Payment/}).click();await page.waitForURL(`**/finance/sites/${site}/payments/${paymentId}`);
      const creation=writes.find(x=>x.route==='/payments'&&x.method==='POST');if(!creation||creation.body.constructionSiteId!==site||creation.body.payrollRecordId) throw Error('Payment lost assigned site or included payroll');
      await visit(`/finance/sites/${site}/payments/${paymentId}`);await page.getByRole('button',{name:'Complete Payment',exact:true}).click();await expect(page.getByRole('button',{name:'Reverse Payment',exact:true})).toBeVisible();await page.getByRole('button',{name:'Reverse Payment',exact:true}).click();await expect(page.getByText('Reversed',{exact:true})).toBeVisible();await geometry();
      await visit(`/finance/sites/${site}/expenses/${expenseId}`);for(const name of ['Submit Expense','Approve Expense','Mark as Paid']) await page.getByRole('button',{name,exact:true}).click();await expect(page.locator('.badge').filter({hasText:/^Paid$/})).toBeVisible();await geometry();
      if(!mixed){
        await visit('/reports');
        const verifyBalances=async()=>{
          const cashLabels=page.locator('.text-muted.small').filter({hasText:/^Cash Balance$/});await expect(cashLabels.first()).toBeVisible();
          for(let i=0;i<await cashLabels.count();i++){await expect(cashLabels.nth(i).locator('..')).toContainText('40.00');await expect(cashLabels.nth(i).locator('..').locator('.fw-bold')).toHaveClass(/text-success/);}
          const budgetLabels=page.locator('.text-muted.small').filter({hasText:/^Budget Remaining$/});await expect(budgetLabels.first()).toBeVisible();
          for(let i=0;i<await budgetLabels.count();i++){await expect(budgetLabels.nth(i).locator('..')).toContainText('-50.00');await expect(budgetLabels.nth(i).locator('..').locator('.fw-bold')).toHaveClass(/text-danger/);}
          await expect(page.getByText('1,040.00',{exact:true})).toHaveCount(0);await geometry();
        };
        await verifyBalances();await page.locator('select').first().selectOption('Site');await page.locator('select').nth(1).selectOption(site);await verifyBalances();
      }
      const budgetStatuses=writes.filter(x=>x.route===`/budgets/${budgetId}/status`).map(x=>x.body.status);if(JSON.stringify(budgetStatuses)!=='[2,3,4]') throw Error('Budget lifecycle did not submit/approve/activate');
      const statuses=writes.filter(x=>x.route===`/expenses/${expenseId}/status`).map(x=>x.body.status);if(JSON.stringify(statuses)!=='[2,3,5]') throw Error('Expense lifecycle did not submit/approve/pay');
      if(reads.some(x=>x.includes('payroll')||x==='/vendors'||x.includes('/projects'))) throw Error('Officer loaded unrelated HR/procurement/project endpoints');
      if(process.env.CSM_SCREENSHOT_DIR){fs.mkdirSync(process.env.CSM_SCREENSHOT_DIR,{recursive:true});await page.screenshot({path:path.join(process.env.CSM_SCREENSHOT_DIR,`finance-officer-${width}.png`),fullPage:true});}
      console.log('PASS FinanceOfficer assigned-site forms/payment/expense/actions/no unrelated lookups',width,mixed?'mixed corporate reader':'officer only');await context.close();
    }
  } catch(error) {
    if(activePage && !activePage.isClosed()) {
      console.error('Local fixture page',activePage.url(),'recent reads',reads.slice(-12));
      console.error('Visible alert',await activePage.locator('.alert').allTextContents());
      if(process.env.CSM_SCREENSHOT_DIR){fs.mkdirSync(process.env.CSM_SCREENSHOT_DIR,{recursive:true});await activePage.screenshot({path:path.join(process.env.CSM_SCREENSHOT_DIR,'failure.png'),fullPage:true});}
    }
    throw error;
  } finally {if(browser)await browser.close();app.kill();api.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
