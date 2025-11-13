# 🚀 Portal de Documentação DataFlow

Portal HTML completo com toda a documentação do projeto DataFlow convertida para HTML com design moderno e navegação intuitiva.

## 📁 Estrutura

```
portal/
├── index.html              # Página inicial
├── css/
│   └── style.css          # Estilos do portal
├── js/
│   └── main.js            # JavaScript do portal
├── architecture/           # Documentos de arquitetura
├── operations/            # Guias de operação
├── tutorials/             # Tutoriais
└── templates/             # Templates
```

## 🚀 Como Usar

### Opção 1: Abrir Diretamente

Simplesmente abra o arquivo `index.html` no seu navegador:

```
portal/index.html
```

### Opção 2: Servir via HTTP (Recomendado)

Para melhor experiência, sirva via servidor HTTP:

**Python:**
```bash
cd portal
python -m http.server 8000
```
Acesse: http://localhost:8000

**Node.js:**
```bash
npx http-server portal -p 8000
```

**PHP:**
```bash
cd portal
php -S localhost:8000
```

## 🎨 Características

- ✅ Design moderno e responsivo
- ✅ Navegação lateral fixa
- ✅ Tema escuro elegante
- ✅ Todos os documentos em HTML
- ✅ Links funcionais entre páginas
- ✅ Código com syntax highlighting
- ✅ Mobile-friendly

## 📝 Documentos Disponíveis

### Arquitetura
- Arquitetura Técnica
- Decisões Técnicas

### Operações
- Instalação Docker
- Docker Compose
- Manual Completo
- Deploy Docker Hub
- Tag e Push Docker

### Tutoriais
- Endpoints da API
- Grafana & Prometheus
- Scripts
- Executar Fora Docker

### Templates
- Modelo de Relatório

## 🔧 Personalização

Edite `css/style.css` para personalizar cores e estilos:

```css
:root {
    --primary: #6366f1;
    --secondary: #8b5cf6;
    /* ... */
}
```

## 📚 Adicionar Novos Documentos

1. Crie um novo arquivo HTML na pasta apropriada
2. Use o mesmo template das outras páginas
3. Adicione o link na sidebar do `index.html`

---

**Desenvolvido para o projeto DataFlow**

