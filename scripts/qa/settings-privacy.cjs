// All requests use a local API fixture; no production accounts or business data.
const { chromium, expect } = require('playwright/test');
const http = require('http');
const { spawn } = require('child_process');
const fs = require('fs');
const path = require('path');
(async () => {
  const user = '11111111-1111-1111-1111-111111111111';
  let preferences = {}, writes = [], reads = [], failSave = false;
  const defaults = () => ({ userId: user, theme:'System', language:'en', timeZone:'Africa/Addis_Ababa', dateFormat:'dd/MM/yyyy', timeFormat:'24h', numberFormat:'en-US',
    compactMode:false, confirmActions:true, rememberNavigation:true, emailNotificationsEnabled:true, inAppNotificationsEnabled:true, pushNotificationsEnabled:true,
    securityAlertsEnabled:true, projectNotificationsEnabled:true, approvalNotificationsEnabled:true, inventoryNotificationsEnabled:true, hrNotificationsEnabled:true,
    financeNotificationsEnabled:true, analyticsConsent:false, productImprovementConsent:false });
  const api = http.createServer((req, res) => {
    const chunks=[]; req.on('data', b=>chunks.push(b)); req.on('end', () => {
      const route=req.url.split('?')[0]; if(req.method==='GET') reads.push(route);
      let data, status=200;
      if(route==='/auth/login') data={accessToken:'local-fixture-only',refreshToken:'local-refresh-only',email:'qa@example.test',fullName:'Local Officer',roles:['ProcurementOfficer'],companyId:user};
      else if(route==='/auth/permissions') data=['Sites_View','Procurement_Inventory_View','Notifications_View'];
      else if(route==='/auth/me' || route==='/auth/me/profile') data={userId:user,name:'Local Officer',email:'qa@example.test',companyId:user,roles:['ProcurementOfficer']};
      else if(route==='/user/preferences') {
        if(req.method==='PUT') { const input=JSON.parse(Buffer.concat(chunks).toString()); writes.push(input); if(failSave) status=503; else preferences={...input,userId:user}; }
        data=preferences;
      }
      else if(route==='/auth/login-history') data=[{id:user,occurredAtUtc:'2026-11-11T00:00:00Z',succeeded:true,deviceName:'Local fixture'}];
      else if(route==='/auth/sessions') data=[{id:user,deviceName:'Current fixture',createdAtUtc:'2026-11-11T00:00:00Z',expiresAtUtc:'2026-12-11T00:00:00Z',isCurrent:true}];
      else if(route==='/auth/organization-membership') data=[];
      else if(route==='/personal-data/export') data={account:{email:'qa@example.test'}};
      else if(route.includes('unread-count')) data=0;
      else if(route.includes('dashboard')) data={generatedAtUtc:new Date().toISOString(),lowStockItems:0};
      else { status=403; data={message:'Fixture denies unexpected endpoint'}; }
      res.writeHead(status,{'Content-Type':'application/json'});res.end(JSON.stringify(data));
    });
  });
  await new Promise(resolve=>api.listen(5078,'127.0.0.1',resolve));
  const app=spawn('dotnet',['run','--project','CSM.Web/CSM.Web.csproj','--configuration','Release','--no-build','--no-launch-profile'], {
    env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',ASPNETCORE_URLS:'http://127.0.0.1:5079',ApiSettings__BaseUrl:'http://127.0.0.1:5078/'},stdio:['ignore','ignore','inherit'] });
  let browser;
  try {
    for(let i=0;i<160;i++){ try { await fetch('http://127.0.0.1:5079/login');break; } catch { await new Promise(resolve=>setTimeout(resolve,250)); } }
    browser=await chromium.launch({headless:true,executablePath:process.env.CSM_CHROMIUM || undefined,args:['--no-sandbox']});
    for(const width of [320,390,1280]) {
      preferences=defaults();writes=[];reads=[];failSave=false;
      const context=await browser.newContext({viewport:{width,height:1000},isMobile:width<500,hasTouch:width<500});
      const page=await context.newPage(), errors=[];page.on('pageerror',e=>errors.push(e.message));
      await page.goto('http://127.0.0.1:5079/login');
      await page.locator('#csm-email').fill('qa@example.test');await page.locator('#csm-password').fill('Local-fixture-only-123!');
      await page.locator('button[type=submit]').click();await page.waitForURL('**/dashboard');
      await page.goto('http://127.0.0.1:5079/settings');
      const saved=page.locator('.settings-status');await expect(saved).toHaveText(/All changes saved/);
      await expect(page.locator('.settings-sidebar > button')).toHaveCount(8);
      if(reads.some(x=>x.includes('audit')||x.includes('login-history')||x.includes('sessions'))) throw Error('Account panels eagerly loaded');
      const compact=page.locator('#compactMode');await compact.locator('..').click();await page.getByRole('button',{name:'Save preferences',exact:true}).click();
      await expect(saved).toHaveText(/All changes saved/);await expect.poll(()=>writes.length).toBe(1);
      if(!writes[0].compactMode || writes[0].timeZone!=='Africa/Addis_Ababa') throw Error('Preference payload lost values');
      await page.reload();await expect(saved).toHaveText(/All changes saved/);await expect(compact).toBeChecked();
      await compact.locator('..').click();await page.getByRole('button',{name:'Discard changes',exact:true}).click();await expect(compact).toBeChecked();
      if(writes.length!==1) throw Error('Discard wrote to API');
      await page.getByRole('button',{name:'Restore defaults',exact:true}).click();await expect(saved).toHaveText(/Unsaved changes/);
      if(writes.length!==1) throw Error('Restore persisted without Save');
      await page.getByRole('button',{name:'Discard changes',exact:true}).click();
      await page.getByRole('button',{name:'Appearance',exact:true}).click();await page.locator('.theme-option').nth(1).click();
      await expect(page.locator('html')).toHaveAttribute('data-bs-theme','dark');
      await page.getByRole('button',{name:'Discard changes',exact:true}).click();
      await page.getByRole('button',{name:'General',exact:true}).click();
      failSave=true;await compact.locator('..').click();await page.getByRole('button',{name:'Save preferences',exact:true}).click();
      await expect(page.locator('.settings-message')).toContainText('Could not save preferences');await expect(compact).not.toBeChecked();await expect(saved).toHaveText(/Unsaved changes/);
      failSave=false;await page.getByRole('button',{name:'Discard changes',exact:true}).click();
      await page.getByRole('button',{name:'Security',exact:true}).click();await expect(page.locator('table')).toContainText('11/11/2026 03:00');
      await page.getByRole('button',{name:'Sessions & Devices',exact:true}).click();await expect(page.getByText('Current fixture',{exact:true})).toBeVisible();
      await expect(page.getByRole('button',{name:'Revoke all other devices',exact:true})).toBeDisabled();
      await page.getByRole('button',{name:'Privacy Controls',exact:true}).click();if(reads.some(x=>x.includes('audit'))) throw Error('Reader queried company audit');
      await page.getByRole('button',{name:'Data Management',exact:true}).click();
      const downloadEvent=page.waitForEvent('download');await page.getByRole('button',{name:'Export my data',exact:true}).click();
      const download=await downloadEvent;const exported=JSON.parse(fs.readFileSync(await download.path(),'utf8'));
      if(exported.account.email!=='qa@example.test') throw Error('Wrong personal download');
      await page.getByRole('button',{name:'General',exact:true}).click();
      await page.locator('#language').selectOption('om');await page.getByRole('button',{name:'Save preferences',exact:true}).click();
      await expect(page.locator('html')).toHaveAttribute('lang','om');await expect(saved).toHaveText(/All changes saved/);
      if(width===320) {
        let navigations=0;const count=frame=>{if(frame===page.mainFrame())navigations++;};page.on('framenavigated',count);
        await page.goto('http://127.0.0.1:5079/settings?culture=en&ui-culture=en',{waitUntil:'domcontentloaded'});
        await expect(page.locator('html')).toHaveAttribute('lang','om',{timeout:5000});
        await expect(saved).toHaveText(/All changes saved/);if(navigations>2) throw Error('Culture query caused repeated reloads');
        page.off('framenavigated',count);
      }
      const geometry=await page.evaluate(()=>({viewport:innerWidth,width:document.documentElement.scrollWidth,errors:document.querySelector('#blazor-error-ui')?.getBoundingClientRect().height || 0}));
      if(geometry.width>geometry.viewport+1 || geometry.errors>0 || errors.length) throw Error('Browser layout/circuit failure '+JSON.stringify({geometry,errors}));
      if(process.env.CSM_SCREENSHOT_DIR) {fs.mkdirSync(process.env.CSM_SCREENSHOT_DIR,{recursive:true});await page.screenshot({path:path.join(process.env.CSM_SCREENSHOT_DIR,`settings-${width}.png`),fullPage:true});}
      console.log('PASS settings save/reopen/discard/defaults/error/theme/language/lazy panels',width);await context.close();
    }
  } finally {if(browser) await browser.close();app.kill();api.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
