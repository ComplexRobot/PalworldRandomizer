const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const root = path.resolve(__dirname, '../../..');
const source = fs.readFileSync(path.join(root, 'PalworldRandomizer.Browser/wwwroot/recovery.js'), 'utf8');
const locale = JSON.parse(fs.readFileSync(path.join(root,
    'PalworldRandomizer/Metadata/Locales/Recovery.en-US.json'), 'utf8'));
const start = source.indexOf('const FALLBACK = {');
const end = source.indexOf('};', start) + 2;
assert(start >= 0 && end > start);
const fallback = vm.runInNewContext(source.slice(start, end) + '\nFALLBACK;');
for (const [key, value] of Object.entries(locale)) {
    assert.equal(fallback[key], value, 'Missing or changed installed-English fallback: ' + key);
}
console.log(Object.keys(locale).length + ' installed-English recovery fallbacks verified.');
