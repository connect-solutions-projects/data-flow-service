// Static Markdown → HTML generator for docs/
// Usage: node scripts/docs/generate-html-from-md.js
// Outputs HTML files into docs/site/, preserving subfolders (architecture, operations, templates).

const fs = require('fs');
const path = require('path');

const projectRoot = path.resolve(__dirname, '..', '..');
const docsRoot = path.join(projectRoot, 'docs');
const outputRoot = path.join(docsRoot, 'site');
const sourceRoots = [
  path.join(docsRoot, 'architecture'),
  path.join(docsRoot, 'operations'),
  path.join(docsRoot, 'templates'),
];

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function readFile(filePath) {
  return fs.readFileSync(filePath, 'utf8');
}

function writeFile(filePath, content) {
  ensureDir(path.dirname(filePath));
  fs.writeFileSync(filePath, content, 'utf8');
}

function listMarkdownFiles(rootDir) {
  const results = [];
  function walk(dir) {
    const entries = fs.readdirSync(dir, { withFileTypes: true });
    for (const e of entries) {
      const full = path.join(dir, e.name);
      if (e.isDirectory()) walk(full);
      else if (e.isFile() && /\.md$/i.test(e.name)) results.push(full);
    }
  }
  if (fs.existsSync(rootDir)) walk(rootDir);
  return results;
}

function escapeHtml(str) {
  return str
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
}

// Minimal Markdown renderer covering headings, lists, code blocks, inline code, bold/italic, links, paragraphs.
function markdownToHtml(md) {
  // Normalize line endings
  md = md.replace(/\r\n?/g, '\n');

  // Handle fenced code blocks ```
  let inCodeBlock = false;
  const lines = md.split('\n');
  const out = [];
  let listMode = null; // 'ul' or 'ol'

  function closeListIfOpen() {
    if (listMode) {
      out.push(`</${listMode}>`);
      listMode = null;
    }
  }

  for (let i = 0; i < lines.length; i++) {
    let line = lines[i];

    // Fenced code blocks
    if (/^```/.test(line)) {
      if (!inCodeBlock) {
        closeListIfOpen();
        inCodeBlock = true;
        out.push('<pre><code>');
      } else {
        inCodeBlock = false;
        out.push('</code></pre>');
      }
      continue;
    }

    if (inCodeBlock) {
      out.push(escapeHtml(line));
      continue;
    }

    // Headings
    const hMatch = /^(#{1,6})\s+(.*)$/.exec(line);
    if (hMatch) {
      closeListIfOpen();
      const level = hMatch[1].length;
      const text = hMatch[2];
      out.push(`<h${level}>${inline(text)}</h${level}>`);
      continue;
    }

    // Ordered list
    if (/^\s*\d+\.\s+/.test(line)) {
      const itemText = line.replace(/^\s*\d+\.\s+/, '');
      if (listMode !== 'ol') {
        closeListIfOpen();
        listMode = 'ol';
        out.push('<ol>');
      }
      out.push(`<li>${inline(itemText)}</li>`);
      continue;
    }

    // Unordered list
    if (/^\s*[-*+]\s+/.test(line)) {
      const itemText = line.replace(/^\s*[-*+]\s+/, '');
      if (listMode !== 'ul') {
        closeListIfOpen();
        listMode = 'ul';
        out.push('<ul>');
      }
      out.push(`<li>${inline(itemText)}</li>`);
      continue;
    }

    // Blank line resets lists and paragraphs
    if (/^\s*$/.test(line)) {
      closeListIfOpen();
      out.push('');
      continue;
    }

    // Paragraph
    closeListIfOpen();
    out.push(`<p>${inline(line)}</p>`);
  }

  closeListIfOpen();
  return out.join('\n');

  function inline(s) {
    // Inline code
    s = s.replace(/`([^`]+)`/g, (_, code) => `<code>${escapeHtml(code)}</code>`);
    // Bold
    s = s.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
    // Italic
    s = s.replace(/(?<!\*)\*([^*]+)\*(?!\*)/g, '<em>$1</em>');
    // Links [text](url)
    s = s.replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2">$1</a>');
    return s;
  }
}

function wrapHtml(title, bodyHtml) {
  const cssHref = 'index.css'; // copied into docs/site
  return `<!DOCTYPE html>
<html lang="pt-BR">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>${escapeHtml(title)}</title>
  <link rel="stylesheet" href="${cssHref}" />
  <style>
    main { max-width: 1000px; margin: 2rem auto; padding: 0 1rem; }
    nav { background: #111; color: #eee; padding: .75rem 1rem; }
    nav a { color: #9cd; margin-right: 1rem; text-decoration: none; }
    footer { color: #777; font-size: .9rem; text-align: center; padding: 2rem 0; }
    pre { background: #0e0e0e; color: #ddd; padding: .75rem; border-radius: 8px; overflow: auto; }
  </style>
</head>
<body>
  <nav>
    <a href="./index.html">Início</a>
  </nav>
  <main>
    ${bodyHtml}
  </main>
  <footer>Documentação gerada a partir de Markdown • DataFlow</footer>
</body>
</html>`;
}

function relativeToOutput(file) {
  const rel = path.relative(docsRoot, file); // e.g., architecture/foo.md
  const outPath = path.join(outputRoot, rel).replace(/\.md$/i, '.html');
  return outPath;
}

function getTitleFromMd(md, fallback) {
  const m = md.match(/^#\s+(.*)$/m);
  return m ? m[1].trim() : fallback;
}

function generateIndex(pages) {
  const groups = pages.reduce((acc, p) => {
    const rel = path.relative(outputRoot, p.output);
    const seg = rel.split(path.sep);
    const group = seg[0];
    acc[group] = acc[group] || [];
    acc[group].push({ title: p.title, href: rel.replace(/\\/g, '/') });
    return acc;
  }, {});

  let body = '<h1>Documentação (HTML gerado)</h1>';
  body += '<p>Conteúdo convertido do Markdown presente em <code>docs/</code>.</p>';
  body += '<div class="columns">';
  for (const [group, items] of Object.entries(groups)) {
    body += `<div class="card"><h3>${escapeHtml(group)}</h3><ul>`;
    for (const it of items) {
      body += `<li><a href="${it.href}">${escapeHtml(it.title)}</a></li>`;
    }
    body += '</ul></div>';
  }
  body += '</div>';
  return wrapHtml('Documentação — HTML gerado', body);
}

function copyCss() {
  const cssSrc = path.join(docsRoot, 'index.css');
  if (fs.existsSync(cssSrc)) {
    const cssDest = path.join(outputRoot, 'index.css');
    ensureDir(path.dirname(cssDest));
    fs.copyFileSync(cssSrc, cssDest);
  }
}

function main() {
  ensureDir(outputRoot);
  copyCss();

  const pages = [];
  for (const root of sourceRoots) {
    const files = listMarkdownFiles(root);
    for (const file of files) {
      const md = readFile(file);
      const title = getTitleFromMd(md, path.basename(file));
      const htmlBody = markdownToHtml(md);
      const wrapped = wrapHtml(title, htmlBody);
      const outPath = relativeToOutput(file);
      writeFile(outPath, wrapped);
      pages.push({ source: file, output: outPath, title });
      console.log(`Generated: ${path.relative(projectRoot, outPath)}`);
    }
  }

  const indexHtml = generateIndex(pages);
  writeFile(path.join(outputRoot, 'index.html'), indexHtml);
  console.log('Done. Open docs/site/index.html');
}

main();

