// Build a multi-resolution Windows ICO from the approved transparent artwork.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require(process.argv[2] || 'sharp');
const assets = path.join(__dirname, '..', 'assets');
(async () => {
  const sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];
  const pngs = await Promise.all(sizes.map(size => sharp(path.join(assets,'app-icon.png')).resize(size,size).png().toBuffer()));
  const header = Buffer.alloc(6 + sizes.length * 16);
  header.writeUInt16LE(1,2); header.writeUInt16LE(sizes.length,4);
  let offset = header.length;
  sizes.forEach((size,i) => {
    const entry = 6 + i * 16;
    header[entry] = size === 256 ? 0 : size; header[entry+1] = header[entry];
    header.writeUInt16LE(1,entry+4); header.writeUInt16LE(32,entry+6);
    header.writeUInt32LE(pngs[i].length,entry+8); header.writeUInt32LE(offset,entry+12);
    offset += pngs[i].length;
  });
  fs.writeFileSync(path.join(assets,'app-icon.ico'),Buffer.concat([header,...pngs]));
  const metadata = await sharp(path.join(assets,'app-icon.png')).metadata();
  if (!metadata.hasAlpha) throw new Error('App icon must retain transparency.');
  console.log(`Built ICO with ${sizes.length} sizes (16–256 px), transparent source.`);
})().catch(error => { console.error(error); process.exit(1); });
