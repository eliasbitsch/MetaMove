// MetaMove Launcher - Electron shell.
// Every action goes through scripts/launcher.ps1, which prints one JSON object.
// The script runs in demo mode until the real steps are wired in.
const { app, BrowserWindow, ipcMain, shell } = require('electron');
const { execFile } = require('child_process');
const path = require('path');

const SCRIPT = path.join(__dirname, 'scripts', 'launcher.ps1');

// Hosts without a real GPU (the lab PC: ASPEED server graphics) stutter while Chromium's
// GPU process fails and falls back - render in software from the start there. Opt-in per
// machine: an empty file "no-gpu" next to this script.
if (require('fs').existsSync(path.join(__dirname, 'no-gpu'))) app.disableHardwareAcceleration();
const DASHBOARD_URL = 'http://localhost:8080';

function runPs(action) {
  return new Promise((resolve) => {
    execFile('powershell.exe',
      ['-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', SCRIPT, '-Action', action],
      { windowsHide: true, timeout: 60000 },
      (err, stdout, stderr) => {
        try { resolve(JSON.parse(stdout)); }
        catch { resolve({ ok: false, error: (stderr || err?.message || 'no output').trim() }); }
      });
  });
}

let win, streamWin;
function createWindow() {
  win = new BrowserWindow({
    width: 1180, height: 760, minWidth: 980, minHeight: 640,
    frame: false, backgroundColor: '#f2f0ec', show: false,
    icon: path.join(__dirname, 'assets', 'metamove.ico'),
    webPreferences: { preload: path.join(__dirname, 'preload.js'), contextIsolation: true },
  });
  win.loadFile(path.join(__dirname, 'renderer', 'index.html'));
  win.once('ready-to-show', () => win.show());
  win.on('closed', () => app.quit());

  // --capture <file.png> [delayMs]: render once, write a screenshot, quit (design review
  // without clicking). MM_CAPTURE_JS drives the UI first; MM_CAPTURE_STREAM=1 shoots the
  // headset-view window instead of the main one.
  const i = process.argv.indexOf('--capture');
  if (i > -1) {
    const js = process.env.MM_CAPTURE_JS;
    win.webContents.once('did-finish-load', () => {
      if (js) setTimeout(() => win.webContents.executeJavaScript(js), 800);
      if (process.env.MM_CAPTURE_STREAM) openStream();
      setTimeout(async () => {
        const target = process.env.MM_CAPTURE_STREAM ? streamWin : win;
        const img = await target.webContents.capturePage();
        require('fs').writeFileSync(process.argv[i + 1], img.toPNG());
        app.quit();
      }, Number(process.argv[i + 2] || 2500));
    });
  }
}

// Headset view: left eye only, locked to 16:9. The picture source is not wired yet.
function openStream() {
  if (streamWin && !streamWin.isDestroyed()) { streamWin.focus(); return; }
  streamWin = new BrowserWindow({
    width: 960, height: 540, minWidth: 480, minHeight: 270,
    frame: false, backgroundColor: '#15171c', show: false,
    title: 'MetaMove - Headset view',
    icon: path.join(__dirname, 'assets', 'metamove.ico'),
    webPreferences: { preload: path.join(__dirname, 'preload.js'), contextIsolation: true },
  });
  streamWin.setAspectRatio(16 / 9);
  streamWin.loadFile(path.join(__dirname, 'renderer', 'stream.html'));
  streamWin.once('ready-to-show', () => streamWin.show());
}

ipcMain.handle('ps', (_e, action) => runPs(action));
ipcMain.handle('open-dashboard', (_e, sub) => shell.openExternal(DASHBOARD_URL + (sub || '')));
ipcMain.handle('open-stream', () => openStream());
// Window buttons act on whichever window sent them.
ipcMain.on('win', (e, cmd) => {
  const w = BrowserWindow.fromWebContents(e.sender);
  e.returnValue = false;
  if (!w) return;
  if (cmd === 'min') w.minimize();
  else if (cmd === 'max') w.isMaximized() ? w.unmaximize() : w.maximize();
  else if (cmd === 'close') w.close();
  else if (cmd === 'pin') { w.setAlwaysOnTop(!w.isAlwaysOnTop()); e.returnValue = w.isAlwaysOnTop(); }
  else if (cmd === 'full') w.setFullScreen(!w.isFullScreen());
});

app.whenReady().then(createWindow);
app.on('window-all-closed', () => app.quit());
