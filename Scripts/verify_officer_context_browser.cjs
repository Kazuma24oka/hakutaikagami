// node verify_officer_context_browser.cjs <installed-playwright-module-path> [repo-root]
const fs = require('fs');
const path = require('path');
const { pathToFileURL } = require('url');
const { chromium } = require(process.argv[2] || 'playwright');
const root = path.resolve(process.argv[3] || path.join(__dirname, '..'));
(async () => {
  const browser = await chromium.launch({ headless: true, channel: 'msedge' });
  try {
    const page = await browser.newPage({ viewport: { width: 1280, height: 900 }, deviceScaleFactor: 1 });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.goto(pathToFileURL(path.join(root, 'Docs/Proposals/officer-context-review.html')).href);
    const profile = JSON.parse(fs.readFileSync(path.join(root, 'Game/Data/officer_visual_profile.json'), 'utf8'));
    const initial = await page.locator('#frame').getAttribute('src');
    const sizeId = profile.canvas_pixels.join('x');
    const suffix = profile.default_display_scale === 1 ? '-1x.png' : '.png';
    if (!initial.endsWith('battle-' + sizeId + suffix)) throw Error('Initial HTML state differs from adopted profile.');
    const states = [];
    for (const scene of ['battle', 'dialogue']) for (const size of ['32x48', '48x64', '64x96']) for (const zoom of [1, 2]) {
      await page.evaluate(values => {
        for (const [key, value] of Object.entries(values))
          Array.from(document.querySelectorAll('button')).find(button => button.dataset[key] === String(value)).click();
      }, { scene, size, zoom });
      await page.waitForFunction(() => Array.from(document.images).every(image => image.complete && image.naturalWidth === 1100 && image.naturalHeight === 720));
      const data = await page.locator('#frame').evaluate(image => ({ file: image.getAttribute('src'), width: image.getBoundingClientRect().width, height: image.getBoundingClientRect().height }));
      if (data.width !== 1100 || data.height !== 720 || !data.file.includes(scene + '-' + size) || (zoom === 1) !== data.file.endsWith('-1x.png'))
        throw Error('Displayed frame/scale does not match selection.');
      states.push({ scene, size, zoom, ...data });
    }
    if (errors.length) throw Error(errors.join('; '));
    fs.writeFileSync(path.join(root, 'Art/Trials/OfficerContextV1/browser-verification.json'), JSON.stringify({ errors, states }, null, 2) + '\n');
    console.log('OFFICER_CONTEXT_BROWSER_PASS states=12 all_images_loaded=1 frame_css=1100x720 native_input=0');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exit(1); });
