const AdmZip = require('adm-zip');
const path = require('path');
const os = require('os');
const fs = require('fs');

function extractZip(zipPath, extractTo) {
    const zip = new AdmZip(zipPath);
    zip.extractAllTo(extractTo, true);
}

function getVersion() {
    const pluginJson = JSON.parse(fs.readFileSync(path.resolve(__dirname, '../Community.PowerToys.Run.Plugin.CurrencyConverter/plugin.json'), 'utf8'));
    return pluginJson.Version;
}

const version = getVersion();
const zipPath = path.resolve(__dirname, `../bin/CurrencyConverter-${version}-${process.arch === 'arm64' ? 'ARM64' : 'x64'}.zip`);
const extractTo = path.join(os.homedir(), 'AppData', 'Local', 'Microsoft', 'PowerToys', 'PowerToys Run', 'Plugins', 'CurrencyConverter');

extractZip(zipPath, extractTo);
