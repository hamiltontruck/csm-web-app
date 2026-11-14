// Real Blazor against an isolated local API fixture; no production PO changes.
const { chromium, expect } = require('playwright/test');
const http = require('http'), { spawn } = require('child_process'), fs = require('fs'), assert = require('assert/strict');
(async () => {
  const id = '11111111-1111-1111-1111-111111111111', company = '22222222-2222-2222-2222-222222222222', site = '33333333-3333-3333-3333-333333333333', vendor = '44444444-4444-4444-4444-444444444444', material = '55555555-5555-5555-5555-555555555555', link = '66666666-6666-6666-6666-666666666666';
  const grants = [...fs.readFileSync('CSM.Domain/Enums/GranularPermission.cs', 'utf8').matchAll(/^\s*(\w+)(?:\s*=\s*\d+)?\s*,?\s*$/gm)].map(x => x[1]).filter(x => x.includes('_'));
  let order, writes, fail, manage = true;
  const reset = () => { writes = []; fail = false; manage = true; order = { id, companyId: company, constructionSiteId: site, vendorId: vendor, vendorName: 'Local supplier', purchaseOrderNumber: 'PO-006', orderDate: '2026-11-02', status: 'Draft', projectId: link, materialRequestId: link, expectedDeliveryDate: '2026-11-20', currencyCode: 'USD', exchangeRate: 2, discountAmount: 1, deliveryAddress: 'Warehouse', paymentTerms: 'Net30', notes: 'Original note', totalAmount: 22, lines: [{ id: link, materialId: material, materialName: 'Cement', costCodeId: link, orderedQuantity: 2, unitPrice: 10, taxAmount: 3, lineTotal: 23 }] }; };
  reset();
  const api = http.createServer((req, res) => { const chunks = []; req.on('data', x => chunks.push(x)); req.on('end', () => {
    const path = new URL(req.url, 'http://fixture').pathname.toLowerCase(); let data = [], status = 200;
    if (path === '/auth/login') data = { accessToken: 'local-po-edit', refreshToken: 'local-refresh', email: 'admin@example.test', fullName: 'Local SuperAdmin', companyId: null, roles: ['SuperAdmin'] };
    else if (path === '/auth/permissions') data = grants.filter(x => manage || x !== 'Procurement_PurchaseOrder_Manage');
    else if (path === '/auth/me' || path === '/auth/me/profile') data = { userId: id, companyId: null, name: 'Local SuperAdmin', email: 'admin@example.test', roles: ['SuperAdmin'] };
    else if (path === '/materials') data = [{ id: material, companyId: company, name: 'Cement', isActive: true }, { id: link, companyId: link, name: 'Foreign material', isActive: true }];
    else if (path === '/vendors') data = [{ id: vendor, companyId: company, name: 'Local supplier', isActive: true }, { id: link, companyId: link, name: 'Foreign supplier', isActive: true }];
    else if (path === `/purchaseorders/${id}` && req.method === 'PUT') {
      const body = JSON.parse(Buffer.concat(chunks)); writes.push(body);
      if (fail) { status = fail === 400 ? 400 : 500; data = { message: status === 400 ? 'A purchase order with this purchase order number already exists in the company.' : 'private diagnostic: database failure' }; }
      else { order = { ...order, ...body, totalAmount: body.lines.reduce((sum, x) => sum + x.orderedQuantity * x.unitPrice + x.taxAmount, 0) - body.discountAmount }; data = order; }
    } else if (path === `/purchaseorders/${id}`) data = order;
    else if (req.method === 'POST') { status = 500; data = { message: 'Unexpected POST' }; }
    res.writeHead(status, { 'Content-Type': 'application/json' }); res.end(JSON.stringify(data));
  }); });
  await new Promise(r => api.listen(5102, '127.0.0.1', r));
  const app = spawn('dotnet', ['run', '--project', 'CSM.Web/CSM.Web.csproj', '-c', 'Release', '--no-build', '--no-launch-profile'], { env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: 'http://127.0.0.1:5103', ApiSettings__BaseUrl: 'http://127.0.0.1:5102/' }, stdio: ['ignore', 'ignore', 'inherit'] });
  let browser;
  try {
    for (let i = 0; i < 160; i++) { try { await fetch('http://127.0.0.1:5103/login'); break; } catch { await new Promise(r => setTimeout(r, 250)); } }
    browser = await chromium.launch({ headless: true, executablePath: process.env.CSM_CHROMIUM || undefined, args: ['--no-sandbox'] });
    for (const width of [390, 1280]) {
      reset(); const context = await browser.newContext({ viewport: { width, height: 1000 } }), page = await context.newPage(), errors = [];
      page.on('pageerror', e => errors.push(e.message));
      await page.goto('http://127.0.0.1:5103/login'); await page.locator('#csm-email').fill('admin@example.test'); await page.locator('#csm-password').fill('Local-Fixture-Password-123!'); await page.locator('button[type=submit]').click(); await page.waitForURL('**/dashboard');
      await page.goto(`http://127.0.0.1:5103/purchase-orders/${id}`);
      const edit = page.getByRole('button', { name: 'Edit', exact: true }); await expect(edit).toBeVisible();
      const box = await edit.boundingBox(); assert(box && box.x >= 0 && box.x + box.width <= width + 1, 'Edit outside viewport');
      await edit.click(); await page.waitForURL(`**/purchase-orders/edit/${id}`);
      const field = name => page.locator('label').filter({ hasText: name }).locator('..').locator('input,select,textarea').first();
      await expect(field('PO Number')).toHaveValue('PO-006'); await expect(field('Supplier')).toHaveValue(vendor); await expect(field('Material')).toHaveValue(material); await expect(field('Quantity')).toHaveValue('2');
      await expect(page.getByRole('option', { name: 'Foreign supplier' })).toHaveCount(0); await expect(page.getByRole('option', { name: 'Foreign material' })).toHaveCount(0);
      await field('Quantity').fill('3'); await field('Remarks').fill('Updated note'); await field('Unit Price').fill('12.50');
      fail = true; await page.getByRole('button', { name: 'Save Changes', exact: true }).click(); await expect(page.getByRole('alert')).toContainText('Unable to save'); await expect(field('Quantity')).toHaveValue('3'); await expect(page.getByRole('alert')).not.toContainText('private diagnostic');
      fail = 400; await page.getByRole('button', { name: 'Save Changes', exact: true }).click(); await expect(page.getByRole('alert')).toContainText('already exists'); await expect(field('Unit Price')).toHaveValue('12.50');
      fail = false; await page.getByRole('button', { name: 'Save Changes', exact: true }).click(); await page.waitForURL(`**/purchase-orders/${id}`); await expect(page.getByText('USD 39.50', { exact: true })).toBeVisible();
      assert.equal(writes.length, 3); const body = writes[2]; assert.equal(body.constructionSiteId, site); assert.equal(body.vendorId, vendor); assert.equal(body.projectId, link); assert.equal(body.materialRequestId, link); assert.equal(body.currencyCode, 'USD'); assert.equal(body.exchangeRate, 2); assert.equal(body.discountAmount, 1); assert.equal(body.expectedDeliveryDate, '2026-11-20'); assert.equal(body.deliveryAddress, 'Warehouse'); assert.equal(body.paymentTerms, 'Net30'); assert.equal(body.notes, 'Updated note'); assert.equal(body.lines[0].taxAmount, 3); assert.equal(body.lines[0].costCodeId, link); assert.equal(body.lines[0].orderedQuantity, 3); assert.equal(body.lines[0].unitPrice, 12.5);
      order.status = 'Approved'; await page.reload(); await expect(page.getByRole('heading', { name: 'PO-006', exact: true })).toBeVisible(); await expect(edit).toHaveCount(0);
      await page.goto(`http://127.0.0.1:5103/purchase-orders/edit/${id}`); await expect(page.getByRole('alert')).toContainText('Only Draft'); await expect(page.locator('form')).toHaveCount(0);
      order.status = 'Draft'; manage = false; await page.goto(`http://127.0.0.1:5103/purchase-orders/${id}`); await expect(page.getByRole('heading', { name: 'PO-006', exact: true })).toBeVisible(); await expect(edit).toHaveCount(0);
      assert.equal(writes.length, 3); assert.equal(errors.length, 0); await expect(page.locator('#blazor-error-ui')).not.toBeVisible();
      console.log(`PASS ${width}px SuperAdmin Draft Edit, metadata retention, typed inputs, safe retry, updated total, and non-Draft/read-only guards`);
      await context.close();
    }
  } finally { await browser?.close(); app.kill('SIGTERM'); await new Promise(r => api.close(r)); }
})().catch(e => { console.error(e); process.exitCode = 1; });
