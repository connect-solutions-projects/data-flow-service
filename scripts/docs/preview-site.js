// Simple static file server for docs/site
// Usage: node scripts/docs/preview-site.js [port]
// Default port: 8080

const http = require('http');
const fs = require('fs');
const path = require('path');
const url = require('url');

const projectRoot = path.resolve(__dirname, '..', '..');
const siteRoot = path.join(projectRoot, 'docs', 'site');
const port = parseInt(process.argv[2] || '8080', 10);

const mime = {
  '.html': 'text/html',
  '.css': 'text/css',
  '.js': 'text/javascript',
  '.json': 'application/json',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.gif': 'image/gif',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon',
};

const server = http.createServer((req, res) => {
  const parsed = url.parse(req.url);
  let pathname = decodeURIComponent(parsed.pathname);
  if (pathname === '/') pathname = '/index.html';
  const filePath = path.join(siteRoot, pathname);

  // Prevent path traversal
  if (!filePath.startsWith(siteRoot)) {
    res.writeHead(403);
    return res.end('Forbidden');
  }

  fs.stat(filePath, (err, stat) => {
    if (err || !stat.isFile()) {
      res.writeHead(404);
      return res.end('Not Found');
    }
    const ext = path.extname(filePath).toLowerCase();
    const type = mime[ext] || 'application/octet-stream';
    res.writeHead(200, { 'Content-Type': type });
    fs.createReadStream(filePath).pipe(res);
  });
});

server.listen(port, () => {
  console.log(`Preview server running at http://localhost:${port}/`);
  console.log(`Serving: ${siteRoot}`);
});

