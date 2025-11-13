# Site de Documentação DataFlow

## 📋 Descrição

Foi criado um site HTML estático para visualizar toda a documentação do projeto DataFlow de forma organizada e navegável.

## 🎨 O que foi criado

### Arquivos Principais

1. **`index.html`** - Página principal do site com:
   - Header com título e subtítulo
   - Sidebar de navegação organizada por categorias
   - Seções de conteúdo (Início Rápido, Links Úteis, etc.)
   - Cards informativos e links rápidos

2. **`styles.css`** - Estilização completa com:
   - Design moderno e responsivo
   - Cores e tema consistentes
   - Layout com sidebar fixa
   - Cards e componentes estilizados
   - Estilos para conteúdo Markdown

3. **`script.js`** - Funcionalidades JavaScript:
   - Navegação entre seções
   - Scroll suave
   - Preparado para carregar conteúdo Markdown dinamicamente

## 🚀 Como Usar

### Opção 1: Abrir Diretamente

1. Abra o arquivo `docs/index.html` no navegador
2. Navegue pelos links na sidebar

### Opção 2: Servir via HTTP (Recomendado)

Para melhor experiência e evitar problemas de CORS:

**Python:**
```bash
cd docs
python -m http.server 8000
```
Acesse: http://localhost:8000

**Node.js (http-server):**
```bash
npx http-server docs -p 8000
```

**PHP:**
```bash
cd docs
php -S localhost:8000
```

### Opção 3: Integrar ao Docker

Você pode servir o site através do Nginx ou adicionar um serviço no docker-compose:

```yaml
docs-site:
  image: nginx:alpine
  ports:
    - "8080:80"
  volumes:
    - ./docs:/usr/share/nginx/html:ro
```

## 📝 Estrutura de Navegação

O site está organizado em:

- **Início** - Visão geral e início rápido
- **Arquitetura** - Documentos técnicos de arquitetura
- **Operações** - Guias de instalação e operação
- **Tutoriais** - Tutoriais passo a passo
- **Templates** - Modelos reutilizáveis

## 🔄 Conversão de Markdown para HTML

Atualmente, os links apontam para arquivos `.html`. Para converter os arquivos `.md` para `.html`, você pode:

### Opção 1: Usar MkDocs (Recomendado)

```bash
pip install mkdocs mkdocs-material
mkdocs new .
mkdocs build
mkdocs serve
```

### Opção 2: Usar Pandoc

```bash
pandoc arquivo.md -o arquivo.html -s --css styles.css
```

### Opção 3: Usar Biblioteca JavaScript

Adicione ao `script.js` uma biblioteca como `marked.js`:

```html
<script src="https://cdn.jsdelivr.net/npm/marked/marked.min.js"></script>
```

E modifique o `script.js` para carregar `.md` dinamicamente.

## 🎯 Funcionalidades

- ✅ Navegação lateral organizada
- ✅ Design responsivo (mobile-friendly)
- ✅ Links rápidos para serviços (Grafana, Prometheus, etc.)
- ✅ Cards informativos
- ✅ Guia de início rápido
- ✅ Preparado para expansão com conteúdo Markdown

## 🔧 Personalização

### Cores

Edite as variáveis CSS em `styles.css`:

```css
:root {
    --primary-color: #2563eb;
    --secondary-color: #1e40af;
    /* ... */
}
```

### Adicionar Novos Links

Edite o `index.html` na seção de navegação:

```html
<li><a href="caminho/para/arquivo.html">
    <i class="fas fa-icon"></i> Nome do Link
</a></li>
```

## 📚 Próximos Passos

1. **Converter Markdown para HTML** - Use uma das opções acima
2. **Adicionar Busca** - Integre uma biblioteca de busca
3. **Adicionar Syntax Highlighting** - Para blocos de código
4. **Deploy** - Publique em GitHub Pages, Netlify ou similar

## 🌐 Deploy

### GitHub Pages

1. Crie um branch `gh-pages`
2. Coloque os arquivos do site na raiz
3. Ative GitHub Pages nas configurações do repositório

### Netlify

1. Conecte o repositório
2. Configure o diretório de build como `docs`
3. Deploy automático a cada push

## 📞 Suporte

Para melhorar o site ou adicionar funcionalidades, edite os arquivos:
- `index.html` - Estrutura e conteúdo
- `styles.css` - Estilos e tema
- `script.js` - Funcionalidades JavaScript

---

**Nota:** Este é um site estático básico. Para funcionalidades avançadas como busca, conversão automática de Markdown, etc., considere usar ferramentas como MkDocs, Docusaurus ou VuePress.

