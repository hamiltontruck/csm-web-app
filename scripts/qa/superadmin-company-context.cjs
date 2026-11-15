// Local API fixtures only: no production users or business records.
const { chromium, expect } = require('playwright/test');
const http = require('http'), { spawn } = require('child_process'), fs = require('fs'), assert = require('assert/strict');
(async () => {
  const alpha = '11111111-1111-1111-1111-111111111111', beta = '22222222-2222-2222-2222-222222222222', id = '33333333-3333-3333-3333-333333333333';
  const permissions = [...fs.readFileSync('CSM.Domain/Enums/GranularPermission.cs', 'utf8').matchAll(/^\s*(\w+)(?:\s*=\s*\d+)?\s*,?\s*$/gm)].map(x => x[1]).filter(x => x.includes('_'));
  let loadFailure = false, saveFailure = true, writes = [];
  const api = http.createServer((req, res) => {
    const chunks = []; req.on('data', x => chunks.push(x)); req.on('end', () => {
      const path = new URL(req.url, 'http://fixture').pathname.toLowerCase(); let status = 200, data = [];
      if (path === '/auth/login') data = { accessToken: 'local-context', refreshToken: 'local-refresh', email: 'admin@example.test', fullName: 'Local SuperAdmin', roles: ['SuperAdmin'], companyId: null };
      else if (path === '/auth/permissions') data = permissions;
      else if (path === '/auth/me' || path === '/auth/me/profile') data = { userId: id, name: 'Local SuperAdmin', email: 'admin@example.test', roles: ['SuperAdmin'], companyId: null };
      else if (['/documents/company-options', '/documentcategories/company-options', '/shifts/company-options', '/invoices/create-company-options'].includes(path)) {
        if (loadFailure) { status = 500; data = { message: 'private diagnostic' }; }
        else data = { canSelectCompany: true, companyId: null, companies: [{ id: alpha, code: 'A', name: 'Alpha' }, { id: beta, code: 'B', name: 'Beta' }] };
      } else if (path === '/invoices/vendor-options') {
        const company = new URL(req.url, 'http://fixture').searchParams.get('companyId');
        if (![alpha, beta].includes(company)) { status = 400; data = { message: 'A company is required for vendor options' }; }
        else data = [{ id, vendorCode: 'VENDOR', name: company === beta ? 'Beta vendor' : 'Alpha vendor' }];
      } else if (['/documents', '/documentcategories', '/shifts', '/invoices'].includes(path) && req.method === 'POST') {
        const body = JSON.parse(Buffer.concat(chunks)); writes.push(body);
        if (saveFailure) { status = 400; data = { message: 'Test validation failure' }; }
        else data = path === '/invoices' ? { id } : { id, ...body, documentType: 'General' };
      } else if (path === `/documents/${id}`) data = { id, companyId: beta, documentNumber: 'DOC-NEW', name: 'New document', documentType: 'General', fileName: 'document.pdf', fileSizeBytes: 0 };
      else if (path === `/invoices/${id}`) data = { id, companyId: beta, invoiceNumber: 'INV-NEW', type: 'VendorBill', status: 'Draft', invoiceDate: '2026-11-01', subtotal: 100, totalAmount: 100, currencyCode: 'ETB', exchangeRate: 1 };
      else if (!['/documentcategories', '/documents', '/shifts', `/invoicelines/invoice/${id}`].includes(path)) { status = 403; data = { message: 'Fixture denies unexpected endpoint' }; }
      res.writeHead(status, { 'Content-Type': 'application/json' }); res.end(JSON.stringify(data));
    });
  });
  await new Promise(r => api.listen(5120, '127.0.0.1', r));
  const app = spawn('dotnet', ['run', '--project', 'CSM.Web/CSM.Web.csproj', '-c', process.env.CSM_CONFIGURATION || 'Release', '--no-build', '--no-launch-profile'], { env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: 'http://127.0.0.1:5121', ApiSettings__BaseUrl: 'http://127.0.0.1:5120/' }, stdio: ['ignore', 'ignore', 'inherit'] });
  let browser;
  try {
    for (let i = 0; i < 160; i++) { try { await fetch('http://127.0.0.1:5121/login'); break; } catch { await new Promise(r => setTimeout(r, 250)); } }
    browser = await chromium.launch({ headless: true, executablePath: process.env.CSM_CHROMIUM || undefined, args: ['--no-sandbox'] });
    for (const width of [390, 1280]) {
      const context = await browser.newContext({ viewport: { width, height: 1000 } }), page = await context.newPage(), errors = [];
      page.on('pageerror', e => errors.push(e.message));
      await page.goto('http://127.0.0.1:5121/login'); await page.locator('#csm-email').fill('admin@example.test'); await page.locator('#csm-password').fill('Local-Fixture-Password-123!'); await page.locator('button[type=submit]').click(); await page.waitForURL('**/dashboard');
      for (const category of [false, true]) {
        loadFailure = true; saveFailure = true; writes = [];
        const path = category ? '/compliance/document-categories' : '/compliance/documents';
        await page.goto(`http://127.0.0.1:5121${path}/create`);
        await expect(page.getByRole('alert')).toContainText('Unable to load'); await expect(page.locator('input,select,textarea')).toHaveCount(0);
        loadFailure = false; await page.getByRole('button', { name: 'Retry', exact: true }).click();
        await expect(page.locator('#company-selection')).toBeVisible(); await expect(page.locator('#company-selection')).toHaveValue('');
        await page.locator('#company-selection').selectOption(beta);
        await page.locator(`input[maxlength="${category ? 30 : 50}"]`).fill(category ? 'CAT-NEW' : 'DOC-NEW');
        await page.locator(`input[maxlength="${category ? 200 : 250}"]`).fill(category ? 'New category' : 'New document');
        if (!category) { await page.locator('input[maxlength="255"]').fill('document.pdf'); await page.locator('select').filter({ has: page.locator('option[value="1"]') }).selectOption('1'); }
        await page.getByRole('button', { name: 'Save', exact: true }).click(); await expect(page.getByText(category ? 'Test validation failure' : 'Unable to save the document', { exact: false })).toBeVisible();
        await expect(page.locator('#company-selection')).toHaveValue(beta); await expect(page.locator(`input[maxlength="${category ? 30 : 50}"]`)).toHaveValue(category ? 'CAT-NEW' : 'DOC-NEW');
        saveFailure = false; await page.getByRole('button', { name: 'Save', exact: true }).click();
        await page.waitForURL(category ? `**${path}` : `**${path}/${id}/edit`);
        assert.equal(writes.length, 2); assert(writes.every(x => x.companyId === beta));
        await expect(page.locator('#blazor-error-ui')).not.toBeVisible(); assert.equal(errors.length, 0);
      }
      const field = label => page.locator('label').filter({ hasText: label }).locator('..').locator('input,select,textarea').first();
      loadFailure = false; saveFailure = true; writes = [];
      await page.goto('http://127.0.0.1:5121/hr/shifts/create');
      await expect(page.locator('#shift-company')).toBeVisible();
      await field('Shift Code').fill('DAY'); await field('Shift Name').fill('Day');
      await field('Start Time').fill('08:00'); await field('End Time').fill('16:00');
      await page.getByRole('button', { name: 'Save Shift', exact: true }).click();
      await expect(page.getByText('Please select a company.', { exact: true })).toBeVisible(); assert.equal(writes.length, 0);
      await page.locator('#shift-company').selectOption(beta);
      await page.getByRole('button', { name: 'Save Shift', exact: true }).click();
      await expect(page.getByText('Test validation failure', { exact: false })).toBeVisible();
      await expect(field('Shift Code')).toHaveValue('DAY'); await expect(page.locator('#shift-company')).toHaveValue(beta);
      saveFailure = false; await page.getByRole('button', { name: 'Save Shift', exact: true }).click();
      await page.waitForURL('**/hr/shifts'); assert.equal(writes.length, 2); assert(writes.every(x => x.companyId === beta));

      saveFailure = true; writes = [];
      await page.goto('http://127.0.0.1:5121/finance/company/invoices/create');
      await expect(page.locator('#invoice-company')).toBeVisible(); await expect(page.locator('#invoice-company')).toHaveValue('');
      await expect(page.getByRole('button', { name: 'Create Invoice', exact: true })).toBeDisabled();
      await page.locator('#invoice-company').selectOption(beta); await expect(field('Vendor').locator(`option[value="${id}"]`)).toHaveCount(1);
      await field('Vendor').selectOption(id); await page.locator('#invoice-company').selectOption(alpha); await expect(field('Vendor')).toHaveValue('');
      await page.locator('#invoice-company').selectOption(beta); await expect(field('Vendor').locator(`option[value="${id}"]`)).toHaveCount(1); await field('Vendor').selectOption(id);
      await field('Invoice Number').fill('INV-NEW'); await field('Subtotal').fill('100');
      await page.getByRole('button', { name: 'Create Invoice', exact: true }).click();
      await expect(page.getByText('Test validation failure', { exact: false })).toBeVisible();
      await expect(page.locator('#invoice-company')).toHaveValue(beta); await expect(field('Vendor')).toHaveValue(id);
      saveFailure = false; await page.getByRole('button', { name: 'Create Invoice', exact: true }).click();
      await page.waitForURL(`**/finance/invoices/${id}`); assert.equal(writes.length, 2); assert(writes.every(x => x.companyId === beta && x.vendorId === id));
      await expect(page.locator('#blazor-error-ui')).not.toBeVisible(); assert.equal(errors.length, 0);
      console.log(`PASS ${width}px companyless SuperAdmin document/category/shift/invoice company pickers, scoped vendor reset, load/save retry and payload ownership`);
      await context.close();
    }
  } finally { await browser?.close(); app.kill('SIGTERM'); await new Promise(r => api.close(r)); }
})().catch(e => { console.error(e); process.exitCode = 1; });
