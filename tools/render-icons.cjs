// Developer-only asset preparation; sharp is not needed to run Color Ramp.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require(process.argv[2] || 'sharp');
const assets = path.join(__dirname, '..', 'assets');
const svg = fs.readFileSync(path.join(assets, 'eyedropper.svg'), 'utf8');
(async () => {
  for (const [name, color] of [['eyedropper-light.png', '#1E2023'], ['eyedropper-dark.png', '#ECEDEF']]) {
    await sharp(Buffer.from(svg.replace('<svg ', `<svg fill="${color}" `)), { density: 384 })
      .rotate(0).resize(54, 54, { fit: 'inside' })
      .resize(64, 64, { fit: 'contain', background: { r: 0, g: 0, b: 0, alpha: 0 } })
      .png().toFile(path.join(assets, name));
  }
})().catch(error => { console.error(error); process.exit(1); });
