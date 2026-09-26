const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('mm', {
  status: () => ipcRenderer.invoke('ps', 'status'),
  start: () => ipcRenderer.invoke('ps', 'start'),
  stop: () => ipcRenderer.invoke('ps', 'stop'),
  launchQuestApp: () => ipcRenderer.invoke('ps', 'quest'),
  openDashboard: (sub) => ipcRenderer.invoke('open-dashboard', sub),
  openStream: () => ipcRenderer.invoke('open-stream'),
  togglePin: () => ipcRenderer.sendSync('win', 'pin'),
  win: (cmd) => ipcRenderer.send('win', cmd),
});
