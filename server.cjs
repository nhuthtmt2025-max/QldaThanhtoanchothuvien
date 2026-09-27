const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const root = path.join(__dirname, 'dist');
const files = { '/api-live.js': ['api-live.js', 'text/javascript'], '/live.js': ['live.js', 'text/javascript'], '/admin.js': ['admin.js', 'text/javascript'], '/': ['index.html', 'text/html'], '/index.html': ['index.html', 'text/html'], '/styles.css': ['styles.css', 'text/css'], '/app.js': ['app.js', 'text/javascript'], '/config.js': ['config.js', 'text/javascript'], '/data.js': ['data.js', 'text/javascript'], '/api.js': ['api.js', 'text/javascript'] };
const server = http.createServer((req, res) => {
  const file = files[new URL(req.url, 'http://localhost').pathname];
  if (!file) { res.writeHead(404); res.end('Not found'); return; }
  fs.readFile(path.join(root, file[0]), (error, data) => {
    if (error) { res.writeHead(500); res.end('Unable to read file'); return; }
    res.writeHead(200, { 'Content-Type': `${file[1]}; charset=utf-8`, 'X-Content-Type-Options': 'nosniff' });
    res.end(data);
  });
});
server.listen(4173, '127.0.0.1', () => console.log('Libra: http://127.0.0.1:4173'));
server.on('error', error => { console.error(error.message); process.exitCode = 1; });
